using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats.Tests;

public sealed class WarbandCurveTests : IDisposable
{
    // One hero, a curve given out of order with one broken entry and a repeated turn (the first one counts).
    private const string Payload = """
        {"lastUpdateDate":"2026-09-26T12:10:35.950Z","dataPoints":322049,"mmrPercentiles":[],"heroStats":[
          {"heroCardId":"BG20_HERO_100","dataPoints":1199,"totalOffered":9000,"totalPicked":1500,"averagePosition":4.1,
           "placementDistribution":[],"tribeStats":[],"mmrPercentile":100,"timePeriod":"last-patch",
           "warbandStats":[{"turn":3,"averageStats":20.7},{"turn":1,"averageStats":7.3},{"turn":2,"averageStats":8.96},
                           {"turn":"four","averageStats":30},{"turn":3,"averageStats":99}]},
          {"heroCardId":"TB_BaconShop_HERO_28","dataPoints":800,"averagePosition":4.4,"placementDistribution":[],"tribeStats":[]}
        ]}
        """;

    private readonly string _directory = Directory.CreateTempSubdirectory("bbhud-warband-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static HeroStatsFile Imported() =>
        FirestoneHeroStatsImporter.Import(Payload, "https://static.zerotoheroes.com/api/bgs/hero-stats/mmr-100/last-patch/overview-from-hourly.gz.json",
            new DateTimeOffset(2026, 9, 26, 18, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Import_KeepsTheCurveInTurnOrder_WithoutBrokenOrRepeatedTurns_AndItSurvivesTheCache()
    {
        var file = Imported();
        var curve = file.Find("BG20_HERO_100")!.WarbandCurve;
        Assert.Equal(new[] { (1, 7.3), (2, 8.96), (3, 20.7) }, curve.Select(p => (p.Turn, p.AverageStats)));
        Assert.Empty(file.Find("TB_BaconShop_HERO_28")!.WarbandCurve);

        var copy = HeroStatsLoader.Parse(HeroStatsLoader.Serialize(file));
        Assert.Equal(HeroStatsFile.CurrentSchema, copy.Schema);
        Assert.Equal(curve.Select(p => (p.Turn, p.AverageStats)), copy.Find("BG20_HERO_100")!.WarbandCurve.Select(p => (p.Turn, p.AverageStats)));
    }

    [Fact]
    public void ThreeSuccessiveTurns_BelowOnTopAbove_ThenBeyondTheCurve_ThenAnotherHero()
    {
        var sources = new[] { Imported() };

        Assert.Equal("Board 5 · hero avg 7 at turn 1 · −32%", WarbandCurve.Compare(1, 5, "BG20_HERO_100", sources).Line);
        Assert.Equal("Board 9 · hero avg 9 at turn 2 · +0%", WarbandCurve.Compare(2, 9, "BG20_HERO_100", sources).Line);
        var third = WarbandCurve.Compare(3, 31, "BG20_HERO_100", sources);
        Assert.Equal("Board 31 · hero avg 21 at turn 3 · +50%", third.Line);
        Assert.Equal((3, 31, 20.7), (third.Turn, third.BoardStats, third.Average!.Value));

        Assert.Equal("Board 88 · no average for turn 4", WarbandCurve.Compare(4, 88, "BG20_HERO_100", sources).Line);
        Assert.Equal("Board 12 · no curve for this hero", WarbandCurve.Compare(2, 12, "TB_BaconShop_HERO_28", sources).Line);
        Assert.Equal("Board 12 · no curve for this hero", WarbandCurve.Compare(2, 12, "UNKNOWN_HERO", sources).Line);
    }

    [Fact]
    public void AHandTypedFileWithoutCurve_DoesNotHideFirestones()
    {
        var manual = new HeroStatsFile(StatsSources.HsReplayManual, new[] { new HeroStat("BG20_HERO_100", 3.9, 0) }, schema: 1);
        Assert.Equal("Board 21 · hero avg 9 at turn 2 · +134%", WarbandCurve.Compare(2, 21, "BG20_HERO_100", new[] { manual, Imported() }).Line);
    }

    [Fact]
    public void BoardStats_SumsAttackAndHealthOfEveryMinion()
    {
        Assert.Equal(3 + 4 + 12 + 4 + 1 + 1, WarbandCurve.BoardStats(new[] { (3, 4), (12, 4), (1, 1) }));
        Assert.Equal(0, WarbandCurve.BoardStats(Array.Empty<(int, int)>()));
    }

    [Fact]
    public async Task ACachedFileOfSchema1_IsDownloadedAgainForItsCurve_WhileAHandTypedOneStillLoads()
    {
        // The cache as written before this version: same content, schema 1, no curve.
        var old = JObject.Parse(HeroStatsLoader.Serialize(Imported()));
        old["schema"] = 1;
        foreach (var hero in old["heroes"]!)
        {
            ((JObject)hero).Remove("warbandStats");
        }

        var cachePath = Path.Combine(_directory, "firestone-hero-stats-mmr-100-last-patch.json");
        File.WriteAllText(cachePath, old.ToString());
        Assert.Equal(1, HeroStatsLoader.Load(cachePath).Schema); // still readable as a hand-typed file

        var fetches = 0;
        var cache = new StatsCache(_directory, new Fetcher(() => { fetches++; return Payload; }), () => new DateTimeOffset(2026, 9, 26, 19, 0, 0, TimeSpan.Zero));
        var result = await cache.GetHeroStatsAsync(100, "last-patch", RefreshPolicy.HeroStats, CancellationToken.None);

        Assert.True(result.Downloaded);
        Assert.Equal(1, fetches);
        Assert.Equal(3, result.File!.Find("BG20_HERO_100")!.WarbandCurve.Count);

        File.WriteAllText(cachePath, old.ToString());
        var failing = new StatsCache(_directory, new Fetcher(() => throw new HttpRequestException("Response status code does not indicate success: 503 (Service Unavailable).")),
            () => new DateTimeOffset(2026, 9, 26, 21, 0, 0, TimeSpan.Zero));
        var failed = await failing.GetHeroStatsAsync(100, "last-patch", RefreshPolicy.HeroStats, CancellationToken.None);
        Assert.Null(failed.File);
        Assert.StartsWith("cache: schema 1 ≠ 2, redownload failed: Response status code does not indicate success: 503", failed.Error);
    }

    private sealed class Fetcher : IConditionalFetcher
    {
        private readonly Func<string> _response;
        public Fetcher(Func<string> response) => _response = response;

        public Task<FetchedText> FetchAsync(string url, string? ifNoneMatch, CancellationToken cancellationToken)
        {
            try
            {
                return Task.FromResult(FetchedText.Changed(_response(), etag: null));
            }
            catch (Exception e)
            {
                return Task.FromException<FetchedText>(e);
            }
        }
    }
}
