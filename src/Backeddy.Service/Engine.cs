using System.Net.Sockets;
using Backeddy.Core;

namespace Backeddy.Service;

/// <summary>
/// O coração do serviço: guarda a configuração, agenda e faz os backups, lista os momentos para a linha do tempo,
/// restaura e acompanha os alertas do servidor.
/// </summary>
public sealed class Engine(KopiaRunner kopia, ILogger<Engine> log) : BackgroundService
{
    static string ConfigPath => Path.Combine(AppInfo.DataDir, "config.json");

    readonly object gate = new();
    readonly SemaphoreSlim repoLock = new(1, 1);      // uma mudança de repositório por vez (configurar, backup)
    readonly SemaphoreSlim wake = new(0, int.MaxValue);
    readonly ServerClient server = new();
    readonly DateTimeOffset started = DateTimeOffset.Now;

    BackupConfig cfg = BackupConfig.Load(ConfigPath);
    Secrets secrets = Secrets.Load();
    bool manualRequested, policiesDirty = true, running;
    DateTimeOffset? runningSince;
    WaitReason wait;
    List<Alert> alerts = new();
    RestoreStatus? restore;
    string? connectedUrl;
    CancellationToken stopping;

    // ------------------------------------------------------------ situação

    public ServiceStatus Status()
    {
        lock (gate)
        {
            var state = cfg.Destination == DestinationKind.None ? BackupState.NotConfigured
                : running ? BackupState.Running
                : wait != WaitReason.None ? BackupState.Waiting
                : cfg.LastError is not null ? BackupState.Error
                : BackupState.Idle;
            return new ServiceStatus
            {
                Version = AppInfo.Version,
                StartedAt = started,
                State = state,
                Destination = cfg.Destination,
                DestinationName = cfg.DisplayName,
                LastBackup = cfg.LastSuccess,
                NextBackup = running ? null : Schedule.Next(cfg),
                PausedUntil = cfg.PausedUntil is { } p && p > DateTimeOffset.Now ? p : null,   // vencida é como se não existisse
                RunningSince = runningSince,
                LastError = cfg.LastError,
                Wait = wait,
                LastFiles = cfg.LastFiles,
                LastBytes = cfg.LastBytes,
                OverdueDays = Schedule.OverdueDays(cfg, DateTimeOffset.Now),
                Alerts = alerts.ToList(),
                Sources = cfg.Profile is null ? new() : AllSources(),
                Excluded = cfg.Excluded.ToList(),
                Restore = restore,
                Message = KopiaRunner.Installed ? null : "kopiaMissing",
            };
        }
    }

    List<SourceInfo> AllSources()
    {
        var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).Select(d => d.Name);
        var system = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        return Sources.Compute(cfg.Profile!, drives, system, cfg.Destination == DestinationKind.Disk ? cfg.DiskRoot : null);
    }

    List<SourceInfo> ActiveSources() => AllSources().Where(s => !Sources.IsExcludedEntirely(s, cfg.Excluded)).ToList();

    void Update(Func<BackupConfig, BackupConfig> change)
    {
        lock (gate)
        {
            cfg = change(cfg);
            cfg.Save(ConfigPath);
        }
    }

    // ------------------------------------------------------------ comandos do app

    public async Task<IpcResponse> HandleAsync(IpcRequest req, string? caller, CancellationToken ct)
    {
        if (req.Cmd != Cmd.Status && cfg.UserName is not null && caller is not null
            && !string.Equals(caller, cfg.UserName, StringComparison.OrdinalIgnoreCase))
            return Ipc.Fail(Err.OtherUser);   // só quem configurou o Backeddy mexe nele
        try
        {
            switch (req.Cmd)
            {
                case Cmd.Status: return Ipc.Success(Status());
                case Cmd.SetupDisk: return await SetupDiskAsync(Ipc.From<DiskSetup>(req.Data), ct);
                case Cmd.SetupServer: return await SetupServerAsync(Ipc.From<ServerSetup>(req.Data), ct);
                case Cmd.Forget: return await ForgetAsync(ct);
                case Cmd.BackupNow:
                    if (cfg.Destination == DestinationKind.None) return Ipc.Fail(Err.NotConfigured);
                    lock (gate) manualRequested = true;
                    wake.Release();
                    return Ipc.Success();
                case Cmd.SetExclusions:
                    var ex = Ipc.From<ExclusionsPayload>(req.Data)?.Paths ?? new();
                    Update(c => c with { Excluded = ex.Distinct(StringComparer.OrdinalIgnoreCase).ToList() });
                    lock (gate) policiesDirty = true;
                    return Ipc.Success();
                case Cmd.Pause:
                    if (cfg.Destination == DestinationKind.None) return Ipc.Fail(Err.NotConfigured);
                    var pausedUntil = Schedule.PauseUntil(Ipc.From<PauseRequest>(req.Data)?.Kind ?? PauseKind.Resume, DateTimeOffset.Now);
                    Update(c => c with { PausedUntil = pausedUntil });
                    wake.Release();   // o agendador já recalcula o estado na hora
                    return Ipc.Success();
                case Cmd.Points: return await PointsAsync(ct);
                case Cmd.ListDir:
                    var obj = Ipc.From<DirRequest>(req.Data)?.Obj;
                    if (!FileHistory.IsRef(obj)) return Ipc.Fail(Err.Unknown);
                    var r = await kopia.RunAsync(KopiaCli.Show(obj!), secrets.RepoPassword, ct, TimeSpan.FromMinutes(2));
                    return r.Ok ? Ipc.Success(KopiaCli.ParseDirectory(r.Out)) : Ipc.Fail(r.Error);
                case Cmd.Restore: return StartRestore(Ipc.From<RestoreRequest>(req.Data));
                case Cmd.Versions: return await VersionsAsync(Ipc.From<VersionsRequest>(req.Data), ct);
                case Cmd.Ack:
                    var days = Math.Clamp(Ipc.From<AckRequest>(req.Data)?.Days ?? 1, 1, 30);
                    if (cfg.Destination != DestinationKind.Server || secrets.ServerPassword is null) return Ipc.Fail(Err.NotConfigured);
                    if (!await server.AckAsync(ServerHosts(), secrets.ServerPassword, days, ct)) return Ipc.Fail(Err.Unreachable);
                    await PollAlertsAsync(ct);
                    return Ipc.Success();
                default: return Ipc.Fail(Err.Unknown);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            log.LogError(e, "comando {Cmd}", req.Cmd);
            return Ipc.Fail(Err.Unknown);
        }
    }

    // versões de um arquivo: percorre os backups da origem (pastas iguais são lidas uma vez só)
    async Task<IpcResponse> VersionsAsync(VersionsRequest? v, CancellationToken ct)
    {
        if (v is null || string.IsNullOrWhiteSpace(v.RelPath) || v.RelPath.Contains("..")) return Ipc.Fail(Err.Unknown);
        if (cfg.Destination == DestinationKind.None) return Ipc.Fail(Err.NotConfigured);
        if (cfg.Destination == DestinationKind.Server && connectedUrl is null && !await EnsureServerConnectedAsync(ct))
            return Ipc.Fail(Err.Unreachable);
        var r = await kopia.RunAsync(KopiaCli.SnapshotList(), secrets.RepoPassword, ct, TimeSpan.FromMinutes(2));
        if (!r.Ok) return Ipc.Fail(r.Error);
        var snaps = KopiaCli.ParseSnapshots(r.Out)
            .Where(s => string.Equals(s.Source.TrimEnd('\\'), v.Source.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        var list = await FileHistory.CollectAsync(snaps, v.RelPath, async obj =>
        {
            var d = await kopia.RunAsync(KopiaCli.Show(obj), secrets.RepoPassword, ct, TimeSpan.FromMinutes(2));
            return d.Ok ? KopiaCli.ParseDirectory(d.Out) : null;
        });
        return Ipc.Success(list);
    }

    async Task<IpcResponse> SetupDiskAsync(DiskSetup? d, CancellationToken ct)
    {
        if (d is null || string.IsNullOrWhiteSpace(d.Root)) return Ipc.Fail(Err.Unknown);
        if (!KopiaRunner.Installed) return Ipc.Fail("kopiaMissing");
        if (!await repoLock.WaitAsync(0, ct)) return Ipc.Fail(Err.Busy);
        try
        {
            var root = Path.GetPathRoot(d.Root) ?? d.Root;
            var probe = new BackupConfig { DiskRoot = root };
            var repo = probe.DiskRepo!;
            var host = Environment.MachineName;
            bool exists = File.Exists(Path.Combine(repo, "kopia.repository.f")) || File.Exists(Path.Combine(repo, "kopia.repository"));
            string key;
            string? newKey = null;
            await kopia.RunAsync(KopiaCli.Disconnect(), null, ct, TimeSpan.FromMinutes(1));
            connectedUrl = null;
            if (exists)
            {
                if (d.Key is null) return Ipc.Success(new DiskSetupResult(null, true));
                var typed = RecoveryKey.Normalize(d.Key);
                if (typed is null) return Ipc.Fail(Err.WrongKey);
                key = typed;
                var r = await kopia.RunAsync(KopiaCli.ConnectFilesystem(repo, host), key, ct, TimeSpan.FromMinutes(5));
                if (!r.Ok) return Ipc.Fail(r.Error == Err.Unknown ? Err.WrongKey : r.Error);
            }
            else
            {
                key = newKey = RecoveryKey.New();
                Directory.CreateDirectory(repo);
                var r = await kopia.RunAsync(KopiaCli.CreateFilesystem(repo, host), key, ct, TimeSpan.FromMinutes(5));
                if (!r.Ok) return Ipc.Fail(r.Error);
            }
            string? label = null;
            try { label = new DriveInfo(root).VolumeLabel; } catch (Exception) { }
            secrets = new Secrets { RepoPassword = key };
            secrets.Save();
            Update(_ => new BackupConfig
            {
                Destination = DestinationKind.Disk,
                DiskRoot = root,
                DiskLabel = string.IsNullOrWhiteSpace(label) ? null : label,
                Profile = d.Profile,
                UserName = d.UserName,
            });
            lock (gate) { policiesDirty = true; alerts = new(); }
            await ApplyPoliciesAsync(ct);
            wake.Release();
            log.LogInformation("destino: HD {Root} ({Kind})", root, exists ? "existente" : "novo");
            return Ipc.Success(new DiskSetupResult(newKey, false));
        }
        finally { repoLock.Release(); }
    }

    async Task<IpcResponse> SetupServerAsync(ServerSetup? s, CancellationToken ct)
    {
        if (s is null || string.IsNullOrWhiteSpace(s.Host)) return Ipc.Fail(Err.Unknown);
        if (!KopiaRunner.Installed) return Ipc.Fail("kopiaMissing");
        if (!await repoLock.WaitAsync(0, ct)) return Ipc.Fail(Err.Busy);
        try
        {
            var (pair, err) = await server.PairAsync(new[] { s.Host, s.Ip ?? "" }, s.Password, s.PcName, ct);
            if (pair is null) return Ipc.Fail(err ?? Err.Unknown);
            await kopia.RunAsync(KopiaCli.Disconnect(), null, ct, TimeSpan.FromMinutes(1));
            connectedUrl = null;
            string? lastErr = Err.Unreachable;
            foreach (var url in new[] { pair.Url, pair.UrlIp }.Where(u => !string.IsNullOrEmpty(u)).Distinct())
            {
                var r = await kopia.RunAsync(KopiaCli.ConnectServer(url!, pair.Fingerprint, pair.Username, pair.Hostname), pair.Password, ct, TimeSpan.FromMinutes(2));
                if (r.Ok) { connectedUrl = url; lastErr = null; break; }
                lastErr = r.Error;
            }
            if (lastErr is not null) return Ipc.Fail(lastErr);
            secrets = new Secrets { RepoPassword = pair.Password, ServerPassword = s.Password };
            secrets.Save();
            Update(_ => new BackupConfig
            {
                Destination = DestinationKind.Server,
                ServerName = pair.Name,
                ServerHost = s.Host,
                ServerIp = pair.Ip ?? s.Ip,
                ServerUrl = pair.Url,
                ServerUrlIp = pair.UrlIp,
                Fingerprint = pair.Fingerprint,
                ServerUser = pair.Username,
                PcName = pair.Hostname,
                Profile = s.Profile,
                UserName = s.UserName,
            });
            lock (gate) policiesDirty = true;
            await ApplyPoliciesAsync(ct);
            await PollAlertsAsync(ct);
            wake.Release();
            log.LogInformation("destino: servidor {Host} como {Pc}", s.Host, pair.Hostname);
            return Ipc.Success();
        }
        finally { repoLock.Release(); }
    }

    async Task<IpcResponse> ForgetAsync(CancellationToken ct)
    {
        if (!await repoLock.WaitAsync(0, ct)) return Ipc.Fail(Err.Busy);
        try
        {
            await kopia.RunAsync(KopiaCli.Disconnect(), null, ct, TimeSpan.FromMinutes(1));
            connectedUrl = null;
            Secrets.Delete();
            secrets = new Secrets();
            Update(c => new BackupConfig { Excluded = c.Excluded });   // os backups continuam no destino
            lock (gate) { alerts = new(); wait = WaitReason.None; }
            return Ipc.Success();
        }
        finally { repoLock.Release(); }
    }

    async Task<IpcResponse> PointsAsync(CancellationToken ct)
    {
        if (cfg.Destination == DestinationKind.None) return Ipc.Fail(Err.NotConfigured);
        if (cfg.Destination == DestinationKind.Server && connectedUrl is null && !await EnsureServerConnectedAsync(ct))
            return Ipc.Fail(Err.Unreachable);
        var r = await kopia.RunAsync(KopiaCli.SnapshotList(), secrets.RepoPassword, ct, TimeSpan.FromMinutes(2));
        if (!r.Ok) return Ipc.Fail(r.Error);
        return Ipc.Success(KopiaCli.GroupPoints(KopiaCli.ParseSnapshots(r.Out), AllSources()));
    }

    IpcResponse StartRestore(RestoreRequest? req)
    {
        if (req is null || !FileHistory.IsRef(req.Obj)) return Ipc.Fail(Err.Unknown);
        if (cfg.Profile is null) return Ipc.Fail(Err.NotConfigured);
        if (!RestorePlan.InsideSources(req.OriginalPath, AllSources())) return Ipc.Fail(Err.OutsideSources);
        lock (gate)
        {
            if (restore?.Running == true) return Ipc.Fail(Err.Busy);
            var word = new string((req.Word ?? "").Where(ch => char.IsLetter(ch) || ch == ' ').Take(20).ToArray()).Trim();
            if (word.Length == 0) word = "restored";
            var target = req.Original ? req.OriginalPath.TrimEnd('\\')
                : RestorePlan.Beside(req.OriginalPath, req.IsDir, req.PointTime, word, p => File.Exists(p) || Directory.Exists(p));
            restore = new RestoreStatus(true, target, null, null);
            _ = Task.Run(() => RunRestoreAsync(req, target));
            return Ipc.Success(restore);
        }
    }

    async Task RunRestoreAsync(RestoreRequest req, string target)
    {
        string? error = null;
        try
        {
            var r = await kopia.RunAsync(KopiaCli.Restore(req.Obj, target, req.Original), secrets.RepoPassword, stopping);
            if (!r.Ok) error = r.Error;
        }
        catch (Exception e)
        {
            log.LogError(e, "restauração");
            error = Err.Unknown;
        }
        lock (gate) restore = new RestoreStatus(false, target, error, DateTimeOffset.Now);
    }

    // ------------------------------------------------------------ agendador

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        stopping = stop;
        Directory.CreateDirectory(AppInfo.LogDir);
        log.LogInformation("Backeddy {Version} iniciado; destino {Dest}", AppInfo.Version, cfg.Destination);
        var lastAlerts = DateTimeOffset.MinValue;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                if (cfg.Destination == DestinationKind.Server && DateTimeOffset.Now - lastAlerts > TimeSpan.FromMinutes(1))
                {
                    lastAlerts = DateTimeOffset.Now;
                    await PollAlertsAsync(stop);
                }
                await TickAsync(stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception e) { log.LogError(e, "agendador"); }
            await wake.WaitAsync(TimeSpan.FromSeconds(30), stop).ContinueWith(_ => { });
        }
    }

    async Task TickAsync(CancellationToken stop)
    {
        bool manual;
        lock (gate) manual = manualRequested;
        var (onBattery, pct) = Power.Read();
        bool available = cfg.Destination switch
        {
            DestinationKind.Disk => Directory.Exists(cfg.DiskRepo),
            DestinationKind.Server => await ServerAvailableAsync(manual, stop),
            _ => false,
        };
        var decision = Schedule.Decide(cfg, DateTimeOffset.Now, manual, onBattery, pct, available);
        lock (gate)
        {
            wait = decision switch
            {
                Decision.LowBattery => WaitReason.LowBattery,
                Decision.DestinationMissing => WaitReason.DestinationMissing,
                Decision.Paused => WaitReason.Paused,
                _ => WaitReason.None,
            };
            // só apaga o pedido manual que este tick viu (ou satisfez com um backup); quem chegou no meio espera o próximo
            if (manual || decision == Decision.Run) manualRequested = false;
        }
        if (decision == Decision.Run) await BackupAsync(stop);
    }

    async Task BackupAsync(CancellationToken stop)
    {
        if (!await repoLock.WaitAsync(0, stop)) return;
        lock (gate) { running = true; runningSince = DateTimeOffset.Now; }
        string? error = null;
        try
        {
            if (!KopiaRunner.Installed) error = "kopiaMissing";
            else if (cfg.Destination == DestinationKind.Server && !await EnsureServerConnectedAsync(stop)) error = Err.Unreachable;
            else
            {
                if (policiesDirty) await ApplyPoliciesAsync(stop);
                var sources = ActiveSources().Select(s => s.Path).ToList();
                var r = await kopia.RunAsync(KopiaCli.Snapshot(sources), secrets.RepoPassword, stop);
                if (!r.Ok) error = r.Error;
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            log.LogError(e, "backup");
            error = Err.Unknown;
        }
        finally
        {
            lock (gate) { running = false; runningSince = null; }
            repoLock.Release();
        }

        var now = DateTimeOffset.Now;
        if (error is null)
        {
            var (files, bytes) = await TotalsAsync(stop);
            Update(c => c with { LastSuccess = now, LastAttempt = now, LastError = null, LastFiles = files, LastBytes = bytes });
            log.LogInformation("backup feito: {Files} arquivos", files);
        }
        else
        {
            if (error == Err.Unreachable) connectedUrl = null;
            Update(c => c with { LastAttempt = now, LastError = error });
        }
    }

    // soma do último backup de cada origem
    async Task<(long, long)> TotalsAsync(CancellationToken ct)
    {
        try
        {
            var r = await kopia.RunAsync(KopiaCli.SnapshotList(), secrets.RepoPassword, ct, TimeSpan.FromMinutes(2));
            if (!r.Ok) return (cfg.LastFiles, cfg.LastBytes);
            var latest = KopiaCli.ParseSnapshots(r.Out).GroupBy(s => s.Source, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(s => s.Start).First()).ToList();
            return (latest.Sum(s => s.Files), latest.Sum(s => s.Bytes));
        }
        catch (Exception) { return (cfg.LastFiles, cfg.LastBytes); }
    }

    async Task ApplyPoliciesAsync(CancellationToken ct)
    {
        // falhas aqui não impedem o backup (no servidor, a política geral é do servidor)
        if (cfg.Destination == DestinationKind.Disk)
            await kopia.RunAsync(KopiaCli.GlobalPolicy(), secrets.RepoPassword, ct, TimeSpan.FromMinutes(2));
        foreach (var s in AllSources())
        {
            await kopia.RunAsync(KopiaCli.ClearIgnores(s.Path), secrets.RepoPassword, ct, TimeSpan.FromMinutes(2));
            await kopia.RunAsync(KopiaCli.SourcePolicy(s.Path, Sources.Ignores(s, cfg.Excluded)), secrets.RepoPassword, ct, TimeSpan.FromMinutes(2));
        }
        lock (gate) policiesDirty = false;
    }

    // ------------------------------------------------------------ servidor

    IEnumerable<string> ServerHosts() => new[] { cfg.ServerHost ?? "", cfg.ServerIp ?? "" };

    // só testa o servidor quando um backup está na hora: cada teste vira uma linha "TLS handshake error" no
    // registro do servidor, então nada de testar a cada 30 segundos à toa
    async Task<bool> ServerAvailableAsync(bool manual, CancellationToken ct)
    {
        var due = Schedule.Next(cfg) is not { } next || next <= DateTimeOffset.Now;
        if (!manual && !due) return true;
        return await ReachableUrlAsync(ct) is not null;
    }

    async Task<string?> ReachableUrlAsync(CancellationToken ct)
    {
        foreach (var url in new[] { cfg.ServerUrl, cfg.ServerUrlIp }.Where(u => !string.IsNullOrEmpty(u)))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) continue;
            try
            {
                using var tcp = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(3));
                await tcp.ConnectAsync(u.Host, u.Port, cts.Token);
                return url;
            }
            catch (Exception e) when (e is SocketException or OperationCanceledException && !ct.IsCancellationRequested) { }
        }
        return null;
    }

    // conecta pelo endereço que responde agora (nome .local ou IP)
    async Task<bool> EnsureServerConnectedAsync(CancellationToken ct)
    {
        var url = await ReachableUrlAsync(ct);
        if (url is null) return false;
        if (url == connectedUrl) return true;
        var r = await kopia.RunAsync(KopiaCli.ConnectServer(url, cfg.Fingerprint!, KopiaUser(), cfg.PcName!), secrets.RepoPassword, ct, TimeSpan.FromMinutes(2));
        if (r.Ok) connectedUrl = url;
        return r.Ok;
    }

    // no servidor, o usuário do Kopia é o que o pareamento devolveu
    string KopiaUser() => cfg.ServerUser ?? "backeddy";

    async Task PollAlertsAsync(CancellationToken ct)
    {
        if (cfg.Destination != DestinationKind.Server || secrets.ServerPassword is null) return;
        var list = await server.AlertsAsync(ServerHosts(), secrets.ServerPassword, ct);
        if (list is not null) lock (gate) alerts = list;
    }
}
