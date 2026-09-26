using static BronzebeardHud.Stats.Tests.TestData;

namespace BronzebeardHud.Stats.Tests;

public class FirestoneImportTests
{
    private const string Url = "https://static.zerotoheroes.com/api/bgs/hero-stats/mmr-25/past-seven/overview-from-hourly.gz.json";
    private static readonly DateTimeOffset FetchedAt = new(2026, 9, 26, 18, 5, 0, TimeSpan.Zero);

    // Ranks deliberately out of order: the importer must sort them.
    private static readonly string ShuffledDistribution =
        "[" + string.Join(",", Rank(3, 11.2), Rank(1, 10.4), Rank(8, 13.56), Rank(2, 12.44),
            Rank(5, 13.0), Rank(4, 12.9), Rank(7, 13.4), Rank(6, 13.1)) + "]";

    private static readonly string SevenRanks =
        "[" + string.Join(",", Enumerable.Range(1, 7).Select(r => Rank(r, 10 + r))) + "]";

    [Fact]
    public void Import_MapsFirestoneFieldsToTheLocalFormat()
    {
        var payload = FirestonePayload(
            // A skin listed BEFORE its base hero, with fewer games: the base hero must win.
            FirestoneHero("TB_BaconShop_HERO_39_SKIN_C", 6.0, dataPoints: 12, offered: 90, picked: 12),
            FirestoneHero("TB_BaconShop_HERO_39", 4.29, dataPoints: 1125, offered: 13291, picked: 1121, ShuffledDistribution),
            FirestoneHero("BG22_HERO_004", 3.61, dataPoints: 640, offered: 5000, picked: 900),
            FirestoneHero("BG31_HERO_802", 5.02, dataPoints: 90, offered: 0, picked: 0, SevenRanks),
            // Firestone drops heroes with a zero average position; so do we.
            FirestoneHero("BG20_HERO_101", 0, dataPoints: 3, offered: 40, picked: 3));

        var file = FirestoneHeroStatsImporter.Import(payload, Url, FetchedAt);

        Assert.Equal(StatsSources.Firestone, file.Source);
        Assert.Equal(Url, file.SourceUrl);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 12, 10, 35, 950, TimeSpan.Zero), file.GeneratedAt);
        Assert.Equal(FetchedAt, file.FetchedAt);
        Assert.Equal(25, file.MmrPercentile);
        Assert.Equal("past-seven", file.TimePeriod);
        Assert.Equal(new[] { "BG22_HERO_004", "BG31_HERO_802", "TB_BaconShop_HERO_39" }, file.Heroes.Select(h => h.HeroCardId));

        var wagtoggle = file.Find("TB_BaconShop_HERO_39")!;
        Assert.Equal(4.29, wagtoggle.AveragePlacement);
        Assert.Equal(1125, wagtoggle.DataPoints);
        Assert.Equal(1121.0 / 13291, wagtoggle.PickRate);
        Assert.Null(wagtoggle.Tier);
        Assert.Equal(new[] { 10.4, 12.44, 11.2, 12.9, 13.0, 13.1, 13.4, 13.56 }, wagtoggle.PlacementDistribution);

        var second = file.Find("BG22_HERO_004")!;
        Assert.Equal(3.61, second.AveragePlacement);
        Assert.Equal(640, second.DataPoints);
        Assert.Equal(900.0 / 5000, second.PickRate);
        Assert.Null(second.PlacementDistribution);

        var neverOffered = file.Find("BG31_HERO_802")!;
        Assert.Null(neverOffered.PickRate);
        Assert.Null(neverOffered.PlacementDistribution);

        Assert.Null(file.Find("BG20_HERO_101"));
    }

    [Theory]
    [InlineData("not json at all", "invalid JSON")]
    [InlineData("[{\"heroCardId\":\"BG22_HERO_004\"}]", "root")]
    [InlineData("{\"heroes\":[]}", "heroStats")]
    [InlineData("{\"heroStats\":5}", "heroStats")]
    public void Import_MalformedPayload_IsRejected(string payload, string expectedInMessage)
    {
        var e = Assert.Throws<StatsFormatException>(() => FirestoneHeroStatsImporter.Import(payload, Url, FetchedAt));
        Assert.Contains(expectedInMessage, e.Message);
    }

    [Fact]
    public void Import_PayloadWithoutAnyUsableHero_IsRejected()
    {
        var payload = FirestonePayload(FirestoneHero("BG22_HERO_004", 0, dataPoints: 3, offered: 10, picked: 1));
        var e = Assert.Throws<StatsFormatException>(() => FirestoneHeroStatsImporter.Import(payload, Url, FetchedAt));
        Assert.StartsWith("heroes:", e.Message);
    }

    [Fact]
    public void Endpoints_BuildTheUrlsFirestoneUses()
    {
        Assert.Equal(
            "https://static.zerotoheroes.com/api/bgs/hero-stats/mmr-10/all-time/overview-from-hourly.gz.json",
            FirestoneEndpoints.HeroStats(10, "all-time"));
        Assert.Equal(
            "https://static.zerotoheroes.com/api/bgs/hero-stats/last-patch/mmr-percentiles.gz.json",
            FirestoneEndpoints.MmrPercentileTable("last-patch"));
    }

    [Theory]
    [InlineData(30, "last-patch")]
    [InlineData(100, "past-2")]
    public void Endpoints_UnknownParameter_Throws(int percentile, string period)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FirestoneEndpoints.HeroStats(percentile, period));
    }
}
