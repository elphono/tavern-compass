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

        // 4: Ursa, ticked though nothing of it is held, comes first; Wolf, now fourth, gives its colour up to it.
        Assert.True(tracker.Toggle(U.Id));
        var fourth = tracker.Next(CompGuideMatch.Rank(All, Board("V1", "V2", "Z1", "W1")), 3);
        Assert.Equal(new[] { ("Ursa", P0), ("Vile", P1), ("Zeal", P2) }, NamesAndColours(fourth));
        Assert.Equal(new[] { true, false, false }, fourth.Select(t => t.Ticked));
        Assert.Same(fourth, tracker.Targets);
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
    public void TickedGuidesComeFirst_InTheOrderTheyWereTicked_ThenTheMostProbable()
    {
        var board = CompGuideMatch.Rank(All, Board("X1", "X2", "Y1", "Z1"));

        var chosen = CompTargets.Choose(board, new[] { U.Id, W.Id }, 3);

        Assert.Equal(new[] { ("Ursa", true), ("Wolf", true), ("Xeno", false) }, chosen.Select(c => (c.Progress.Guide.Name, c.Ticked)));
        Assert.Equal(0.0, chosen[0].Progress.Score); // ticked: a target whatever its score
    }

    [Fact]
    public void ATickedGuideThatIsAlsoProbable_IsOneTarget_NotTwo()
    {
        var board = CompGuideMatch.Rank(All, Board("X1", "X2", "Y1", "Z1"));

        var chosen = CompTargets.Choose(board, new[] { Y.Id }, 3);

        Assert.Equal(new[] { "Yeti", "Xeno", "Zeal" }, chosen.Select(c => c.Progress.Guide.Name));
    }

    [Fact]
    public void TickedGuides_AreTargetsBeyondTheCount_UnknownIdsAreSkipped_AutomaticOnesNeedAScore()
    {
        var board = CompGuideMatch.Rank(All, Board("X1"));

        var four = CompTargets.Choose(board, new[] { U.Id, V.Id, W.Id, Z.Id }, 2);
        var unknown = CompTargets.Choose(board, new[] { "Gone/0", U.Id }, 3);

        Assert.Equal(new[] { "Ursa", "Vile", "Wolf", "Zeal" }, four.Select(c => c.Progress.Guide.Name));
        Assert.Equal(new[] { "Ursa", "Xeno" }, unknown.Select(c => c.Progress.Guide.Name)); // only Xeno scores
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
    public void Untick_TheGuideStaysATargetWhenItIsProbable_WithItsColour()
    {
        var tracker = new CompTargetTracker();
        var board = CompGuideMatch.Rank(All, Board("X1", "X2", "Y1"));
        tracker.Toggle(Y.Id);
        var ticked = tracker.Next(board, 2);
        Assert.Equal(new[] { ("Yeti", P0), ("Xeno", P1) }, NamesAndColours(ticked));

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
        tracker.Toggle(U.Id);
        tracker.Next(CompGuideMatch.Rank(All, Board("X1", "X2")), 2);

        tracker.BeginGame(7);
        Assert.Equal(new[] { U.Id }, tracker.Ticked);
        Assert.Equal(2, tracker.Targets.Count);

        tracker.BeginGame(8);
        Assert.Empty(tracker.Ticked);
        Assert.Empty(tracker.Targets);
        var fresh = tracker.Next(CompGuideMatch.Rank(All, Board("X1", "X2", "Y1")), 2);
        Assert.Equal(new[] { ("Xeno", P0), ("Yeti", P1) }, NamesAndColours(fresh)); // Xeno had P1 in game 7: not remembered
    }

    [Fact]
    public void Tiers_TheTargetsComeFirstInTheirTier_TheOthersKeepHdtsOrder()
    {
        var s1 = Guide("S one", 1, 0, new[] { "S1" });
        var s2 = Guide("S two", 1, 1, new[] { "S2" });
        var set = Set(s1, s2, X, Y, Z);
        var tracker = new CompTargetTracker();
        tracker.Toggle(Z.Id);
        var board = CompGuideMatch.Rank(set, Board("S2", "X1", "X2", "Y1"));

        var targets = tracker.Next(board, 2);
        var tiers = CompTargets.Tiers(board, targets);

        Assert.Equal(new[] { "Zeal", "Xeno" }, targets.Select(t => t.Guide.Name));
        Assert.Equal(new[] { "S one", "S two" }, tiers[0].Rows.Select(r => r.Guide.Name)); // S two is highlighted, but no target: HDT's place
        Assert.Equal(new[] { "Zeal", "Xeno", "Yeti" }, tiers[1].Rows.Select(r => r.Guide.Name));
        Assert.Same(targets[0], CompTargets.Find(targets, Z));
        Assert.Null(CompTargets.Find(targets, Y));
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
            [11] = "UNDEAD", [14] = "MURLOC", [15] = "DEMON", [17] = "MECHANICAL", [18] = "ELEMENTAL", [20] = "BEAST",
            [23] = "PIRATE", [24] = "DRAGON", [43] = "QUILBOAR", [92] = "NAGA", [126] = "ABERRATION",
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
