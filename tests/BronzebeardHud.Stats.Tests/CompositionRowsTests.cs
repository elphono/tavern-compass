using System.Globalization;

namespace BronzebeardHud.Stats.Tests;

public class CompositionRowsTests
{
    // Three compositions with distinct reference boards (left to right), one without any board order.
    private static readonly Composition Undead = new("undead_butcher", "Undead Butcher", new[] { "UNDEAD" },
        new[] { "BG32_324", "BG25_010" }, new[] { "BG32_880" }, averagePlacement: 3.81,
        referenceBoard: new[] { "BG28_309", "BG32_324", "BG36_515", "BG36_515", "BG25_010", "BG32_880", "BG25_354" });
    private static readonly Composition Pirate = new("pirate_discover", "Pirate Discover", new[] { "PIRATE" },
        new[] { "BG26_817", "BG33_823" }, new[] { "BG33_825" }, averagePlacement: 3.93,
        referenceBoard: new[] { "BG33_823", "BG33_825", "BG26_817" });
    private static readonly Composition Mech = new("mech_magnet", "Mech Magnet", new[] { "MECHANICAL" },
        new[] { "BG24_022", "BG25_040" }, new[] { "BG28_300" }, averagePlacement: 4.12,
        referenceBoard: new[] { "BG25_040", "BG28_300", "BG24_022", "BG24_022" });
    private static readonly Composition Handwritten = new("hsr-beasts", "Beasts", new[] { "BEAST" },
        new[] { "BG31_808", "BG30_002" }, new[] { "BG29_300" }, averagePlacement: 3.46);

    private static readonly Composition[] Lobby = { Undead, Pirate, Mech, Handwritten };

    private static IEnumerable<(string, bool)> Board(CompositionRow row) => row.Vignettes.Select(v => (v.CardId, v.Owned));

    [Fact]
    public void Board1_ReachableCompsByPlacement_VignettesInFinalBoardOrder()
    {
        var owned = new[] { new OwnedCard("BG32_324_G", "UNDEAD"), new OwnedCard("BG36_515", "UNDEAD"), new OwnedCard("BG31_808", "BEAST") };

        var rows = CompositionRows.Build(CompAdvisor.Suggest(owned, Lobby, 3), owned);

        // Beasts (3.46) before undead (3.81): best placement first; pirates and mechs hold nothing, so they are not suggested.
        Assert.Equal(new[] { "hsr-beasts", "undead_butcher" }, rows.Select(r => r.Composition.Id));
        var undead = rows[1];
        // Golden Butcher counts; one copy of BG36_515 marks only the first of its two positions.
        Assert.Equal(new[] { ("BG28_309", false), ("BG32_324", true), ("BG36_515", true), ("BG36_515", false), ("BG25_010", false), ("BG32_880", false), ("BG25_354", false) },
            Board(undead));
        Assert.Equal(Enumerable.Range(1, 7), undead.Vignettes.Select(v => v.Position));
        Assert.Equal((1, 2), (undead.KeyOwned, undead.KeyTotal));
        Assert.True(undead.OrderKnown);
        Assert.Equal("3,8", undead.PlacementText);

        // No board order in the hand-written source: key pieces then add-ons, flagged as unknown order.
        Assert.False(rows[0].OrderKnown);
        Assert.Equal(new[] { "BG31_808", "BG30_002", "BG29_300" }, rows[0].Vignettes.Select(v => v.CardId));
        Assert.Equal("3,5", rows[0].PlacementText);
    }

    [Fact]
    public void Board2_TwoReachableComps_EachInItsOwnOrder()
    {
        var owned = new[] { new OwnedCard("BG33_823", "PIRATE"), new OwnedCard("BG26_817", "PIRATE"), new OwnedCard("BG24_022", "MECHANICAL") };

        var rows = CompositionRows.Build(CompAdvisor.Suggest(owned, Lobby, 3), owned);

        Assert.Equal(new[] { "pirate_discover", "mech_magnet" }, rows.Select(r => r.Composition.Id));
        Assert.Equal(new[] { 7.0, 3.5 }, rows.Select(r => r.Score));
        Assert.Equal(new[] { ("BG33_823", true), ("BG33_825", false), ("BG26_817", true) }, Board(rows[0]));
        Assert.Equal(new[] { ("BG25_040", false), ("BG28_300", false), ("BG24_022", true), ("BG24_022", false) }, Board(rows[1]));
        Assert.Equal("4,1", rows[1].PlacementText);
    }

    [Fact]
    public void Board3_HeldInHandCounts_AndTheRowCountIsCapped()
    {
        var owned = new[]
        {
            new OwnedCard("BG32_324", "UNDEAD"), new OwnedCard("BG26_817", "PIRATE"),
            new OwnedCard("BG25_040", "MECHANICAL"), new OwnedCard("BG31_808", "BEAST"),
        };
        var rows = CompositionRows.Build(CompAdvisor.Suggest(owned, Lobby, 3), owned);

        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "hsr-beasts", "undead_butcher", "pirate_discover" }, rows.Select(r => r.Composition.Id)); // all 3.5: best placement first
        Assert.Equal(new[] { ("BG31_808", true), ("BG30_002", false), ("BG29_300", false) }, Board(rows[0]));
    }

    [Fact]
    public void Suggestions_AreToldApartFromTheTickedTarget()
    {
        var owned = new[]
        {
            new OwnedCard("BG32_324", "UNDEAD"), new OwnedCard("BG26_817", "PIRATE"),
            new OwnedCard("BG25_040", "MECHANICAL"), new OwnedCard("BG31_808", "BEAST"),
        };
        var shown = CompAdvisor.Suggest(owned, Lobby, 3);

        // Nothing ticked: every row is a suggestion (drawn plain, light grey; a ticked one is bold, in its colour).
        var untouched = CompositionRows.Build(shown, owned);
        Assert.Equal(3, untouched.Count);
        Assert.All(untouched, r => Assert.True(r.IsSuggestion && !r.IsChecked, r.Composition.Id));

        // One ticked: exactly that row is the target, the other two stay suggestions.
        var ticked = CompositionRows.Build(shown, owned, chosen: new[] { "undead_butcher" });
        Assert.Equal(new[] { "undead_butcher" }, ticked.Where(r => r.IsChecked).Select(r => r.Composition.Id));
        Assert.Equal(new[] { "hsr-beasts", "pirate_discover" }, ticked.Where(r => r.IsSuggestion).Select(r => r.Composition.Id));
    }

    [Fact]
    public void PlacementText_UsesTheDecimalCommaWhateverTheCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var owned = new[] { new OwnedCard("BG24_022", "MECHANICAL") };
            var row = CompositionRows.Build(CompAdvisor.Suggest(owned, new[] { Mech }, 3), owned).Single();
            Assert.Equal("4,1", row.PlacementText);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(OverlayPhase.OutOfGame, false, false)]
    [InlineData(OverlayPhase.HeroSelection, false, false)]
    [InlineData(OverlayPhase.Shop, true, true)]
    [InlineData(OverlayPhase.Combat, true, false)]
    public void Visibility_PanelAndMarkersPerPhase(OverlayPhase phase, bool panel, bool markers)
    {
        Assert.Equal((panel, markers), CompositionPanelState.Visibility(phase));
    }

    [Fact]
    public void ShopCombatShop_OverThreeRounds_ThePanelKeepsTheLastShopRowsInCombat()
    {
        var state = new CompositionPanelState();
        var computed = 0;
        IReadOnlyList<CompositionRow> RowsFor(params OwnedCard[] owned)
        {
            computed++;
            return CompositionRows.Build(CompAdvisor.Suggest(owned, Lobby, 3), owned);
        }

        var shops = new[]
        {
            new[] { new OwnedCard("BG32_324", "UNDEAD") },
            new[] { new OwnedCard("BG26_817", "PIRATE"), new OwnedCard("BG33_823", "PIRATE") },
            new[] { new OwnedCard("BG24_022", "MECHANICAL"), new OwnedCard("BG25_040", "MECHANICAL") },
        };
        var expectedFirst = new[] { "undead_butcher", "pirate_discover", "mech_magnet" };

        state.Update(OverlayPhase.HeroSelection, () => throw new InvalidOperationException("no rows in hero selection"));
        Assert.False(state.PanelVisible);
        for (var round = 0; round < 3; round++)
        {
            state.Update(OverlayPhase.Shop, () => RowsFor(shops[round]));
            Assert.True(state.PanelVisible && state.MarkersVisible);
            Assert.Equal(expectedFirst[round], state.Rows[0].Composition.Id);

            state.Update(OverlayPhase.Combat, () => throw new InvalidOperationException("rows are not recomputed in combat"));
            Assert.True(state.PanelVisible);
            Assert.False(state.MarkersVisible);
            Assert.Equal(expectedFirst[round], state.Rows[0].Composition.Id);
        }

        Assert.Equal(3, computed);
        state.Update(OverlayPhase.OutOfGame, () => throw new InvalidOperationException());
        Assert.False(state.PanelVisible);
        Assert.Empty(state.Rows);
    }

    [Fact]
    public void Import_ReferenceBoardIsTheMostRepresentative_InZonePositionOrder()
    {
        string Minion(string id, int position) => $"{{\"cardID\":\"{id}\",\"tags\":{{\"ZONE_POSITION\":{position},\"ATK\":5}}}}";
        string Board(int mmr, params (string Id, int Pos)[] minions) =>
            $"{{\"mmr\":{mmr},\"finalComp\":{{\"board\":[{string.Join(",", minions.Select(m => Minion(m.Id, m.Pos)))}]}}}}";
        var boards = new List<string>
        {
            // Two key pieces, listed out of order in the array: ZONE_POSITION must win.
            Board(7000, ("BG25_010", 3), ("BG32_324_G", 1), ("BG32_880", 2), ("BG28_309", 4)),
            // Higher MMR but a single key piece: less representative.
            Board(9900, ("BG32_324", 1), ("BG28_309", 2)),
        };
        boards.AddRange(Enumerable.Range(0, 18).Select(i => Board(5000 + i, ("BG32_324", 1), ("BG25_010", 2), ($"TOKEN_{i}", 3))));
        var payload = "{\"timePeriod\":\"last-patch\",\"compStats\":[{\"archetype\":\"undead_butcher\",\"dataPoints\":6461,\"averagePlacement\":3.8," +
                      "\"heroStats\":[{\"finalBoards\":[" + string.Join(",", boards) + "]}]}]}";

        var comp = FirestoneCompImporter.Import(payload, "https://example.invalid/", DateTimeOffset.UnixEpoch).Compositions.Single();

        Assert.Equal(new[] { "BG32_324", "BG25_010" }, comp.CoreCards);
        Assert.Equal(new[] { "BG32_324", "BG32_880", "BG25_010", "BG28_309" }, comp.ReferenceBoard);
        var copy = CompositionLoader.Parse(CompositionLoader.Serialize(new CompositionFile(StatsSources.Firestone, new[] { comp })));
        Assert.Equal(comp.ReferenceBoard, copy.Compositions.Single().ReferenceBoard);
    }

    [Fact]
    public void HsReplayText_BoardLineGivesTheOrder_DuplicatesKept_EightRefused()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Mummifier"] = "BG28_309", ["Drustfallen Butcher"] = "BG32_324" };
        var file = HsReplayCompText.Parse("comp: Undead\ncore: BG32_324\nboard: Mummifier, Drustfallen Butcher, BG36_515, BG36_515",
            n => names.TryGetValue(n, out var id) ? id : null);
        Assert.Equal(new[] { "BG28_309", "BG32_324", "BG36_515", "BG36_515" }, file.Compositions.Single().ReferenceBoard);

        var e = Assert.Throws<StatsFormatException>(() => HsReplayCompText.Parse(
            "comp: X\ncore: BG32_324\nboard: A_1, B_2, C_3, D_4, E_5, F_6, G_7, H_8", _ => null));
        Assert.Equal("line 3: a board holds at most 7 minions", e.Message);
    }
}
