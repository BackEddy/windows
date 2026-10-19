using System.Net;
using System.Text;
using Backeddy.Core;
using Xunit;

public class SourcesTests
{
    [Fact]
    public void ProfileAndOtherDrivesButNotSystemOrDestination()
    {
        var s = Sources.Compute(@"C:\Users\ana", new[] { @"C:\", @"D:\", "E:", @"F:\" }, @"C:\", @"F:\");
        Assert.Equal(new[] { @"C:\Users\ana", @"D:\", @"E:\" }, s.Select(x => x.Path).ToArray());
        Assert.Equal("profile", s[0].Label);
        Assert.Equal("drive", s[1].Label);
    }

    [Fact]
    public void UserExclusionsBecomeAnchoredIgnoreRules()
    {
        var profile = new SourceInfo(@"C:\Users\ana", "profile");
        var rules = Sources.Ignores(profile, new[] { @"C:\Users\ana\Downloads", @"C:\Users\ana\Jogos [old]\", @"D:\Filmes" });
        Assert.Contains("/Downloads/", rules);
        Assert.Contains(@"/Jogos \[old\]/", rules);
        Assert.DoesNotContain(rules, r => r.Contains("Filmes"));
        Assert.Contains("/AppData/Local/Temp/", rules);
        Assert.Contains("/OneDrive/", rules);
    }

    [Fact]
    public void DriveRulesSkipSystemFiles()
    {
        var d = new SourceInfo(@"D:\", "drive");
        var rules = Sources.Ignores(d, new[] { @"D:\Filmes" });
        Assert.Contains("/$Recycle.Bin/", rules);
        Assert.Contains("/pagefile.sys", rules);
        Assert.Contains("/Filmes/", rules);
        Assert.True(Sources.IsExcludedEntirely(d, new[] { @"D:" }));
        Assert.False(Sources.IsExcludedEntirely(d, new[] { @"D:\Filmes" }));
    }
}

public class RecoveryKeyTests
{
    [Fact]
    public void NewKeysAreGroupedAndUnambiguous()
    {
        var k = RecoveryKey.New();
        Assert.Equal(29, k.Length);   // 24 caracteres + 5 hífens
        Assert.Equal(6, k.Split('-').Length);
        Assert.DoesNotContain(k, c => "01OIL".Contains(c));
        Assert.NotEqual(k, RecoveryKey.New());
    }

    [Fact]
    public void TypedKeyIsNormalized()
    {
        var k = RecoveryKey.New();
        var messy = " " + k.Replace("-", " ").ToLowerInvariant() + " ";
        Assert.Equal(k, RecoveryKey.Normalize(messy));
        Assert.Null(RecoveryKey.Normalize("ABCD-EFGH"));
        Assert.Null(RecoveryKey.Normalize(k.Replace(k[0], 'O')));
    }
}

public class ScheduleTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    static BackupConfig Cfg(DateTimeOffset? ok, DateTimeOffset? tried) =>
        new() { Destination = DestinationKind.Disk, LastSuccess = ok, LastAttempt = tried };

    [Fact]
    public void RunsHourly()
    {
        Assert.Equal(Decision.Run, Schedule.Decide(Cfg(null, null), Now, false, false, null, true));
        Assert.Equal(Decision.NotDue, Schedule.Decide(Cfg(Now.AddMinutes(-30), Now.AddMinutes(-30)), Now, false, false, null, true));
        Assert.Equal(Decision.Run, Schedule.Decide(Cfg(Now.AddMinutes(-61), Now.AddMinutes(-61)), Now, false, false, null, true));
    }

    [Fact]
    public void RetriesFifteenMinutesAfterAFailure()
    {
        var c = Cfg(Now.AddHours(-5), Now.AddMinutes(-10));
        Assert.Equal(Decision.NotDue, Schedule.Decide(c, Now, false, false, null, true));
        Assert.Equal(Now.AddMinutes(5), Schedule.Next(c));
        Assert.Equal(Decision.Run, Schedule.Decide(c, Now.AddMinutes(6), false, false, null, true));
    }

    [Fact]
    public void WaitsForBatteryAndDestination()
    {
        var c = Cfg(null, null);
        Assert.Equal(Decision.LowBattery, Schedule.Decide(c, Now, false, true, 15, true));
        Assert.Equal(Decision.Run, Schedule.Decide(c, Now, false, true, 50, true));
        Assert.Equal(Decision.Run, Schedule.Decide(c, Now, true, true, 15, true));   // "Fazer backup agora" ignora a bateria
        Assert.Equal(Decision.DestinationMissing, Schedule.Decide(c, Now, false, false, null, false));
        Assert.Equal(Decision.NotConfigured, Schedule.Decide(new BackupConfig(), Now, true, false, null, true));
    }

    [Fact]
    public void PausedBackupWaitsUntilTheEnd()
    {
        var c = Cfg(null, null) with { PausedUntil = Now.AddMinutes(30) };
        Assert.Equal(Decision.Paused, Schedule.Decide(c, Now, false, false, null, true));
        Assert.Equal(Decision.Run, Schedule.Decide(c, Now.AddMinutes(30), false, false, null, true));   // pausa venceu e era a hora
    }

    [Fact]
    public void ManualBackupIgnoresThePause()
    {
        var c = Cfg(Now.AddMinutes(-30), Now.AddMinutes(-30)) with { PausedUntil = Now.AddHours(2) };
        Assert.Equal(Decision.Paused, Schedule.Decide(c, Now, false, false, null, true));
        Assert.Equal(Decision.Run, Schedule.Decide(c, Now, true, false, null, true));
    }

    [Fact]
    public void NextBackupRespectsThePause()
    {
        var c = Cfg(Now.AddMinutes(-30), Now.AddMinutes(-30));   // próximo calculado: daqui a 30 minutos
        Assert.Equal(Now.AddHours(2), Schedule.Next(c with { PausedUntil = Now.AddHours(2) }));
        Assert.Equal(Now.AddMinutes(30), Schedule.Next(c with { PausedUntil = Now.AddHours(-1) }));   // pausa vencida não interfere
        Assert.Equal(Now.AddHours(1), Schedule.Next(new BackupConfig { Destination = DestinationKind.Disk, PausedUntil = Now.AddHours(1) }));
    }

    [Fact]
    public void PauseUntilComputesTheEnd()
    {
        Assert.Null(Schedule.PauseUntil(PauseKind.Resume, Now));
        Assert.Equal(Now.AddHours(1), Schedule.PauseUntil(PauseKind.Hour, Now));
        Assert.Equal(new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero), Schedule.PauseUntil(PauseKind.Tomorrow, Now));
        // madrugada em UTC-3: 08:00 locais do dia seguinte
        var lateNight = new DateTimeOffset(2026, 10, 17, 2, 0, 0, TimeSpan.FromHours(-3));
        Assert.Equal(new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.FromHours(-3)), Schedule.PauseUntil(PauseKind.Tomorrow, lateNight));
    }

    [Fact]
    public void OverdueAfterThreeDays()
    {
        Assert.Null(Schedule.OverdueDays(Cfg(Now - TimeSpan.FromDays(2) - TimeSpan.FromHours(23), null), Now));   // ainda não fez 3 dias
        Assert.Equal(3, Schedule.OverdueDays(Cfg(Now - TimeSpan.FromDays(3), null), Now));
        Assert.Equal(5, Schedule.OverdueDays(Cfg(Now - TimeSpan.FromDays(5) - TimeSpan.FromHours(12), null), Now));   // dias inteiros, para baixo
        Assert.Null(Schedule.OverdueDays(Cfg(null, null), Now));   // sem backup ainda não é atraso
        Assert.Null(Schedule.OverdueDays(new BackupConfig { LastSuccess = Now - TimeSpan.FromDays(9) }, Now));   // sem destino
    }

    [Fact]
    public void KeepAwakeOnlyOnBatteryWhenAsked()
    {
        Assert.True(Schedule.ShouldKeepAwake(Cfg(null, null), false));   // tomada: sempre
        Assert.False(Schedule.ShouldKeepAwake(Cfg(null, null), true));   // bateria sem a opção: não
        Assert.True(Schedule.ShouldKeepAwake(Cfg(null, null) with { KeepAwakeOnBattery = true }, true));
        Assert.True(Schedule.ShouldKeepAwake(Cfg(null, null) with { KeepAwakeOnBattery = true }, false));
    }
}

public class KopiaTests
{
    const string SnapJson = """
    [
      {"id":"a1","source":{"host":"pc","userName":"backeddy","path":"C:\\Users\\ana"},"startTime":"2026-10-17T10:00:05Z",
       "rootEntry":{"name":"ana","type":"d","obj":"kA1","summ":{"size":1000,"files":10,"dirs":2}}},
      {"id":"a2","source":{"host":"pc","userName":"backeddy","path":"D:\\"},"startTime":"2026-10-17T10:03:00Z",
       "rootEntry":{"name":"D:","type":"d","obj":"kD1","summ":{"size":500,"files":3}}},
      {"id":"b1","source":{"host":"pc","userName":"backeddy","path":"C:\\Users\\ana"},"startTime":"2026-10-17T11:00:00Z",
       "rootEntry":{"name":"ana","type":"d","obj":"kA2","summ":{"size":1100,"files":11}}},
      {"id":"x","source":{"path":"C:\\Users\\ana"},"startTime":"2026-10-17T09:00:00Z","rootEntry":{}}
    ]
    """;

    [Fact]
    public void SnapshotsAreReadAndGroupedIntoMoments()
    {
        var snaps = KopiaCli.ParseSnapshots(SnapJson);
        Assert.Equal(3, snaps.Count);
        Assert.Equal(1000, snaps[0].Bytes);
        var sources = new List<SourceInfo> { new(@"C:\Users\ana", "profile"), new(@"D:\", "drive") };
        var points = KopiaCli.GroupPoints(snaps, sources);
        Assert.Equal(2, points.Count);
        Assert.Single(points[0].Items);
        Assert.Equal("kA2", points[0].Items[0].Obj);
        Assert.Equal(new[] { "kA1", "kD1" }, points[1].Items.Select(i => i.Obj).ToArray());
        Assert.Equal("profile", points[1].Items[0].Label);
    }

    [Fact]
    public void DirectoryListingPutsFoldersFirst()
    {
        const string dir = """
        {"stream":"kopia:directory","entries":[
          {"name":"notas.txt","type":"f","mode":"0666","mtime":"2026-10-01T12:00:00Z","size":42,"obj":"f1"},
          {"name":"Fotos","type":"d","mode":"0777","mtime":"2026-10-02T12:00:00Z","obj":"kF","summ":{"size":9000,"files":3}},
          {"name":"semobj","type":"f"}
        ]}
        """;
        var e = KopiaCli.ParseDirectory(dir);
        Assert.Equal(2, e.Count);
        Assert.True(e[0].IsDir);
        Assert.Equal(9000, e[0].Size);
        Assert.Equal(42, e[1].Size);
    }

    [Fact]
    public void ErrorsAreClassified()
    {
        Assert.Equal(Err.WrongKey, KopiaCli.Classify("ERROR invalid repository password"));
        Assert.Equal(Err.Certificate, KopiaCli.Classify("x509: certificate signed by unknown authority"));
        Assert.Equal(Err.Unreachable, KopiaCli.Classify("dial tcp: connectex: No connection could be made"));
        Assert.Equal(Err.NoSpace, KopiaCli.Classify("write: There is not enough space on the disk"));
        Assert.Equal(Err.Dropped, KopiaCli.Classify("ERROR flush error: error waiting for async writes: EOF: EOF"));
        Assert.Equal(Err.Unknown, KopiaCli.Classify("boom"));
    }

    [Fact]
    public void PasswordsNeverGoOnTheCommandLine()
    {
        var all = KopiaCli.ConnectServer("https://x.local:51515", "ab", "backeddy", "pc")
            .Concat(KopiaCli.CreateFilesystem(@"E:\Backeddy", "pc")).Concat(KopiaCli.ConnectFilesystem(@"E:\Backeddy", "pc"));
        Assert.DoesNotContain(all, a => a.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("--no-persist-credentials", KopiaCli.ConnectServer("u", "f", "u", "h"));
        var pol = KopiaCli.SourcePolicy(@"D:\", new[] { "/a/", "/b/" });
        Assert.Equal(2, pol.Count(a => a == "--add-ignore"));
    }
}

public class ServerClientTests
{
    sealed class Fake(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Seen { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Seen.Add(r);
            return Task.FromResult(f(r));
        }
    }

    static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task PairFallsBackToTheIpAndReadsTheAnswer()
    {
        var fake = new Fake(r => r.RequestUri!.Host == "casa.local"
            ? throw new HttpRequestException("no such host")
            : Json(HttpStatusCode.OK, """{"pc":"pc-sala","username":"backeddy","hostname":"pc-sala","password":"x","url":"https://casa.local:51515","fingerprint":"ab","name":"Casa"}"""));
        var (r, err) = await new ServerClient(new HttpClient(fake)).PairAsync(new[] { "casa.local", "192.0.2.5" }, "s", "PC-SALA");
        Assert.Null(err);
        Assert.Equal("pc-sala", r!.Hostname);
        Assert.Equal("ab", r.Fingerprint);
        Assert.Equal(2, fake.Seen.Count);
    }

    [Fact]
    public async Task WrongPasswordAndOldServerAreReported()
    {
        var c401 = new ServerClient(new HttpClient(new Fake(_ => Json(HttpStatusCode.Unauthorized, "wrong"))));
        Assert.Equal(Err.WrongPassword, (await c401.PairAsync(new[] { "h" }, "x", "pc")).Error);
        var c404 = new ServerClient(new HttpClient(new Fake(_ => Json(HttpStatusCode.NotFound, ""))));
        Assert.Equal("oldServer", (await c404.PairAsync(new[] { "h" }, "x", "pc")).Error);
    }

    [Fact]
    public async Task AlertsUseBasicAuth()
    {
        var fake = new Fake(_ => Json(HttpStatusCode.OK, """{"name":"Casa","alerts":[{"ID":"degraded","Text":"Falta um disco"}],"status":"..."}"""));
        var alerts = await new ServerClient(new HttpClient(fake)).AlertsAsync(new[] { "h" }, "segredo");
        Assert.Single(alerts!);
        Assert.Equal("degraded", alerts![0].Id);
        Assert.Equal("Basic", fake.Seen[0].Headers.Authorization!.Scheme);
    }
}

public class ConfigTests
{
    [Fact]
    public void SavesAndLoads()
    {
        var path = Path.Combine(Path.GetTempPath(), "bk-" + Guid.NewGuid().ToString("N"), "config.json");
        var c = new BackupConfig { Destination = DestinationKind.Disk, DiskRoot = @"E:\", DiskLabel = "Backup", Excluded = { @"C:\x" } };
        c.Save(path);
        var back = BackupConfig.Load(path);
        Assert.Equal(DestinationKind.Disk, back.Destination);
        Assert.Equal("Backup (E:)", back.DisplayName);
        Assert.Equal(@"E:\Backeddy", back.DiskRepo);
        Assert.Single(back.Excluded);
        Assert.Equal(DestinationKind.None, BackupConfig.Load(path + ".nao-existe").Destination);
    }

    [Fact]
    public void KeepsTheBatteryOptionAndOldFilesMeanOff()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bk-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "config.json");
        new BackupConfig { Destination = DestinationKind.Disk, KeepAwakeOnBattery = true }.Save(path);
        Assert.True(BackupConfig.Load(path).KeepAwakeOnBattery);
        // arquivo antigo, sem o campo: vale como desligado
        var old = Path.Combine(dir, "antigo.json");
        File.WriteAllText(old, """{"destination":"disk"}""");
        Assert.False(BackupConfig.Load(old).KeepAwakeOnBattery);
    }

    [Fact]
    public void RequestsCarryTypedData()
    {
        var req = Ipc.Request(Cmd.SetupDisk, new DiskSetup(@"E:\", null, @"C:\Users\ana", "ana"));
        var back = Ipc.Decode<IpcRequest>(Ipc.Encode(req))!;
        Assert.Equal(Cmd.SetupDisk, back.Cmd);
        Assert.Equal(@"E:\", Ipc.From<DiskSetup>(back.Data)!.Root);
        var resp = Ipc.Decode<IpcResponse>(Ipc.Encode(Ipc.Fail(Err.WrongKey)))!;
        Assert.False(resp.Ok);
        Assert.Equal(Err.WrongKey, resp.Error);
    }
}

public class RestorePlanTests
{
    static readonly DateTimeOffset When = new DateTimeOffset(2026, 10, 17, 10, 0, 0, TimeSpan.Zero).ToLocalTime();

    [Fact]
    public void FilesKeepTheirExtension()
    {
        var p = RestorePlan.Beside(@"C:\Users\ana\Docs\nota.txt", false, When, "restaurado", _ => false);
        Assert.Equal(@"C:\Users\ana\Docs\nota (restaurado 2026-10-17 " + When.ToString("HH") + "h00).txt", p);
    }

    [Fact]
    public void FoldersGetANumberWhenTheNameIsTaken()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var first = RestorePlan.Beside(@"D:\Fotos.2020\", true, When, "restored", taken.Contains);
        Assert.StartsWith(@"D:\Fotos.2020 (restored 2026-10-17 ", first);
        Assert.EndsWith(")", first);
        taken.Add(first);
        var second = RestorePlan.Beside(@"D:\Fotos.2020", true, When, "restored", taken.Contains);
        Assert.EndsWith(" 2)", second);
    }

    [Fact]
    public void OnlyInsideTheBackedUpFolders()
    {
        var src = new[] { new SourceInfo(@"C:\Users\ana", "profile"), new SourceInfo(@"D:\", "drive") };
        Assert.True(RestorePlan.InsideSources(@"C:\Users\ana\Docs\a.txt", src));
        Assert.True(RestorePlan.InsideSources(@"D:\Filmes", src));
        Assert.False(RestorePlan.InsideSources(@"C:\Windows\System32\x.dll", src));
        Assert.False(RestorePlan.InsideSources(@"C:\Users\anabel\x", src));
        Assert.False(RestorePlan.InsideSources(@"C:\Users\ana\..\..\Windows\x", src));
    }
}

public class FormatTests
{
    [Fact]
    public void SizesAndDates()
    {
        var old = Strings.Current;
        try
        {
            Strings.Current = Strings.Lang.Pt;
            Assert.Equal("512 B", Format.Size(512));
            Assert.Equal("1,5 KB", Format.Size(1536));
            Assert.Equal("20 GB", Format.Size(20L << 30));
            var now = new DateTimeOffset(new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Local));
            Assert.StartsWith("hoje", Format.When(now.AddHours(-1), now));
            Assert.StartsWith("ontem", Format.When(now.AddDays(-1), now));
            Assert.Equal(Strings.T("err.wrongKey"), Format.Error(Err.WrongKey));
            Assert.Equal(Strings.T("err.unknown"), Format.Error("qualquer"));
            Strings.Current = Strings.Lang.En;
            Assert.Equal("1.5 KB", Format.Size(1536));
        }
        finally { Strings.Current = old; }
    }

    [Fact]
    public void EveryErrorHasAText()
    {
        foreach (var f in typeof(Err).GetFields())
            Assert.Contains("err." + f.GetValue(null), Strings.Keys);
        Assert.Contains("err.kopiaMissing", Strings.Keys);
    }
}

public class FileHistoryTests
{
    static SnapshotInfo Snap(string root, int hour) =>
        new("s" + hour, @"C:\Users\ana", new DateTimeOffset(2026, 10, 17, hour, 0, 0, TimeSpan.Zero), root, 1, 1, null);

    static DirEntry D(string name, string obj) => new(name, true, 0, null, obj);
    static DirEntry F(string name, string obj, long size) => new(name, false, size, null, obj);

    [Fact]
    public async Task DistinctVersionsNewestFirstWithCache()
    {
        // 3 backups: o arquivo muda entre o 1º e o 2º; no 3º só outra pasta mudou
        var dirs = new Dictionary<string, List<DirEntry>>
        {
            ["r1"] = new() { D("Docs", "d1") }, ["r2"] = new() { D("Docs", "d2") }, ["r3"] = new() { D("Docs", "d2"), D("Novo", "n") },
            ["d1"] = new() { F("nota.txt", "fA", 10) }, ["d2"] = new() { F("nota.txt", "fB", 20) },
        };
        int calls = 0;
        var v = await FileHistory.CollectAsync(new[] { Snap("r1", 8), Snap("r2", 9), Snap("r3", 10) }, @"Docs\nota.txt",
            obj => { calls++; return Task.FromResult<List<DirEntry>?>(dirs.GetValueOrDefault(obj)); });
        Assert.Equal(2, v.Count);
        Assert.Equal(20, v[0].Size);
        Assert.Equal("d2/nota.txt", v[0].Ref);
        Assert.Equal(9, v[0].PointTime.Hour);
        Assert.Equal("d1/nota.txt", v[1].Ref);
        Assert.Equal(5, calls);   // d2 lido uma vez só
    }

    [Fact]
    public void RefsFromTheAppAreChecked()
    {
        Assert.True(FileHistory.IsRef("k1a2b3"));
        Assert.True(FileHistory.IsRef("k1a2b3/Minhas Fotos/foto (1).jpg"));
        Assert.False(FileHistory.IsRef("--overwrite"));
        Assert.False(FileHistory.IsRef("k1/../x"));
        Assert.False(FileHistory.IsRef("k1/a\\b"));
        Assert.False(FileHistory.IsRef(""));
    }
}

public class MdnsTests
{
    // resposta típica do Avahi: PTR + SRV + A, com nomes comprimidos
    static byte[] Answer()
    {
        var b = new List<byte> { 0, 0, 0x84, 0, 0, 0, 0, 3, 0, 0, 0, 0 };
        int svc = b.Count;
        void Name(params string[] labels) { foreach (var l in labels) { var x = System.Text.Encoding.UTF8.GetBytes(l); b.Add((byte)x.Length); b.AddRange(x); } b.Add(0); }
        void Ptr(int off) { b.Add((byte)(0xC0 | off >> 8)); b.Add((byte)off); }
        void Rr(ushort type, byte[] data) { b.Add((byte)(type >> 8)); b.Add((byte)type); b.AddRange(new byte[] { 0, 1, 0, 0, 0, 120, (byte)(data.Length >> 8), (byte)data.Length }); b.AddRange(data); }
        Name("_backeddy", "_tcp", "local");
        // PTR → "Backup da Casa._backeddy._tcp.local"
        var inst = new List<byte>(); var lab = System.Text.Encoding.UTF8.GetBytes("Backup da Casa");
        inst.Add((byte)lab.Length); inst.AddRange(lab); inst.Add(0xC0); inst.Add((byte)svc);
        int instOff = b.Count + 10 + 0;   // onde o nome da instância vai ficar (dentro do dado do PTR)
        Rr(12, inst.ToArray());
        // SRV de "Backup da Casa..." (ponteiro para a instância) → backup-da-casa.local:80
        Ptr(instOff);
        var srv = new List<byte> { 0, 0, 0, 0, 0, 80 }; var h = System.Text.Encoding.UTF8.GetBytes("backup-da-casa");
        srv.Add((byte)h.Length); srv.AddRange(h); srv.Add(5); srv.AddRange(System.Text.Encoding.UTF8.GetBytes("local")); srv.Add(0);
        Rr(33, srv.ToArray());
        Name("backup-da-casa", "local");
        Rr(1, new byte[] { 192, 168, 0, 85 });
        return b.ToArray();
    }

    [Fact]
    public void ParsesAvahiAnswer()
    {
        var r = new Mdns.Records();
        Mdns.Parse(Answer(), System.Net.IPAddress.Parse("192.168.0.85"), r);
        var s = r.Services("_backeddy._tcp.local");
        Assert.Single(s);
        Assert.Equal("Backup da Casa", s[0].Name);
        Assert.Equal("backup-da-casa.local", s[0].Host);
        Assert.Equal(80, s[0].Port);
        Assert.Equal("192.168.0.85", s[0].Ip);
    }

    [Fact]
    public void QueryIsWellFormed()
    {
        var q = Mdns.BuildQuery("_backeddy._tcp.local", 12);
        Assert.Equal(12 + 1 + 9 + 1 + 4 + 1 + 5 + 1 + 4, q.Length);
        Assert.Equal(1, q[5]);
    }
}
