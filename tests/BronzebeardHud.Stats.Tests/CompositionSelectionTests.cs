using System.Globalization;

namespace BronzebeardHud.Stats.Tests;

public class CompositionSelectionTests
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
    public void ThreeShops_ATickedCompIsTheOnlyOneMarked_EvenOutOfTheRanking_ThenANewGameForgetsIt()
    {
        var selection = new CompositionSelection();
        selection.BeginGame(1);
        Assert.True(selection.Toggle("pirate_discover"));
        var tavern = new[] { "BG25_010", "BG33_825", "BG26_817", "BG28_573" };

        // Shop 1: the board says undead, Ali says pirate. Undead's key piece is no longer marked; the pirate add-on is.
        var owned1 = new[] { new OwnedCard("BG32_324", "UNDEAD") };
        var shop1 = TavernAdvisor.Advise(tavern, owned1, All, Lobby, selection.Checked);
        Assert.Equal(new[] { "pirate_discover", "undead_butcher" }, shop1.Targets.Select(t => t.Composition.Id)); // ticked, then suggested
        Assert.Empty(shop1.Cards[0].Advances);
        Assert.Equal(new[] { ("pirate_discover", false) }, Marks(shop1.Cards[1]));
        Assert.Equal(new[] { ("pirate_discover", true) }, Marks(shop1.Cards[2]));

        // Shop 2: three undead pieces, Pirate is nowhere in the automatic targets: it stays aimed at, and first in the panel.
        var owned2 = new[] { new OwnedCard("BG32_324", "UNDEAD"), new OwnedCard("BG25_010", "UNDEAD"), new OwnedCard("BG28_309", "UNDEAD") };
        var shop2 = TavernAdvisor.Advise(tavern, owned2, All, Lobby, selection.Checked);
        Assert.DoesNotContain(CompAdvisor.Suggest(owned2, All, 8), t => t.Composition.Id == "pirate_discover");
        Assert.Equal(new[] { ("pirate_discover", false) }, Marks(shop2.Cards[1]));
        var rows = CompositionRows.Build(shop2.Targets, owned2, selection.Checked);
        Assert.Equal(("pirate_discover", true), (rows[0].Composition.Id, rows[0].IsChecked));
        Assert.Equal("undead_butcher", rows[1].Composition.Id);
        Assert.False(rows[1].IsChecked);

        // Shop 3, next game: nothing ticked any more, the automatic targets are back.
        selection.BeginGame(2);
        Assert.False(selection.Any);
        var shop3 = TavernAdvisor.Advise(tavern, owned2, All, Lobby, selection.Checked);
        Assert.Equal(new[] { ("undead_butcher", true) }, Marks(shop3.Cards[0]));
        Assert.Empty(shop3.Cards[1].Advances);
    }

    [Fact]
    public void Choices_FollowTheTickedCompsToo()
    {
        var options = new[] { new OfferedOption(1, "BG25_010", "MINION"), new OfferedOption(2, "BG24_022", "MINION"), new OfferedOption(3, "BG33_825", "MINION") };
        var owned = new[] { new OwnedCard("BG32_324", "UNDEAD") };

        var auto = ChoiceAdvisor.Advise(options, owned, All, Lobby);
        var ticked = ChoiceAdvisor.Advise(options, owned, All, Lobby, chosen: new[] { "mech_magnet" });

        // Automatic: only undead is reachable, so only undead is aimed at.
        Assert.Equal(new[] { "undead_butcher", null, null }, auto.Options.Select(o => o.Effects.FirstOrDefault()?.Composition.Id));
        Assert.Equal(new[] { null, "mech_magnet", null }, ticked.Options.Select(o => o.Effects.FirstOrDefault()?.Composition.Id));
        Assert.True(ticked.Options[1].Effects[0].IsCurrent);
    }

    [Fact]
    public void Colours_StayWithTheirCompWhateverTheRanking_FreedWhenUnticked_FourAtMost()
    {
        var selection = new CompositionSelection();
        selection.BeginGame(7);
        selection.Toggle("pirate_discover");
        selection.Toggle("mech_magnet");
        Assert.Equal(("#FF2BD6", "#B8FF1F"), (selection.ColourOf("pirate_discover"), selection.ColourOf("mech_magnet")));

        // Three shops where the ranking moves: the colours do not.
        var boards = new[]
        {
            new[] { new OwnedCard("BG26_817", "PIRATE") },
            new[] { new OwnedCard("BG24_022", "MECHANICAL"), new OwnedCard("BG25_040", "MECHANICAL") },
            new[] { new OwnedCard("BG24_022", "MECHANICAL"), new OwnedCard("BG26_817", "PIRATE"), new OwnedCard("BG33_823", "PIRATE") },
        };
        var firstRows = new List<string>();
        foreach (var owned in boards)
        {
            var targets = CompAdvisor.Rank(owned, All);
            firstRows.Add(targets[0].Composition.Id);
            Assert.Equal("#FF2BD6", selection.ColourOf("pirate_discover"));
            Assert.Equal("#B8FF1F", selection.ColourOf("mech_magnet"));
        }

        Assert.Equal(new[] { "pirate_discover", "mech_magnet", "pirate_discover" }, firstRows); // the ranking did move

        // Unticking frees a colour for the next tick; the other composition keeps its own.
        selection.Toggle("pirate_discover");
        selection.Toggle("undead_butcher");
        Assert.Equal(("#FF2BD6", "#B8FF1F", (string?)null), (selection.ColourOf("undead_butcher"), selection.ColourOf("mech_magnet"), selection.ColourOf("pirate_discover")));

        Assert.True(selection.Toggle("beast_lobster"));
        Assert.True(selection.Toggle("naga_spells"));
        Assert.False(selection.Toggle("pirate_discover")); // four colours, four compositions
        Assert.Equal(new[] { "mech_magnet", "undead_butcher", "beast_lobster", "naga_spells" }, selection.Checked);

        // A marker takes the colour of the first ticked composition its card advances; white when nothing is ticked.
        Assert.Equal("#B8FF1F", selection.MarkerColour(new[] { "pirate_discover", "mech_magnet", "undead_butcher" }));
        selection.BeginGame(7);
        Assert.Equal(4, selection.Checked.Count); // same game: kept
        selection.BeginGame(8);
        Assert.Equal(CompositionSelection.AutoColour, selection.MarkerColour(new[] { "mech_magnet" }));
    }

    [Fact]
    public void Palette_ReadsOnTheDarkPanel_AndStaysAwayFromTheOrangeBorders()
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
        var colours = CompositionSelection.Palette.ToList();
        Assert.Equal(4, colours.Distinct().Count());
        Assert.Contains(CompositionSelection.AutoColour, colours); // readable too, and never shown with the palette
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

    [Fact]
    public void TickedDuringCombat_TheRowsFollowAtOnce_ButNotOutOfGame_AndClearGoesBackToAutomatic()
    {
        var selection = new CompositionSelection();
        var owned = new[] { new OwnedCard("BG32_324", "UNDEAD") };
        IReadOnlyList<CompositionRow> Rows() =>
            CompositionRows.Build(TavernAdvisor.Aim(All, All, owned, selection.Checked, 3, null).Shown, owned, selection.Checked);
        var state = new CompositionPanelState();
        state.Update(OverlayPhase.Shop, Rows);
        state.Update(OverlayPhase.Combat, () => throw new InvalidOperationException());
        Assert.Equal("undead_butcher", state.Rows[0].Composition.Id);

        selection.Toggle("naga_spells");
        state.Replace(Rows());
        Assert.Equal(("naga_spells", true), (state.Rows[0].Composition.Id, state.Rows[0].IsChecked));
        Assert.Equal(2, state.Rows.Count); // the ticked one, then the only reachable one
        selection.Toggle("mech_magnet");
        selection.Toggle("beast_lobster");
        state.Replace(Rows());
        Assert.Equal(new[] { true, true, true, false }, state.Rows.Select(r => r.IsChecked));

        state.Update(OverlayPhase.OutOfGame, () => throw new InvalidOperationException());
        state.Replace(Rows());
        Assert.Empty(state.Rows);

        selection.Clear();
        Assert.False(selection.Any);
        Assert.Equal(CompositionSelection.AutoColour, selection.MarkerColour(new[] { "naga_spells" }));
    }
}
