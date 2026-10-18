namespace Backeddy.Core;

/// <summary>
/// Referências do Kopia e histórico de um arquivo.
/// Uma referência é "objeto da pasta" ou "objeto da pasta/nome": assim o Kopia sabe o nome, a data e se é pasta.
/// </summary>
public static class FileHistory
{
    public static string Ref(string parentObj, string name) => parentObj + "/" + name;

    /// <summary>Referência aceitável vinda do app: objeto (letras e números) e, opcionalmente, nomes sem "..".</summary>
    public static bool IsRef(string? s)
    {
        if (string.IsNullOrEmpty(s) || s.Length > 4000 || s.Contains('\n') || s.Contains('\\') || s.Contains('\0')) return false;
        var parts = s.Split('/');
        if (parts[0].Length is 0 or > 200 || !parts[0].All(char.IsLetterOrDigit)) return false;
        return parts.Skip(1).All(p => p.Length > 0 && p != "." && p != "..");
    }

    /// <summary>
    /// Versões diferentes do arquivo relPath (ex.: "Documentos/nota.txt") nos backups de uma origem, da mais nova para
    /// a mais antiga. list(obj) devolve o conteúdo de uma pasta; pastas iguais têm o mesmo objeto, então o cache evita
    /// repetir a leitura.
    /// </summary>
    public static async Task<List<FileVersion>> CollectAsync(IEnumerable<SnapshotInfo> snaps, string relPath,
        Func<string, Task<List<DirEntry>?>> list, int maxSnapshots = 300)
    {
        var parts = relPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return new();
        var cache = new Dictionary<string, List<DirEntry>?>();
        async Task<List<DirEntry>?> Cached(string obj)
        {
            if (!cache.TryGetValue(obj, out var v)) cache[obj] = v = await list(obj);
            return v;
        }

        // do mais antigo para o mais novo: cada conteúdo novo é uma versão
        var found = new List<(SnapshotInfo Snap, DirEntry Entry, string Parent)>();
        foreach (var s in snaps.OrderByDescending(s => s.Start).Take(maxSnapshots).OrderBy(s => s.Start))
        {
            string obj = s.RootObj;
            DirEntry? entry = null;
            for (int i = 0; i < parts.Length; i++)
            {
                var dir = await Cached(obj);
                entry = dir?.FirstOrDefault(e => string.Equals(e.Name, parts[i], StringComparison.OrdinalIgnoreCase));
                if (entry is null) break;
                if (i < parts.Length - 1)
                {
                    if (!entry.IsDir) { entry = null; break; }
                    obj = entry.Obj;
                }
            }
            if (entry is null || entry.IsDir) continue;
            if (found.Count > 0 && found[^1].Entry.Obj == entry.Obj) continue;   // mesmo conteúdo da versão anterior
            found.Add((s, entry, obj));
        }
        return found.Select(f => new FileVersion(f.Snap.Start, f.Entry.Size, f.Entry.Modified, Ref(f.Parent, f.Entry.Name)))
            .Reverse().ToList();
    }
}
