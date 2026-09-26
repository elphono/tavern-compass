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
    public void BuildRows_NoOfferedHero_NoRow()
    {
        Assert.Empty(HeroPickAdvisor.BuildRows(Array.Empty<OfferedHero>(), new[] { TestData.GridPool() }));
    }
}
