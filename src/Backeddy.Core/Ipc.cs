using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backeddy.Core;

/// <summary>Pedido do app ao serviço. Uma linha JSON por mensagem.</summary>
public sealed record IpcRequest(string Cmd);

/// <summary>Estados que o app mostra.</summary>
public enum BackupState { Idle, Running, NotConfigured, Error }

/// <summary>Situação do serviço, respondida a "status".</summary>
public sealed record ServiceStatus
{
    public string Version { get; init; } = "";
    public DateTimeOffset StartedAt { get; init; }
    public BackupState State { get; init; } = BackupState.NotConfigured;
    public DateTimeOffset? LastBackup { get; init; }
    public string? Message { get; init; }
}

public static class Ipc
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);

    public static T? Decode<T>(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return default;
        try { return JsonSerializer.Deserialize<T>(line, Json); }
        catch (JsonException) { return default; }
    }

    /// <summary>Escreve uma mensagem (uma linha) no canal.</summary>
    public static async Task WriteAsync<T>(Stream s, T value, CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(Encode(value) + "\n");
        await s.WriteAsync(bytes, ct).ConfigureAwait(false);
        await s.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Lê uma mensagem (até o fim da linha, no máximo 64 KB).</summary>
    public static async Task<T?> ReadAsync<T>(Stream s, CancellationToken ct = default)
    {
        var buf = new List<byte>();
        var one = new byte[1];
        while (buf.Count < 65536)
        {
            int n = await s.ReadAsync(one, ct).ConfigureAwait(false);
            if (n == 0 || one[0] == (byte)'\n') break;
            buf.Add(one[0]);
        }
        return Decode<T>(Encoding.UTF8.GetString(buf.ToArray()));
    }
}
