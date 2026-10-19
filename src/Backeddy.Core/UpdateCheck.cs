using System.Net.Http;
using System.Text.Json;

namespace Backeddy.Core;

/// <summary>Uma versão nova publicada no GitHub.</summary>
public sealed record UpdateInfo(string Version, string Url);

/// <summary>
/// Pergunta ao GitHub se existe uma release mais nova que esta. Só avisa: não baixa nem instala nada.
/// Qualquer falha (sem internet, limite do GitHub) vira "nada a avisar".
/// </summary>
public static class UpdateCheck
{
    public const string LatestApi = "https://api.github.com/repos/BackEddy/windows/releases/latest";

    /// <summary>"v1.2.0" → 1.2.0. Aceita "1.2.0-beta" (ignora o sufixo). Outra coisa → null.</summary>
    internal static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var s = text.Trim().TrimStart('v', 'V');
        var cut = s.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut >= 0) s = s[..cut];
        if (!System.Version.TryParse(s, out var v)) return null;
        // "1.2" e "1.2.0.0" comparam igual a "1.2.0"
        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
    }

    public static bool IsNewer(string? latest, string? current)
    {
        var l = ParseVersion(latest);
        var c = ParseVersion(current);
        return l is not null && c is not null && l > c;
    }

    /// <summary>Lê a resposta de /releases/latest. Rascunho e pré-lançamento não contam.</summary>
    internal static UpdateInfo? Parse(string json, string current)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True) return null;
            if (root.TryGetProperty("prerelease", out var p) && p.ValueKind == JsonValueKind.True) return null;
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            if (!IsNewer(tag, current)) return null;
            var page = root.TryGetProperty("html_url", out var u) ? u.GetString() : null;
            // só abrimos endereços do próprio repositório
            if (page is null || !page.StartsWith(AppInfo.RepoUrl + "/", StringComparison.OrdinalIgnoreCase))
                page = AppInfo.RepoUrl + "/releases/latest";
            return new UpdateInfo(ParseVersion(tag)!.ToString(), page);
        }
        catch (Exception) { return null; }
    }

    public static async Task<UpdateInfo?> CheckAsync(string current, HttpClient? http = null, CancellationToken ct = default)
    {
        if (ParseVersion(current) is null) return null; // "dev"
        var own = http is null;
        http ??= new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, LatestApi);
            req.Headers.UserAgent.ParseAdd("Backeddy-for-Windows/" + current);
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var res = await http.SendAsync(req, ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return null;
            return Parse(await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false), current);
        }
        catch (Exception) { return null; }
        finally { if (own) http.Dispose(); }
    }
}
