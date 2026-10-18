using System.Globalization;

namespace Backeddy.Core;

/// <summary>Para onde vai uma restauração.</summary>
public static class RestorePlan
{
    /// <summary>
    /// "Ao lado": mesmo lugar, nome novo. C:\Docs\nota.txt → C:\Docs\nota (restaurado 2026-10-17 10h00).txt.
    /// Se já existir, acrescenta " 2", " 3"…
    /// </summary>
    public static string Beside(string original, bool isDir, DateTimeOffset when, string word, Func<string, bool> exists)
    {
        var path = original.TrimEnd('\\');
        var dir = Parent(path);
        var name = path[(dir.Length)..].TrimStart('\\');
        string stem = name, ext = "";
        if (!isDir)
        {
            int dot = name.LastIndexOf('.');
            if (dot > 0) { stem = name[..dot]; ext = name[dot..]; }
        }
        var tag = $"{word} {when.ToLocalTime().ToString("yyyy-MM-dd HH'h'mm", CultureInfo.InvariantCulture)}";
        for (int n = 1; ; n++)
        {
            var extra = n == 1 ? "" : $" {n}";
            var candidate = $"{dir.TrimEnd('\\')}\\{stem} ({tag}{extra}){ext}";
            if (!exists(candidate)) return candidate;
        }
    }

    /// <summary>O caminho fica dentro de alguma origem do backup? (o serviço só restaura aí)</summary>
    public static bool InsideSources(string path, IEnumerable<SourceInfo> sources)
    {
        var p = path.TrimEnd('\\') + "\\";
        if (p.Contains("\\..\\")) return false;
        return sources.Any(s => p.StartsWith(s.Path.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
    }

    static string Parent(string path)
    {
        int i = path.LastIndexOf('\\');
        return i < 0 ? "" : path[..i];
    }
}
