using System.Globalization;
using Backeddy.Core;
using Xunit;

public class StringsTests
{
    [Theory]
    [InlineData("pt-BR", Strings.Lang.Pt)]
    [InlineData("pt-PT", Strings.Lang.Pt)]
    [InlineData("es-MX", Strings.Lang.Es)]
    [InlineData("en-US", Strings.Lang.En)]
    [InlineData("de-DE", Strings.Lang.En)]
    public void LanguageFollowsWindows(string culture, Strings.Lang expected) =>
        Assert.Equal(expected, Strings.FromCulture(new CultureInfo(culture)));

    [Fact]
    public void EveryTextHasThreeLanguages()
    {
        foreach (var (key, v) in Strings.Table)
        {
            Assert.False(string.IsNullOrWhiteSpace(v.Pt), key);
            Assert.False(string.IsNullOrWhiteSpace(v.En), key);
            Assert.False(string.IsNullOrWhiteSpace(v.Es), key);
            // os três têm os mesmos {0}, {1}…
            Assert.Equal(Placeholders(v.Pt), Placeholders(v.En));
            Assert.Equal(Placeholders(v.Pt), Placeholders(v.Es));
        }
    }

    [Fact]
    public void MissingKeyComesBack() => Assert.Equal("nao-existe", Strings.T("nao-existe"));

    static string Placeholders(string s) =>
        string.Join(",", System.Text.RegularExpressions.Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x));
}

public class IpcTests
{
    [Fact]
    public async Task StatusRoundTripsOverAStream()
    {
        var sent = new ServiceStatus { Version = "1.2.3", State = BackupState.Idle, StartedAt = DateTimeOffset.UnixEpoch, Message = "olá" };
        using var ms = new MemoryStream();
        await Ipc.WriteAsync(ms, sent);
        await Ipc.WriteAsync(ms, new IpcRequest("status"));
        ms.Position = 0;
        var got = await Ipc.ReadAsync<ServiceStatus>(ms);
        var req = await Ipc.ReadAsync<IpcRequest>(ms);
        Assert.Equal(sent, got);
        Assert.Equal("status", req?.Cmd);
    }

    [Fact]
    public void EnumsAreReadableText() => Assert.Contains("\"state\":\"notConfigured\"", Ipc.Encode(new ServiceStatus()));

    [Fact]
    public void GarbageIsIgnored() => Assert.Null(Ipc.Decode<ServiceStatus>("{não é json"));
}
