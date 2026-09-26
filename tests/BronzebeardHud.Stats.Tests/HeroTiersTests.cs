namespace BronzebeardHud.Stats.Tests;

public class HeroTiersTests
{
    // mean 4 and σ 0.5 put every bound on an exact binary value:
    // S < 2.5 ≤ A < 3.25 ≤ B < 4.0 ≤ C < 4.5 ≤ D < 5.0 ≤ E.
    [Theory]
    [InlineData(2.49, "S")]
    [InlineData(2.5, "A")]
    [InlineData(3.2, "A")]
    [InlineData(3.25, "B")]
    [InlineData(3.99, "B")]
    [InlineData(4.0, "C")]
    [InlineData(4.49, "C")]
    [InlineData(4.5, "D")]
    [InlineData(4.99, "D")]
    [InlineData(5.0, "E")]
    [InlineData(8.0, "E")]
    public void TierFor_FollowsFirestoneBounds(double placement, string expected)
    {
        Assert.Equal(expected, HeroTiers.TierFor(placement, mean: 4.0, standardDeviation: 0.5));
    }

    [Fact]
    public void Distribution_UsesThePopulationStandardDeviation()
    {
        // Population σ = sqrt(0.5) ≈ 0.7071; the sample σ would be sqrt(0.625) ≈ 0.7906.
        var (mean, sd) = HeroTiers.Distribution(new[] { 3.0, 3.5, 4.0, 4.5, 5.0 });
        Assert.Equal(4.0, mean, precision: 10);
        Assert.Equal(Math.Sqrt(0.5), sd, precision: 10);
    }

    [Fact]
    public void Compute_RanksEachHeroAgainstTheWholePool()
    {
        var tiers = HeroTiers.Compute(TestData.GridPool());

        // Bounds hand-computed in TestData.GridPool: A < 3.085 ≤ B < 3.95 ≤ C < 4.527 ≤ D.
        Assert.Equal("A", tiers["GRID_00"]); // 3.0
        Assert.Equal("B", tiers["GRID_01"]); // 3.1
        Assert.Equal("B", tiers["GRID_09"]); // 3.9
        Assert.Equal("C", tiers["GRID_10"]); // 4.0
        Assert.Equal("C", tiers["GRID_15"]); // 4.5
        Assert.Equal("D", tiers["GRID_16"]); // 4.6
        Assert.Equal("D", tiers["GRID_19"]); // 4.9
        Assert.DoesNotContain("S", tiers.Values);
    }

    [Fact]
    public void Compute_TierGivenByTheSource_Wins()
    {
        var pool = TestData.GridPool();
        var heroes = pool.Heroes.Select(h => h.HeroCardId == "GRID_19"
            ? new HeroStat(h.HeroCardId, h.AveragePlacement, h.DataPoints, tier: "S")
            : h).ToList();

        var tiers = HeroTiers.Compute(new HeroStatsFile(StatsSources.HsReplayManual, heroes));

        Assert.Equal("S", tiers["GRID_19"]);
        Assert.Equal("D", tiers["GRID_18"]);
    }

    [Fact]
    public void Compute_FileTooSmallToBeAPool_GivesNoComputedTier()
    {
        var heroes = new[]
        {
            new HeroStat("BG22_HERO_004", 3.2, 0),
            new HeroStat("TB_BaconShop_HERO_39", 4.4, 0, tier: "B"),
            new HeroStat("BG31_HERO_802", 5.6, 0),
        };

        var tiers = HeroTiers.Compute(new HeroStatsFile(StatsSources.HsReplayManual, heroes));

        Assert.Null(tiers["BG22_HERO_004"]);
        Assert.Equal("B", tiers["TB_BaconShop_HERO_39"]);
        Assert.Null(tiers["BG31_HERO_802"]);
    }
}
