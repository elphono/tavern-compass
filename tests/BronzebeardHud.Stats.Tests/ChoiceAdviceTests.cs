namespace BronzebeardHud.Stats.Tests;

public class ChoiceAdviceTests
{
    private static readonly Composition Undead = new("undead_butcher", "Undead Butcher", new[] { "UNDEAD" },
        coreCards: new[] { "BG32_324", "BG25_010", "BG28_309" }, addonCards: new[] { "BG32_880" }, averagePlacement: 3.81);
    private static readonly Composition Pirate = new("pirate_discover", "Pirate Discover", new[] { "PIRATE" },
        coreCards: new[] { "BG26_817", "BG33_823" }, addonCards: new[] { "BG33_825" }, averagePlacement: 3.93);
    private static readonly Composition Mech = new("mech_magnet", "Mech Magnet", new[] { "MECHANICAL" },
        coreCards: new[] { "BG24_022", "BG25_040" }, addonCards: new[] { "BG28_300" }, averagePlacement: 4.12);

    private static readonly Composition[] All = { Undead, Pirate, Mech };
    private static readonly string[] Lobby = { "UNDEAD", "PIRATE", "MECHANICAL", "BEAST", "DEMON" };

    /// <summary>Undead is being played (two key pieces); a pirate that is no piece makes Pirate a second, weaker target.</summary>
    private static readonly OwnedCard[] UndeadWithAPirate =
    {
        new("BG32_324", "UNDEAD"), new("BG28_309", "UNDEAD"), new("BG21_005", "PIRATE"),
    };

    private const int Wide = 40;

    private static OfferedOption Minion(int id, string cardId, bool darkGift = false, int zone = ChoiceClassifier.SetAsideZone) =>
        new(id, cardId, "MINION", hasDarkGift: darkGift, zone: zone);

    private static OfferedOption Trinket(int id, string cardId, string text) => new(id, cardId, "BATTLEGROUND_TRINKET", text: text);

    private static IReadOnlyList<IReadOnlyList<string>> Labels(ChoiceAdvice advice, bool statsLoaded = true) =>
        advice.Options.Select(o => ChoiceAdvisor.Lines(o, Wide, statsLoaded)).ToList();

    [Fact]
    public void ThreeOptions_CurrentCompKeyPiece_AttainableCompKeyPiece_Neutral_ThreeDistinctLabelsWithNmBeforeAfter()
    {
        var options = new[] { Minion(501, "BG25_010"), Minion(502, "BG26_817"), Minion(503, "BG20_101") };

        var advice = ChoiceAdvisor.Advise(options, UndeadWithAPirate, All, Lobby);

        Assert.Equal(ChoiceKind.Discover, advice.Kind);
        Assert.Equal(new[] { "undead_butcher", "pirate_discover" }, advice.Targets.Select(t => t.Composition.Id));
        var current = Assert.Single(advice.Options[0].Effects);
        Assert.Equal((0, 2, 3, 3), (current.TargetRank, current.KeyBefore, current.KeyAfter, current.KeyTotal));
        var attainable = Assert.Single(advice.Options[1].Effects);
        Assert.Equal((1, 0, 1, 2), (attainable.TargetRank, attainable.KeyBefore, attainable.KeyAfter, attainable.KeyTotal));
        Assert.Empty(advice.Options[2].Effects);

        Assert.Equal(new[] { "★ Undead Butcher 2/3→3/3" }, Labels(advice)[0]);
        Assert.Equal(new[] { "★ Pirate Discover 0/2→1/2" }, Labels(advice)[1]);
        Assert.Equal(new[] { "no target comp" }, Labels(advice)[2]);
    }

    [Fact]
    public void TwoOptions_AHeldKeyPieceIsACopy_AnAddOnKeepsTheCount()
    {
        var advice = ChoiceAdvisor.Advise(new[] { Minion(601, "BG32_324_G"), Minion(602, "BG32_880") }, UndeadWithAPirate, All, Lobby);

        Assert.Equal(new[] { "★ Undead Butcher 2/3 copy" }, Labels(advice)[0]);
        Assert.Equal(new[] { "+ Undead Butcher 2/3" }, Labels(advice)[1]);
    }

    [Fact]
    public void FourOptions_DarkGift_UnreachableComp_AddOn_Copy_Neutral()
    {
        var options = new[]
        {
            Minion(701, "BG24_022", darkGift: true), Minion(702, "BG33_825", darkGift: true),
            Minion(703, "BG28_309", darkGift: true), Minion(704, "BG20_101", darkGift: true),
        };

        var advice = ChoiceAdvisor.Advise(options, UndeadWithAPirate, All, Lobby);

        Assert.Equal(ChoiceKind.DarkGift, advice.Kind);
        Assert.Empty(advice.Options[0].Effects); // Mech is playable but nothing of it is held: not shown, not aimed at
        Assert.Equal(
            new[] { "no target comp", "+ Pirate Discover 0/2", "★ Undead Butcher 2/3 copy", "no target comp" },
            Labels(advice).Select(l => Assert.Single(l)));
    }

    [Fact]
    public void ThreeSuccessiveChoices_TheLabelsFollowTheCompositionsInReach()
    {
        // Choice 1, nothing held: the pirate key piece counts for a playable composition, nothing is targeted yet.
        var first = ChoiceAdvisor.Advise(new[] { Minion(801, "BG26_817"), Minion(802, "BG33_825") }, Array.Empty<OwnedCard>(), All, Lobby);
        Assert.Empty(first.Targets);
        Assert.Equal(new[] { "★ Pirate Discover 0/2→1/2", "no target comp" }, Labels(first).Select(l => Assert.Single(l)));

        // Choice 2, one pirate key piece held: Pirate is the target, its add-on is now worth taking.
        var pirates = new[] { new OwnedCard("BG26_817", "PIRATE") };
        var second = ChoiceAdvisor.Advise(new[] { Minion(811, "BG33_823"), Minion(812, "BG33_825") }, pirates, All, Lobby);
        Assert.Equal(new[] { "★ Pirate Discover 1/2→2/2", "+ Pirate Discover 1/2" }, Labels(second).Select(l => Assert.Single(l)));

        // Choice 3, pirates sold for undead: the pirate add-on no longer counts, the undead piece does.
        var undead = new[] { new OwnedCard("BG32_324", "UNDEAD"), new OwnedCard("BG25_010", "UNDEAD") };
        var third = ChoiceAdvisor.Advise(new[] { Minion(821, "BG33_825"), Minion(822, "BG28_309") }, undead, All, Lobby);
        Assert.Equal(new[] { "no target comp", "★ Undead Butcher 2/3→3/3" }, Labels(third).Select(l => Assert.Single(l)));
    }

    [Fact]
    public void TheTavernMarkersAndTheChoiceMarkersUseTheSameEngine()
    {
        var tavern = TavernAdvisor.Advise(new[] { "BG25_010", "BG26_817", "BG20_101", "BG32_880" }, UndeadWithAPirate, All, Lobby);
        var choice = ChoiceAdvisor.Advise(new[] { Minion(1, "BG25_010"), Minion(2, "BG26_817"), Minion(3, "BG20_101"), Minion(4, "BG32_880") },
            UndeadWithAPirate, All, Lobby);

        Assert.Equal(
            tavern.Cards.Select(c => c.Advances.Select(a => (a.Composition.Id, a.IsKeyPiece)).ToList()),
            choice.Options.Select(o => o.Effects.Select(e => (e.Composition.Id, e.IsKeyPiece)).ToList()));
    }

    [Fact]
    public void Trinkets_AdjustedForTheCurrentComp_LessForAnAttainableOne_NotForTheRest_WithinTheBound()
    {
        var stats = new Dictionary<string, TrinketStat>
        {
            ["BG30_MagicItem_706"] = new("BG30_MagicItem_706", 3.70, 900, 0.41, new Dictionary<int, double> { [25] = 3.80 }),
            ["BG30_MagicItem_426"] = new("BG30_MagicItem_426", 4.00, 800, 0.22, new Dictionary<int, double> { [25] = 4.10 }),
            ["BG30_MagicItem_703"] = new("BG30_MagicItem_703", 4.30, 700, 0.31, new Dictionary<int, double> { [25] = 4.40 }),
        };
        var options = new[]
        {
            Trinket(3436, "BG30_MagicItem_706", "Your <b>Undead</b> have +3 Attack."),
            Trinket(3434, "BG30_MagicItem_426", "After you buy a Pirate, gain 1 Gold."),
            Trinket(3437, "BG30_MagicItem_703", "Your Mechs have +2 Health."),
        };

        var advice = ChoiceAdvisor.Advise(options, UndeadWithAPirate, All, Lobby, id => stats.TryGetValue(id, out var s) ? s : null, bracket: 25);

        Assert.Equal(ChoiceKind.Trinket, advice.Kind);
        var notes = advice.Options.Select(o => o.Trinket!).ToList();
        Assert.Equal(new[] { 0.3, 0.15, 0.0 }, notes.Select(n => Math.Round(n.Adjustment, 6)));
        Assert.Equal(new[] { "undead_butcher", "pirate_discover", null }, notes.Select(n => n.JustifiedBy?.Id));
        Assert.Equal(new[] { "avg 3.80 → ≈3.50", "≈ Undead Butcher" }, Labels(advice)[0]);
        Assert.Equal(new[] { "avg 4.10 → ≈3.95", "≈ Pirate Discover" }, Labels(advice)[1]);
        Assert.Equal(new[] { "avg 4.40 · 31%" }, Labels(advice)[2]); // Mech is not targeted: global note only

        // Naming both targets takes the larger weight, never the sum: the bound holds.
        var both = TrinketAffinity.Adjust("Your Undead and Pirates have +1/+1.", advice.Targets);
        Assert.Equal((TrinketAffinity.MaxAdjustment, "undead_butcher"), (both.Adjustment, both.JustifiedBy!.Id));
        Assert.Equal((0.0, (Composition?)null), TrinketAffinity.Adjust("Gain 2 Gold.", advice.Targets));
    }

    [Fact]
    public void Trinkets_WithoutStats_SayLoadingThenNoData()
    {
        var advice = ChoiceAdvisor.Advise(new[] { Trinket(1, "BG31_MagicItem_001", "Undead"), Trinket(2, "BG31_MagicItem_002", "") },
            UndeadWithAPirate, All, Lobby, _ => null);
        Assert.Equal(new[] { "loading…" }, ChoiceAdvisor.Lines(advice.Options[0], Wide, statsLoaded: false));
        Assert.Equal(new[] { "no data" }, ChoiceAdvisor.Lines(advice.Options[1], Wide, statsLoaded: true));
    }

    [Fact]
    public void Labels_ShortenToFitNarrowMarkers()
    {
        var advice = ChoiceAdvisor.Advise(new[] { Minion(501, "BG25_010"), Minion(502, "BG20_101") }, UndeadWithAPirate, All, Lobby);
        Assert.Equal(new[] { "★ U. Butcher 2/3→3/3" }, ChoiceAdvisor.Lines(advice.Options[0], 21, statsLoaded: true));
        Assert.Equal(new[] { "★ UB 2/3→3/3" }, ChoiceAdvisor.Lines(advice.Options[0], 14, statsLoaded: true));
        Assert.All(new[] { 21, 14, 11 }, width =>
            Assert.All(ChoiceAdvisor.Lines(advice.Options[0], width, true), line => Assert.True(MarkerText.DisplayLength(line) <= width, line)));
    }

    [Theory]
    [InlineData("BATTLEGROUND_TRINKET,BATTLEGROUND_TRINKET,BATTLEGROUND_TRINKET", "", "6,6,6", ChoiceKind.Trinket)]
    [InlineData("MINION,MINION,MINION", "", "6,6,6", ChoiceKind.Discover)]
    [InlineData("MINION,BATTLEGROUND_SPELL", "", "6,6", ChoiceKind.Discover)]
    [InlineData("MINION,MINION,MINION", "1", "6,6,6", ChoiceKind.DarkGift)]
    [InlineData("HERO_POWER,HERO_POWER", "", "6,6", ChoiceKind.Unsupported)]
    [InlineData("BATTLEGROUND_QUEST_REWARD,BATTLEGROUND_QUEST_REWARD,BATTLEGROUND_QUEST_REWARD", "", "6,6,6", ChoiceKind.Unsupported)]
    [InlineData("MINION,MINION,MINION", "", "6,3,6", ChoiceKind.None)] // one option picked into the hand: over
    [InlineData("BATTLEGROUND_TRINKET,BATTLEGROUND_TRINKET", "", "1,6", ChoiceKind.None)] // one trinket in play: over
    [InlineData("MINION", "", "6", ChoiceKind.None)] // not a choice
    public void Classifier_FollowsHdtsLayoutRule(string types, string darkGiftAt, string zones, ChoiceKind expected)
    {
        var typeList = types.Split(',');
        var zoneList = zones.Split(',').Select(int.Parse).ToList();
        var options = typeList.Select((t, i) => new OfferedOption(900 + i, $"CARD_{i}", t, hasDarkGift: darkGiftAt == (i + 1).ToString(), zone: zoneList[i])).ToList();
        Assert.Equal(expected, ChoiceClassifier.Kind(options));
    }

    [Fact]
    public void DiagnosticLine_HasTheExactFormat()
    {
        var options = new[] { Minion(1285, "BG29_300"), Minion(1283, "BG25_010"), Minion(1284, "BG20_101") };
        var advice = ChoiceAdvisor.Advise(options, UndeadWithAPirate, All, Lobby);
        var first = ChoiceLayout.Labels(ChoiceKind.Discover, 3, 2291, 1360, 1)[0];

        var line = ChoiceAdvisor.DiagnosticLine(options, advice, 24, "ok", Labels(advice), first, 2291, 1360);

        Assert.Equal("Bronzebeard HUD: choice kind=discover options=3 order=[1285,1283,1284] cards=[BG29_300,BG25_010,BG20_101] " +
                     "comps=24 (ok) targets=[Undead Butcher 7; Pirate Discover 0.5] " +
                     "advice=[#0 no target comp; #1 ★ Undead Butcher 2/3→3/3; #2 no target comp] " +
                     "first=x=466 y=355 w=381 h=31 canvas=2291x1360", line);

        var powers = new[] { new OfferedOption(40, "BG28_HERO_p1", "HERO_POWER"), new OfferedOption(41, "BG28_HERO_p2", "HERO_POWER") };
        Assert.Equal("Bronzebeard HUD: choice kind=unsupported options=2 order=[40,41] cards=[BG28_HERO_p1,BG28_HERO_p2] types=[HERO_POWER,HERO_POWER]",
            ChoiceAdvisor.DiagnosticLine(powers, ChoiceAdvisor.Advise(powers, UndeadWithAPirate, All, Lobby), 24, "ok",
                Array.Empty<IReadOnlyList<string>>(), null, 2291, 1360));
    }
}
