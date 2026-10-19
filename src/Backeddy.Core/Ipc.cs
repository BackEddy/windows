using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backeddy.Core;

/// <summary>Pedido do app ao serviço: comando + dados (uma linha JSON por mensagem).</summary>
public sealed record IpcRequest(string Cmd, JsonElement? Data = null);

/// <summary>Resposta do serviço.</summary>
public sealed record IpcResponse(bool Ok, string? Error = null, JsonElement? Data = null);

public static class Ipc
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);

    public static T? Decode<T>(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return default;
        try { return JsonSerializer.Deserialize<T>(line, Json); }
        catch (JsonException) { return default; }
    }

    public static JsonElement ToElement<T>(T value) => JsonSerializer.SerializeToElement(value, Json);

    public static T? From<T>(JsonElement? e)
    {
        if (e is null || e.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return default;
        try { return e.Value.Deserialize<T>(Json); }
        catch (JsonException) { return default; }
    }

    public static IpcRequest Request<T>(string cmd, T data) => new(cmd, ToElement(data));
    public static IpcResponse Success<T>(T data) => new(true, null, ToElement(data));
    public static IpcResponse Success() => new(true);
    public static IpcResponse Fail(string error) => new(false, error);

    /// <summary>Escreve uma mensagem (uma linha) no canal.</summary>
    public static async Task WriteAsync<T>(Stream s, T value, CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(Encode(value) + "\n");
        await s.WriteAsync(bytes, ct).ConfigureAwait(false);
        await s.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Lê uma mensagem (até o fim da linha, no máximo 4 MB).</summary>
    public static async Task<T?> ReadAsync<T>(Stream s, CancellationToken ct = default)
    {
        var buf = new MemoryStream();
        var chunk = new byte[1];
        while (buf.Length < 4 << 20)
        {
            int n = await s.ReadAsync(chunk, ct).ConfigureAwait(false);
            if (n == 0 || chunk[0] == (byte)'\n') break;
            buf.WriteByte(chunk[0]);
        }
        return Decode<T>(Encoding.UTF8.GetString(buf.ToArray()));
    }
}

/// <summary>Nomes dos comandos do canal.</summary>
public static class Cmd
{
    public const string Status = "status";
    public const string SetupDisk = "setup.disk";
    public const string SetupServer = "setup.server";
    public const string Forget = "destination.forget";
    public const string BackupNow = "backup.now";
    public const string SetExclusions = "exclusions.set";
    public const string Pause = "pause.set";
    public const string KeepAwake = "keepawake.set";
    public const string Points = "points.list";
    public const string ListDir = "dir.list";
    public const string Restore = "restore";
    public const string Ack = "alerts.ack";
    public const string Versions = "file.versions";
}
