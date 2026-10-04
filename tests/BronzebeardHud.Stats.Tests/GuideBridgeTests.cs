using static BronzebeardHud.Stats.Tests.BridgeTestData;
using static BronzebeardHud.Stats.Tests.GuideTestData;

namespace BronzebeardHud.Stats.Tests;

public class GuideBridgeTests
{
    // Guide: four core cards (half = 2) and three add-ons. Every comp below is told apart by the cards it shares.
    private static readonly CompGuide Undead = Guide("Undead Butcher", 2, 0, new[] { "K1", "K2", "K3", "K4" }, addons: new[] { "A1", "A2", "A3" },
        enablers: new[] { "E1" }, tribe: 11);

    [Fact]
    public void TwoCandidates_OnlyTheHonestOneBridges()
    {
        // honest: K1 K2 in its lists, K3 only on a final board (golden), A1 on the reference board: 3 keys, 4 cards.
        var honest = Comp("honest", new[] { "K1", "K2", "X1" }, games: 500, boards: new[] { Board(12, "K3_G", "X2") }, reference: new[] { "A1", "X3" });
        // lookalike: as many cards in common (K1 A1 A2 A3) but a single key piece (1 < half of 4), and far more games.
        var lookalike = Comp("lookalike", new[] { "K1", "A1", "A2" }, addons: new[] { "A3" }, games: 9000, boards: new[] { Board(14, "E1", "A1", "Y1") });

        var evidence = GuideBridge.Match(Undead, new[] { lookalike, honest });

        Assert.NotNull(evidence);
        Assert.Equal(("honest", 3, 4), (evidence!.Composition.Id, evidence.SharedKeyCount, evidence.SharedCards.Count));
        Assert.Equal(new[] { "K1", "K2", "K3", "A1" }, evidence.SharedCards); // the guide's order: core cards, then add-ons
        Assert.Same(Undead, evidence.Guide);

        // Alone, the lookalike bridges nothing: 4 cards in common are not enough without half of the key pieces.
        Assert.Null(GuideBridge.Match(Undead, new[] { lookalike }));
    }

    [Fact]
    public void Ranking_KeysFirst_ThenCards_ThenGames_ThenId_WhateverTheOrderGiven()
    {
        var threeKeys = Comp("three_keys", new[] { "K1", "K2", "K3" }, games: 100);
        var twoKeysManyCards = Comp("two_keys", new[] { "K1", "K2", "A1", "A2", "A3" }, games: 9000); // 5 cards, 2 keys
        var threeKeysOneMore = Comp("three_keys_plus", new[] { "K1", "K2", "K3", "A1" }, games: 50); // 4 cards, 3 keys
        var threeKeysMoreGames = Comp("three_keys_games", new[] { "K1", "K2", "K4" }, games: 500); // 3 cards, 3 keys, more games
        var sameAsThreeKeysGames = Comp("a_same", new[] { "K1", "K3", "K4" }, games: 500); // a full tie with it: the id decides

        // More key pieces beat more cards and more games.
        Assert.Equal("three_keys", GuideBridge.Match(Undead, new[] { twoKeysManyCards, threeKeys })!.Composition.Id);
        Assert.Equal("three_keys", GuideBridge.Match(Undead, new[] { threeKeys, twoKeysManyCards })!.Composition.Id);

        // Same key pieces: more cards in common win, even with fewer games.
        Assert.Equal("three_keys_plus", GuideBridge.Match(Undead, new[] { threeKeys, threeKeysOneMore })!.Composition.Id);
        Assert.Equal("three_keys_plus", GuideBridge.Match(Undead, new[] { threeKeysOneMore, threeKeys })!.Composition.Id);

        // Same keys and cards: more games win.
        Assert.Equal("three_keys_games", GuideBridge.Match(Undead, new[] { threeKeys, threeKeysMoreGames })!.Composition.Id);
        Assert.Equal("three_keys_games", GuideBridge.Match(Undead, new[] { threeKeysMoreGames, threeKeys })!.Composition.Id);

        // A full tie: the smaller id, in either order, so that the bridge never depends on the cache's order.
        Assert.Equal("a_same", GuideBridge.Match(Undead, new[] { threeKeysMoreGames, sameAsThreeKeysGames })!.Composition.Id);
        Assert.Equal("a_same", GuideBridge.Match(Undead, new[] { sameAsThreeKeysGames, threeKeysMoreGames })!.Composition.Id);
    }

    [Fact]
    public void OneSharedCard_IsNoBridge_EvenWhenItIsHalfTheKeys()
    {
        var twoKeys = Guide("Two Keys", 1, 0, new[] { "T1", "T2" });
        var oneKey = Guide("One Key", 1, 1, new[] { "O1" }, addons: new[] { "OA" });
        var comp = Comp("comp", new[] { "T1", "O1", "Z1" }, games: 800, boards: new[] { Board(11, "T1", "O1") });

        Assert.Null(GuideBridge.Match(twoKeys, new[] { comp })); // T1: half of the keys, but one card
        Assert.Null(GuideBridge.Match(oneKey, new[] { comp }));  // O1: every key, but one card

        // The same guide with its add-on on a board (golden): two cards, and the bridge is made.
        var withAddOn = Comp("with_addon", new[] { "O1" }, boards: new[] { Board(11, "OA_G") });
        var evidence = GuideBridge.Match(oneKey, new[] { withAddOn })!;
        Assert.Equal(("with_addon", 1, 2), (evidence.Composition.Id, evidence.SharedKeyCount, evidence.SharedCards.Count));
    }

    [Fact]
    public void ExactlyHalfTheKeys_Bridges_OneLess_DoesNot()
    {
        var half = Comp("half", new[] { "K1", "K2" }, addons: new[] { "A1" });   // 2 of 4 keys
        var less = Comp("less", new[] { "K1", "A1", "A2" }, addons: new[] { "A3" }); // 1 of 4 keys, 4 cards

        Assert.Equal("half", GuideBridge.Match(Undead, new[] { half })!.Composition.Id);
        Assert.Null(GuideBridge.Match(Undead, new[] { less }));

        // Three key pieces: half is 1.5, so two are needed.
        var three = Guide("Three", 1, 0, new[] { "H1", "H2", "H3" }, addons: new[] { "HA" });
        Assert.Null(GuideBridge.Match(three, new[] { Comp("one_of_three", new[] { "H1", "HA" }) }));
        Assert.NotNull(GuideBridge.Match(three, new[] { Comp("two_of_three", new[] { "H1", "H2" }) }));
    }

    [Fact]
    public void EnablersAndUnlistedCards_DoNotCount_AndAGuideWithoutKeyPiecesIsNeverBridged()
    {
        // QE is an enabler of the guide: not one of its cards for the bridge (core and add-ons only). Counted, it would make
        // two cards with half of the keys, and a bridge.
        var withEnabler = Guide("With Enabler", 1, 0, new[] { "Q1", "Q2" }, enablers: new[] { "QE" });
        var comp = Comp("comp", new[] { "Q1", "QE" }, boards: new[] { Board(10, "QE", "Z9") });
        Assert.Null(GuideBridge.Match(withEnabler, new[] { comp }));

        var noKeys = Guide("Add-ons Only", 1, 0, Array.Empty<string>(), addons: new[] { "N1", "N2" });
        Assert.Null(GuideBridge.Match(noKeys, new[] { Comp("n", new[] { "N1", "N2" }) }));
        Assert.Null(GuideBridge.Match(Undead, Array.Empty<Composition>()));
    }

    [Fact]
    public void TwoVariantsOfOneComp_MayBridgeToTheSameComposition()
    {
        // Allowed on purpose: HSReplay splits what Firestone counts as one archetype (two variants of one comp).
        var butcher = Guide("Undead Butcher", 2, 0, new[] { "U1", "U2" }, addons: new[] { "UA" }, tribe: 11);
        var attack = Guide("Undead Attack", 2, 1, new[] { "U1", "U2", "U3" }, tribe: 11);
        var beasts = Guide("Beast Lobster", 1, 0, new[] { "B1", "B2" }, tribe: 20);
        var undead = Comp("undead_fs", new[] { "U1", "U2", "U3" }, addons: new[] { "UA" }, games: 1234);

        var bridge = GuideBridge.For(Set(butcher, attack, beasts), new[] { undead });

        Assert.Equal(2, bridge.Count);
        Assert.Same(undead, bridge[butcher.Id].Composition);
        Assert.Same(undead, bridge[attack.Id].Composition);
        Assert.Equal((2, 3), (bridge[butcher.Id].SharedKeyCount, bridge[butcher.Id].SharedCards.Count));
        Assert.Equal((3, 3), (bridge[attack.Id].SharedKeyCount, bridge[attack.Id].SharedCards.Count));
        Assert.False(bridge.ContainsKey(beasts.Id));
    }

    [Fact]
    public void Line_NamesEachGuide_InHdtsOrder_WithItsCompOrNoMatch()
    {
        var butcher = Guide("Undead Butcher", 2, 0, new[] { "U1", "U2", "U3" }, addons: new[] { "UA" }, tribe: 11);
        var beasts = Guide("Beast Lobster", 1, 0, new[] { "B1", "B2" }, tribe: 20);
        var handWritten = Guide("Mech Magnet", 3, 0, new[] { "M1", "M2" }, tribe: 17);
        var comps = new[]
        {
            Comp("undead_fs", new[] { "U1", "U2" }, addons: new[] { "UA" }, games: 1234),
            Comp("mech_manual", new[] { "M1", "M2" }), // a hand-written comp: no game count
        };
        var set = Set(butcher, beasts, handWritten);

        var line = GuideBridge.Line(set, GuideBridge.For(set, comps));

        Assert.Equal("Bronzebeard HUD: bridge: Beast Lobster → no match; Undead Butcher → undead_fs (2/3 keys, 3 cards, 1234 games); " +
                     "Mech Magnet → mech_manual (2/2 keys, 2 cards, ? games)", line);
        Assert.Equal("Bronzebeard HUD: bridge: no guides", GuideBridge.Line(CompGuideSet.Empty(CompGuideSources.HdtFree), new Dictionary<string, GuideEvidence>()));
    }
}
