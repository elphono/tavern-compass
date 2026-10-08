namespace BronzebeardHud.Stats.Tests;

/// <summary>The bracket button of the panel: one click, the next bracket Firestone publishes.</summary>
public class BracketChoiceTests
{
    [Fact]
    public void Next_GoesThroughEveryBracket_AndComesBack_TwiceOver()
    {
        var seen = new List<int>();
        var bracket = MmrBracket.EveryPlayer;
        for (var i = 0; i < 10; i++)
        {
            bracket = BracketChoice.Next(bracket);
            seen.Add(bracket);
        }

        Assert.Equal(new[] { 50, 25, 10, 1, 100, 50, 25, 10, 1, 100 }, seen);
    }

    [Fact]
    public void Next_FromABracketFirestoneDoesNotPublish_StartsOverFromEveryPlayer()
    {
        Assert.Equal(MmrBracket.EveryPlayer, BracketChoice.Next(33));
    }

    [Fact]
    public void Label_NamesEveryPlayer_ThenTheTopPercent()
    {
        Assert.Equal("all MMR", BracketChoice.Label(100));
        Assert.Equal("top 25%", BracketChoice.Label(25));
        Assert.Equal("top 1%", BracketChoice.Label(1));
    }
}
