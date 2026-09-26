namespace BronzebeardHud.Stats.Tests;

public class TavernPinsTests
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Brann Bronzebeard"] = "BG_LOE_077",
        ["Drakkari Enchanter"] = "BG26_ICC_901",
    };

    private static string? Resolve(string name) => Names.TryGetValue(name, out var id) ? id : null;

    [Fact]
    public void Parse_NamesIdsCommentsAndGoldens()
    {
        var pins = TavernPins.Parse("# my pins\nBrann Bronzebeard\n\n  BG32_324_G  \ndrakkari enchanter\nBG32_324\n", Resolve);

        Assert.Equal(new[] { "BG_LOE_077", "BG32_324", "BG26_ICC_901" }, pins.CardIds);
        Assert.True(pins.IsPinned("BG32_324_G"));
        Assert.False(pins.IsPinned("BG25_010"));
    }

    [Fact]
    public void Parse_UnknownCard_NamesTheLine()
    {
        var e = Assert.Throws<StatsFormatException>(() => TavernPins.Parse("Brann Bronzebeard\nNot A Card", Resolve));
        Assert.Equal("line 2: unknown card \"Not A Card\"", e.Message);
    }

    [Fact]
    public void Toggle_PinsUnpinsAndPinsAgain_AndSurvivesAReload()
    {
        var pins = TavernPins.Parse("BG_LOE_077", Resolve);

        Assert.True(pins.Toggle("BG25_010"));      // game 1: pin
        pins = TavernPins.Parse(pins.Serialize(), Resolve);
        Assert.Equal(new[] { "BG_LOE_077", "BG25_010" }, pins.CardIds);

        Assert.False(pins.Toggle("BG25_010_G"));   // game 2: unpin, through its golden copy
        pins = TavernPins.Parse(pins.Serialize(), Resolve);
        Assert.Equal(new[] { "BG_LOE_077" }, pins.CardIds);

        Assert.True(pins.Toggle("BG25_010"));      // game 3: pin again
        pins = TavernPins.Parse(pins.Serialize(), Resolve);
        Assert.Equal(new[] { "BG_LOE_077", "BG25_010" }, pins.CardIds);
    }

    [Fact]
    public void Empty_PinsNothing_AndSerializesToAComment()
    {
        Assert.Empty(TavernPins.Empty.CardIds);
        Assert.Empty(TavernPins.Parse(TavernPins.Empty.Serialize(), Resolve).CardIds);
    }
}
