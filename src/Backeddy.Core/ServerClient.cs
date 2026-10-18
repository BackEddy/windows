using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Backeddy.Core;

/// <summary>Resposta do pareamento (/api/pair do Backeddy Server 0.9+).</summary>
public sealed record PairResult(string Pc, string Username, string Hostname, string Password, string Url, string? UrlIp, string Fingerprint, string? Name, string? Ip);

/// <summary>Conversa com o painel do Backeddy Server (porta 80, rede local).</summary>
public sealed class ServerClient
{
    readonly HttpClient http;

    public ServerClient(HttpClient? http = null)
    {
        this.http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    /// <summary>Pareia este PC. Tenta cada endereço (nome .local e IP) até um responder.</summary>
    public async Task<(PairResult? Result, string? Error)> PairAsync(IEnumerable<string> hosts, string password, string pcName, CancellationToken ct = default)
    {
        string error = Err.Unreachable;
        foreach (var h in hosts.Where(h => !string.IsNullOrWhiteSpace(h)).Distinct())
        {
            try
            {
                using var resp = await http.PostAsJsonAsync($"http://{h}/api/pair", new { password, name = pcName }, ct);
                switch (resp.StatusCode)
                {
                    case HttpStatusCode.OK:
                        var r = await resp.Content.ReadFromJsonAsync<PairResult>(Ipc.Json, ct);
                        if (r is null || string.IsNullOrEmpty(r.Fingerprint)) return (null, Err.Unknown);
                        return (r, null);
                    case HttpStatusCode.Unauthorized: return (null, Err.WrongPassword);
                    case HttpStatusCode.TooManyRequests: return (null, Err.TooMany);
                    case HttpStatusCode.NotFound: return (null, "oldServer");   // servidor anterior à 0.9
                    default: error = Err.Unknown; break;
                }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { }
        }
        return (null, error);
    }

    /// <summary>Alertas do servidor (precisa da senha do servidor). Null se não deu para falar com ele.</summary>
    public async Task<List<Alert>?> AlertsAsync(IEnumerable<string> hosts, string password, CancellationToken ct = default)
    {
        foreach (var h in hosts.Where(h => !string.IsNullOrWhiteSpace(h)).Distinct())
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"http://{h}/api/status");
                req.Headers.Authorization = Basic(password);
                using var resp = await http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) continue;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                var list = new List<Alert>();
                if (doc.RootElement.TryGetProperty("alerts", out var a) && a.ValueKind == JsonValueKind.Array)
                    foreach (var x in a.EnumerateArray())
                    {
                        var id = x.TryGetProperty("ID", out var i) ? i.GetString() : null;
                        var text = x.TryGetProperty("Text", out var t) ? t.GetString() : null;
                        if (id is not null && text is not null) list.Add(new Alert(id, text));
                    }
                return list;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException) { }
        }
        return null;
    }

    /// <summary>Reconhece os alertas: o servidor para de bipar e lembra de novo em "days" dias.</summary>
    public async Task<bool> AckAsync(IEnumerable<string> hosts, string password, int days, CancellationToken ct = default)
    {
        foreach (var h in hosts.Where(h => !string.IsNullOrWhiteSpace(h)).Distinct())
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, $"http://{h}/api/ack")
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { days }), Encoding.UTF8, "application/json"),
                };
                req.Headers.Authorization = Basic(password);
                using var resp = await http.SendAsync(req, ct);
                if (resp.IsSuccessStatusCode) return true;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { }
        }
        return false;
    }

    static AuthenticationHeaderValue Basic(string password) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("backeddy:" + password)));
}
