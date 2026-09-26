namespace BronzebeardHud.Stats.Tests;

public class HeroPickAdvisorTests
{
    [Fact]
    public void BuildRows_OneColumnPerOfferedHero_SourcesInPriorityOrder()
    {
        var manual = new HeroStatsFile(StatsSources.HsReplayManual, new[]
        {
            new HeroStat("GRID_16", 3.7, 0, pickRate: 0.31, tier: "A"),
            new HeroStat("GRID_02", 4.8, 0),
        });
        var firestone = TestData.GridPool();
        var offered = new[]
        {
            new OfferedHero(entityId: 41, cardId: "GRID_16_SKIN_A", baseCardId: "GRID_16", position: 1),
            new OfferedHero(entityId: 42, cardId: "GRID_09", baseCardId: "GRID_09", position: 2),
            new OfferedHero(entityId: 43, cardId: "UNKNOWN_HERO", baseCardId: "UNKNOWN_HERO", position: 3),
        };

        var rows = HeroPickAdvisor.BuildRows(offered, new[] { manual, firestone });

        Assert.Equal(new[] { 41, 42, 43 }, rows.Select(r => r.Hero.EntityId));

        var both = rows[0].Figures;
        Assert.Equal(new[] { StatsSources.HsReplayManual, StatsSources.Firestone }, both.Select(f => f.Source));
        Assert.Equal("A", both[0].Tier);
        Assert.Equal(3.7, both[0].AveragePlacement);
        Assert.Equal(0.31, both[0].PickRate);
        Assert.Equal("D", both[1].Tier); // GRID_16 = 4.6 in the pool
        Assert.Equal(4.6, both[1].AveragePlacement);
        Assert.Equal(116, both[1].DataPoints);

        var firestoneOnly = Assert.Single(rows[1].Figures);
        Assert.Equal(StatsSources.Firestone, firestoneOnly.Source);
        Assert.Equal("B", firestoneOnly.Tier); // GRID_09 = 3.9, just under the mean 3.95
        Assert.Equal(3.9, firestoneOnly.AveragePlacement);

        Assert.False(rows[2].HasData);
        Assert.Empty(rows[2].Figures);
    }

    [Fact]
    public void BuildRows_CarriesTheBracketOfEachSource()
    {
        var top25 = new HeroStatsFile(StatsSources.Firestone, TestData.GridPool().Heroes, mmrPercentile: 25);
        var hero = new OfferedHero(entityId: 51, cardId: "GRID_04", baseCardId: "GRID_04", position: 1);

        var figures = Assert.Single(HeroPickAdvisor.BuildRows(new[] { hero }, new[] { top25 })[0].Figures);

        Assert.Equal(25, figures.MmrPercentile);
        Assert.Equal(3.4, figures.AveragePlacement);
    }

    [Fact]
    public void FourHeroes_Top4AndFirstFromTheirOwnDistribution()
    {
        // Each distribution is distinct: a swap between heroes, or a top 3 or top 5, would show.
        var pool = new HeroStatsFile(StatsSources.Firestone, new[]
        {
            new HeroStat("HERO_A", 3.6, 900, placementDistribution: new[] { 10.4, 12.4, 18.0, 13.0, 12.0, 11.6, 11.2, 11.4 }),
            new HeroStat("HERO_B", 4.4, 800, placementDistribution: new[] { 20.0, 10.0, 10.0, 10.0, 12.5, 12.5, 12.5, 12.5 }),
            // Percentages summing to 50, not 100: shares are taken from their sum.
            new HeroStat("HERO_C", 4.1, 700, placementDistribution: new[] { 2.5, 5.0, 5.0, 7.5, 7.5, 7.5, 7.5, 7.5 }),
            new HeroStat("HERO_D", 4.0, 600),
        });
        var offered = new[] { "HERO_A", "HERO_B", "HERO_C", "HERO_D" }
            .Select((id, i) => new OfferedHero(entityId: 60 + i, cardId: id, baseCardId: id, position: i + 1)).ToList();

        var figures = HeroPickAdvisor.BuildRows(offered, new[] { pool }).Select(r => r.Figures.Single()).ToList();

        Assert.Equal(new double?[] { 53.8, 50.0, 40.0, null }, figures.Select(f => f.Top4Rate is { } t ? Math.Round(t, 6) : (double?)null));
        Assert.Equal(new double?[] { 10.4, 20.0, 5.0, null }, figures.Select(f => f.FirstRate is { } t ? Math.Round(t, 6) : (double?)null));
        Assert.Equal(new[] { "top 4 54% · 1st 10%", "top 4 50% · 1st 20%", "top 4 40% · 1st 5%", null }, figures.Select(f => f.OddsText));
    }

    [Theory]
    [InlineData(new double[0])]
    [InlineData(new[] { 0.0, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData(new[] { 12.5, 12.5, 12.5, 12.5, 12.5, 12.5, 12.5 })]
    public void PlacementOdds_NothingToDivide_NoOdds(double[] distribution)
    {
        Assert.Equal((null, null), HeroPickAdvisor.PlacementOdds(distribution));
    }

    [Fact]
    public void BuildRows_NoOfferedHero_NoRow()
    {
        Assert.Empty(HeroPickAdvisor.BuildRows(Array.Empty<OfferedHero>(), new[] { TestData.GridPool() }));
    }
}
