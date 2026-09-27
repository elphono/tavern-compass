namespace BronzebeardHud.Stats.Tests;

public class TavernHighlightsTests
{
    // Synthetic tiers, every card distinct.
    private static readonly Dictionary<string, int> Tiers = new()
    {
        ["KEY_A5"] = 5, ["SHARED_T3"] = 3, ["EARLY_A2"] = 2, ["KEY_B4"] = 4, ["EARLY_B1"] = 1, ["KEY_S6"] = 6, ["EARLY_S2"] = 2, ["OTHER_T1"] = 1,
    };

    private static int? TierOf(string id) => Tiers.TryGetValue(id, out var t) ? t : null;

    private static Composition Comp(string id, string name, string[] core, string[] addons) =>
        new(id, name, Array.Empty<string>(), core, addons, finalBoards: new[] { new FinalBoard(9000, 11, core.Concat(addons).ToList()) });

    // A: key piece KEY_A5, early enablers EARLY_A2 and SHARED_T3. B: key pieces KEY_B4 and SHARED_T3, early enabler EARLY_B1.
    // S (a suggestion): key piece KEY_S6, early enabler EARLY_S2.
    private static readonly Composition A = Comp("a", "Undead Butcher", new[] { "KEY_A5" }, new[] { "EARLY_A2", "SHARED_T3" });
    private static readonly Composition B = Comp("b", "Mech Magnet", new[] { "KEY_B4", "SHARED_T3" }, new[] { "EARLY_B1" });
    private static readonly Composition S = Comp("s", "Pirate Gold", new[] { "KEY_S6" }, new[] { "EARLY_S2" });

    private static readonly Dictionary<string, string> Palette = new() { ["a"] = "#FF2BD6", ["b"] = "#B8FF1F" };

    private static IReadOnlyList<TavernHighlight> Run(string[] bob, Composition[] ticked, Composition[] suggestions) =>
        TavernHighlights.For(bob, ticked, suggestions, c => CompDetail.For(c, TierOf), id => Palette.TryGetValue(id, out var c) ? c : null);

    private static IEnumerable<(HighlightKind, string?, string?)> Summary(IEnumerable<TavernHighlight> highlights) =>
        highlights.Select(h => (h.Kind, h.Composition?.Id, h.Colour));

    [Fact]
    public void TickedCompositions_KeyPiecesAreCommit_EarlyCardsAreEnabler_InTheirColour()
    {
        var bob = new[] { "KEY_A5_G", "EARLY_A2", "KEY_B4", "EARLY_B1", "OTHER_T1", "BG_SPELL" };

        var highlights = Run(bob, new[] { A, B }, new[] { S });

        Assert.Equal(new (HighlightKind, string?, string?)[]
        {
            (HighlightKind.Commit, "a", "#FF2BD6"),  // golden copy of A's key piece
            (HighlightKind.Enabler, "a", "#FF2BD6"),
            (HighlightKind.Commit, "b", "#B8FF1F"),
            (HighlightKind.Enabler, "b", "#B8FF1F"),
            (HighlightKind.None, null, null),
            (HighlightKind.None, null, null),
        }, Summary(highlights));
    }

    [Fact]
    public void ACardThatIsCommitForOneAndEnablerForAnother_IsCommit()
    {
        // SHARED_T3 is an early enabler of A (listed first) and a key piece of B.
        var highlight = Run(new[] { "SHARED_T3" }, new[] { A, B }, Array.Empty<Composition>()).Single();

        Assert.Equal((HighlightKind.Commit, "b"), (highlight.Kind, highlight.Composition!.Id));
    }

    [Fact]
    public void TickedCompositionsComeFirst_SuggestionsOnlyWhenNothingIsTicked()
    {
        var bob = new[] { "KEY_S6", "EARLY_S2", "KEY_A5" };

        Assert.Equal(new (HighlightKind, string?, string?)[] { (HighlightKind.None, null, null), (HighlightKind.None, null, null), (HighlightKind.Commit, "a", "#FF2BD6") },
            Summary(Run(bob, new[] { A }, new[] { S })));
        Assert.Equal(new (HighlightKind, string?, string?)[]
            {
                (HighlightKind.Commit, "s", TavernHighlights.SuggestionColour), (HighlightKind.Enabler, "s", TavernHighlights.SuggestionColour), (HighlightKind.None, null, null),
            },
            Summary(Run(bob, Array.Empty<Composition>(), new[] { S })));
    }

    [Fact]
    public void MarkerLines_TheHighlightFirst_ItsCompositionNeverTwice_TwoLinesAtMost()
    {
        var commit = new TavernHighlight(HighlightKind.Commit, A, "#FF2BD6");
        var advances = new (Composition, bool)[] { (A, true), (B, false) };
        int KeyOwned(Composition c) => c.Id == "a" ? 1 : 0;

        Assert.Equal(new[] { "commit UB 1/1", "+ M. Magnet 0/2" }, TavernHighlights.MarkerLines(commit, advances, KeyOwned, pinned: false, maxChars: 15));
        Assert.Equal(new[] { "◆ pinned", "commit UB 1/1" }, TavernHighlights.MarkerLines(commit, advances, KeyOwned, pinned: true, maxChars: 15));
        Assert.Equal(new[] { "enabler UB" }, TavernHighlights.MarkerLines(new TavernHighlight(HighlightKind.Enabler, A, "#FFFFFF"), new (Composition, bool)[] { (A, false) },
            KeyOwned, pinned: false, maxChars: 15));
        Assert.Equal(new[] { "★ UB 1/1", "+ M. Magnet 0/2" }, TavernHighlights.MarkerLines(TavernHighlight.None, advances, KeyOwned, pinned: false, maxChars: 15));
    }

    [Fact]
    public void MarkerLines_Crowded_TheHighlightKeepsItsLine_TheOthersAreCounted()
    {
        // The card is A's commit piece and also marks B and S: one line left after the highlight.
        var commit = new TavernHighlight(HighlightKind.Commit, A, "#FF2BD6");
        var advances = new (Composition, bool)[] { (B, false), (A, true), (S, false) };

        var lines = TavernHighlights.MarkerLines(commit, advances, _ => 0, pinned: false, maxChars: 15);

        Assert.Equal(new[] { "commit UB 0/1", "+2 more" }, lines);
    }
}
