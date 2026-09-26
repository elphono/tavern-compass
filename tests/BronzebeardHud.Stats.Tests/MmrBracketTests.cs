using static BronzebeardHud.Stats.Tests.TestData;

namespace BronzebeardHud.Stats.Tests;

public class MmrBracketTests
{
    // Firestone's table for last-patch, as read on 2026-09-26.
    private static readonly MmrThreshold[] Table =
    {
        new(100, 0), new(50, 6043), new(25, 6575), new(10, 7177), new(1, 9484),
    };

    [Theory]
    [InlineData(4800, 100)]
    [InlineData(6042, 100)]
    [InlineData(6043, 50)]  // reaching the threshold is enough
    [InlineData(6840, 25)]  // Ali on 2026-09-26
    [InlineData(7177, 10)]
    [InlineData(9483, 10)]
    [InlineData(12650, 1)]
    public void Select_GivesTheMostExclusiveBracketReached(int rating, int expected)
    {
        Assert.Equal(expected, MmrBracket.Select(rating, Table));
    }

    [Fact]
    public void Select_NoRatingOrNoTable_MeansEveryPlayer()
    {
        Assert.Equal(100, MmrBracket.Select(null, Table));
        Assert.Equal(100, MmrBracket.Select(7300, Array.Empty<MmrThreshold>()));
    }

    [Fact]
    public void Select_IgnoresBracketsFirestoneDoesNotPublish()
    {
        Assert.Equal(25, MmrBracket.Select(8000, new[] { new MmrThreshold(25, 6575), new MmrThreshold(5, 7900) }));
    }

    [Fact]
    public void Import_KeepsFirestonesThresholdTable_AndTheCacheRoundTripsIt()
    {
        var payload = FirestonePayload(FirestoneHero("BG22_HERO_004", 3.61, 640, 5000, 900))
            .Replace("\"mmrPercentiles\":[]",
                "\"mmrPercentiles\":[{\"percentile\":100,\"mmr\":0},{\"percentile\":50,\"mmr\":6043},{\"percentile\":25,\"mmr\":6575}]");
        var file = FirestoneHeroStatsImporter.Import(payload, "https://example.invalid/", DateTimeOffset.UnixEpoch);

        Assert.Equal(new[] { (100, 0), (50, 6043), (25, 6575) }, file.MmrThresholds.Select(t => (t.Percentile, t.Mmr)));
        var copy = HeroStatsLoader.Parse(HeroStatsLoader.Serialize(file));
        Assert.Equal(file.MmrThresholds.Select(t => (t.Percentile, t.Mmr)), copy.MmrThresholds.Select(t => (t.Percentile, t.Mmr)));
    }

    [Theory]
    [InlineData("[{\"percentile\":25}]")]
    [InlineData("[{\"percentile\":0,\"mmr\":10}]")]
    [InlineData("[{\"percentile\":25,\"mmr\":6575},{\"percentile\":25,\"mmr\":6600}]")]
    [InlineData("{\"percentile\":25,\"mmr\":6575}")]
    public void Loader_MalformedThresholds_AreRejected(string table)
    {
        var json = "{\"schema\":1,\"source\":\"firestone\",\"mmrThresholds\":" + table +
                   ",\"heroes\":[{\"heroCardId\":\"BG22_HERO_004\",\"averagePlacement\":3.6,\"dataPoints\":640}]}";
        var e = Assert.Throws<StatsFormatException>(() => HeroStatsLoader.Parse(json));
        Assert.StartsWith("mmrThresholds", e.Message);
    }
}
