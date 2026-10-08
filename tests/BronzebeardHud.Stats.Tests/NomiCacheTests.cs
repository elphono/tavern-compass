namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// The agreement with nomi.gg's owner (CLAUDE.md, "Décisions"): latest.json, then the patch's analysis, in conditional
/// requests, **one attempt a day at most** — HDT restarts included (a new NomiCache over the same folder), failures
/// included. Firestone's cache asks at every start; this one must not.
/// </summary>
public sealed class NomiCacheTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "nomi-cache-" + Guid.NewGuid().ToString("N"));
    private readonly Fetcher _fetcher = new();
    private DateTimeOffset _now = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);

    public NomiCacheTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>A new instance each time: what HDT's restart does.</summary>
    private Task<(NomiAnalysisFile? File, bool Downloaded, string? Error, bool Unchanged)> Ask() =>
        new NomiCache(_folder, _fetcher, () => _now).GetAsync(CancellationToken.None);

    private static string Latest(string patch) => $$"""{ "version": "{{patch}}", "published": "2026-10-01T19:48:00Z" }""";

    [Fact]
    public async Task FirstAsk_LatestThenTheAnalysis_ThenNothingTheSameDay_AcrossRestarts()
    {
        _fetcher.Bodies[NomiCache.LatestUrl] = Latest("36.6.3");
        _fetcher.Bodies[NomiCache.AnalysisUrl("36.6.3")] = NomiAnalysisTests.Payload;

        var first = await Ask();
        Assert.Equal((true, (string?)null), (first.Downloaded, first.Error));
        Assert.Equal("36.6.3", first.File!.Provenance.Patch);
        Assert.Equal(new[] { NomiCache.LatestUrl, NomiCache.AnalysisUrl("36.6.3") }, _fetcher.Urls);

        _now = _now.AddHours(23).AddMinutes(59);
        var later = await Ask(); // HDT restarted, the same day
        Assert.Equal(2, _fetcher.Urls.Count);
        Assert.Equal((false, false, "36.6.3"), (later.Downloaded, later.Unchanged, later.File!.Provenance.Patch));
    }

    /// <summary>Three days: one attempt each, conditional (the ETags of the day before), the file kept on 304.</summary>
    [Fact]
    public async Task OneAttemptADay_ThreeDaysInARow_Conditional()
    {
        _fetcher.Bodies[NomiCache.LatestUrl] = Latest("36.6.3");
        _fetcher.Bodies[NomiCache.AnalysisUrl("36.6.3")] = NomiAnalysisTests.Payload;

        await Ask();
        foreach (var day in new[] { 1, 2 })
        {
            _now = _now.AddDays(1);
            var result = await Ask();
            await Ask(); // and a restart that day
            Assert.Equal((false, true, (string?)null), (result.Downloaded, result.Unchanged, result.Error));
            Assert.NotNull(result.File);
            Assert.Equal(2 + 2 * day, _fetcher.Urls.Count);
        }

        var latest = Fetcher.ETagOf(Latest("36.6.3"));
        var analysis = Fetcher.ETagOf(NomiAnalysisTests.Payload);
        Assert.Equal(new string?[] { null, null, latest, analysis, latest, analysis }, _fetcher.SentETags);
    }

    /// <summary>A failure counts as the day's attempt: no hammering a site that is down, restarts or not.</summary>
    [Fact]
    public async Task AFailure_IsTheDaysAttempt_TheNextDayAsksAgain()
    {
        var failed = await Ask(); // no body: the fetcher throws
        Assert.NotNull(failed.Error);
        Assert.Null(failed.File);
        Assert.Single(_fetcher.Urls);

        _now = _now.AddHours(6);
        Assert.Null((await Ask()).File);
        Assert.Single(_fetcher.Urls);

        _fetcher.Bodies[NomiCache.LatestUrl] = Latest("36.6.3");
        _fetcher.Bodies[NomiCache.AnalysisUrl("36.6.3")] = NomiAnalysisTests.Payload;
        _now = _now.AddDays(1);
        Assert.True((await Ask()).Downloaded);
    }

    /// <summary>A new patch: its own file is asked; the closed patch's is never asked again.</summary>
    [Fact]
    public async Task ANewPatch_IsAsked_TheClosedOneNeverAgain()
    {
        _fetcher.Bodies[NomiCache.LatestUrl] = Latest("36.6.3");
        _fetcher.Bodies[NomiCache.AnalysisUrl("36.6.3")] = NomiAnalysisTests.Payload;
        await Ask();

        _fetcher.Bodies[NomiCache.LatestUrl] = Latest("36.6.10");
        _fetcher.Bodies[NomiCache.AnalysisUrl("36.6.10")] = NomiAnalysisTests.Payload.Replace("\"36.6.3\"", "\"36.6.10\"");
        _now = _now.AddDays(1);
        var result = await Ask();

        Assert.Equal("36.6.10", result.File!.Provenance.Patch);
        Assert.Equal(new[] { NomiCache.LatestUrl, NomiCache.AnalysisUrl("36.6.3"), NomiCache.LatestUrl, NomiCache.AnalysisUrl("36.6.10") }, _fetcher.Urls);
        Assert.Null(_fetcher.SentETags[3]); // a new file: a plain request, never the old patch's ETag
    }

    /// <summary>The patch comes from the network and names a file and a URL: anything but digits and dots is refused.</summary>
    [Theory]
    [InlineData("../../x")]
    [InlineData("36.6.3/../../y")]
    [InlineData("")]
    public async Task AStrangePatchName_IsRefused_NothingElseAsked(string patch)
    {
        _fetcher.Bodies[NomiCache.LatestUrl] = Latest(patch);

        var result = await Ask();

        Assert.StartsWith("latest.json: version", result.Error);
        Assert.Equal(new[] { NomiCache.LatestUrl }, _fetcher.Urls);
        Assert.Equal(new[] { "nomi-state.json" }, Directory.GetFiles(_folder).Select(Path.GetFileName));
    }

    /// <summary>The agreement: a User-Agent that names the plugin, its version and its repository, so that the owner can reach us.</summary>
    [Fact]
    public void TheUserAgent_NamesThePluginItsVersionAndItsRepository()
    {
        var version = typeof(HttpStatsFetcher).Assembly.GetName().Version!;

        Assert.Equal($"TavernCompass/{version.Major}.{version.Minor}.{version.Build} (+https://github.com/elphono/tavern-compass)", HttpStatsFetcher.UserAgent);
        Assert.NotEqual("0.0.0", $"{version.Major}.{version.Minor}.{version.Build}");
        using var fetcher = new HttpStatsFetcher(); // ParseAdd throws on a malformed header: the plugin would not even load
    }

    /// <summary>Like a server: the ETag follows the content, so a changed file is sent again despite the old ETag.</summary>
    private sealed class Fetcher : IConditionalFetcher
    {
        public static string ETagOf(string body) => "W/\"" + body.GetHashCode().ToString("x") + "\"";

        public Dictionary<string, string> Bodies { get; } = new();
        public List<string> Urls { get; } = new();
        public List<string?> SentETags { get; } = new();

        public Task<FetchedText> FetchAsync(string url, string? ifNoneMatch, CancellationToken cancellationToken)
        {
            Urls.Add(url);
            SentETags.Add(ifNoneMatch);
            if (!Bodies.TryGetValue(url, out var body))
            {
                return Task.FromException<FetchedText>(new HttpRequestException($"404 for {url}"));
            }

            var etag = ETagOf(body);
            return Task.FromResult(ifNoneMatch == etag ? FetchedText.NotModified : FetchedText.Changed(body, etag));
        }
    }
}
