using System.Globalization;
using static BronzebeardHud.Stats.Tests.GuideTestData;

namespace BronzebeardHud.Stats.Tests;

/// <summary>Synthetic guides, all in tier A, two core cards each, every card distinct: a wrong attribution shows.</summary>
public class CompTargetsTests
{
    private static readonly CompGuide X = Guide("Xeno", 2, 0, new[] { "X1", "X2" });
    private static readonly CompGuide Y = Guide("Yeti", 2, 1, new[] { "Y1", "Y2" });
    private static readonly CompGuide Z = Guide("Zeal", 2, 2, new[] { "Z1", "Z2" });
    private static readonly CompGuide W = Guide("Wolf", 2, 3, new[] { "W1", "W2" });
    private static readonly CompGuide V = Guide("Vile", 2, 4, new[] { "V1", "V2" });
    private static readonly CompGuide U = Guide("Ursa", 2, 5, new[] { "U1", "U2" });
    private static readonly CompGuideSet All = Set(X, Y, Z, W, V, U);

    private static readonly string P0 = CompTargetTracker.Palette[0];
    private static readonly string P1 = CompTargetTracker.Palette[1];
    private static readonly string P2 = CompTargetTracker.Palette[2];

    private static IEnumerable<(string, string)> NamesAndColours(IEnumerable<CompTarget> targets) => targets.Select(t => (t.Guide.Name, t.Colour));

    [Fact]
    public void FourIterations_ATargetThatStaysKeepsItsColour_AFreedColourGoesToTheNextNewTarget()
    {
        var tracker = new CompTargetTracker();
        tracker.BeginGame(1);

        // 1: Xeno 6, Yeti 3, Zeal 3: the palette in order.
        var first = tracker.Next(CompGuideMatch.Rank(All, Board("X1", "X2", "Y1", "Z1")), 3);
        Assert.Equal(new[] { ("Xeno", P0), ("Yeti", P1), ("Zeal", P2) }, NamesAndColours(first));

        // 2: Xeno is gone, Wolf comes. Zeal is now first but keeps its colour; Wolf takes Xeno's, the first one free.
        var second = tracker.Next(CompGuideMatch.Rank(All, Board("W1", "W2", "Z1", "Z2", "Y1")), 3);
        Assert.Equal(new[] { ("Zeal", P2), ("Wolf", P0), ("Yeti", P1) }, NamesAndColours(second));
        Assert.Equal(new[] { 1, 2, 3 }, second.Select(t => t.Rank));

        // 3: Yeti is gone, Vile comes and takes Yeti's colour; Zeal and Wolf keep theirs.
        var third = tracker.Next(CompGuideMatch.Rank(All, Board("V1", "V2", "Z1", "W1")), 3);
        Assert.Equal(new[] { ("Vile", P1), ("Zeal", P2), ("Wolf", P0) }, NamesAndColours(third));

        // 4: Ursa is ticked, though nothing of it is held: it comes first. Vile, both of its key cards held, is in progress and
        // stays a target with its colour; Zeal and Wolf, one key card each, were only guesses: set aside.
        Assert.True(tracker.Toggle(U.Id));
        var fourth = tracker.Next(CompGuideMatch.Rank(All, Board("V1", "V2", "Z1", "W1")), 3);
        Assert.Equal(new[] { ("Ursa", P0), ("Vile", P1) }, NamesAndColours(fourth));
        Assert.Equal(new[] { TargetKind.Chosen, TargetKind.InProgress }, fourth.Select(t => t.Kind));
        Assert.Equal(new[] { true, false }, fourth.Select(t => t.Ticked));
        Assert.Same(fourth, tracker.Targets);

        // 5: Ursa is unticked: the automatic targets are back; Vile kept its colour all along, Zeal and Wolf did not.
        Assert.True(tracker.Toggle(U.Id));
        var fifth = tracker.Next(CompGuideMatch.Rank(All, Board("V1", "V2", "Z1", "W1")), 3);
        Assert.Equal(new[] { ("Vile", P1), ("Zeal", P0), ("Wolf", P2) }, NamesAndColours(fifth));
        Assert.All(fifth, t => Assert.Equal(TargetKind.Probable, t.Kind));
    }

    [Fact]
    public void TheSameBoardTwice_ChangesNothing()
    {
        var tracker = new CompTargetTracker();
        var board = CompGuideMatch.Rank(All, Board("Z1", "Z2", "W1"));

        var once = NamesAndColours(tracker.Next(board, 3)).ToList();
        var twice = NamesAndColours(tracker.Next(board, 3)).ToList();

        Assert.Equal(new[] { ("Zeal", P0), ("Wolf", P1) }, once); // only two guides score: no third target
        Assert.Equal(once, twice);
    }

    [Fact]
    public void TickedGuides_ComeFirst_InTheOrderTheyWereTicked_ThenOnlyTheGuidesInProgress_UpToTheCount()
    {
        var board = CompGuideMatch.Rank(All, Board("X1", "X2", "Y1", "Z1"));

        var chosen = CompTargets.Choose(board, new[] { U.Id, W.Id }, 3);

        // Xeno (both key cards held) is in progress; Yeti and Zeal (one each) were the speculative automatic targets: left out.
        Assert.Equal(new[] { ("Ursa", TargetKind.Chosen), ("Wolf", TargetKind.Chosen), ("Xeno", TargetKind.InProgress) },
            chosen.Select(c => (c.Progress.Guide.Name, c.Kind)));
        Assert.Equal(0.0, chosen[0].Progress.Score); // ticked: a target whatever its score
        // The count is the most compositions wanted (Ali, 2026-10-10): the guides in progress fill what the ticks leave of it.
        Assert.Equal(new[] { "Ursa", "Wolf" }, CompTargets.Choose(board, new[] { U.Id, W.Id }, 2).Select(c => c.Progress.Guide.Name));
        Assert.Equal(new[] { "Ursa", "Wolf" }, CompTargets.Choose(board, new[] { U.Id, W.Id }, 1).Select(c => c.Progress.Guide.Name)); // never a tick dropped
        Assert.Equal(new[] { "Ursa", "Wolf", "Xeno" }, CompTargets.Choose(board, new[] { U.Id, W.Id }, 4).Select(c => c.Progress.Guide.Name));
    }

    /// <summary>
    /// Ali, 2026-10-10: "quand on a sélectionné le nombre de compos qui correspond au nombre max, il faudrait automatiquement
    /// update le panel pour ne laisser afficher plus qu'elles". As many ticks as compositions wanted: the panel lists the
    /// ticked guides alone, in their tiers; fewer ticks, or none: every guide.
    /// </summary>
    [Fact]
    public void AsManyTicksAsWanted_ThePanelListsTheTickedGuidesAlone()
    {
        var tracker = new CompTargetTracker();
        var board = CompGuideMatch.Rank(All, Board("X1", "X2", "Y1"));
        tracker.Toggle(W.Id);
        tracker.Toggle(U.Id);

        var two = CompTargets.Listed(board, tracker.Next(board, 2), 2);
        Assert.Equal(new[] { "Ursa", "Wolf" }, two.All.Select(p => p.Guide.Name).OrderBy(n => n));
        Assert.Equal(board.Tiers.Where(t => t.Rows.Any(r => r.Guide.Name is "Ursa" or "Wolf")).Select(t => t.Tier), two.Tiers.Select(t => t.Tier));
        Assert.True(CompTargets.OnlyTicked(tracker.Targets, 2));

        Assert.Same(board, CompTargets.Listed(board, tracker.Next(board, 3), 3)); // one more wanted: every guide again
        Assert.False(CompTargets.OnlyTicked(tracker.Targets, 3));
        Assert.Same(board, CompTargets.Listed(board, new CompTargetTracker().Next(board, 1), 1)); // nothing ticked
    }

    /// <summary>− works with ticks too (Ali, 2026-10-10), but never goes under the ticked guides; without a tick it is always live.</summary>
    [Theory]
    [InlineData(0, 3, true)]
    [InlineData(0, 1, true)]   // no tick: a press at 1 sizes the panel again, as before
    [InlineData(2, 3, true)]
    [InlineData(2, 2, false)]  // two ticked, two wanted: untick to go lower
    [InlineData(3, 2, false)]  // ticked while fewer were wanted
    [InlineData(1, 4, true)]
    public void Minus_WithTicks_NeverUnderTheTickedGuides(int ticked, int count, bool minus)
    {
        var tracker = new CompTargetTracker();
        foreach (var guide in new[] { U, V, W, X }.Take(ticked))
        {
            tracker.Toggle(guide.Id);
        }

        var targets = tracker.Next(CompGuideMatch.Rank(All, Board("Y1")), count);

        Assert.Equal(minus, CompTargets.CanDecrease(targets, count));
    }

    [Fact]
    public void TickingTheGuideAlreadyFirst_KeepsTheOtherGuidesInProgress_AndOnlyThem()
    {
        // As on 2026-10-06 17:14 in Ali's game: he ticked the first target, and the second one went away (he unticked 2 s later).
        // Here the second has two key cards held: it stays, in its colour; the third, one key card, was a guess: it goes.
        var tracker = new CompTargetTracker();
        var board = CompGuideMatch.Rank(All, Board("X1", "X2", "Y1", "Y2", "Z1"));
        Assert.Equal(new[] { ("Xeno", P0), ("Yeti", P1), ("Zeal", P2) }, NamesAndColours(tracker.Next(board, 3)));

        tracker.Toggle(X.Id);
        var ticked = tracker.Next(board, 3);

        Assert.Equal(new[] { ("Xeno", P0), ("Yeti", P1) }, NamesAndColours(ticked));
        Assert.Equal(new[] { TargetKind.Chosen, TargetKind.InProgress }, ticked.Select(t => t.Kind));
    }

    [Theory]
    [InlineData(new[] { "X1" }, false)]             // one of two key cards: a guess
    [InlineData(new[] { "X1", "X2" }, true)]        // two key cards
    [InlineData(new[] { "X1", "X1_G" }, false)]     // the same card twice (a golden copy) is one key card
    [InlineData(new[] { "SOLO" }, true)]            // a guide of one key card: holding it is all there is
    public void InProgress_IsTwoKeyCardsHeld_OrTheOnlyOneOfAOneCardGuide(string[] held, bool expected)
    {
        var solo = Guide("Solo", 3, 0, new[] { "SOLO" });
        var board = CompGuideMatch.Rank(Set(X, solo), Board(held));
        var progress = board.All.Single(p => p.Guide.Name == (held[0] == "SOLO" ? "Solo" : "Xeno"));

        Assert.Equal(expected, CompTargets.IsInProgress(progress));
    }

    [Fact]
    public void WithTicks_GuidesInProgress_FillOnlyWhatTheCountLeaves()
    {
        // Xeno, Yeti and Zeal are all in progress; three ticked guides of four wanted leave room for one: the most probable.
        var board = CompGuideMatch.Rank(All, Board("X1", "X2", "Y1", "Y2", "Z1", "Z2", "X1_G"));

        var chosen = CompTargets.Choose(board, new[] { U.Id, W.Id, V.Id }, 4);

        Assert.Equal(new[] { "Ursa", "Wolf", "Vile", "Xeno" }, chosen.Select(c => c.Progress.Guide.Name));
        Assert.Equal(new[] { "Ursa", "Wolf", "Vile" }, CompTargets.Choose(board, new[] { U.Id, W.Id, V.Id }, 3).Select(c => c.Progress.Guide.Name));
    }

    [Fact]
    public void ATargetTiedWithANewcomer_KeepsItsPlace_UntilItIsBeaten_FourRounds()
    {
        // One automatic target. Ties used to go to HDT's order (Xeno before Yeti before Zeal), so the target jumped to Xeno as
        // soon as one of its cards was bought, then back; a target now keeps its place against an equal score.
        var tracker = new CompTargetTracker();
        string Target(params string[] held) => tracker.Next(CompGuideMatch.Rank(All, Board(held)), 1).Single().Guide.Name;

        Assert.Equal("Yeti", Target("Y1"));               // the only one that scores
        Assert.Equal("Yeti", Target("Y1", "X1"));         // Xeno ties at 3: Yeti stays
        Assert.Equal("Yeti", Target("Y1", "X1", "Z1"));   // a three-way tie: Yeti stays
        Assert.Equal("Xeno", Target("Y1", "X1", "X2"));   // Xeno 6 against 3: beaten, it changes
        Assert.Equal("Xeno", Target("Y1", "Y2", "X1", "X2")); // tied again at 6: the new target stays
    }

    [Fact]
    public void FourTickedGuides_AreFourTargets_UnknownIdsAreSkipped_AndNothingKnownTickedMeansAutomatic()
    {
        var board = CompGuideMatch.Rank(All, Board("X1"));

        var four = CompTargets.Choose(board, new[] { U.Id, V.Id, W.Id, Z.Id }, 2);
        var oneUnknown = CompTargets.Choose(board, new[] { "Gone/0", U.Id }, 3);
        var allUnknown = CompTargets.Choose(board, new[] { "Gone/0" }, 3);

        Assert.Equal(new[] { "Ursa", "Vile", "Wolf", "Zeal" }, four.Select(c => c.Progress.Guide.Name));
        Assert.Equal(new[] { "Ursa" }, oneUnknown.Select(c => c.Progress.Guide.Name));
        // A tick the board does not know restricts nothing: back to the automatic targets (only Xeno scores).
        Assert.Equal(new[] { ("Xeno", false) }, allUnknown.Select(c => (c.Progress.Guide.Name, c.Ticked)));
    }

    [Fact]
    public void Choose_NeverGivesMoreThanFourTicked_NorTheSameGuideTwice()
    {
        var board = CompGuideMatch.Rank(All, Board("X1"));

        var five = CompTargets.Choose(board, new[] { U.Id, V.Id, W.Id, Z.Id, Y.Id }, 3);
        var twice = CompTargets.Choose(board, new[] { U.Id, W.Id, U.Id }, 3);

        Assert.Equal(new[] { "Ursa", "Vile", "Wolf", "Zeal" }, five.Select(c => c.Progress.Guide.Name)); // the fifth is not a target
        Assert.Equal(new[] { "Ursa", "Wolf" }, twice.Select(c => c.Progress.Guide.Name));
    }

    [Fact]
    public void TickingSilencesTheGuesses_UntickingEverythingGivesThemBack_ThreeRounds()
    {
        // Two rounds validate the transition; the third shows that nothing keeps the restriction alive.
        var tracker = new CompTargetTracker();
        var board = CompGuideMatch.Rank(All, Board("X1", "X2", "Y1", "Z1"));
        IEnumerable<string> Names() => tracker.Next(board, 3).Select(t => t.Guide.Name);

        Assert.Equal(new[] { "Xeno", "Yeti", "Zeal" }, Names());       // nothing ticked: the probable ones

        tracker.Toggle(W.Id);
        Assert.Equal(new[] { "Wolf", "Xeno" }, Names());               // one ticked: it, then Xeno, in progress
        tracker.Toggle(U.Id);
        Assert.Equal(new[] { "Wolf", "Ursa", "Xeno" }, Names());       // two ticked: in the order ticked
        tracker.Toggle(W.Id);
        Assert.Equal(new[] { "Ursa", "Xeno" }, Names());
        tracker.Toggle(U.Id);
        Assert.Equal(new[] { "Xeno", "Yeti", "Zeal" }, Names());       // nothing ticked again

        tracker.Toggle(Z.Id);
        Assert.Equal(new[] { "Zeal", "Xeno" }, Names());               // third round: Zeal, ticked, comes first
        tracker.Toggle(Z.Id);
        Assert.Equal(new[] { "Xeno", "Zeal", "Yeti" }, Names());       // Zeal, a target until now, keeps its place over Yeti (both 3)
        Assert.All(tracker.Targets, t => Assert.False(t.Ticked));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(8, 4)]
    public void TheCount_IsBroughtInsideOneToFour(int count, int expected)
    {
        var board = CompGuideMatch.Rank(All, Board("X1", "Y1", "Z1", "W1", "V1", "U1"));

        Assert.Equal(expected, CompTargets.Choose(board, Array.Empty<string>(), count).Count);
    }

    [Fact]
    public void Untick_TheGuideStaysATargetWhenItIsProbable_WithItsColour_AndTheOthersKeepTheirs()
    {
        var tracker = new CompTargetTracker();
        var board = CompGuideMatch.Rank(All, Board("X1", "X2", "Y1"));
        tracker.Toggle(Y.Id);
        var ticked = tracker.Next(board, 2);
        Assert.Equal(new[] { ("Yeti", P0), ("Xeno", P1) }, NamesAndColours(ticked)); // ticked first, Xeno in progress

        Assert.True(tracker.Toggle(Y.Id));
        var unticked = tracker.Next(board, 2);

        Assert.Equal(new[] { ("Xeno", P1), ("Yeti", P0) }, NamesAndColours(unticked));
        Assert.All(unticked, t => Assert.False(t.Ticked));
    }

    [Fact]
    public void FourTickedAtMost_TheFifthIsRefused_AndTheLogSaysSo()
    {
        var tracker = new CompTargetTracker();
        foreach (var guide in new[] { X, Y, Z, W })
        {
            Assert.True(tracker.Toggle(guide.Id));
        }

        Assert.False(tracker.Toggle(V.Id));
        Assert.Equal(new[] { X.Id, Y.Id, Z.Id, W.Id }, tracker.Ticked);
        Assert.Equal("Bronzebeard HUD: Vile/0 not ticked, four guides already are", tracker.ToggleLine(V.Id, accepted: false));
        Assert.Equal("Bronzebeard HUD: ticked guides=[Xeno/0,Yeti/0,Zeal/0,Wolf/0]", tracker.ToggleLine(W.Id, accepted: true));

        var targets = tracker.Next(CompGuideMatch.Rank(All, Board("V1", "V2")), 4);
        Assert.Equal(new[] { "Xeno", "Yeti", "Zeal", "Wolf" }, targets.Select(t => t.Guide.Name)); // no room left for Vile
        Assert.Equal(CompTargetTracker.Palette, targets.Select(t => t.Colour));
    }

    [Fact]
    public void ANewGame_ForgetsTicksAndColours_TheSameGameKeepsThem()
    {
        var tracker = new CompTargetTracker();
        tracker.BeginGame(7);
        tracker.Next(CompGuideMatch.Rank(All, Board("Y1", "Y2", "X1")), 2); // Yeti first colour, Xeno second
        tracker.Toggle(U.Id);

        tracker.BeginGame(7);
        Assert.Equal(new[] { U.Id }, tracker.Ticked);
        Assert.Equal(2, tracker.Targets.Count); // the last targets, until the next Next

        tracker.BeginGame(8);
        Assert.Empty(tracker.Ticked);
        Assert.Empty(tracker.Targets);
        var fresh = tracker.Next(CompGuideMatch.Rank(All, Board("X1", "X2", "Y1")), 2);
        Assert.Equal(new[] { ("Xeno", P0), ("Yeti", P1) }, NamesAndColours(fresh)); // Xeno had P1 in game 7: not remembered
    }

    [Fact]
    public void Tiers_TheTargetsComeFirstInTheirTier_TheOthersKeepHdtsOrder()
    {
        var s1 = Guide("S one", 1, 0, new[] { "S1", "S3" });
        var s2 = Guide("S two", 1, 1, new[] { "S2", "S4" });
        var set = Set(s1, s2, X, Y, Z);
        var tracker = new CompTargetTracker();
        tracker.Toggle(Z.Id);
        var board = CompGuideMatch.Rank(set, Board("S2", "X1", "X2", "Y1"));

        var targets = tracker.Next(board, 2);
        var tiers = CompTargets.Tiers(board, targets);

        Assert.Equal(new[] { "Zeal", "Xeno" }, targets.Select(t => t.Guide.Name)); // ticked, then in progress; S two (one key card) set aside
        Assert.Equal(new[] { "S one", "S two" }, tiers[0].Rows.Select(r => r.Guide.Name)); // S two is highlighted, but no target: HDT's place
        Assert.Equal(new[] { "Zeal", "Xeno", "Yeti" }, tiers[1].Rows.Select(r => r.Guide.Name));
        Assert.Same(targets[0], CompTargets.Find(targets, Z));
        Assert.Null(CompTargets.Find(targets, Y));
        Assert.Null(CompTargets.Find(targets, s2)); // the second most probable guide of the board, a guess the tick silenced
        Assert.Equal($"[Zeal {P0}; Xeno {P1}]", CompTargets.Summary(targets));
        Assert.Equal("none", CompTargets.Summary(Array.Empty<CompTarget>()));
    }

    [Fact]
    public void Rank_HighlightsTheCountAsked_AndRanksEveryGuideThatScores()
    {
        var cards = Board("X1", "X2", "Y1", "Z1", "W1");

        var three = CompGuideMatch.Rank(All, cards);
        var four = CompGuideMatch.Rank(All, cards, count: 4);
        var none = CompGuideMatch.Rank(All, cards, count: 0);

        Assert.Equal(new[] { "Xeno", "Yeti", "Zeal" }, three.Highlighted.Select(p => p.Guide.Name));
        Assert.Equal(new[] { "Xeno", "Yeti", "Zeal", "Wolf" }, four.Highlighted.Select(p => p.Guide.Name));
        Assert.Empty(none.Highlighted);
        Assert.Equal(new[] { "Xeno", "Yeti", "Zeal", "Wolf" }, three.Ranked.Select(p => p.Guide.Name));
        Assert.Equal(new int?[] { 1, 2, 3, null }, three.Ranked.Select(p => p.Highlight));
        Assert.Equal(new[] { "Xeno", "Yeti", "Zeal", "Wolf", "Vile", "Ursa" }, three.All.Select(p => p.Guide.Name)); // HDT's order
    }

    [Fact]
    public void GuideId_IsTheNameAndTheTribe()
    {
        var undead = Guide("Same name", 2, 0, new[] { "A1" }, tribe: 11);
        var beasts = Guide("Same name", 2, 1, new[] { "B1" }, tribe: 20);

        Assert.Equal("Same name/11", undead.Id);
        Assert.NotEqual(undead.Id, beasts.Id);
        Assert.Equal(undead.Id, Guide("Same name", 3, 4, new[] { "C1" }, tribe: 11).Id); // tier and cards do not change it
    }

    [Fact]
    public void GuideTribes_HearthDbRaceValues_MapToTheLobbyNames()
    {
        // Written out from HearthDb.dll (HDT 1.58.6): the constants of HearthDb.Enums.Race.
        var expected = new Dictionary<int, string>
        {
            [11] = "UNDEAD",
            [14] = "MURLOC",
            [15] = "DEMON",
            [17] = "MECHANICAL",
            [18] = "ELEMENTAL",
            [20] = "BEAST",
            [23] = "PIRATE",
            [24] = "DRAGON",
            [43] = "QUILBOAR",
            [92] = "NAGA",
            [126] = "ABERRATION",
        };

        Assert.All(expected, kv => Assert.Equal(kv.Value, GuideTribes.NameOf(kv.Key)));
        Assert.Equal(Tribes.All.OrderBy(t => t), expected.Values.OrderBy(t => t)); // every Battlegrounds tribe, once
        Assert.Null(GuideTribes.NameOf(0));
        Assert.Null(GuideTribes.NameOf(16)); // SCOURGE: no Battlegrounds tribe
        Assert.Null(GuideTribes.NameOf(26)); // ALL

        var lobby = new[] { "UNDEAD", "BEAST" };
        Assert.True(GuideTribes.InLobby(11, lobby));
        Assert.False(GuideTribes.InLobby(17, lobby));
        Assert.True(GuideTribes.InLobby(17, Array.Empty<string>())); // lobby unknown: nothing left out
        Assert.True(GuideTribes.InLobby(0, lobby));                  // no tribe: playable anywhere
        Assert.True(GuideTribes.InLobby(999, lobby));                // unknown value: never left out on a guess
    }

    [Fact]
    public void Palette_OneColourPerTarget_ReadsOnTheDarkPanel_AndStaysAwayFromTheOrangeBorders()
    {
        static (double R, double G, double B) Rgb(string hex) => (
            int.Parse(hex.Substring(1, 2), NumberStyles.HexNumber), int.Parse(hex.Substring(3, 2), NumberStyles.HexNumber), int.Parse(hex.Substring(5, 2), NumberStyles.HexNumber));
        static double Luminance(string hex)
        {
            static double Channel(double c) => (c /= 255) <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            var (r, g, b) = Rgb(hex);
            return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
        }
        static double Distance(string a, string b)
        {
            var (r1, g1, b1) = Rgb(a);
            var (r2, g2, b2) = Rgb(b);
            return Math.Sqrt((r1 - r2) * (r1 - r2) + (g1 - g2) * (g1 - g2) + (b1 - b2) * (b1 - b2));
        }

        const string panel = "#14141E", orange = "#D9480F";
        var colours = CompTargetTracker.Palette.ToList();
        Assert.Equal(4, colours.Distinct().Count());
        Assert.Equal(HudSettings.MaxSuggested, colours.Count);   // never more targets than colours
        Assert.Equal(CompTargetTracker.MaxTicked, colours.Count);
        foreach (var colour in colours)
        {
            var contrast = (Luminance(colour) + 0.05) / (Luminance(panel) + 0.05);
            Assert.True(contrast >= 4.5, $"{colour}: contrast {contrast:0.0} on the panel");
            Assert.True(Distance(colour, orange) > 150, $"{colour} too close to the orange borders");
        }

        for (var i = 0; i < colours.Count; i++)
        {
            for (var j = i + 1; j < colours.Count; j++)
            {
                Assert.True(Distance(colours[i], colours[j]) > 150, $"{colours[i]} and {colours[j]} too close");
            }
        }
    }
}
