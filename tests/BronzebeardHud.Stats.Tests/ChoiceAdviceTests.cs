using static BronzebeardHud.Stats.Tests.GuideTestData;

namespace BronzebeardHud.Stats.Tests;

public class ChoiceAdviceTests
{
    // Synthetic guides (HearthDb Race values as tribes): two Mech guides share the core card M1, in tiers S and B; the
    // Beast guide's tribe is not in the lobby.
    private static readonly CompGuide MechShield = Guide("Mech Shield", 1, 0, new[] { "M1", "MS2" }, tribe: 17);
    private static readonly CompGuide BeastLobster = Guide("Beast Lobster", 1, 1, new[] { "B1", "B2" }, tribe: 20);
    private static readonly CompGuide Undead = Guide("Undead Butcher", 2, 0, new[] { "U1", "U2", "U3" }, addons: new[] { "UA" }, enablers: new[] { "UE" }, tribe: 11);
    private static readonly CompGuide Pirates = Guide("Pirate Discover", 2, 1, new[] { "P1", "P2" }, addons: new[] { "PA" }, tribe: 23);
    private static readonly CompGuide MechMagnet = Guide("Mech Magnet", 3, 0, new[] { "M1", "M2" }, addons: new[] { "MA" }, tribe: 17);
    private static readonly CompGuideSet All = Set(MechShield, BeastLobster, Undead, Pirates, MechMagnet);
    private static readonly string[] Lobby = { "UNDEAD", "PIRATE", "MECHANICAL", "DEMON", "DRAGON" };

    /// <summary>Undead is being played (two core cards); a Pirate add-on makes Pirates the second, weaker target.</summary>
    private static readonly OwnedCard[] UndeadWithAPirateAddOn = Owned("U1", "U3", "PA");

    private const int Wide = 40;

    private static IReadOnlyList<CompTarget> TargetsFor(OwnedCard[] owned, int count = 2) =>
        Targets(All, new PlayerCards(owned, Array.Empty<OwnedCard>()), count);

    private static ChoiceAdvice Advise(OfferedOption[] options, OwnedCard[] owned, IReadOnlyCollection<string>? lobby = null) =>
        ChoiceAdvisor.Advise(options, owned, TargetsFor(owned), All, lobby ?? Lobby);

    private static OfferedOption Minion(int id, string cardId, bool darkGift = false, int zone = ChoiceClassifier.SetAsideZone) =>
        new(id, cardId, "MINION", hasDarkGift: darkGift, zone: zone);

    private static OfferedOption Trinket(int id, string cardId, string text) => new(id, cardId, "BATTLEGROUND_TRINKET", text: text);

    private static IReadOnlyList<IReadOnlyList<string>> Labels(ChoiceAdvice advice, bool statsLoaded = true) =>
        advice.Options.Select(o => ChoiceAdvisor.Lines(o, Wide, statsLoaded)).ToList();

    [Fact]
    public void ThreeOptions_CoreOfTheFirstTarget_CoreOfTheSecond_Neutral_InTheirColours()
    {
        var advice = Advise(new[] { Minion(501, "U2"), Minion(502, "P1"), Minion(503, "NEUTRAL") }, UndeadWithAPirateAddOn);

        Assert.Equal(ChoiceKind.Discover, advice.Kind);
        Assert.Equal(new[] { "Undead Butcher", "Pirate Discover" }, advice.Targets.Select(t => t.Guide.Name));
        var first = Assert.Single(advice.Options[0].Effects);
        Assert.Equal((GuideCardRole.Core, 2, 3, 3), (first.Role, first.CoreBefore, first.CoreAfter, first.CoreTotal));
        var second = Assert.Single(advice.Options[1].Effects);
        Assert.Equal((GuideCardRole.Core, 0, 1, 2), (second.Role, second.CoreBefore, second.CoreAfter, second.CoreTotal));

        Assert.Equal(new[] { "★ core Undead Butcher 2/3→3/3" }, Labels(advice)[0]);
        Assert.Equal(new[] { "★ core Pirate Discover 0/2→1/2" }, Labels(advice)[1]);
        Assert.Equal(new[] { "—" }, Labels(advice)[2]); // never "no target comp"
        Assert.Equal(new[] { CompTargetTracker.Palette[0], CompTargetTracker.Palette[1], null }, advice.Options.Select(o => o.Colour));
        Assert.Equal(new[] { ChoiceReason.Target, ChoiceReason.Target, ChoiceReason.None }, advice.Options.Select(o => o.Reason));
    }

    [Fact]
    public void AHeldCoreCardIsACopy_AnAddOnOrAnEnablerSaysPlus_WithoutCount()
    {
        var advice = Advise(new[] { Minion(601, "U1_G"), Minion(602, "UA"), Minion(603, "UE") }, UndeadWithAPirateAddOn);

        Assert.Equal(new[] { "★ core Undead Butcher 2/3 copy", "+ Undead Butcher", "+ Undead Butcher" }, Labels(advice).Select(l => Assert.Single(l)));
        Assert.Equal(new[] { GuideCardRole.Core, GuideCardRole.Addon, GuideCardRole.Enabler }, advice.Options.Select(o => o.Effects.Single().Role));
    }

    [Fact]
    public void NoTarget_TheCoreCardOfAnotherGuide_BestTierFirst_OnlyInTheLobby()
    {
        // M1 is a core card of Mech Shield (S) and of Mech Magnet (B), neither a target; B1 is Beast Lobster's, not in the lobby.
        var advice = Advise(new[] { Minion(701, "M1"), Minion(702, "B1"), Minion(703, "MA") }, UndeadWithAPirateAddOn);

        Assert.Equal(new[] { "core Mech Shield (S)", "core Mech Magnet (B)" }, Labels(advice)[0]);
        Assert.Equal(new[] { "—" }, Labels(advice)[1]);
        Assert.Equal(new[] { "—" }, Labels(advice)[2]); // an add-on of a guide that is no target says nothing
        Assert.Equal(new[] { ChoiceReason.Guide, ChoiceReason.None, ChoiceReason.None }, advice.Options.Select(o => o.Reason));
        Assert.All(advice.Options, o => Assert.Null(o.Colour)); // neutral

        var lobbyUnknown = Advise(new[] { Minion(711, "B1"), Minion(712, "M1") }, UndeadWithAPirateAddOn, Array.Empty<string>());
        Assert.Equal(new[] { "core Beast Lobster (S)" }, Labels(lobbyUnknown)[0]);
    }

    [Fact]
    public void ACardOfATarget_IsNeverAlsoGivenAsAnotherGuide()
    {
        var mechs = Owned("M1", "M2");
        var advice = ChoiceAdvisor.Advise(new[] { Minion(1, "M1"), Minion(2, "MS2") }, mechs, TargetsFor(mechs, count: 1), All, Lobby);

        Assert.Equal(new[] { "Mech Magnet" }, advice.Targets.Select(t => t.Guide.Name));
        Assert.Equal(new[] { "★ core Mech Magnet 2/2 copy" }, Labels(advice)[0]); // Mech Shield is not listed after it
        Assert.Empty(advice.Options[0].Guides);
        Assert.Equal(new[] { "core Mech Shield (S)" }, Labels(advice)[1]);
    }

    [Fact]
    public void DarkGift_UsesTheSameLabels()
    {
        var options = new[] { Minion(801, "M2", darkGift: true), Minion(802, "PA", darkGift: true), Minion(803, "U3", darkGift: true), Minion(804, "NEUTRAL", darkGift: true) };

        var advice = Advise(options, UndeadWithAPirateAddOn);

        Assert.Equal(ChoiceKind.DarkGift, advice.Kind);
        Assert.Equal(new[] { "core Mech Magnet (B)", "+ Pirate Discover", "★ core Undead Butcher 2/3 copy", "—" }, Labels(advice).Select(l => Assert.Single(l)));
    }

    [Fact]
    public void ThreeSuccessiveChoices_TheLabelsFollowTheTargets()
    {
        // Choice 1, nothing held: no target yet; the Pirate core card still says which guide it starts.
        var first = Advise(new[] { Minion(901, "P1"), Minion(902, "PA") }, Array.Empty<OwnedCard>());
        Assert.Empty(first.Targets);
        Assert.Equal(new[] { "core Pirate Discover (A)", "—" }, Labels(first).Select(l => Assert.Single(l)));

        // Choice 2, one Pirate core card held: Pirates is the target, its add-on is now worth taking.
        var second = Advise(new[] { Minion(911, "P2"), Minion(912, "PA") }, Owned("P1"));
        Assert.Equal(new[] { "★ core Pirate Discover 1/2→2/2", "+ Pirate Discover" }, Labels(second).Select(l => Assert.Single(l)));

        // Choice 3, pirates sold for undead: the Pirate add-on says nothing any more, the undead core card does.
        var third = Advise(new[] { Minion(921, "PA"), Minion(922, "U3") }, Owned("U1", "U2"));
        Assert.Equal(new[] { "—", "★ core Undead Butcher 2/3→3/3" }, Labels(third).Select(l => Assert.Single(l)));
    }

    [Fact]
    public void ACardOfSeveralTargets_TheFirstThenTheCount_CoreBeforeTheRest()
    {
        // SHARED: an add-on of the first target, a core card of the second and of the third.
        var a = Guide("Alpha", 1, 0, new[] { "A1" }, addons: new[] { "SHARED" });
        var b = Guide("Bravo", 1, 1, new[] { "B1", "SHARED" });
        var c = Guide("Charlie", 1, 2, new[] { "C1", "SHARED" });
        var set = Set(a, b, c);
        var owned = Owned("A1", "B1", "C1");
        var targets = Targets(set, new PlayerCards(owned, Array.Empty<OwnedCard>()), 3);

        var advice = ChoiceAdvisor.Advise(new[] { Minion(1, "SHARED"), Minion(2, "A1") }, owned, targets, set, Lobby);

        Assert.Equal(new[] { "Alpha", "Bravo", "Charlie" }, targets.Select(t => t.Guide.Name));
        Assert.Equal(new[] { "Bravo", "Charlie", "Alpha" }, advice.Options[0].Effects.Select(e => e.Target.Guide.Name));
        Assert.Equal(new[] { "★ core Bravo 1/2→2/2", "+2 more" }, ChoiceAdvisor.Lines(advice.Options[0], Wide, statsLoaded: true));
        Assert.Equal(new[] { "★ core Bravo 1/2→2/2", "★ core Charlie 1/2→2/2", "+ Alpha" }, ChoiceAdvisor.Lines(advice.Options[0], Wide, true, maxLines: 3));
        Assert.Equal(targets[1].Colour, advice.Options[0].Colour); // the first label's colour: Bravo's
    }

    [Fact]
    public void TheTavernFramesAndTheChoiceLabelsUseTheSameEngine()
    {
        var cards = new[] { "U2", "P1", "NEUTRAL", "UA", "UE", "PA", "M1" };
        var targets = TargetsFor(UndeadWithAPirateAddOn);

        var tavern = TavernHighlights.For(cards, targets);
        var choice = ChoiceAdvisor.Advise(cards.Select((c, i) => Minion(i, c)).ToArray(), UndeadWithAPirateAddOn, targets, All, Lobby);

        Assert.Equal(
            tavern.Select(h => h.Effect == null
                ? new List<(string, GuideCardRole, int, int)>()
                : new[] { h.Effect }.Concat(h.Others).Select(e => (e.Target.Guide.Name, e.Role, e.CoreBefore, e.CoreAfter)).ToList()),
            choice.Options.Select(o => o.Effects.Select(e => (e.Target.Guide.Name, e.Role, e.CoreBefore, e.CoreAfter)).ToList()));
        Assert.Equal(5, tavern.Count(h => h.Kind != HighlightKind.None)); // the comparison is not between empty lists
    }

    [Fact]
    public void Trinkets_AdjustedForTheFirstTarget_LessForTheSecond_NotForTheRest_WithinTheBound()
    {
        var stats = new Dictionary<string, TrinketStat>
        {
            ["TRINKET_U"] = new("TRINKET_U", 3.70, 900, 0.41, new Dictionary<int, double> { [25] = 3.80 }),
            ["TRINKET_P"] = new("TRINKET_P", 4.00, 800, 0.22, new Dictionary<int, double> { [25] = 4.10 }),
            ["TRINKET_M"] = new("TRINKET_M", 4.30, 700, 0.31, new Dictionary<int, double> { [25] = 4.40 }),
        };
        var options = new[]
        {
            Trinket(3436, "TRINKET_U", "Your <b>Undead</b> have +3 Attack."),
            Trinket(3434, "TRINKET_P", "After you buy a Pirate, gain 1 Gold."),
            Trinket(3437, "TRINKET_M", "Your Mechs have +2 Health."),
        };
        var targets = TargetsFor(UndeadWithAPirateAddOn);

        var advice = ChoiceAdvisor.Advise(options, UndeadWithAPirateAddOn, targets, All, Lobby, id => stats.TryGetValue(id, out var s) ? s : null, bracket: 25);

        Assert.Equal(ChoiceKind.Trinket, advice.Kind);
        var notes = advice.Options.Select(o => o.Trinket!).ToList();
        Assert.Equal(new[] { 0.3, 0.15, 0.0 }, notes.Select(n => Math.Round(n.Adjustment, 6)));
        Assert.Equal(new[] { "Undead Butcher", "Pirate Discover", null }, notes.Select(n => n.JustifiedBy?.Guide.Name));
        Assert.Equal(new[] { "avg 3.80 → ≈3.50", "≈ Undead Butcher" }, Labels(advice)[0]);
        Assert.Equal(new[] { "avg 4.10 → ≈3.95", "≈ Pirate Discover" }, Labels(advice)[1]);
        Assert.Equal(new[] { "avg 4.40 · 31%" }, Labels(advice)[2]); // Mechs is no target: the global note only
        Assert.All(advice.Options, o => Assert.Equal(ChoiceReason.Trinket, o.Reason));

        // Naming both targets takes the larger weight, never the sum: the bound holds.
        var both = TrinketAffinity.Adjust("Your Undead and Pirates have +1/+1.", targets);
        Assert.Equal((TrinketAffinity.MaxAdjustment, "Undead Butcher"), (both.Adjustment, both.JustifiedBy!.Guide.Name));
        Assert.Equal((0.0, (CompTarget?)null), TrinketAffinity.Adjust("Gain 2 Gold.", targets));
        Assert.Equal(0.0, TrinketAffinity.Affinity("Your Undead have +1 Attack.", primaryTribe: 0)); // a guide without tribe matches no text
        Assert.Equal(1.0, TrinketAffinity.Affinity("Your Undead have +1 Attack.", primaryTribe: 11));
    }

    [Fact]
    public void Trinkets_WithoutStats_SayLoadingThenNoData()
    {
        var advice = ChoiceAdvisor.Advise(new[] { Trinket(1, "TRINKET_1", "Undead"), Trinket(2, "TRINKET_2", "") },
            UndeadWithAPirateAddOn, TargetsFor(UndeadWithAPirateAddOn), All, Lobby, _ => null);
        Assert.Equal(new[] { "loading…" }, ChoiceAdvisor.Lines(advice.Options[0], Wide, statsLoaded: false));
        Assert.Equal(new[] { "no data" }, ChoiceAdvisor.Lines(advice.Options[1], Wide, statsLoaded: true));
    }

    [Fact]
    public void Labels_ShortenToFitNarrowMarkers()
    {
        var advice = Advise(new[] { Minion(501, "U2"), Minion(502, "M1") }, UndeadWithAPirateAddOn);

        Assert.Equal(new[] { "★ core U. Butcher 2/3→3/3" }, ChoiceAdvisor.Lines(advice.Options[0], 26, statsLoaded: true));
        Assert.Equal(new[] { "★ core UB 2/3→3/3" }, ChoiceAdvisor.Lines(advice.Options[0], 18, statsLoaded: true));
        Assert.All(new[] { 26, 18, 15, 11 }, width =>
            Assert.All(advice.Options.SelectMany(o => ChoiceAdvisor.Lines(o, width, true)), line => Assert.True(MarkerText.DisplayLength(line) <= width, line)));
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
        var options = new[] { Minion(1285, "NEUTRAL"), Minion(1283, "U2"), Minion(1284, "M1") };
        var advice = Advise(options, UndeadWithAPirateAddOn);
        var first = ChoiceLayout.Labels(ChoiceKind.Discover, 3, 2291, 1360, 1)[0];

        var line = ChoiceAdvisor.DiagnosticLine(options, advice, All.Count, "hdt-free", Labels(advice), first, 2291, 1360);

        Assert.Equal("Bronzebeard HUD: choice kind=discover options=3 order=[1285,1283,1284] cards=[NEUTRAL,U2,M1] " +
                     "guides=5 (hdt-free) targets=[Undead Butcher #FF2BD6; Pirate Discover #B8FF1F] " +
                     "advice=[#0 none: —; #1 target: ★ core Undead Butcher 2/3→3/3; #2 guide: core Mech Shield (S) / core Mech Magnet (B)] " +
                     "first=x=466 y=355 w=381 h=31 canvas=2291x1360", line);

        var powers = new[] { new OfferedOption(40, "BG28_HERO_p1", "HERO_POWER"), new OfferedOption(41, "BG28_HERO_p2", "HERO_POWER") };
        Assert.Equal("Bronzebeard HUD: choice kind=unsupported options=2 order=[40,41] cards=[BG28_HERO_p1,BG28_HERO_p2] types=[HERO_POWER,HERO_POWER]",
            ChoiceAdvisor.DiagnosticLine(powers, Advise(powers, UndeadWithAPirateAddOn), All.Count, "hdt-free",
                Array.Empty<IReadOnlyList<string>>(), null, 2291, 1360));
    }
}
