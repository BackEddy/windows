using Backeddy.Core;

namespace Backeddy.Service;

/// <summary>Situação atual do serviço (o que o app mostra).</summary>
public sealed class StatusStore
{
    readonly object gate = new();
    ServiceStatus status = new()
    {
        Version = AppInfo.Version,
        StartedAt = DateTimeOffset.Now,
        State = BackupState.NotConfigured,
    };

    public ServiceStatus Get() { lock (gate) return status; }

    public void Set(Func<ServiceStatus, ServiceStatus> change) { lock (gate) status = change(status); }
}
