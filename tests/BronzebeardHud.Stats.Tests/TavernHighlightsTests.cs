using static BronzebeardHud.Stats.Tests.GuideTestData;

namespace BronzebeardHud.Stats.Tests;

public class TavernHighlightsTests
{
    // Undead: core KEY_U1 KEY_U2, enabler EN_U, add-on ADD_U; SHARED is an enabler of Undead and a core card of Mechs.
    // Mechs: core KEY_M1 SHARED, add-on ADD_M. Pirates: core KEY_P1, add-on ADD_P. Every other card distinct.
    private static readonly CompGuide Undead = Guide("Undead Butcher", 2, 0, new[] { "KEY_U1", "KEY_U2" }, addons: new[] { "ADD_U" }, enablers: new[] { "EN_U", "SHARED" });
    private static readonly CompGuide Mechs = Guide("Mech Magnet", 2, 1, new[] { "KEY_M1", "SHARED" }, addons: new[] { "ADD_M" });
    private static readonly CompGuide Pirates = Guide("Pirate Gold", 2, 2, new[] { "KEY_P1" }, addons: new[] { "ADD_P", "ADD_M" });
    private static readonly CompGuideSet All = Set(Undead, Mechs, Pirates);

    private static readonly string P0 = CompTargetTracker.Palette[0];
    private static readonly string P1 = CompTargetTracker.Palette[1];
    private static readonly string P2 = CompTargetTracker.Palette[2];

    /// <summary>Undead first (two core cards held), then Mechs (one), then Pirates (ticked last but probable: an add-on).</summary>
    private static IReadOnlyList<CompTarget> ThreeTargets() => Targets(All, Board("KEY_U1", "KEY_U2", "KEY_M1", "ADD_P"), 3);

    private static IEnumerable<(HighlightKind, string?, string?, string)> Summary(IEnumerable<TavernHighlight> highlights) =>
        highlights.Select(h => (h.Kind, h.Target?.Guide.Name, h.Colour, h.Tag));

    [Fact]
    public void CoreCardsAreCommit_EnablersAndAddOnsAreEnabler_InTheirTargetsColour()
    {
        var targets = ThreeTargets();
        Assert.Equal(new[] { "Undead Butcher", "Mech Magnet", "Pirate Gold" }, targets.Select(t => t.Guide.Name));
        var bob = new[] { "KEY_U1_G", "EN_U", "ADD_U", "KEY_M1", "KEY_P1", "OTHER", "BG_SPELL" };

        var highlights = TavernHighlights.For(bob, targets);

        Assert.Equal(new (HighlightKind, string?, string?, string)[]
        {
            (HighlightKind.Commit, "Undead Butcher", P0, "core"),  // golden copy of a core card
            (HighlightKind.Enabler, "Undead Butcher", P0, "enabler"),
            (HighlightKind.Enabler, "Undead Butcher", P0, "+"),
            (HighlightKind.Commit, "Mech Magnet", P1, "core"),
            (HighlightKind.Commit, "Pirate Gold", P2, "core"),
            (HighlightKind.None, null, null, ""),
            (HighlightKind.None, null, null, ""),
        }, Summary(highlights));
    }

    [Fact]
    public void ACardThatIsCoreForOneAndEnablerForAnEarlierOne_IsCore_OfTheLaterOne()
    {
        // SHARED is an enabler of Undead (first target) and a core card of Mechs (second).
        var highlight = TavernHighlights.For(new[] { "SHARED" }, ThreeTargets()).Single();

        Assert.Equal((HighlightKind.Commit, "Mech Magnet", P1), (highlight.Kind, highlight.Target!.Guide.Name, highlight.Colour));
        Assert.Equal(new[] { ("Undead Butcher", GuideCardRole.Enabler) }, highlight.Others.Select(e => (e.Target.Guide.Name, e.Role)));
    }

    [Fact]
    public void AmongTargetsOfTheSameKind_TheFirstInTargetOrderWins()
    {
        // ADD_M is an add-on of Mechs (second target) and of Pirates (third).
        var highlight = TavernHighlights.For(new[] { "ADD_M" }, ThreeTargets()).Single();

        Assert.Equal((HighlightKind.Enabler, "Mech Magnet", P1, "+"), (highlight.Kind, highlight.Target!.Guide.Name, highlight.Colour, highlight.Tag));
        Assert.Equal(new[] { "Pirate Gold" }, highlight.Others.Select(e => e.Target.Guide.Name));
    }

    [Fact]
    public void OnlyTargetsCount_AGuideThatIsNoTargetHighlightsNothing()
    {
        var oneTarget = Targets(All, Board("KEY_U1"), 1);

        var highlights = TavernHighlights.For(new[] { "KEY_M1", "KEY_P1", "KEY_U2" }, oneTarget);

        Assert.Equal(new[] { HighlightKind.None, HighlightKind.None, HighlightKind.Commit }, highlights.Select(h => h.Kind));
        Assert.Empty(TavernHighlights.For(Array.Empty<string>(), oneTarget));
        Assert.All(TavernHighlights.For(new[] { "KEY_U1" }, Array.Empty<CompTarget>()), h => Assert.Same(TavernHighlight.None, h));
    }

    // Bob's row for the ticks: a core card and an add-on of Pirates, a core card, an enabler and an add-on of Undead, a core
    // card of Mechs. Distinct cards for each guide, so that whose frame each one gets shows.
    private static readonly string[] TickRow = { "KEY_P1", "ADD_P", "KEY_U1", "EN_U", "ADD_U", "KEY_M1" };

    // Held: both key cards of Undead (in progress), one of Mechs (a guess), an add-on of Pirates (a guess).
    private static readonly PlayerCards TickBoard = Board("KEY_U1", "KEY_U2", "KEY_M1", "ADD_P");

    [Fact]
    public void ATickedTarget_FramesAlone_AGuideInProgressFramesNothing_AGuessNeither()
    {
        // (Ali, 2026-10-07: "quand on sélectionne des compos vers lesquelles on veut tendre, on ne devrait plus surligner aucun
        // autre sbire dans le shop".) Pirates ticked: it comes first and frames its cards. Undead Butcher, both of its key cards
        // held, is being built: it stays a target (the panel lists it, "in progress"), but none of its cards is framed. Mech
        // Magnet, one key card held, was only a guess: no target, no frame.
        var targets = Targets(All, TickBoard, 3, Pirates);
        Assert.Equal(new[] { ("Pirate Gold", TargetKind.Chosen), ("Undead Butcher", TargetKind.InProgress) }, targets.Select(t => (t.Guide.Name, t.Kind)));

        var highlights = TavernHighlights.For(TickRow, targets);

        Assert.Equal(new (HighlightKind, string?, string?, string)[]
        {
            (HighlightKind.Commit, "Pirate Gold", P0, "core"),
            (HighlightKind.Enabler, "Pirate Gold", P0, "+"),
            (HighlightKind.None, null, null, ""),
            (HighlightKind.None, null, null, ""),
            (HighlightKind.None, null, null, ""),
            (HighlightKind.None, null, null, ""),
        }, Summary(highlights));
        Assert.Equal("KEY_P1:core:Pirate Gold/0,ADD_P:addon:Pirate Gold/0", TavernHighlights.Summary(TickRow, highlights));
    }

    [Fact]
    public void NothingTicked_EveryTargetFramesItsCards_AsBefore()
    {
        // The same row, the same board, nothing ticked: Undead Butcher, Mech Magnet and Pirate Gold are the probable targets,
        // and each frames its cards in its colour.
        var targets = Targets(All, TickBoard, 3);
        Assert.Equal(new[] { ("Undead Butcher", TargetKind.Probable), ("Mech Magnet", TargetKind.Probable), ("Pirate Gold", TargetKind.Probable) },
            targets.Select(t => (t.Guide.Name, t.Kind)));

        var highlights = TavernHighlights.For(TickRow, targets);

        Assert.Equal(new (HighlightKind, string?, string?, string)[]
        {
            (HighlightKind.Commit, "Pirate Gold", P2, "core"),
            (HighlightKind.Enabler, "Pirate Gold", P2, "+"),
            (HighlightKind.Commit, "Undead Butcher", P0, "core"),
            (HighlightKind.Enabler, "Undead Butcher", P0, "enabler"),
            (HighlightKind.Enabler, "Undead Butcher", P0, "+"),
            (HighlightKind.Commit, "Mech Magnet", P1, "core"),
        }, Summary(highlights));
        Assert.Same(targets, TavernHighlights.Framing(targets));
    }

    [Fact]
    public void ATickedTarget_TheOthersUnderItsLabel_AreTickedOnesOnly()
    {
        // ADD_M is an add-on of Mechs and of Pirates. Mechs, two key cards held (KEY_M1, SHARED), is in progress; Pirates is
        // ticked: ADD_M is Pirates' "+", and Mechs is not listed under it.
        var targets = Targets(All, Board("KEY_M1", "SHARED"), 3, Pirates);
        Assert.Equal(new[] { ("Pirate Gold", TargetKind.Chosen), ("Mech Magnet", TargetKind.InProgress) }, targets.Select(t => (t.Guide.Name, t.Kind)));

        var highlight = TavernHighlights.For(new[] { "ADD_M" }, targets).Single();

        Assert.Equal((HighlightKind.Enabler, "Pirate Gold", P0, "+"), (highlight.Kind, highlight.Target!.Guide.Name, highlight.Colour, highlight.Tag));
        Assert.Empty(highlight.Others);
        Assert.Equal(new[] { "+ Pirate Gold" }, TavernHighlights.MarkerLines(highlight, pinned: false, maxChars: 20));
    }

    [Fact]
    public void TheLogLine_SaysWhichTargetsFrame_WhenAGuideIsTicked()
    {
        var ticked = Targets(All, TickBoard, 3, Pirates);
        var auto = Targets(All, TickBoard, 3);

        Assert.Equal($"Bronzebeard HUD: tavern highlights=[KEY_P1:core:Pirate Gold/0,ADD_P:addon:Pirate Gold/0] targets=[Pirate Gold {P0}; Undead Butcher {P1}] "
                     + $"frames from ticked=[Pirate Gold {P0}]",
            TavernHighlights.LogLine(TavernHighlights.Summary(TickRow, TavernHighlights.For(TickRow, ticked)), ticked));
        Assert.Equal($"Bronzebeard HUD: tavern highlights=[KEY_P1:core:Pirate Gold/0,ADD_P:addon:Pirate Gold/0,KEY_U1:core:Undead Butcher/0,"
                     + $"EN_U:enabler:Undead Butcher/0,ADD_U:addon:Undead Butcher/0,KEY_M1:core:Mech Magnet/0] targets=[Undead Butcher {P0}; Mech Magnet {P1}; Pirate Gold {P2}]",
            TavernHighlights.LogLine(TavernHighlights.Summary(TickRow, TavernHighlights.For(TickRow, auto)), auto));
    }

    [Fact]
    public void MarkerLines_TheHighlightFirst_WithTheCoreCardsHeld_ThenTheOthers_TwoLinesAtMost()
    {
        var targets = ThreeTargets();
        TavernHighlight For(string card) => TavernHighlights.For(new[] { card }, targets).Single();

        Assert.Equal(new[] { "core UB 2/2" }, TavernHighlights.MarkerLines(For("KEY_U2"), pinned: false, maxChars: 15));
        Assert.Equal(new[] { "core Mech Magnet 1/2", "+ Undead Butcher 2/2" }, TavernHighlights.MarkerLines(For("SHARED"), pinned: false, maxChars: 20));
        Assert.Equal(new[] { "core M. Magnet 1/2", "+ U. Butcher 2/2" }, TavernHighlights.MarkerLines(For("SHARED"), pinned: false, maxChars: 18));
        Assert.Equal(new[] { "◆ pinned", "core M. Magnet 1/2" }, TavernHighlights.MarkerLines(For("SHARED"), pinned: true, maxChars: 18));
        Assert.Equal(new[] { "enabler UB" }, TavernHighlights.MarkerLines(For("EN_U"), pinned: false, maxChars: 15));
        Assert.Equal(new[] { "+ U. Butcher" }, TavernHighlights.MarkerLines(For("ADD_U"), pinned: false, maxChars: 15));
        Assert.Equal(new[] { "◆ pinned" }, TavernHighlights.MarkerLines(TavernHighlight.None, pinned: true, maxChars: 15));
        Assert.Empty(TavernHighlights.MarkerLines(TavernHighlight.None, pinned: false, maxChars: 15));
    }

    [Fact]
    public void MarkerLines_TheCardValue_TakesTheLastLineLeft_AndNeverPushesARoleOut()
    {
        var targets = ThreeTargets();
        TavernHighlight For(string card) => TavernHighlights.For(new[] { card }, targets).Single();
        const string value = "t6 ▲ 3.6 vs 3.9";

        Assert.Equal(new[] { value }, TavernHighlights.MarkerLines(TavernHighlight.None, pinned: false, maxChars: 20, value: value));
        Assert.Equal(new[] { "core UB 2/2", value }, TavernHighlights.MarkerLines(For("KEY_U2"), pinned: false, maxChars: 15, value: value));
        Assert.Equal(new[] { "◆ pinned", value }, TavernHighlights.MarkerLines(TavernHighlight.None, pinned: true, maxChars: 20, value: value));
        // Two lines already: the value is the one left out.
        Assert.Equal(new[] { "core Mech Magnet 1/2", "+ Undead Butcher 2/2" },
            TavernHighlights.MarkerLines(For("SHARED"), pinned: false, maxChars: 20, value: value));
        Assert.Equal(new[] { "◆ pinned", "core UB 2/2" }, TavernHighlights.MarkerLines(For("KEY_U2"), pinned: true, maxChars: 15, value: value));
    }

    [Fact]
    public void MarkerLines_Crowded_TheHighlightKeepsItsLine_TheOthersAreCounted()
    {
        // CROWD: a core card of the first target and an add-on of three others.
        var a = Guide("Alpha", 1, 0, new[] { "CROWD" });
        var b = Guide("Bravo", 1, 1, new[] { "B1" }, addons: new[] { "CROWD" });
        var c = Guide("Charlie", 1, 2, new[] { "C1" }, addons: new[] { "CROWD" });
        var d = Guide("Delta", 1, 3, new[] { "D1" }, addons: new[] { "CROWD" });
        var targets = Targets(Set(a, b, c, d), Board("CROWD", "B1", "C1", "D1"), 4);

        var lines = TavernHighlights.MarkerLines(TavernHighlights.For(new[] { "CROWD" }, targets).Single(), pinned: false, maxChars: 15);

        Assert.Equal(new[] { "core Alpha 1/1", "+3 more" }, lines);
        Assert.Equal(new[] { "core Alpha 1/1", "+ Bravo 1/1", "+2 more" },
            TavernHighlights.MarkerLines(TavernHighlights.For(new[] { "CROWD" }, targets).Single(), pinned: false, maxChars: 15, maxLines: 3));
    }
}
