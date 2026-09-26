namespace BronzebeardHud.Stats.Tests;

public class TavernAdvisorTests
{
    private static readonly Composition Undead = new("undead_butcher", "Undead Butcher", new[] { "UNDEAD" },
        coreCards: new[] { "BG32_324", "BG25_010" }, addonCards: new[] { "BG32_880" }, averagePlacement: 3.81);
    private static readonly Composition Pirate = new("pirate_discover", "Pirate Discover", new[] { "PIRATE" },
        coreCards: new[] { "BG26_817", "BG_LOE_077" }, addonCards: new[] { "BG33_825" }, averagePlacement: 3.93);
    private static readonly Composition Naga = new("naga_end_of_turn", "Naga End Of Turn", new[] { "NAGA" },
        coreCards: new[] { "BG23_008" }, addonCards: Array.Empty<string>(), averagePlacement: 3.50);
    private static readonly Composition Neutral = new("neutral_tea_set", "Neutral Tea Set", Array.Empty<string>(),
        coreCards: new[] { "BG_LOE_077", "BG31_815" }, addonCards: Array.Empty<string>(), averagePlacement: 3.70);

    private static readonly Composition[] All = { Undead, Pirate, Naga, Neutral };
    private static readonly string[] Lobby = { "UNDEAD", "PIRATE", "BEAST", "DEMON", "MECHANICAL" }; // no naga

    private static IEnumerable<(string, bool)> Marks(ShopAdvice card) => card.Advances.Select(a => (a.Composition.Id, a.IsKeyPiece));

    [Fact]
    public void Turn1_EmptyBoard_KeyPiecesOfLobbyCompsAreMarked()
    {
        var advice = TavernAdvisor.Advise(new[] { "BG32_324", "BG20_101", "BG23_008", "BG_LOE_077" }, Array.Empty<OwnedCard>(), All, Lobby);

        Assert.Empty(advice.Targets);
        Assert.Equal(3, advice.PlayableCompositions); // naga is not in the lobby
        Assert.Equal(new[] { ("undead_butcher", true) }, Marks(advice.Cards[0]));
        Assert.Empty(advice.Cards[1].Advances);   // not a piece of anything
        Assert.Empty(advice.Cards[2].Advances);   // naga key piece, naga absent from the lobby
        // Key piece of two compositions: one marker, both names, better placement first when nothing is targeted.
        Assert.Equal(new[] { ("neutral_tea_set", true), ("pirate_discover", true) }, Marks(advice.Cards[3]));
        Assert.Equal(2, advice.MarkerCount);
    }

    [Fact]
    public void ThreeSuccessiveShops_TargetsAndMarkersFollowTheBoard()
    {
        // Shop 2: a pirate that is not a piece makes pirate the target; its add-on is now marked.
        var shop2 = TavernAdvisor.Advise(new[] { "BG33_825", "BG32_880", "BG_LOE_077" },
            new[] { new OwnedCard("BG21_005", "PIRATE") }, All, Lobby);
        Assert.Equal(new[] { "pirate_discover" }, shop2.Targets.Select(t => t.Composition.Id));
        Assert.Equal(new[] { ("pirate_discover", false) }, Marks(shop2.Cards[0]));
        Assert.Empty(shop2.Cards[1].Advances); // undead add-on, undead not targeted
        Assert.Equal(new[] { ("pirate_discover", true), ("neutral_tea_set", true) }, Marks(shop2.Cards[2])); // target first

        // Shop 3: two undead key pieces held; undead is the target, its add-on is marked, the pirate add-on no longer.
        var owned3 = new[] { new OwnedCard("BG32_324", "UNDEAD"), new OwnedCard("BG25_010", "UNDEAD") };
        var shop3 = TavernAdvisor.Advise(new[] { "BG32_880", "BG33_825", "BG32_324_G" }, owned3, All, Lobby);
        Assert.Equal(new[] { "undead_butcher" }, shop3.Targets.Select(t => t.Composition.Id));
        Assert.Equal(new[] { ("undead_butcher", false) }, Marks(shop3.Cards[0]));
        Assert.Empty(shop3.Cards[1].Advances);
        Assert.Equal(new[] { ("undead_butcher", true) }, Marks(shop3.Cards[2])); // a copy of a held key piece: triple

        // Shop 4: board sold for pirates; pirate is back as the target, undead gone.
        var shop4 = TavernAdvisor.Advise(new[] { "BG33_825" }, new[] { new OwnedCard("BG26_817", "PIRATE") }, All, Lobby);
        Assert.Equal(new[] { "pirate_discover" }, shop4.Targets.Select(t => t.Composition.Id));
        Assert.Equal(new[] { ("pirate_discover", false) }, Marks(shop4.Cards[0]));
    }

    [Fact]
    public void EmptyBoard_AKeyPieceHeldInHand_MakesItsCompositionATarget()
    {
        var cards = new PlayerCards(board: Array.Empty<OwnedCard>(), hand: new[] { new OwnedCard("BG25_010", "UNDEAD") });

        var advice = TavernAdvisor.Advise(new[] { "BG32_880", "BG33_825" }, cards.All, All, Lobby);

        Assert.Equal(new[] { "undead_butcher" }, advice.Targets.Select(t => t.Composition.Id));
        Assert.Equal(new[] { "BG25_010" }, advice.Targets[0].CoreOwned);
        Assert.Equal(new[] { ("undead_butcher", false) }, Marks(advice.Cards[0])); // the target's add-on is now marked
        Assert.Empty(advice.Cards[1].Advances);
        // The same key piece with nothing in hand: no target, so the hand is what made the difference.
        Assert.Empty(TavernAdvisor.Advise(new[] { "BG32_880" }, new PlayerCards(Array.Empty<OwnedCard>(), Array.Empty<OwnedCard>()).All, All, Lobby).Targets);
    }

    [Fact]
    public void AnOwnedAddonIsNotMarked_AndAnUnknownLobbyFiltersNothing()
    {
        var owned = new[] { new OwnedCard("BG21_005", "PIRATE"), new OwnedCard("BG33_825", "PIRATE") };
        Assert.Empty(TavernAdvisor.Advise(new[] { "BG33_825" }, owned, All, Lobby).Cards[0].Advances);

        var unknownLobby = TavernAdvisor.Advise(new[] { "BG23_008" }, Array.Empty<OwnedCard>(), All, Array.Empty<string>());
        Assert.Equal(4, unknownLobby.PlayableCompositions);
        Assert.Equal(new[] { ("naga_end_of_turn", true) }, Marks(unknownLobby.Cards[0]));
    }

    [Fact]
    public void DiagnosticLine_HasTheExactFormat()
    {
        var shop = TavernAdvisor.Advise(new[] { "BG20_101", "BG33_825", "BG_LOE_077" }, new[] { new OwnedCard("BG21_005", "PIRATE") }, All, Lobby);
        var first = TavernLayout.Markers(2291, 1360, 3)[1];

        var cards = new PlayerCards(new[] { new OwnedCard("BG21_005", "PIRATE") }, new[] { new OwnedCard("BG25_010", "UNDEAD"), new OwnedCard("BG24_022", "MECHANICAL") });
        var line = TavernAdvisor.DiagnosticLine(3, 24, "ok", shop, cards, first, 2291, 1360);

        Assert.Equal("Bronzebeard HUD: tavern round=3 comps=24 (ok) compsInLobby=3 board=1 hand=2 targets=[Pirate Discover 0.5] tavern=3 markers=2 " +
                     "first=#1 x=1063 y=601 w=165 h=31 canvas=2291x1360", line);
        Assert.EndsWith("targets=none tavern=0 markers=0 first=none canvas=2000x1220",
            TavernAdvisor.DiagnosticLine(1, 0, "loading", TavernAdvisor.Advise(Array.Empty<string>(), Array.Empty<OwnedCard>(), All, Lobby), PlayerCards.None, null, 2000, 1220));
    }
}
