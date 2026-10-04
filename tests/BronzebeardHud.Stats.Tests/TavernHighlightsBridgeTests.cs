using static BronzebeardHud.Stats.Tests.BridgeFixture;

namespace BronzebeardHud.Stats.Tests;

public class TavernHighlightsBridgeTests
{
    private static readonly string P0 = CompTargetTracker.Palette[0];
    private static readonly string P1 = CompTargetTracker.Palette[1];

    // TOP: in no guide, 3/5 of Undead Butcher's boards. ONCE: 1/5. PA: an add-on of Pirate Discover, 3/5 of Undead Butcher's
    // boards. P1: a core card of Pirate Discover, 2/5 of Undead Butcher's boards. RTOP: in no target's guide, 2/5.
    private static readonly string[] Bob = { "TOP", "ONCE", "U2", "PA", "P1", "RTOP", "NEUTRAL" };

    private static IEnumerable<(HighlightKind, string?, string?, string)> Summary(IEnumerable<TavernHighlight> highlights) =>
        highlights.Select(h => (h.Kind, h.Target?.Guide.Name, h.Colour, h.Tag));

    [Fact]
    public void ACardNoTargetsGuideLists_OnTwoOrMoreOfItsCompsBoards_IsDotted_PlusTarget_AfterEveryRole()
    {
        var highlights = TavernHighlights.For(Bob, Targets(), Bridge());

        Assert.Equal(new (HighlightKind, string?, string?, string)[]
        {
            (HighlightKind.Enabler, "Undead Butcher", P0, "+"),   // TOP: top boards
            (HighlightKind.None, null, null, ""),                 // ONCE: 1 of 5 is not enough
            (HighlightKind.Commit, "Undead Butcher", P0, "core"),
            (HighlightKind.Enabler, "Pirate Discover", P1, "+"),  // PA: its add-on role first, not Undead Butcher's boards
            (HighlightKind.Commit, "Pirate Discover", P1, "core"), // P1: a core card first, not Undead Butcher's boards
            (HighlightKind.Enabler, "Undead Butcher", P0, "+"),   // RTOP: 2 of 5
            (HighlightKind.None, null, null, ""),
        }, Summary(highlights));
        Assert.Null(highlights[0].Effect);
        Assert.Equal("3/5", highlights[0].Boards!.Card.Count);
        Assert.Null(highlights[3].Boards);
        Assert.Null(highlights[4].Boards);
    }

    [Fact]
    public void WithoutABridge_TheFramesAreThoseFromBefore()
    {
        var without = TavernHighlights.For(Bob, Targets());
        var withNull = TavernHighlights.For(Bob, Targets(), null);
        var with = TavernHighlights.For(Bob, Targets(), Bridge());

        Assert.Equal(new[] { HighlightKind.None, HighlightKind.None, HighlightKind.Commit, HighlightKind.Enabler, HighlightKind.Commit, HighlightKind.None, HighlightKind.None },
            without.Select(h => h.Kind));
        Assert.Equal(Summary(without), Summary(withNull));
        // Only the cards no target's guide lists change with the bridge.
        Assert.Equal(new[] { 0, 5 }, Enumerable.Range(0, Bob.Length).Where(i => with[i].Kind != without[i].Kind));
    }

    [Fact]
    public void MarkerLines_PlusTargetAndTheBoards_ShortenedToFit()
    {
        var top = TavernHighlights.For(new[] { "TOP" }, Targets(), Bridge()).Single();

        Assert.Equal(new[] { "+ Undead Butcher 3/5" }, TavernHighlights.MarkerLines(top, pinned: false, maxChars: 20));
        Assert.Equal(new[] { "+ UB 3/5" }, TavernHighlights.MarkerLines(top, pinned: false, maxChars: 15));
        Assert.Equal(new[] { "◆ pinned", "+ U. Butcher 3/5" }, TavernHighlights.MarkerLines(top, pinned: true, maxChars: 18));
    }

    [Fact]
    public void LogSummary_KeepsThePluginsFormat_AndNamesTheBoards()
    {
        Assert.Equal("U2:core:Undead Butcher/11,PA:addon:Pirate Discover/23,P1:core:Pirate Discover/23",
            TavernHighlights.Summary(Bob, TavernHighlights.For(Bob, Targets())));
        Assert.Equal("TOP:boards 3/5:Undead Butcher/11,U2:core:Undead Butcher/11,PA:addon:Pirate Discover/23,P1:core:Pirate Discover/23,RTOP:boards 2/5:Undead Butcher/11",
            TavernHighlights.Summary(Bob, TavernHighlights.For(Bob, Targets(), Bridge())));
    }
}
