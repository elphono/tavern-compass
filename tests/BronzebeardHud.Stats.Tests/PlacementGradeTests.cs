namespace BronzebeardHud.Stats.Tests;

public class PlacementGradeTests
{
    private const int Many = 5000; // 2 × 2.3 / √5000 = 0.07: the band, not the noise, decides

    [Theory]
    [InlineData(3.50, PlacementBand.Good)]
    [InlineData(3.80, PlacementBand.Good)]    // exactly the band below the mean: good
    [InlineData(3.81, PlacementBand.Average)]
    [InlineData(4.00, PlacementBand.Average)]
    [InlineData(4.19, PlacementBand.Average)]
    [InlineData(4.20, PlacementBand.Bad)]     // exactly the band above: bad
    [InlineData(4.60, PlacementBand.Bad)]
    public void AgainstTheBracketsMean_TheBandSplitsGoodAverageBad(double placement, PlacementBand expected)
    {
        Assert.Equal(expected, PlacementGrade.Of(placement, 4.00, Many));
    }

    [Fact]
    public void TheMeanMovesWithTheBracket_SoDoesTheBand()
    {
        // 3.70 is good among trinkets placing 4.04 on average (top 25%), average among those placing 3.86 (every player).
        Assert.Equal(PlacementBand.Good, PlacementGrade.Of(3.70, 4.04, Many));
        Assert.Equal(PlacementBand.Average, PlacementGrade.Of(3.70, 3.86, Many));
    }

    [Fact]
    public void UnderTheNoise_NothingIsCalledGoodOrBad()
    {
        // 200 games: 2 × 2.3 / √200 = 0.33 of a place of noise, wider than the band.
        Assert.Equal(PlacementBand.Average, PlacementGrade.Of(3.70, 4.00, 200));
        Assert.Equal(PlacementBand.Average, PlacementGrade.Of(4.30, 4.00, 200));
        Assert.Equal(PlacementBand.Good, PlacementGrade.Of(3.65, 4.00, 200));
        Assert.Equal(PlacementBand.Bad, PlacementGrade.Of(4.35, 4.00, 200));
        Assert.Equal(PlacementBand.None, PlacementGrade.Of(3.00, 4.00, 0));
    }

    [Fact]
    public void Colours_GreenWhiteRed_GreyUngraded_AllDistinct()
    {
        var colours = new[] { PlacementBand.Good, PlacementBand.Average, PlacementBand.Bad, PlacementBand.None }.Select(PlacementGrade.Colour).ToList();

        Assert.Equal(new[] { "#3DDC84", "#FFFFFF", "#FF5C5C", "#C8CDD8" }, colours);
        Assert.Equal(4, colours.Distinct().Count());
        Assert.DoesNotContain(HeroNote.Contested, colours); // amber stays the contest's
    }

    [Fact]
    public void Reference_TheBracketsMeanWeighedByGames_EveryPlayerWhenTheBracketIsMissing()
    {
        var trinkets = new[]
        {
            new TrinketStat("T1", 3.90, 1000, 0.3, new Dictionary<int, double> { [100] = 3.90, [25] = 4.10 }),
            new TrinketStat("T2", 3.60, 3000, 0.2, new Dictionary<int, double> { [100] = 3.60 }), // no top 25%: every player's
            new TrinketStat("T3", 1.00, 0, null, new Dictionary<int, double> { [25] = 1.00 }),    // no games: not counted
        };

        var reference = TrinketReference.Of(trinkets, 25)!;

        Assert.Equal((4.10 * 1000 + 3.60 * 3000) / 4000, reference.Mean, precision: 9);
        Assert.Equal((1000, 3000, 0, 0), (reference.GamesOf("T1"), reference.GamesOf("T2"), reference.GamesOf("T3"), reference.GamesOf("NONE")));
        Assert.Equal((3.90 * 1000 + 3.60 * 3000) / 4000, TrinketReference.Of(trinkets, 100)!.Mean, precision: 9);
        Assert.Null(TrinketReference.Of(null, 25));
        Assert.Null(TrinketReference.Of(new[] { trinkets[2] }, 25));
    }
}
