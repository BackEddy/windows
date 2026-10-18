using System.Text.Json;

namespace Backeddy.Core;

/// <summary>Um backup (snapshot) de uma origem, como o Kopia lista.</summary>
public sealed record SnapshotInfo(string Id, string Source, DateTimeOffset Start, string RootObj, long Files, long Bytes, string? Run);

/// <summary>
/// Monta os comandos do kopia.exe e lê as respostas dele. Quem executa é o serviço; aqui só a parte testável.
/// A senha nunca vai na linha de comando: vai na variável KOPIA_PASSWORD.
/// </summary>
public static class KopiaCli
{
    /// <summary>Usuário fixo no HD: depois de reinstalar o Windows (mesmo nome de PC), os backups antigos aparecem.</summary>
    public const string DiskUser = "backeddy";

    public static string[] CreateFilesystem(string repoPath, string host) =>
        new[] { "repository", "create", "filesystem", "--path", repoPath, "--description", "Backeddy", "--no-persist-credentials",
                "--override-username", DiskUser, "--override-hostname", host };

    public static string[] ConnectFilesystem(string repoPath, string host) =>
        new[] { "repository", "connect", "filesystem", "--path", repoPath, "--no-persist-credentials",
                "--override-username", DiskUser, "--override-hostname", host };

    public static string[] ConnectServer(string url, string fingerprint, string user, string host) =>
        new[] { "repository", "connect", "server", "--url", url, "--server-cert-fingerprint", fingerprint,
                "--override-username", user, "--override-hostname", host, "--no-persist-credentials" };

    public static string[] Disconnect() => new[] { "repository", "disconnect" };

    /// <summary>Quanto histórico guardar (parecido com o Time Machine) e compressão.</summary>
    public static string[] GlobalPolicy() =>
        new[] { "policy", "set", "--global", "--keep-latest", "24", "--keep-hourly", "24", "--keep-daily", "30",
                "--keep-weekly", "12", "--keep-monthly", "24", "--keep-annual", "5", "--compression", "zstd-fastest",
                "--ignore-file-errors=true", "--ignore-dir-errors=true" };

    /// <summary>Regras de uma origem: o que ignorar e a cópia de sombra (VSS) para arquivos abertos.</summary>
    public static string[] SourcePolicy(string source, IEnumerable<string> ignores)
    {
        var a = new List<string> { "policy", "set", source, "--enable-volume-shadow-copy=when-available" };
        foreach (var i in ignores) { a.Add("--add-ignore"); a.Add(i); }
        return a.ToArray();
    }

    /// <summary>Limpa as regras de ignorar da origem (antes de aplicar a lista atual).</summary>
    public static string[] ClearIgnores(string source) => new[] { "policy", "set", source, "--clear-ignore" };

    public static string[] Snapshot(IEnumerable<string> sources)
    {
        var a = new List<string> { "snapshot", "create" };
        a.AddRange(sources);
        a.Add("--description");
        a.Add("Backeddy");
        return a.ToArray();
    }

    public static string[] SnapshotList() => new[] { "snapshot", "list", "--json" };

    public static string[] Show(string obj) => new[] { "show", obj };

    public static string[] Restore(string obj, string target, bool overwrite) =>
        new[] { "restore", obj, target, overwrite ? "--overwrite-files" : "--no-overwrite-files",
                "--overwrite-directories", "--ignore-permission-errors" };

    /// <summary>Lê "kopia snapshot list --json" (vários formatos de versões diferentes são aceitos).</summary>
    public static List<SnapshotInfo> ParseSnapshots(string json)
    {
        var list = new List<SnapshotInfo>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            string id = Str(e, "id") ?? "";
            string source = e.TryGetProperty("source", out var src) ? Str(src, "path") ?? "" : "";
            var start = DateTimeOffset.TryParse(Str(e, "startTime"), out var st) ? st : DateTimeOffset.MinValue;
            string root = "";
            long files = 0, bytes = 0;
            if (e.TryGetProperty("rootEntry", out var re))
            {
                root = Str(re, "obj") ?? "";
                if (re.TryGetProperty("summ", out var summ))
                {
                    files = Num(summ, "files");
                    bytes = Num(summ, "size");
                }
            }
            string? run = null;
            if (e.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object)
                foreach (var t in tags.EnumerateObject())
                    if (t.Name.EndsWith("run", StringComparison.OrdinalIgnoreCase)) run = t.Value.GetString();
            if (root.Length > 0) list.Add(new SnapshotInfo(id, source, start, root, files, bytes, run));
        }
        return list;
    }

    /// <summary>
    /// Junta os backups das origens feitos na mesma rodada (começados com menos de 30 min de diferença, uma vez por
    /// origem) num "momento" da linha do tempo.
    /// </summary>
    public static List<TimePoint> GroupPoints(IEnumerable<SnapshotInfo> snaps, IReadOnlyList<SourceInfo> sources)
    {
        string Label(string path) => sources.FirstOrDefault(s => string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase))?.Label
                                     ?? (path.TrimEnd('\\').Length <= 2 ? "drive" : "profile");
        var groups = new List<List<SnapshotInfo>>();
        foreach (var s in snaps.OrderByDescending(s => s.Start))
        {
            var g = groups.LastOrDefault();
            if (g is null || g[0].Start - s.Start > TimeSpan.FromMinutes(30)
                || g.Any(x => string.Equals(x.Source, s.Source, StringComparison.OrdinalIgnoreCase)))
                groups.Add(g = new List<SnapshotInfo>());
            g.Add(s);
        }
        return groups.Select(g => new TimePoint(
                g.Min(s => s.Start).ToUnixTimeSeconds().ToString(),
                g.Max(s => s.Start),
                g.Select(s => new PointItem(s.Source, Label(s.Source), s.RootObj))
                 .OrderBy(i => i.Label == "profile" ? 0 : 1).ThenBy(i => i.Source).ToList()))
            .ToList();
    }

    /// <summary>Lê uma pasta de um backup ("kopia show OBJ" devolve o conteúdo da pasta em JSON).</summary>
    public static List<DirEntry> ParseDirectory(string json)
    {
        var list = new List<DirEntry>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array) return list;
        foreach (var e in entries.EnumerateArray())
        {
            var name = Str(e, "name");
            var obj = Str(e, "obj");
            if (name is null || obj is null) continue;
            bool dir = Str(e, "type") == "d";
            long size = Num(e, "size");
            if (dir && e.TryGetProperty("summ", out var summ)) size = Num(summ, "size");
            DateTimeOffset? mt = DateTimeOffset.TryParse(Str(e, "mtime"), out var m) ? m : null;
            list.Add(new DirEntry(name, dir, size, mt, obj));
        }
        return list.OrderByDescending(d => d.IsDir).ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Erro do Kopia → código que o app traduz.</summary>
    public static string Classify(string stderr)
    {
        var s = stderr.ToLowerInvariant();
        if (s.Contains("invalid repository password") || s.Contains("invalid password") || s.Contains("unable to decrypt")) return Err.WrongKey;
        if (s.Contains("certificate") || s.Contains("fingerprint") || s.Contains("x509")) return Err.Certificate;
        if (s.Contains("no space left") || s.Contains("not enough space") || s.Contains("disk full")) return Err.NoSpace;
        if (s.Contains("connection refused") || s.Contains("no such host") || s.Contains("timeout") || s.Contains("unreachable")
            || s.Contains("unable to connect") || s.Contains("connectex")) return Err.Unreachable;
        // o servidor fechou a conexão no meio (reiniciou, ficou sem memória, caiu a rede)
        if (s.Contains("async writes") || s.Contains(": eof") || s.Contains("connection reset") || s.Contains("unavailable")
            || s.Contains("broken pipe")) return Err.Dropped;
        return Err.Unknown;
    }

    static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static long Num(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;
}
