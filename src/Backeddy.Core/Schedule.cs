namespace Backeddy.Core;

public enum Decision { Run, NotDue, NotConfigured, LowBattery, DestinationMissing, Paused }

/// <summary>Quando fazer backup: de hora em hora; depois de uma falha, tenta de novo em 15 minutos.</summary>
public static class Schedule
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan OverdueAfter = TimeSpan.FromDays(3);
    public const int MinBattery = 20;

    public static DateTimeOffset? Next(BackupConfig c)
    {
        if (c.Destination == DestinationKind.None) return null;
        var bySuccess = c.LastSuccess is { } s ? s + Interval : DateTimeOffset.MinValue;
        var byAttempt = c.LastAttempt is { } a && (c.LastSuccess is null || a > c.LastSuccess) ? a + RetryAfterFailure : DateTimeOffset.MinValue;
        var next = bySuccess > byAttempt ? bySuccess : byAttempt;
        DateTimeOffset? result = next == DateTimeOffset.MinValue ? null : next;
        // pausa futura empurra o próximo horário; pausa vencida o Decide já ignora
        return c.PausedUntil is { } until && (result is null || until > result) ? until : result;
    }

    /// <summary>Dias sem backup que dão motivo para avisar (3 ou mais); null quando não há o que avisar —
    /// sem destino configurado ou sem backup nenhum ainda, que não é atraso.</summary>
    public static int? OverdueDays(BackupConfig c, DateTimeOffset now)
    {
        if (c.Destination == DestinationKind.None || c.LastSuccess is not { } last) return null;
        if (now - last < OverdueAfter) return null;
        return (int)(now - last).TotalDays;
    }

    /// <summary>Manter o PC acordado durante o backup: sempre na tomada; na bateria só se a pessoa marcar.</summary>
    public static bool ShouldKeepAwake(BackupConfig c, bool onBattery) => !onBattery || c.KeepAwakeOnBattery;

    /// <summary>Quando a pausa pedida acaba: 1 hora ou 08:00 do dia seguinte, no horário do PC.</summary>
    public static DateTimeOffset? PauseUntil(PauseKind kind, DateTimeOffset now) => kind switch
    {
        PauseKind.Hour => now.AddHours(1),
        PauseKind.Tomorrow => new DateTimeOffset(now.Date.AddDays(1).AddHours(8), now.Offset),
        _ => null,   // Resume: sem pausa
    };

    /// <param name="onBattery">true se o PC está sem tomada</param>
    /// <param name="batteryPercent">0–100, ou null se não houver bateria</param>
    public static Decision Decide(BackupConfig c, DateTimeOffset now, bool manual, bool onBattery, int? batteryPercent, bool destinationAvailable)
    {
        if (c.Destination == DestinationKind.None) return Decision.NotConfigured;
        if (!manual && c.PausedUntil is { } until && now < until) return Decision.Paused;   // "Fazer backup agora" passa por cima
        var next = Next(c);
        bool due = manual || next is null || now >= next;
        if (!due) return Decision.NotDue;
        if (!destinationAvailable) return Decision.DestinationMissing;
        if (!manual && onBattery && batteryPercent is { } b && b < MinBattery) return Decision.LowBattery;
        return Decision.Run;
    }
}
