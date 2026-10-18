using System.Text.Json;

namespace Backeddy.Core;

/// <summary>Configuração do serviço (C:\ProgramData\Backeddy\config.json). Senhas ficam à parte, protegidas pelo Windows.</summary>
public sealed record BackupConfig
{
    public DestinationKind Destination { get; init; }
    public string? DiskRoot { get; init; }          // "E:\"
    public string? DiskLabel { get; init; }
    public string? ServerName { get; init; }        // "Backup da Casa"
    public string? ServerHost { get; init; }        // "backup-da-casa.local"
    public string? ServerIp { get; init; }          // IP do painel (se o nome .local falhar)
    public string? ServerUrl { get; init; }         // "https://backup-da-casa.local:51515"
    public string? ServerUrlIp { get; init; }
    public string? Fingerprint { get; init; }
    public string? ServerUser { get; init; }        // usuário do Kopia no servidor
    public string? PcName { get; init; }            // nome do PC no servidor (hostname do Kopia)
    public string? Profile { get; init; }           // C:\Users\nome
    public string? UserName { get; init; }
    public List<string> Excluded { get; init; } = new();
    public DateTimeOffset? LastSuccess { get; init; }
    public DateTimeOffset? LastAttempt { get; init; }
    public string? LastError { get; init; }
    public long LastFiles { get; init; }
    public long LastBytes { get; init; }

    public string? DisplayName => Destination switch
    {
        DestinationKind.Disk => string.IsNullOrWhiteSpace(DiskLabel) ? DiskRoot : $"{DiskLabel} ({DiskRoot?.TrimEnd('\\')})",
        DestinationKind.Server => ServerName ?? ServerHost,
        _ => null,
    };

    /// <summary>Pasta do repositório no HD externo.</summary>
    public string? DiskRepo => DiskRoot is null ? null : DiskRoot.TrimEnd('\\', '/') + @"\Backeddy";

    public static BackupConfig Load(string path)
    {
        try { return Ipc.Decode<BackupConfig>(File.ReadAllText(path)) ?? new BackupConfig(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new BackupConfig(); }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions(Ipc.Json) { WriteIndented = true }));
        File.Move(tmp, path, overwrite: true);
    }
}
