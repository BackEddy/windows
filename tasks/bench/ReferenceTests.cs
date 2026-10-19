using Backeddy.Core;
using Xunit;

// Gabarito do benchmark (tasks/bench.sh): independe dos testes que o modelo escreve.
// Todos os nomes começam com "Ref" para o script separá-los dos testes do projeto.
public class BenchReferenceTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    static BackupConfig Disk(DateTimeOffset? ok) => new() { Destination = DestinationKind.Disk, LastSuccess = ok };

    [Fact]
    public void RefBoundaryIsThreeDays()
    {
        Assert.Null(Schedule.OverdueDays(Disk(Now - TimeSpan.FromDays(2) - TimeSpan.FromHours(23)), Now));
        Assert.Equal(3, Schedule.OverdueDays(Disk(Now - TimeSpan.FromDays(3)), Now));
    }

    [Fact]
    public void RefWholeDaysRoundDown() =>
        Assert.Equal(5, Schedule.OverdueDays(Disk(Now - TimeSpan.FromDays(5) - TimeSpan.FromHours(12)), Now));

    [Fact]
    public void RefNoBackupYetIsNotOverdue() => Assert.Null(Schedule.OverdueDays(Disk(null), Now));

    [Fact]
    public void RefNoDestinationIsNotOverdue() =>
        Assert.Null(Schedule.OverdueDays(new BackupConfig { LastSuccess = Now - TimeSpan.FromDays(9) }, Now));

    [Fact]
    public void RefThresholdConstant() => Assert.Equal(TimeSpan.FromDays(3), Schedule.OverdueAfter);

    [Fact]
    public void RefStatusCarriesOverdueDays()
    {
        var back = Ipc.Decode<ServiceStatus>(Ipc.Encode(new ServiceStatus { OverdueDays = 4 }))!;
        Assert.Equal(4, back.OverdueDays);
    }

    [Fact]
    public void RefTextsInThreeLanguages()
    {
        foreach (var k in new[] { "overdueTitle", "overdueDisk", "overdueServer" })
        {
            Assert.Contains(k, Strings.Table.Keys);
            var v = Strings.Table[k];
            Assert.Contains("{0}", v.Pt);
            Assert.Contains("{0}", v.En);
            Assert.Contains("{0}", v.Es);
        }
    }
}
