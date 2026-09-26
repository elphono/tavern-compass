namespace BronzebeardHud.Stats.Tests;

public class TrinketTests
{
    private const string Url = "https://static.zerotoheroes.com/api/bgs/trinket-stats/last-patch/overview-from-hourly.gz.json";

    // Same shape as Firestone's file (2026-09-26): trinketStats[] with averagePlacementAtMmr per bracket.
    private const string Payload = """
        {"lastUpdateDate":"2026-09-26T12:10:37.006Z","dataPoints":616450,"timePeriod":"last-patch","trinketStats":[
          {"trinketCardId":"BG36_MagicItem_400","dataPoints":35577,"pickRate":0.5036,"averagePlacement":3.5488,
           "averagePlacementAtMmr":[{"mmr":100,"dataPoints":35577,"placement":3.5488},{"mmr":50,"dataPoints":20876,"placement":3.7264},
                                    {"mmr":25,"dataPoints":11652,"placement":3.8036},{"mmr":10,"dataPoints":5096,"placement":3.9112}]},
          {"trinketCardId":"BG30_MagicItem_542","dataPoints":8210,"pickRate":0.3121,"averagePlacement":4.4187,"averagePlacementAtMmr":[]},
          {"trinketCardId":"BG31_MagicItem_999","dataPoints":12,"pickRate":0.9,"averagePlacement":0}
        ]}
        """;

    [Fact]
    public void ImportFirestone_KeepsEveryBracket_AndFallsBackToEveryPlayer()
    {
        var file = TrinketStatsLoader.ImportFirestone(Payload, Url, new DateTimeOffset(2026, 9, 26, 22, 0, 0, TimeSpan.Zero));

        Assert.Equal(new[] { "BG36_MagicItem_400", "BG30_MagicItem_542" }, file.Trinkets.Select(t => t.TrinketCardId)); // placement 0 dropped
        var first = file.Find("BG36_MagicItem_400")!;
        Assert.Equal((35577, 0.5036), (first.DataPoints, first.PickRate!.Value));
        Assert.Equal(3.8036, first.PlacementFor(25));
        Assert.Equal(3.9112, first.PlacementFor(10));
        Assert.Equal(3.5488, first.PlacementFor(1)); // bracket not published: every player
        Assert.Equal(4.4187, file.Find("BG30_MagicItem_542")!.PlacementFor(25));
        Assert.Equal("last-patch", file.TimePeriod);

        var copy = TrinketStatsLoader.Parse(TrinketStatsLoader.Serialize(file));
        Assert.Equal(first.PlacementByPercentile.OrderBy(kv => kv.Key), copy.Find("BG36_MagicItem_400")!.PlacementByPercentile.OrderBy(kv => kv.Key));
    }

    [Theory]
    [InlineData("{\"trinketStats\": [ {\"trinketCardId\": ", "invalid JSON")]
    [InlineData("{\"heroStats\": []}", "trinketStats is missing")]
    [InlineData("{\"trinketStats\": [{\"trinketCardId\":\"X\",\"averagePlacement\":0}]}", "trinkets: expected a non-empty array")]
    public void ImportFirestone_MalformedPayload_IsRejected(string payload, string expected)
    {
        Assert.Contains(expected, Assert.Throws<StatsFormatException>(() => TrinketStatsLoader.ImportFirestone(payload, Url, DateTimeOffset.UnixEpoch)).Message);
    }

    [Fact]
    public void Loader_BracketPlacementOutOfRange_IsRejected()
    {
        var json = "{\"schema\":1,\"trinkets\":[{\"trinketCardId\":\"BG36_MagicItem_400\",\"averagePlacement\":3.5,\"dataPoints\":10,\"placementByPercentile\":{\"25\":9.1}}]}";
        Assert.StartsWith("trinkets[0].placementByPercentile", Assert.Throws<StatsFormatException>(() => TrinketStatsLoader.Parse(json)).Message);
    }
}
