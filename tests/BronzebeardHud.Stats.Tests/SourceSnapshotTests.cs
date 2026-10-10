namespace BronzebeardHud.Stats.Tests;

/// <summary>A loaded file, translated into figures with their provenance (SourceSnapshot.Of): one record per figure that has a sample.</summary>
public class SourceSnapshotTests
{
    private static string Describe(StatRecord r) =>
        string.Join("|", r.Kind, r.Subject, r.Measure, r.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), r.Count, r.CountUnit);

    [Fact]
    public void Heroes_PlacementInGames_AHeroWithoutGamesIsLeftOut()
    {
        var file = new HeroStatsFile("hsreplay-manual", new[] { new HeroStat("H1", 3.9, 1200), new HeroStat("H2", 4.6, 0), new HeroStat("H3", 4.2, 80) },
            mmrPercentile: 25, timePeriod: "last-patch");

        var snapshot = SourceSnapshot.Of(file);

        Assert.Equal(new[] { "hero|H1|placement|3.9|1200|games", "hero|H3|placement|4.2|80|games" }, snapshot.Records.Select(Describe));
        Assert.Equal(("hsreplay-manual", 25), (snapshot.Provenance.Source, snapshot.Provenance.MmrPercentile));
        Assert.Equal((1200 * 3.9 + 80 * 4.2) / 1280, snapshot.Means["hero"], precision: 9); // weighed by games, the empty hero out
    }

    [Fact]
    public void Trinkets_EveryPlayersPlacement_InGames()
    {
        var file = new TrinketStatsFile(new[]
        {
            new TrinketStat("T1", 4.1, 300, 0.2, new Dictionary<int, double> { [25] = 3.5 }),
            new TrinketStat("T2", 4.4, 0, null, new Dictionary<int, double>()),
        }, null, null, null, "last-patch");

        Assert.Equal(new[] { "trinket|T1|placement|4.1|300|games" }, SourceSnapshot.Of(file).Records.Select(Describe));
        Assert.Equal(4.1, SourceSnapshot.Of(file).Means["trinket"], precision: 9);
    }

    [Fact]
    public void Cards_OneRecordPerTurn_InPlays()
    {
        var file = new CardStatsFile(new StatProvenance(StatsSources.Firestone, null, null, null, "last-patch", 25, null), new[]
        {
            new CardStat("C1", new[] { new CardTurnStat(3, 250, 4.0), new CardTurnStat(6, 0, 4.4), new CardTurnStat(7, 90, 3.7) }),
        });

        Assert.Equal(new[] { "card|C1|placement at turn 3|4|250|plays", "card|C1|placement at turn 7|3.7|90|plays" },
            SourceSnapshot.Of(file).Records.Select(Describe));
        Assert.Empty(SourceSnapshot.Of(file).Means); // a placement at a turn is never recentred
    }
}
