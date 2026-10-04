using Newtonsoft.Json;

namespace BronzebeardHud.Stats.Tests;

/// <summary>Synthetic comp guides only: invented names, invented dbf ids (9001…) and card ids (TST_…).</summary>
public class CompGuideParserTests
{
    /// <summary>dbf id → card id for the synthetic cards; 9999 is unknown to the card database.</summary>
    internal static string? CardIdOf(int dbfId) => dbfId switch
    {
        9001 => "TST_001",
        9002 => "TST_002",
        9003 => "TST_003",
        9004 => "TST_004",
        9005 => "TST_005_G", // a golden id: counted as its base card
        9006 => "TST_006",
        _ => null,
    };

    private static string Guide(string name, int tier, int tierRank, string core, string addon = "[]", string commit = "\"\"", string enablers = "\"\"") =>
        $"{{\"id\":1,\"name\":\"{name}\",\"tier\":{tier},\"tier_rank\":{tierRank},\"difficulty\":2,\"primary_tribe\":17," +
        $"\"core_cards\":{core},\"addon_cards\":{addon},\"representative_card\":\"TST_001\",\"how_to_play\":\"Play [[Alpha||9001]].\"," +
        $"\"when_to_commit\":{commit},\"common_enablers\":{enablers},\"summary\":\"\",\"previous_tier\":null," +
        "\"last_updated\":\"2026-09-29T16:37:23.773973Z\",\"hidden\":false}";

    [Fact]
    public void FreeList_GroupsByTierInHdtOrder_ThenByTierRank_ThenByName()
    {
        var json = "[" + string.Join(",",
            Guide("Zeta", 2, 0, "[9001]"),
            Guide("Beta", 1, 1, "[9002]"),
            Guide("Alpha", 1, 1, "[9003]"),
            Guide("Gamma", 1, 0, "[9004]"),
            Guide("Delta", 3, 0, "[9006]")) + "]";

        var set = CompGuideParser.Parse(json, CardIdOf, CompGuideSources.HdtFree);

        Assert.Equal(new[] { 1, 2, 3 }, set.Tiers.Select(t => t.Tier));
        Assert.Equal(new[] { "S", "A", "B" }, set.Tiers.Select(t => t.Letter));
        Assert.Equal(new[] { "Gamma", "Alpha", "Beta" }, set.Tiers[0].Guides.Select(g => g.Name)); // rank 0, then rank 1 by name
        Assert.Equal(new[] { "Gamma", "Alpha", "Beta", "Zeta", "Delta" }, set.All.Select(g => g.Name));
        Assert.Equal("S=3 A=1 B=1", set.TierSummary);
        Assert.Equal(5, set.Count);
        Assert.Equal(CompGuideSources.HdtFree, set.Source);
    }

    [Fact]
    public void Cards_AreResolvedFromDbfIds_GoldenMappedToBase_AddonsWithoutKeyPieces_UnknownOnesCounted()
    {
        var json = "[" + Guide("Alpha", 2, 0, "[9001, 9005, 9999]", addon: "[9001, 9002, 9002]",
            commit: "\"[[Alpha card||9001]] + [[Fifth card||9005]] + scaling\"",
            enablers: "\"[[Third card||9003]]\\nEarly gold ([[Second card||9002]]) [[No id]]\"") + "]";

        var guide = CompGuideParser.Parse(json, CardIdOf, CompGuideSources.HdtFree).All.Single();
        var set = CompGuideParser.Parse(json, CardIdOf, CompGuideSources.HdtFree);

        Assert.Equal(new[] { "TST_001", "TST_005" }, guide.CoreCards);
        Assert.Equal(new[] { "TST_002" }, guide.AddonCards);          // 9001 is a key piece; 9002 once
        Assert.Equal(new[] { "TST_001", "TST_005" }, guide.CommitCards);
        Assert.Equal(new[] { "TST_003", "TST_002" }, guide.Enablers);
        Assert.Equal("Alpha card + Fifth card + scaling", guide.WhenToCommit);
        Assert.Equal("Third card\nEarly gold (Second card) No id", guide.CommonEnablers);
        Assert.Equal("Play Alpha.", guide.HowToPlay);
        Assert.Equal((2, 0, 2, 17, "TST_001"), (guide.Tier, guide.TierRank, guide.Difficulty, guide.PrimaryTribe, guide.RepresentativeCard));
        Assert.Equal(1, set.UnknownCards); // 9999
    }

    [Fact]
    public void Tier7List_TakesTheTierFromItsKey_InAscendingOrder()
    {
        var json = "{\"by_tier\": {\"3\": [" + Guide("Gamma", 1, 0, "[9003]") + "], \"1\": [" +
                   Guide("Beta", 2, 1, "[9002]") + "," + Guide("Alpha", 2, 0, "[9001]") + "]}}";

        var set = CompGuideParser.Parse(json, CardIdOf, CompGuideSources.HdtTier7);

        Assert.Equal(new[] { 1, 3 }, set.Tiers.Select(t => t.Tier));
        Assert.Equal(new[] { "Alpha", "Beta" }, set.Tiers[0].Guides.Select(g => g.Name));
        Assert.Equal(3, set.Tiers[1].Guides.Single().Tier); // the key wins over the guide's own field, as HDT groups them
    }

    [Theory]
    [InlineData("{ not json", "invalid JSON")]
    [InlineData("42", "expected a list")]
    [InlineData("[{\"tier\":1,\"core_cards\":[9001]}]", "#1: name")]
    [InlineData("[{\"name\":\"A\",\"core_cards\":[9001]}]", "\"A\": tier is required")]
    [InlineData("[{\"name\":\"A\",\"tier\":\"S\",\"core_cards\":[9001]}]", "\"A\": tier must be an integer")]
    [InlineData("[{\"name\":\"A\",\"tier\":1}]", "\"A\": core_cards is required")]
    [InlineData("[{\"name\":\"A\",\"tier\":1,\"core_cards\":[\"BG1\"]}]", "\"A\": core_cards must be a list of card dbf ids")]
    [InlineData("[{\"name\":\"A\",\"tier\":1,\"core_cards\":[9001],\"when_to_commit\":3}]", "\"A\": when_to_commit must be a string")]
    [InlineData("{\"by_tier\": {\"S\": []}}", "by_tier key \"S\"")]
    public void AGuideThatBreaksARule_RejectsTheWholeList_NamingIt(string json, string expected)
    {
        var error = Assert.Throws<StatsFormatException>(() => CompGuideParser.Parse(json, CardIdOf, CompGuideSources.HdtFree));

        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void AnEmptyList_IsAnEmptySet()
    {
        var set = CompGuideParser.Parse("[]", CardIdOf, CompGuideSources.HdtFree);

        Assert.Equal(0, set.Count);
        Assert.Empty(set.Tiers);
    }

    /// <summary>
    /// The shape of HSReplay.Responses.BattlegroundsCompGuide as decompiled from HDT 1.58.6's HSReplay.dll (same fields in
    /// 1.55.6): the plugin hands these objects over as they are, and the parser reads them through their JSON attributes.
    /// </summary>
    private sealed class HdtLikeGuide
    {
        [JsonProperty("name")] public string Name { get; set; } = string.Empty;
        [JsonProperty("tier")] public int Tier { get; set; }
        [JsonProperty("tier_rank")] public int TierRank { get; set; }
        [JsonProperty("difficulty")] public int Difficulty { get; set; }
        [JsonProperty("primary_tribe")] public int PrimaryTribe { get; set; }
        [JsonProperty("representative_card")] public string? RepresentativeCard { get; set; }
        [JsonProperty("core_cards")] public List<int>? CoreCards { get; set; }
        [JsonProperty("addon_cards")] public List<int>? AddonCards { get; set; }
        [JsonProperty("how_to_play")] public string? HowToPlay { get; set; }
        [JsonProperty("when_to_commit")] public string? WhenToCommit { get; set; }
        [JsonProperty("common_enablers")] public string? CommonEnablers { get; set; }
        [JsonProperty("last_updated")] public DateTime LastUpdated { get; set; }
    }

    [Fact]
    public void HdtObjects_FreeAndTier7_ReadLikeTheirJson()
    {
        var alpha = new HdtLikeGuide { Name = "Alpha", Tier = 2, TierRank = 1, CoreCards = new() { 9001, 9002 }, AddonCards = new() { 9003 }, CommonEnablers = "[[Fourth||9004]]" };
        var beta = new HdtLikeGuide { Name = "Beta", Tier = 1, CoreCards = new() { 9006 }, AddonCards = null, WhenToCommit = null };
        var gamma = new HdtLikeGuide { Name = "Gamma", Tier = 2, TierRank = 0, CoreCards = new() { 9004 } };

        var free = CompGuideParser.FromObjects(new object[] { alpha, beta, gamma }, CardIdOf, CompGuideSources.HdtFree);
        var tier7 = CompGuideParser.FromTiers(new[]
        {
            new KeyValuePair<int, IEnumerable<object>>(2, new object[] { alpha, gamma }),
            new KeyValuePair<int, IEnumerable<object>>(1, new object[] { beta }),
        }, CardIdOf, CompGuideSources.HdtTier7);

        Assert.Equal(new[] { "Beta", "Gamma", "Alpha" }, free.All.Select(g => g.Name));
        Assert.Equal(new[] { "Beta", "Gamma", "Alpha" }, tier7.All.Select(g => g.Name));
        var a = free.All.Single(g => g.Name == "Alpha");
        Assert.Equal(new[] { "TST_001", "TST_002" }, a.CoreCards);
        Assert.Equal(new[] { "TST_003" }, a.AddonCards);
        Assert.Equal(new[] { "TST_004" }, a.Enablers);
        Assert.Empty(free.All.Single(g => g.Name == "Beta").AddonCards); // a null list is an empty one
        Assert.Null(free.All.Single(g => g.Name == "Beta").WhenToCommit);
    }

    [Fact]
    public void Text_UnclosedReferenceStaysText_RepeatedCardListedOnce()
    {
        var parts = CompGuideText.Parse("[[One||9001]] then [[One again||9001]] and [[open", CardIdOf);

        Assert.Equal("One then One again and [[open", parts.PlainText);
        Assert.Equal(new[] { "TST_001" }, parts.CardIds);
        Assert.Null(CompGuideText.Parse("  ", CardIdOf).PlainText);
    }
}
