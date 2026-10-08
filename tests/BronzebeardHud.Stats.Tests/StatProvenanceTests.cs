namespace BronzebeardHud.Stats.Tests;

/// <summary>Every stats file gives its provenance in the common format (chantier b), from the fields it already carries.</summary>
public class StatProvenanceTests
{
    private static readonly DateTimeOffset Generated = new(2026, 10, 8, 0, 10, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Fetched = new(2026, 10, 8, 14, 15, 0, TimeSpan.Zero);

    private static void AssertSame(StatProvenance p, string source, string? url, int? mmr)
    {
        Assert.Equal((source, url, Generated, Fetched, "last-patch", mmr, (string?)null),
            (p.Source, p.SourceUrl, p.GeneratedAt, p.FetchedAt, p.TimePeriod, p.MmrPercentile, p.Patch));
    }

    [Fact]
    public void HeroStats_CarryTheirBracket()
    {
        var file = new HeroStatsFile("hsreplay-manual", Array.Empty<HeroStat>(), "https://h", Generated, Fetched, mmrPercentile: 25, timePeriod: "last-patch");

        AssertSame(file.Provenance, "hsreplay-manual", "https://h", 25);
    }

    [Fact]
    public void Compositions_HaveNoBracket()
    {
        var file = new CompositionFile("nomi.gg", Array.Empty<Composition>(), "https://c", Generated, Fetched, "last-patch");

        AssertSame(file.Provenance, "nomi.gg", "https://c", null);
    }

    /// <summary>One trinket file holds every bracket (averagePlacementAtMmr): its provenance names none.</summary>
    [Fact]
    public void Trinkets_AreFirestones_WithoutABracket()
    {
        var file = new TrinketStatsFile(Array.Empty<TrinketStat>(), "https://t", Generated, Fetched, "last-patch");

        AssertSame(file.Provenance, StatsSources.Firestone, "https://t", null);
    }
}
