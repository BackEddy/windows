using Backeddy.Core;
using Xunit;

// Gabarito da B2 (tasks/bench.sh): independe dos testes que o modelo escreve.
// A classe tem de se chamar BenchReferenceTests: o script a usa para separar os testes.
// Início = fim fica de fora de propósito: a spec se contradiz nesse ponto.
public class BenchReferenceTests
{
    static DateTimeOffset At(int h, int m = 0, int off = 0) => new(2026, 10, 17, h, m, 0, TimeSpan.FromHours(off));
    static BackupConfig Q(int h1, int h2) =>
        new() { Destination = DestinationKind.Disk, QuietStart = TimeSpan.FromHours(h1), QuietEnd = TimeSpan.FromHours(h2) };
    static Decision D(BackupConfig c, DateTimeOffset now, bool manual = false, bool bat = false, int? pct = null, bool dest = true) =>
        Schedule.Decide(c, now, manual, bat, pct, dest);

    [Fact]
    public void RefSameDayWindow()
    {
        var c = Q(13, 15);
        Assert.Equal(Decision.Run, D(c, At(12, 59)));
        Assert.Equal(Decision.Quiet, D(c, At(13)));
        Assert.Equal(Decision.Quiet, D(c, At(14, 59)));
        Assert.Equal(Decision.Run, D(c, At(15)));
    }

    [Fact]
    public void RefWindowCrossesMidnight()
    {
        var c = Q(22, 7);
        Assert.Equal(Decision.Run, D(c, At(21, 59)));
        Assert.Equal(Decision.Quiet, D(c, At(22)));
        Assert.Equal(Decision.Quiet, D(c, At(23, 30)));
        Assert.Equal(Decision.Quiet, D(c, At(3)));
        Assert.Equal(Decision.Quiet, D(c, At(6, 59)));
        Assert.Equal(Decision.Run, D(c, At(7)));
    }

    [Fact]
    public void RefUsesTheLocalClockNotUtc()
    {
        var c = Q(1, 5);
        Assert.Equal(Decision.Quiet, D(c, At(3, 0, -3)));   // 03:00 local = 06:00 UTC
        Assert.Equal(Decision.Run, D(c, At(23, 0, -3)));    // 23:00 local = 02:00 UTC
        Assert.Equal(Decision.Run, D(c, At(6, 0, -3)));
    }

    [Fact]
    public void RefManualIgnoresTheWindow() => Assert.Equal(Decision.Run, D(Q(22, 7), At(23), manual: true));

    [Fact]
    public void RefNeedsBothEdges()
    {
        var one = new BackupConfig { Destination = DestinationKind.Disk, QuietStart = TimeSpan.FromHours(22) };
        var none = new BackupConfig { Destination = DestinationKind.Disk };
        Assert.Equal(Decision.Run, D(one, At(23)));
        Assert.Equal(Decision.Run, D(none, At(23)));
    }

    [Fact]
    public void RefOrderWithOtherDecisions()
    {
        var c = Q(22, 7);
        c.LastSuccess = At(23).AddMinutes(-10);
        Assert.Equal(Decision.NotDue, D(c, At(23)));
        var due = Q(22, 7);
        Assert.Equal(Decision.Quiet, D(due, At(23), dest: false));
        Assert.Equal(Decision.Quiet, D(due, At(23), bat: true, pct: 10));
        Assert.Equal(Decision.DestinationMissing, D(due, At(12), dest: false));
        var none = Q(22, 7); none.Destination = DestinationKind.None;
        Assert.Equal(Decision.NotConfigured, D(none, At(23)));
        var paused = Q(22, 7); paused.PausedUntil = At(23).AddHours(2);
        Assert.Equal(Decision.Paused, D(paused, At(23)));
    }

    [Fact]
    public void RefIsQuietIsPublic()
    {
        Assert.True(Schedule.IsQuiet(Q(22, 7), At(23)));
        Assert.False(Schedule.IsQuiet(Q(22, 7), At(12)));
    }

    [Fact]
    public void RefTextInThreeLanguages()
    {
        Assert.Contains("stateQuiet", Strings.Table.Keys);
        var v = Strings.Table["stateQuiet"];
        Assert.False(string.IsNullOrWhiteSpace(v.Pt));
        Assert.False(string.IsNullOrWhiteSpace(v.En));
        Assert.False(string.IsNullOrWhiteSpace(v.Es));
    }
}
