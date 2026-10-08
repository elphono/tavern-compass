namespace BronzebeardHud.Stats.Tests;

/// <summary>card-stats through the shared cache: one file per MMR bracket, conditional requests like the other files.</summary>
public sealed class CardStatsCacheTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);
    private readonly string _directory = Directory.CreateTempSubdirectory("bbhud-cards-").FullName;
    private readonly ByUrlFetcher _fetcher = new();

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private StatsCache NewCache() => new(_directory, _fetcher, () => T0);

    /// <summary>A payload whose only card has the given placement at turn 6, so each bracket's file is recognisable.</summary>
    private static string Payload(double placement) =>
        $$"""{"lastUpdateDate": "2026-10-08T00:10:31Z", "timePeriod": "last-patch", "cardStats": [{"cardId": "BG_TEST_A", "turnStats": [{"turn": 6, "totalPlayed": 300, "averagePlacement": {{placement.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}]}""";

    private static double Placement((CardStatsFile? File, bool Downloaded, string? Error, bool Unchanged) result) =>
        result.File!.Find("BG_TEST_A")!.At(6)!.AveragePlacement;

    [Fact]
    public async Task EachBracket_HasItsOwnUrlAndFile()
    {
        _fetcher.Bodies["https://static.zerotoheroes.com/api/bgs/card-stats/mmr-25/last-patch/overview-from-hourly.gz.json"] = Payload(3.25);
        _fetcher.Bodies["https://static.zerotoheroes.com/api/bgs/card-stats/mmr-100/last-patch/overview-from-hourly.gz.json"] = Payload(3.75);
        var cache = NewCache();

        var top25 = await cache.GetCardStatsAsync(25, "last-patch", RefreshPolicy.HeroStats, CancellationToken.None);
        var all = await cache.GetCardStatsAsync(100, "last-patch", RefreshPolicy.HeroStats, CancellationToken.None);

        Assert.True(top25.Downloaded && all.Downloaded);
        Assert.Equal(3.25, Placement(top25));
        Assert.Equal(3.75, Placement(all));
        Assert.Equal(25, top25.File!.Provenance.MmrPercentile);
        Assert.True(File.Exists(cache.CardStatsPath(25, "last-patch")));
        Assert.NotEqual(cache.CardStatsPath(25, "last-patch"), cache.CardStatsPath(100, "last-patch"));
    }

    [Fact]
    public async Task NextSession_AsksWithTheETag_AndKeepsTheCacheOn304()
    {
        const string url = "https://static.zerotoheroes.com/api/bgs/card-stats/mmr-50/last-patch/overview-from-hourly.gz.json";
        _fetcher.Bodies[url] = Payload(3.5);
        await NewCache().GetCardStatsAsync(50, "last-patch", RefreshPolicy.HeroStats, CancellationToken.None);

        var again = await NewCache().GetCardStatsAsync(50, "last-patch", RefreshPolicy.HeroStats, CancellationToken.None);

        Assert.True(again.Unchanged);
        Assert.Equal(3.5, Placement(again));
        Assert.Equal(new string?[] { null, "W/\"1\"" }, _fetcher.SentETags);
    }

    /// <summary>Serves a body per URL with an ETag, and 304 to a request carrying that ETag, as the CDN does.</summary>
    private sealed class ByUrlFetcher : IConditionalFetcher
    {
        public Dictionary<string, string> Bodies { get; } = new();
        public List<string?> SentETags { get; } = new();

        public Task<FetchedText> FetchAsync(string url, string? ifNoneMatch, CancellationToken cancellationToken)
        {
            SentETags.Add(ifNoneMatch);
            if (!Bodies.TryGetValue(url, out var body))
            {
                return Task.FromException<FetchedText>(new HttpRequestException($"404 for {url}"));
            }

            return Task.FromResult(ifNoneMatch == "W/\"1\"" ? FetchedText.NotModified : FetchedText.Changed(body, "W/\"1\""));
        }
    }
}
