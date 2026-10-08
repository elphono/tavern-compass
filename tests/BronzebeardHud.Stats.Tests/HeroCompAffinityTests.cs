namespace BronzebeardHud.Stats.Tests;

public class HeroCompAffinityTests
{
    private static Composition Comp(string id, string name, string tribe, double placement, params CompHeroStat[] heroes) =>
        new(id, name, new[] { tribe }, new[] { $"{id}_KEY" }, Array.Empty<string>(), averagePlacement: placement, heroStats: heroes);

    // Hero H: a good raw average on few games (A), a steadier one on many games (B), and a pair under the minimum (C).
    private static readonly Composition A = Comp("undead_a", "Undead A", "UNDEAD", 4.2,
        new CompHeroStat("HERO_H", 12, 2.5), new CompHeroStat("HERO_G", 15, 4.5));
    private static readonly Composition B = Comp("pirate_b", "Pirate B", "PIRATE", 3.6, new CompHeroStat("HERO_H", 40, 3.3));
    private static readonly Composition C = Comp("mech_c", "Mech C", "MECHANICAL", 3.9, new CompHeroStat("HERO_H", 9, 1.8));
    private static readonly Composition[] All = { A, B, C };

    /// <summary>Each comp's estimate for a hero, pulled towards the comp's average; small samples left out; an unknown hero gets nothing.</summary>
    [Fact]
    public void Effects_PullEachPairTowardsTheComp_SmallSamplesLeftOut_UnknownHeroGetsNothing()
    {
        var h = HeroCompAffinity.Effects("HERO_H", All);
        // A: (12 × 2.5 + 30 × 4.2) / 42 = 3.714…; B: (40 × 3.3 + 30 × 3.6) / 70 = 3.428…; C: 9 games, left out.
        Assert.Equal(new[] { ("pirate_b", 3.428571, 40), ("undead_a", 3.714286, 12) },
            h.Values.OrderBy(p => p.Estimate).Select(p => (p.Composition.Id, Math.Round(p.Estimate, 6), p.Games)));

        Assert.Equal(4.3, HeroCompAffinity.Effects("HERO_G", All)["undead_a"].Estimate, precision: 9); // (15 × 4.5 + 30 × 4.2) / 45

        Assert.Empty(HeroCompAffinity.Effects("HERO_K", All));
    }

    [Fact]
    public void Import_KeepsEachHerosGamesAndPlacement_DropsImpossibleOnes_AndTheCacheKeepsThem()
    {
        string Minion(string id, int position) => $"{{\"cardID\":\"{id}\",\"tags\":{{\"ZONE_POSITION\":{position}}}}}";
        var boards = string.Join(",", Enumerable.Range(0, 20).Select(i => $"{{\"mmr\":7000,\"finalComp\":{{\"board\":[{Minion("BG32_324", 1)},{Minion($"TOKEN_{i}", 2)}]}}}}"));
        var payload = "{\"timePeriod\":\"last-patch\",\"compStats\":[{\"archetype\":\"undead_butcher\",\"dataPoints\":6461,\"averagePlacement\":3.8,\"heroStats\":[" +
                      $"{{\"heroCardId\":\"BG27_HERO_801\",\"dataPoints\":23,\"averagePlacement\":2.51,\"finalBoards\":[{boards}]}}," +
                      "{\"heroCardId\":\"TB_BaconShop_HERO_94\",\"dataPoints\":49,\"averagePlacement\":3.87,\"finalBoards\":[]}," +
                      "{\"heroCardId\":\"BG20_HERO_100\",\"dataPoints\":12,\"averagePlacement\":0,\"finalBoards\":[]}]}]}";

        var comp = FirestoneCompImporter.Import(payload, "https://example.invalid/", DateTimeOffset.UnixEpoch).Compositions.Single();

        Assert.Equal(new[] { ("BG27_HERO_801", 23, 2.51), ("TB_BaconShop_HERO_94", 49, 3.87) }, comp.HeroStats.Select(h => (h.HeroCardId, h.DataPoints, h.AveragePlacement)));
        var copy = CompositionLoader.Parse(CompositionLoader.Serialize(new CompositionFile(StatsSources.Firestone, new[] { comp }))).Compositions.Single();
        Assert.Equal(comp.HeroStats.Select(h => (h.HeroCardId, h.DataPoints, h.AveragePlacement)), copy.HeroStats.Select(h => (h.HeroCardId, h.DataPoints, h.AveragePlacement)));
    }
}
