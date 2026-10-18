namespace Backeddy.Core;

public enum Decision { Run, NotDue, NotConfigured, LowBattery, DestinationMissing }

/// <summary>Quando fazer backup: de hora em hora; depois de uma falha, tenta de novo em 15 minutos.</summary>
public static class Schedule
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(15);
    public const int MinBattery = 20;

    public static DateTimeOffset? Next(BackupConfig c)
    {
        if (c.Destination == DestinationKind.None) return null;
        var bySuccess = c.LastSuccess is { } s ? s + Interval : DateTimeOffset.MinValue;
        var byAttempt = c.LastAttempt is { } a && (c.LastSuccess is null || a > c.LastSuccess) ? a + RetryAfterFailure : DateTimeOffset.MinValue;
        var next = bySuccess > byAttempt ? bySuccess : byAttempt;
        return next == DateTimeOffset.MinValue ? null : next;
    }

    /// <param name="onBattery">true se o PC está sem tomada</param>
    /// <param name="batteryPercent">0–100, ou null se não houver bateria</param>
    public static Decision Decide(BackupConfig c, DateTimeOffset now, bool manual, bool onBattery, int? batteryPercent, bool destinationAvailable)
    {
        if (c.Destination == DestinationKind.None) return Decision.NotConfigured;
        var next = Next(c);
        bool due = manual || next is null || now >= next;
        if (!due) return Decision.NotDue;
        if (!destinationAvailable) return Decision.DestinationMissing;
        if (!manual && onBattery && batteryPercent is { } b && b < MinBattery) return Decision.LowBattery;
        return Decision.Run;
    }
}
