namespace Backeddy.Core;

/// <summary>
/// O que entra no backup: a pasta do usuário e os outros discos internos. Tudo entra; a pessoa só tira o que não quiser.
/// As exclusões viram regras de "ignorar" do Kopia (estilo .gitignore), relativas a cada origem.
/// </summary>
public static class Sources
{
    /// <summary>Lixo conhecido dentro da pasta do usuário (temporários, caches de navegador e de jogos, OneDrive).</summary>
    public static readonly string[] ProfileIgnores =
    {
        "/AppData/Local/Temp/",
        "/AppData/Local/Microsoft/Windows/INetCache/",
        "/AppData/Local/Microsoft/Windows/Explorer/",
        "/AppData/Local/Microsoft/Windows/WebCache/",
        "/AppData/Local/CrashDumps/",
        "/AppData/Local/D3DSCache/",
        "/AppData/Local/NVIDIA/DXCache/",
        "/AppData/Local/NVIDIA/GLCache/",
        "/AppData/Local/AMD/DxCache/",
        "/AppData/Local/Packages/*/AC/INetCache/",
        "/AppData/Local/Google/Chrome/User Data/*/Cache/",
        "/AppData/Local/Google/Chrome/User Data/*/Code Cache/",
        "/AppData/Local/Microsoft/Edge/User Data/*/Cache/",
        "/AppData/Local/Microsoft/Edge/User Data/*/Code Cache/",
        "/AppData/Local/BraveSoftware/Brave-Browser/User Data/*/Cache/",
        "/AppData/Local/Mozilla/Firefox/Profiles/*/cache2/",
        "/AppData/Roaming/Microsoft/Windows/Recent/",
        // OneDrive: arquivos "só na nuvem" seriam baixados ao serem lidos; o OneDrive já tem uma cópia
        "/OneDrive/",
        "/OneDrive - */",
        "ntuser.dat*",
        "NTUSER.DAT*",
    };

    /// <summary>Arquivos de sistema na raiz dos outros discos.</summary>
    public static readonly string[] DriveIgnores =
    {
        "/$Recycle.Bin/",
        "/System Volume Information/",
        "/Config.Msi/",
        "/pagefile.sys",
        "/swapfile.sys",
        "/hiberfil.sys",
        "/DumpStack.log*",
        "/Backeddy/",            // um backup do Backeddy noutro disco não entra de novo
    };

    /// <summary>
    /// Origens do backup. fixedDrives: raízes dos discos internos (ex.: "D:\"); systemRoot: "C:\" (só a pasta do usuário entra);
    /// destinationRoot: o HD de destino, que nunca entra.
    /// </summary>
    public static List<SourceInfo> Compute(string profile, IEnumerable<string> fixedDrives, string systemRoot, string? destinationRoot)
    {
        var list = new List<SourceInfo> { new(profile.TrimEnd('\\'), "profile") };
        foreach (var d in fixedDrives)
        {
            var root = NormRoot(d);
            if (Same(root, systemRoot) || (destinationRoot is not null && Same(root, destinationRoot))) continue;
            if (list.Any(s => Same(s.Path, root))) continue;
            list.Add(new SourceInfo(root, "drive"));
        }
        return list;
    }

    /// <summary>Regras de ignorar de uma origem: as padrão + as pastas que a pessoa tirou (só as que ficam dentro dela).</summary>
    public static List<string> Ignores(SourceInfo source, IEnumerable<string> excluded)
    {
        var rules = new List<string>(source.Label == "profile" ? ProfileIgnores : DriveIgnores);
        var root = source.Path.TrimEnd('\\') + "\\";
        foreach (var ex in excluded)
        {
            var e = ex.TrimEnd('\\');
            if (!e.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            var rel = e[root.Length..].Replace('\\', '/');
            if (rel.Length == 0) continue;
            rules.Add("/" + EscapeGlob(rel) + "/");
        }
        return rules;
    }

    /// <summary>A origem inteira foi tirada (ex.: o disco D: todo)?</summary>
    public static bool IsExcludedEntirely(SourceInfo s, IEnumerable<string> excluded) =>
        excluded.Any(e => Same(e, s.Path));

    // caracteres especiais do .gitignore no nome de uma pasta viram literais
    static string EscapeGlob(string p) =>
        p.Replace("[", "\\[").Replace("]", "\\]").Replace("*", "\\*").Replace("?", "\\?");

    static string NormRoot(string d) => d.Length == 2 && d[1] == ':' ? d + "\\" : d;

    static bool Same(string a, string b) =>
        string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
}
