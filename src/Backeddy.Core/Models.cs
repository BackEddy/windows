namespace Backeddy.Core;

public enum DestinationKind { None, Disk, Server }

public enum BackupState { NotConfigured, Idle, Running, Waiting, Error }

/// <summary>Por que o backup está esperando.</summary>
public enum WaitReason { None, LowBattery, DestinationMissing }

public sealed record Alert(string Id, string Text);

public sealed record SourceInfo(string Path, string Label);

public sealed record RestoreStatus(bool Running, string? Target, string? Error, DateTimeOffset? FinishedAt);

/// <summary>Situação do serviço (o que o app mostra).</summary>
public sealed record ServiceStatus
{
    public string Version { get; init; } = "";
    public DateTimeOffset StartedAt { get; init; }
    public BackupState State { get; init; } = BackupState.NotConfigured;
    public DestinationKind Destination { get; init; }
    public string? DestinationName { get; init; }
    public DateTimeOffset? LastBackup { get; init; }
    public DateTimeOffset? NextBackup { get; init; }
    public DateTimeOffset? RunningSince { get; init; }
    public string? LastError { get; init; }
    public WaitReason Wait { get; init; }
    public long LastFiles { get; init; }
    public long LastBytes { get; init; }
    public List<Alert> Alerts { get; init; } = new();
    public List<SourceInfo> Sources { get; init; } = new();
    public List<string> Excluded { get; init; } = new();
    public RestoreStatus? Restore { get; init; }
    public string? Message { get; init; }
}

public sealed record DiskSetup(string Root, string? Key, string Profile, string UserName);
public sealed record DiskSetupResult(string? Key, bool NeedsKey);
public sealed record ServerSetup(string Host, string? Ip, string Password, string PcName, string Profile, string UserName);
public sealed record ExclusionsPayload(List<string> Paths);
public sealed record PointItem(string Source, string Label, string Obj);
public sealed record TimePoint(string Id, DateTimeOffset Time, List<PointItem> Items);
public sealed record DirRequest(string Obj);
public sealed record DirEntry(string Name, bool IsDir, long Size, DateTimeOffset? Modified, string Obj);
public sealed record RestoreRequest(string Obj, bool IsDir, string OriginalPath, bool Original, DateTimeOffset PointTime, string Word = "restored");
public sealed record AckRequest(int Days);
public sealed record VersionsRequest(string Source, string RelPath);
/// <summary>Uma versão diferente de um arquivo: o primeiro momento em que ela aparece no backup.</summary>
public sealed record FileVersion(DateTimeOffset PointTime, long Size, DateTimeOffset? Modified, string Ref);

/// <summary>Códigos de erro do canal (o app traduz).</summary>
public static class Err
{
    public const string WrongKey = "wrongKey";
    public const string WrongPassword = "wrongPassword";
    public const string TooMany = "tooMany";
    public const string Unreachable = "unreachable";
    public const string Certificate = "certificate";
    public const string NoSpace = "noSpace";
    public const string Busy = "busy";
    public const string NotConfigured = "notConfigured";
    public const string Unknown = "unknown";
    public const string OtherUser = "otherUser";
    public const string OutsideSources = "outsideSources";
    public const string OldServer = "oldServer";
}
