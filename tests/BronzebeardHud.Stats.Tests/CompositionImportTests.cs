using System.Globalization;

namespace BronzebeardHud.Stats.Tests;

public class CompositionImportTests
{
    private const string Url = "https://static.zerotoheroes.com/api/bgs/comp-stats/last-patch/overview-from-hourly.gz.json";
    private static readonly DateTimeOffset FetchedAt = new(2026, 9, 26, 20, 15, 0, TimeSpan.Zero);

    private static string Board(params string[] ids) =>
        "{\"mmr\":7000,\"finalComp\":{\"turn\":12,\"board\":[" +
        string.Join(",", ids.Select((id, i) => $"{{\"cardID\":\"{id}\",\"id\":{100 + i},\"tags\":{{\"50\":1}}}}")) + "]}}";

    /// <summary>
    /// 40 boards for "undead_butcher": BG32_324 in 36 (0.90, golden in 6 of them), BG25_010 in 24 (0.60),
    /// BG32_880 in 12 (0.30), BG31_835 in 6 (0.15). 19 boards for "mech_glambot", below the 20-board floor.
    /// </summary>
    private static string Payload()
    {
        var undeadBoards = Enumerable.Range(0, 40).Select(i =>
        {
            var ids = new List<string>();
            if (i < 36) ids.Add(i < 6 ? "BG32_324_G" : "BG32_324");
            if (i < 24) ids.Add("BG25_010");
            if (i >= 28) ids.Add("BG32_880");
            if (i % 7 == 0 && i < 42) ids.Add("BG31_835");
            ids.Add($"TOKEN_{i}");
            return Board(ids.ToArray());
        });
        var mechBoards = Enumerable.Range(0, 19).Select(_ => Board("BG24_022", "BG25_040"));
        string Comp(string id, int dataPoints, double placement, IEnumerable<string> boards) =>
            $"{{\"archetype\":\"{id}\",\"dataPoints\":{dataPoints},\"averagePlacement\":{placement.ToString(CultureInfo.InvariantCulture)}," +
            "\"averagePlacementAtMmr\":[],\"placementDistribution\":[],\"placementDistributionAtMmr\":[]," +
            $"\"heroStats\":[{{\"heroCardId\":\"TB_BaconShop_HERO_94\",\"dataPoints\":3,\"averagePlacement\":3.8,\"finalBoards\":[{string.Join(",", boards)}]}}]}}";
        return "{\"lastUpdateDate\":\"2026-09-26T14:10:43.130Z\",\"dataPoints\":108690,\"timePeriod\":\"last-patch\",\"compStats\":[" +
               Comp("undead_butcher", 6461, 3.807, undeadBoards) + "," + Comp("mech_glambot", 390, 4.44, mechBoards) + "]}";
    }

    [Fact]
    public void FirestoneImport_DerivesKeyPiecesFromFinalBoards()
    {
        var file = FirestoneCompImporter.Import(Payload(), Url, FetchedAt);

        Assert.Equal(StatsSources.Firestone, file.Source);
        Assert.Equal("last-patch", file.TimePeriod);
        Assert.Equal(FetchedAt, file.FetchedAt);
        var comp = Assert.Single(file.Compositions); // mech_glambot has 19 boards: skipped
        Assert.Equal("undead_butcher", comp.Id);
        Assert.Equal("Undead Butcher", comp.Name);
        Assert.Equal(new[] { "UNDEAD" }, comp.Tribes);
        Assert.Equal(new[] { "BG32_324", "BG25_010" }, comp.CoreCards); // 0.90 then 0.60, goldens merged
        Assert.Equal(new[] { "BG32_880" }, comp.AddonCards);           // 0.30; BG31_835 at 0.15 is out
        Assert.Equal(3.807, comp.AveragePlacement);
        Assert.Equal(6461, comp.DataPoints);
    }

    [Theory]
    [InlineData("mech_glambot", "MECHANICAL")]
    [InlineData("abberation_discard", "ABERRATION")]
    [InlineData("quilboar_choose_one", "QUILBOAR")]
    [InlineData("neutral_tea_set", null)]
    public void FirestoneImport_TribeComesFromTheArchetypePrefix(string archetype, string? tribe)
    {
        var boards = string.Join(",", Enumerable.Range(0, 20).Select(i => Board("BG24_022", $"TOKEN_{i}")));
        var payload = "{\"timePeriod\":\"last-patch\",\"compStats\":[{\"archetype\":\"" + archetype + "\",\"dataPoints\":250," +
                      "\"averagePlacement\":4.2,\"heroStats\":[{\"finalBoards\":[" + boards + "]}]}]}";

        var comp = Assert.Single(FirestoneCompImporter.Import(payload, Url, FetchedAt).Compositions);

        Assert.Equal(tribe == null ? Array.Empty<string>() : new[] { tribe }, comp.Tribes);
    }

    [Theory]
    [InlineData("{\"compStats\": [ {\"archetype\": ")]
    [InlineData("{\"lastUpdateDate\":\"2026-09-26\"}")]
    [InlineData("{\"compStats\": 7}")]
    public void FirestoneImport_MalformedPayload_IsRejected(string payload)
    {
        Assert.Throws<StatsFormatException>(() => FirestoneCompImporter.Import(payload, Url, FetchedAt));
    }

    [Theory]
    [InlineData("abberation_deathrattle", "Aberration Deathrattle")]
    [InlineData("mech_glambot", "Mech Glambot")]
    public void Humanize_TurnsArchetypeIdsIntoNames(string id, string expected)
    {
        Assert.Equal(expected, FirestoneCompImporter.Humanize(id));
    }

    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Drustfallen Butcher"] = "BG32_324",
        ["Handless Forsaken"] = "BG25_010",
        ["Friendly Geist"] = "BG32_880",
        ["Blade Collector"] = "BG26_817",
    };

    private const string ManualText = """
        # HSReplay comps, copied on 2026-09-26
        comp: Undead Butcher
        tribes: undead
        tier: A
        avg: 3,95
        core: Drustfallen Butcher, handless forsaken, BG28_309_G
        addon: Friendly Geist

        comp: Pirate Blades
        tribes: pirate, mech
        core: Blade Collector
        """;

    [Fact]
    public void HsReplayText_ParsesBlocks_NamesAndIds()
    {
        var file = HsReplayCompText.Parse(ManualText, n => Names.TryGetValue(n, out var id) ? id : null);

        Assert.Equal(StatsSources.HsReplayManual, file.Source);
        Assert.Equal(2, file.Compositions.Count);
        var undead = file.Compositions[0];
        Assert.Equal(("hsr-undead-butcher", "Undead Butcher", "A", 3.95), (undead.Id, undead.Name, undead.Tier, undead.AveragePlacement));
        Assert.Equal(new[] { "UNDEAD" }, undead.Tribes);
        Assert.Equal(new[] { "BG32_324", "BG25_010", "BG28_309" }, undead.CoreCards);
        Assert.Equal(new[] { "BG32_880" }, undead.AddonCards);
        var pirate = file.Compositions[1];
        Assert.Equal(new[] { "PIRATE", "MECHANICAL" }, pirate.Tribes);
        Assert.Equal(new[] { "BG26_817" }, pirate.CoreCards);
        Assert.Null(pirate.Tier);
    }

    [Theory]
    [InlineData("comp: X\ncore: Unknown Card", "line 2: unknown card \"Unknown Card\"")]
    [InlineData("comp: X\ntribes: undead", "line 1: comp \"X\" has no \"core:\" cards")]
    [InlineData("core: BG32_324", "line 1: \"core\" before any \"comp:\" line")]
    [InlineData("comp: X\ncore: BG32_324\ncolour: red", "line 3: unknown key \"colour\"")]
    [InlineData("comp: X\ntier: Z\ncore: BG32_324", "line 2: tier must be one of")]
    [InlineData("comp: X\ntribes: gnomes\ncore: BG32_324", "line 2: unknown tribe \"GNOMES\"")]
    public void HsReplayText_Errors_NameTheLine(string text, string expectedStart)
    {
        var e = Assert.Throws<StatsFormatException>(() => HsReplayCompText.Parse(text, n => Names.TryGetValue(n, out var id) ? id : null));
        Assert.StartsWith(expectedStart, e.Message);
    }

    [Fact]
    public void CompositionFile_RoundTrip_AndValidation()
    {
        var file = HsReplayCompText.Parse(ManualText, n => Names.TryGetValue(n, out var id) ? id : null);
        var copy = CompositionLoader.Parse(CompositionLoader.Serialize(file));
        Assert.Equal(file.Compositions.Select(c => (c.Id, c.Name, string.Join(",", c.CoreCards), string.Join(",", c.AddonCards))),
            copy.Compositions.Select(c => (c.Id, c.Name, string.Join(",", c.CoreCards), string.Join(",", c.AddonCards))));

        var json = CompositionLoader.Serialize(file);
        Assert.StartsWith("compositions[0]", Assert.Throws<StatsFormatException>(() =>
            CompositionLoader.Parse(json.Replace("\"BG32_880\"", "\"BG32_324\""))).Message);   // core and addon at once
        Assert.StartsWith("compositions[1].tribes", Assert.Throws<StatsFormatException>(() =>
            CompositionLoader.Parse(json.Replace("\"PIRATE\"", "\"PIRATES\""))).Message);
    }
}
