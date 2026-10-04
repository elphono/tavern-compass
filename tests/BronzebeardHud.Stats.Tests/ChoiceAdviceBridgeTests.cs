using static BronzebeardHud.Stats.Tests.BridgeFixture;

namespace BronzebeardHud.Stats.Tests;

public class ChoiceAdviceBridgeTests
{
    private const int Wide = 48;

    private static OfferedOption Minion(int id, string cardId) => new(id, cardId, "MINION");

    private static ChoiceAdvice Advise(IReadOnlyDictionary<string, GuideEvidence>? bridge, params string[] cards) =>
        ChoiceAdvisor.Advise(cards.Select((c, i) => Minion(100 + i, c)).ToArray(), Held, Targets(), All, Lobby, bridge: bridge);

    private static IReadOnlyList<IReadOnlyList<string>> Labels(ChoiceAdvice advice, int width = Wide) =>
        advice.Options.Select(o => ChoiceAdvisor.Lines(o, width, statsLoaded: true)).ToList();

    [Fact]
    public void TheFixtureBridgesAsCountedByHand()
    {
        var bridge = Bridge();

        Assert.Equal("undead_fs", bridge[Undead.Id].Composition.Id);
        Assert.Equal("pirate_fs", bridge[Pirates.Id].Composition.Id);
        Assert.Equal("undead_fs", bridge[Reborn.Id].Composition.Id);
        Assert.False(bridge.ContainsKey(MechShield.Id));
        Assert.Equal(new[] { "Undead Butcher", "Pirate Discover" }, Targets().Select(t => t.Guide.Name));
    }

    [Fact]
    public void ACoreCardOrAnAddOnOfATarget_GainsTheBoardsOfItsComp_WithoutABridgeTheTextIsUnchanged()
    {
        var with = Advise(Bridge(), "U2", "UA", "NEUTRAL");
        var without = Advise(null, "U2", "UA", "NEUTRAL");

        Assert.Equal(new[] { "★ core Undead Butcher 2/3→3/3 · 4/5 boards", "+ Undead Butcher · 1/5 boards", "—" }, Labels(with).Select(l => Assert.Single(l)));
        Assert.Equal(new[] { "★ core Undead Butcher 2/3→3/3", "+ Undead Butcher", "—" }, Labels(without).Select(l => Assert.Single(l)));
        Assert.Equal(new[] { ChoiceReason.Target, ChoiceReason.Target, ChoiceReason.None }, with.Options.Select(o => o.Reason));
        Assert.Equal(new[] { Targets()[0].Colour, Targets()[0].Colour, null }, with.Options.Select(o => o.Colour));
    }

    [Fact]
    public void TheBoardsSuffix_ShortensTheNameBeforeItIsDropped_NeverTheName_AndEveryLineFits()
    {
        var advice = Advise(Bridge(), "U2", "TOP", "R1", "M1", "PA");

        Assert.Equal(new[] { "★ core U. Butcher 2/3→3/3 · 4/5 boards" }, ChoiceAdvisor.Lines(advice.Options[0], 40, true));
        Assert.Equal(new[] { "★ core UB 2/3→3/3 · 4/5 boards" }, ChoiceAdvisor.Lines(advice.Options[0], 31, true));
        Assert.Equal(new[] { "★ core U. Butcher 2/3→3/3" }, ChoiceAdvisor.Lines(advice.Options[0], 26, true)); // no room: the old label
        Assert.All(new[] { 48, 40, 31, 26, 18, 15, 11 }, width =>
            Assert.All(advice.Options.SelectMany(o => ChoiceAdvisor.Lines(o, width, true)), line => Assert.True(MarkerText.DisplayLength(line) <= width, $"{width}: {line}")));
    }

    [Fact]
    public void ACardNoGuideOfATargetLists_InTwoOrMoreOfItsCompsBoards_SaysSo_InThatTargetsColour()
    {
        var advice = Advise(Bridge(), "TOP", "TWO", "ONCE");

        Assert.Equal(new[] { "+ Undead Butcher 3/5 boards", "+ Pirate Discover 2/5 boards" }, Labels(advice)[0]); // both targets, in their order
        Assert.Equal(new[] { "+ Undead Butcher 2/5 boards" }, Labels(advice)[1]);  // 2 of 5: enough
        Assert.Equal(new[] { "—" }, Labels(advice)[2]);                            // 1 of 5: not enough
        Assert.Equal(new[] { ChoiceReason.TopBoards, ChoiceReason.TopBoards, ChoiceReason.None }, advice.Options.Select(o => o.Reason));
        Assert.Equal(new[] { Targets()[0].Colour, Targets()[0].Colour, null }, advice.Options.Select(o => o.Colour));

        // Without a bridge, the same cards say nothing, as before.
        Assert.All(Labels(Advise(null, "TOP", "TWO", "ONCE")), l => Assert.Equal(new[] { "—" }, l));
    }

    [Fact]
    public void ARoleInTheGuideOfATarget_ComesBeforeTheTopBoardsOfAnother()
    {
        // PA: an add-on of Pirate Discover (second target), and on 3 of the 5 boards of Undead Butcher's comp (first target).
        var advice = Advise(Bridge(), "PA", "P1");

        Assert.Equal(new[] { "+ Pirate Discover · 2/5 boards" }, Labels(advice)[0]);
        Assert.Equal(new[] { "★ core Pirate Discover 0/2→1/2 · 4/5 boards" }, Labels(advice)[1]);
        Assert.Equal(new[] { ChoiceReason.Target, ChoiceReason.Target }, advice.Options.Select(o => o.Reason));
        Assert.Equal(new[] { Targets()[1].Colour, Targets()[1].Colour }, advice.Options.Select(o => o.Colour));
    }

    [Fact]
    public void Pivot_ACoreCardOfAGuideATargetCanTurnTo_Neutral_AfterTopBoards_BeforeOtherGuides()
    {
        var advice = Advise(Bridge(), "R1", "RTOP", "M1");

        // R1: a core card of Undead Reborn (Undead Butcher's pivot) and of Mech Shield: the pivot alone.
        Assert.Equal(new[] { "pivot → Undead Reborn (S)" }, Labels(advice)[0]);
        // RTOP: a core card of Undead Reborn too, but on 2 of the 5 boards of Undead Butcher's comp: the boards first.
        Assert.Equal(new[] { "+ Undead Butcher 2/5 boards" }, Labels(advice)[1]);
        // M1: no target, no pivot: the fallback, unchanged.
        Assert.Equal(new[] { "core Mech Shield (S)" }, Labels(advice)[2]);
        Assert.Equal(new[] { ChoiceReason.Pivot, ChoiceReason.TopBoards, ChoiceReason.Guide }, advice.Options.Select(o => o.Reason));
        Assert.Equal(new[] { null, Targets()[0].Colour, null }, advice.Options.Select(o => o.Colour));

        // The pivot needs no Firestone data: with an empty bridge, RTOP is the pivot's.
        Assert.Equal(new[] { "pivot → Undead Reborn (S)", "pivot → Undead Reborn (S)", "core Mech Shield (S)" },
            Labels(Advise(new Dictionary<string, GuideEvidence>(), "R1", "RTOP", "M1")).Select(l => Assert.Single(l)));

        // Without a bridge (null: a caller that does not pass one), the labels from before, word for word: no pivot.
        var before = Advise(null, "R1", "RTOP", "M1");
        Assert.Equal(new[] { new[] { "core Undead Reborn (S)", "core Mech Shield (S)" }, new[] { "core Undead Reborn (S)" }, new[] { "core Mech Shield (S)" } },
            Labels(before));
        Assert.All(before.Options, o => Assert.Equal(ChoiceReason.Guide, o.Reason));
    }

    [Fact]
    public void APivotToAGuideTheLobbyCannotPlay_OrToATarget_IsNoPivot()
    {
        // Undead Reborn is Undead Butcher's pivot, but a lobby without undead cannot play it: R1 says Mech Shield's fallback.
        var noUndead = new[] { "PIRATE", "MECHANICAL", "DEMON", "DRAGON", "BEAST" };
        var advice = ChoiceAdvisor.Advise(new[] { Minion(1, "R1"), Minion(2, "NEUTRAL") }, Held, Targets(), All, noUndead, bridge: Bridge());
        Assert.Equal(new[] { "core Mech Shield (S)" }, Labels(advice)[0]);
        Assert.Empty(advice.Options[0].Pivots);

        // Undead Reborn ticked as a target too: R1 is its core card, a target's role, never a pivot to it.
        var targets = GuideTestData.Targets(All, new PlayerCards(Held, Array.Empty<OwnedCard>()), 2, Undead, Reborn);
        var asTarget = ChoiceAdvisor.Advise(new[] { Minion(1, "R1"), Minion(2, "NEUTRAL") }, Held, targets, All, Lobby, bridge: Bridge());
        Assert.Equal(ChoiceReason.Target, asTarget.Options[0].Reason);
        Assert.Empty(asTarget.Options[0].Pivots);
    }

    [Fact]
    public void TopBoards_OnlyTheTargetsWhoseGuideDoesNotListTheCard()
    {
        // PA: Pirate Discover lists it (an add-on), Undead Butcher does not: only Undead Butcher's boards are "top boards".
        var pa = Advise(Bridge(), "PA", "NEUTRAL").Options[0];
        Assert.Equal(new[] { ("Undead Butcher", "3/5"), ("Pirate Discover", "2/5") }, pa.Boards.Select(b => (b.Target.Guide.Name, b.Card.Count!)));
        Assert.Equal(new[] { "Undead Butcher" }, pa.TopBoards.Select(b => b.Target.Guide.Name));

        // U2: a core card of Undead Butcher, on 4 of its 5 boards: no top boards at all.
        Assert.Empty(Advise(Bridge(), "U2", "NEUTRAL").Options[0].TopBoards);
    }

    [Fact]
    public void ATargetWithoutABridge_OrWhoseCompHasNoBoards_KeepsItsOldLabel()
    {
        // Only Undead Butcher's comp is known, and without final boards: nothing to count on.
        var listsOnly = BridgeTestData.Comp("undead_lists", new[] { "U1", "U2", "U3" }, addons: new[] { "UA" });
        var bridge = GuideBridge.For(All, new[] { listsOnly });
        Assert.Equal("undead_lists", bridge[Undead.Id].Composition.Id);
        Assert.False(bridge.ContainsKey(Pirates.Id));

        var advice = Advise(bridge, "U2", "P1", "TOP");

        Assert.Equal(new[] { "★ core Undead Butcher 2/3→3/3", "★ core Pirate Discover 0/2→1/2", "—" }, Labels(advice).Select(l => Assert.Single(l)));
    }

    [Fact]
    public void DiagnosticLine_SaysTheReasonAndTheEvidence()
    {
        var options = new[] { Minion(1, "U2"), Minion(2, "TOP"), Minion(3, "R1"), Minion(4, "M1"), Minion(5, "NEUTRAL") };
        var advice = ChoiceAdvisor.Advise(options, Held, Targets(), All, Lobby, bridge: Bridge());
        var lines = Labels(advice);

        var line = ChoiceAdvisor.DiagnosticLine(options, advice, All.Count, "hdt-free", lines, null, 2291, 1360);

        Assert.Equal("Bronzebeard HUD: choice kind=discover options=5 order=[1,2,3,4,5] cards=[U2,TOP,R1,M1,NEUTRAL] " +
                     "guides=4 (hdt-free) targets=[Undead Butcher #FF2BD6; Pirate Discover #B8FF1F] advice=[" +
                     "#0 target: ★ core Undead Butcher 2/3→3/3 · 4/5 boards (Undead Butcher → undead_fs 4/5 pos 1); " +
                     "#1 topboards: + Undead Butcher 3/5 boards / + Pirate Discover 2/5 boards (Undead Butcher → undead_fs 3/5 pos 2, Pirate Discover → pirate_fs 2/5 pos 3); " +
                     "#2 pivot: pivot → Undead Reborn (S) (Undead Butcher → Undead Reborn 2 shared); " +
                     "#3 guide: core Mech Shield (S); " +
                     "#4 none: —] first=none canvas=2291x1360", line);
    }
}
