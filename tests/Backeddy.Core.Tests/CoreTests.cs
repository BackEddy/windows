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
        await Ipc.WriteAsync(ms, new IpcRequest(Cmd.Status));
        ms.Position = 0;
        var got = await Ipc.ReadAsync<ServiceStatus>(ms);
        var req = await Ipc.ReadAsync<IpcRequest>(ms);
        Assert.Equal("1.2.3", got!.Version);
        Assert.Equal(BackupState.Idle, got.State);
        Assert.Equal("olá", got.Message);
        Assert.Equal(Cmd.Status, req?.Cmd);
    }

    [Fact]
    public void EnumsAreReadableText() => Assert.Contains("\"state\":\"notConfigured\"", Ipc.Encode(new ServiceStatus()));

    [Fact]
    public void PauseRequestRoundTrips()
    {
        var back = Ipc.Decode<IpcRequest>(Ipc.Encode(Ipc.Request(Cmd.Pause, new PauseRequest(PauseKind.Tomorrow))))!;
        Assert.Equal(Cmd.Pause, back.Cmd);
        Assert.Equal(PauseKind.Tomorrow, Ipc.From<PauseRequest>(back.Data)!.Kind);
    }

    [Fact]
    public void GarbageIsIgnored() => Assert.Null(Ipc.Decode<ServiceStatus>("{não é json"));
}

public class UpdateCheckTests
{
    [Theory]
    [InlineData("v1.0.1", "1.0.0", true)]
    [InlineData("1.1.0", "1.0.9", true)]
    [InlineData("v1.10.0", "1.9.0", true)]
    [InlineData("v1.0.0", "1.0.0", false)]
    [InlineData("v1.0.0", "1.0.1", false)]
    [InlineData("v2.0.0-beta", "1.5.0", true)]
    [InlineData("v1.2", "1.2.0", false)]
    [InlineData("lixo", "1.0.0", false)]
    [InlineData("v1.1.0", "dev", false)]
    [InlineData(null, "1.0.0", false)]
    public void ComparesVersions(string? latest, string current, bool newer) =>
        Assert.Equal(newer, UpdateCheck.IsNewer(latest, current));

    [Fact]
    public void ParsesNewRelease()
    {
        var json = "{\"tag_name\":\"v1.1.0\",\"html_url\":\"https://github.com/BackEddy/windows/releases/tag/v1.1.0\",\"draft\":false,\"prerelease\":false}";
        var info = UpdateCheck.Parse(json, "1.0.0");
        Assert.NotNull(info);
        Assert.Equal("1.1.0", info!.Version);
        Assert.EndsWith("/releases/tag/v1.1.0", info.Url);
    }

    [Fact]
    public void IgnoresSameOlderPrereleaseAndGarbage()
    {
        Assert.Null(UpdateCheck.Parse("{\"tag_name\":\"v1.0.0\"}", "1.0.0"));
        Assert.Null(UpdateCheck.Parse("{\"tag_name\":\"v0.9.0\"}", "1.0.0"));
        Assert.Null(UpdateCheck.Parse("{\"tag_name\":\"v2.0.0\",\"prerelease\":true}", "1.0.0"));
        Assert.Null(UpdateCheck.Parse("{\"tag_name\":\"v2.0.0\",\"draft\":true}", "1.0.0"));
        Assert.Null(UpdateCheck.Parse("não é json", "1.0.0"));
    }

    [Fact]
    public void ForeignLinkFallsBackToOurReleasesPage()
    {
        var json = "{\"tag_name\":\"v1.1.0\",\"html_url\":\"https://evil.example/x\"}";
        Assert.Equal(AppInfo.RepoUrl + "/releases/latest", UpdateCheck.Parse(json, "1.0.0")!.Url);
    }
}
