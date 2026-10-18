using System.Diagnostics;
using System.Text;
using Backeddy.Core;

namespace Backeddy.Service;

public sealed record KopiaResult(int Code, string Out, string Err)
{
    public bool Ok => Code == 0;
    public string Error => KopiaCli.Classify(Err + "\n" + Out);
}

/// <summary>Roda o kopia.exe que vem junto com o serviço, com configuração e cache próprios.</summary>
public sealed class KopiaRunner(ILogger<KopiaRunner> log)
{
    static string Exe => Path.Combine(AppContext.BaseDirectory, "kopia.exe");
    static string Home => Path.Combine(Secrets.PrivateDir, "kopia");

    public static bool Installed => File.Exists(Exe);

    public async Task<KopiaResult> RunAsync(string[] args, string? password, CancellationToken ct, TimeSpan? timeout = null)
    {
        Directory.CreateDirectory(Home);
        var psi = new ProcessStartInfo(Exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["KOPIA_CONFIG_PATH"] = Path.Combine(Home, "repository.config");
        psi.Environment["KOPIA_CACHE_DIRECTORY"] = Path.Combine(Home, "cache");
        psi.Environment["KOPIA_LOG_DIR"] = Path.Combine(AppInfo.LogDir, "kopia");
        psi.Environment["KOPIA_CHECK_FOR_UPDATES"] = "false";
        psi.Environment["KOPIA_PERSIST_CREDENTIALS_ON_CONNECT"] = "false";
        if (password is not null) psi.Environment["KOPIA_PASSWORD"] = password;

        using var p = new Process { StartInfo = psi };
        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (sbOut) sbOut.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (sbErr) sbErr.AppendLine(e.Data); };
        var shown = args.Length > 2 ? string.Join(' ', args.Take(3)) : string.Join(' ', args);
        log.LogInformation("kopia {Args}", shown);
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout is not null) cts.CancelAfter(timeout.Value);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch (Exception) { }
            throw;
        }
        p.WaitForExit();   // garante que a saída foi toda lida
        var r = new KopiaResult(p.ExitCode, sbOut.ToString(), sbErr.ToString());
        if (!r.Ok) log.LogWarning("kopia {Args} saiu com {Code}: {Err}", shown, r.Code, Tail(r.Err));
        return r;
    }

    static string Tail(string s) => s.Length > 2000 ? s[^2000..] : s;
}
