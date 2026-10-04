namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// Ticked Firestone compositions in TavernAdvisor and CompositionRows (ids passed as a plain list). The panel no longer
/// lists Firestone compositions (2026-10-04: the comp guides are the targets, CompTargetsTests), but this code stays.
/// </summary>
public class TickedCompositionsTests
{
    private static readonly Composition Undead = new("undead_butcher", "Undead Butcher", new[] { "UNDEAD" },
        coreCards: new[] { "BG32_324", "BG25_010", "BG28_309" }, addonCards: new[] { "BG32_880" }, averagePlacement: 3.81);
    private static readonly Composition Pirate = new("pirate_discover", "Pirate Discover", new[] { "PIRATE" },
        coreCards: new[] { "BG26_817", "BG33_823" }, addonCards: new[] { "BG33_825" }, averagePlacement: 3.93);
    private static readonly Composition Mech = new("mech_magnet", "Mech Magnet", new[] { "MECHANICAL" },
        coreCards: new[] { "BG24_022", "BG25_040" }, addonCards: new[] { "BG28_300" }, averagePlacement: 4.12);
    private static readonly Composition Beast = new("beast_lobster", "Beast Lobster", new[] { "BEAST" },
        coreCards: new[] { "BG31_808" }, addonCards: new[] { "BG29_300" }, averagePlacement: 3.46);
    private static readonly Composition Naga = new("naga_spells", "Naga Spells", new[] { "NAGA" },
        coreCards: new[] { "BG23_008" }, addonCards: Array.Empty<string>(), averagePlacement: 3.70);

    private static readonly Composition[] All = { Undead, Pirate, Mech, Beast, Naga };
    private static readonly string[] Lobby = { "UNDEAD", "PIRATE", "MECHANICAL", "BEAST", "NAGA" };

    private static IEnumerable<(string, bool)> Marks(ShopAdvice card) => card.Advances.Select(a => (a.Composition.Id, a.IsKeyPiece));

    [Fact]
    public void ThreeShops_ATickedCompIsTheOnlyOneMarked_EvenOutOfTheRanking_ThenNothingTickedBringsTheAutomaticTargetsBack()
    {
        var ticked = new List<string> { "pirate_discover" };
        var tavern = new[] { "BG25_010", "BG33_825", "BG26_817", "BG28_573" };

        // Shop 1: the board says undead, Ali says pirate. Undead's key piece is no longer marked; the pirate add-on is.
        var owned1 = new[] { new OwnedCard("BG32_324", "UNDEAD") };
        var shop1 = TavernAdvisor.Advise(tavern, owned1, All, Lobby, ticked);
        Assert.Equal(new[] { "pirate_discover", "undead_butcher" }, shop1.Targets.Select(t => t.Composition.Id)); // ticked, then suggested
        Assert.Empty(shop1.Cards[0].Advances);
        Assert.Equal(new[] { ("pirate_discover", false) }, Marks(shop1.Cards[1]));
        Assert.Equal(new[] { ("pirate_discover", true) }, Marks(shop1.Cards[2]));

        // Shop 2: three undead pieces, Pirate is nowhere in the automatic targets: it stays aimed at, and first in the panel.
        var owned2 = new[] { new OwnedCard("BG32_324", "UNDEAD"), new OwnedCard("BG25_010", "UNDEAD"), new OwnedCard("BG28_309", "UNDEAD") };
        var shop2 = TavernAdvisor.Advise(tavern, owned2, All, Lobby, ticked);
        Assert.DoesNotContain(CompAdvisor.Suggest(owned2, All, 8), t => t.Composition.Id == "pirate_discover");
        Assert.Equal(new[] { ("pirate_discover", false) }, Marks(shop2.Cards[1]));
        var rows = CompositionRows.Build(shop2.Targets, owned2, ticked);
        Assert.Equal(("pirate_discover", true), (rows[0].Composition.Id, rows[0].IsChecked));
        Assert.Equal("undead_butcher", rows[1].Composition.Id);
        Assert.False(rows[1].IsChecked);

        // Shop 3, nothing ticked any more: the automatic targets are back.
        var shop3 = TavernAdvisor.Advise(tavern, owned2, All, Lobby, Array.Empty<string>());
        Assert.Equal(new[] { ("undead_butcher", true) }, Marks(shop3.Cards[0]));
        Assert.Empty(shop3.Cards[1].Advances);
    }

    [Fact]
    public void TickedDuringCombat_TheRowsFollowAtOnce_ButNotOutOfGame()
    {
        var ticked = new List<string>();
        var owned = new[] { new OwnedCard("BG32_324", "UNDEAD") };
        IReadOnlyList<CompositionRow> Rows() =>
            CompositionRows.Build(TavernAdvisor.Aim(All, All, owned, ticked, 3, null).Shown, owned, ticked);
        var state = new CompositionPanelState();
        state.Update(OverlayPhase.Shop, Rows);
        state.Update(OverlayPhase.Combat, () => throw new InvalidOperationException());
        Assert.Equal("undead_butcher", state.Rows[0].Composition.Id);

        ticked.Add("naga_spells");
        state.Replace(Rows());
        Assert.Equal(("naga_spells", true), (state.Rows[0].Composition.Id, state.Rows[0].IsChecked));
        Assert.Equal(2, state.Rows.Count); // the ticked one, then the only reachable one
        ticked.Add("mech_magnet");
        ticked.Add("beast_lobster");
        state.Replace(Rows());
        Assert.Equal(new[] { true, true, true, false }, state.Rows.Select(r => r.IsChecked));

        state.Update(OverlayPhase.OutOfGame, () => throw new InvalidOperationException());
        state.Replace(Rows());
        Assert.Empty(state.Rows);
    }
}
