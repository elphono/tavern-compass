namespace BronzebeardHud.Stats.Tests;

public class MarkerTextTests
{
    // Font 16 px, 4 px padding each side: 300 px → 29 glyphs, 150 px → 14, 60 px → 5.
    [Theory]
    [InlineData(300, 29)]
    [InlineData(150, 14)]
    [InlineData(60, 5)]
    public void MaxChars_ComesFromTheMarkerWidth(double width, int expected)
    {
        Assert.Equal(expected, MarkerText.MaxChars(width, fontSize: 16, horizontalPadding: 4));
    }

    [Theory]
    // Short name: fits everywhere but the narrowest marker.
    [InlineData("Beetles", 29, "★ Beetles 1/4")]
    [InlineData("Beetles", 14, "★ Beetles 1/4")]
    [InlineData("Beetles", 5, "★1/4")]
    // Medium name: full, then initials but the last word, then mark and count.
    [InlineData("Undead Butcher", 29, "★ Undead Butcher 2/5")]
    [InlineData("Undead Butcher", 14, "★ UB 2/5")]
    [InlineData("Undead Butcher", 5, "★2/5")]
    // Very long name.
    [InlineData("Aberration Deathrattle Discard Tempo", 29, "★ A. D. D. Tempo 0/3")]
    [InlineData("Aberration Deathrattle Discard Tempo", 14, "★ ADDT 0/3")]
    [InlineData("Aberration Deathrattle Discard Tempo", 5, "★0/3")]
    public void Label_IsShortenedUntilItFits(string name, int maxChars, string expected)
    {
        var owned = name == "Beetles" ? 1 : name == "Undead Butcher" ? 2 : 0;
        var total = name == "Beetles" ? 4 : name == "Undead Butcher" ? 5 : 3;

        var label = MarkerText.Label(name, owned, total, isKeyPiece: true, maxChars);

        Assert.Equal(expected, label);
        Assert.True(MarkerText.DisplayLength(label) <= maxChars, $"\"{label}\" is longer than {maxChars}");
    }

    [Fact]
    public void Label_AddonMark_AndMiddleStep()
    {
        Assert.Equal("+ M. Scam 0/2", MarkerText.Label("Murloc Scam", 0, 2, isKeyPiece: false, maxChars: 13));
        Assert.Equal("+ Murloc Scam 0/2", MarkerText.Label("Murloc Scam", 0, 2, isKeyPiece: false, maxChars: 17));
    }

    [Fact]
    public void Label_TooNarrowForAnything_IsCutNotOverflowing()
    {
        var label = MarkerText.Label("Undead Butcher", 2, 5, isKeyPiece: true, maxChars: 3);
        Assert.True(MarkerText.DisplayLength(label) <= 3);
        Assert.StartsWith("★", label);
    }

    [Fact]
    public void Lines_OnePerComposition_AndACountBeyondTheLimit()
    {
        var two = new[] { ("Undead Butcher", 2, 5, true), ("Murloc Scam", 0, 2, false) };
        Assert.Equal(new[] { "★ UB 2/5", "+ M. Scam 0/2" }, MarkerText.Lines(two, maxChars: 14));

        var four = new[] { ("Undead Butcher", 2, 5, true), ("Murloc Scam", 0, 2, false), ("Beetles", 1, 4, true), ("Pirate Discover", 0, 3, true) };
        Assert.Equal(new[] { "★ UB 2/5", "+3 more" }, MarkerText.Lines(four, maxChars: 14));
    }
}
