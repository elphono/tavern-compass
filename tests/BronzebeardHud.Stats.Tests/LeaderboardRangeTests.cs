namespace BronzebeardHud.Stats.Tests;

public class LeaderboardRangeTests
{
    [Fact]
    public void Default_IsTheRangeOfThePlan()
    {
        // docs/plans/2026-09-26-parite-tier7-plan.md, phase 2: EU, 8000 to 8050 MMR.
        var range = LeaderboardRange.Default;
        Assert.Equal("EU", range.Region);
        Assert.Equal(8000, range.MinRating);
        Assert.Equal(8050, range.MaxRating);
    }

    [Fact]
    public void ProvidedValues_ReplaceTheDefault_OneByOne()
    {
        var all = LeaderboardRange.WithOverrides(region: "US", minRating: 8120, maxRating: 8475);
        Assert.Equal(("US", 8120, 8475), (all.Region, all.MinRating, all.MaxRating));

        var onlyMax = LeaderboardRange.WithOverrides(maxRating: 8390);
        Assert.Equal(("EU", 8000, 8390), (onlyMax.Region, onlyMax.MinRating, onlyMax.MaxRating));

        var onlyRegion = LeaderboardRange.WithOverrides(region: "AP");
        Assert.Equal(("AP", 8000, 8050), (onlyRegion.Region, onlyRegion.MinRating, onlyRegion.MaxRating));
    }

    [Fact]
    public void InvalidValues_AreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LeaderboardRange.WithOverrides(region: "CN"));
        Assert.Throws<ArgumentException>(() => LeaderboardRange.WithOverrides(minRating: 8300, maxRating: 8100));
    }
}
