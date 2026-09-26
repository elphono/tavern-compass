using static BronzebeardHud.Stats.Tests.TestData;

namespace BronzebeardHud.Stats.Tests;

public class LobbyTribesTests
{
    // Hero at 4.2 over 1000 games. DEMON (40 games, not > 1000/20) and PIRATE (5 games without the
    // tribe, not > 200/20) are too thin and must be ignored.
    private static readonly HeroStat Hero = new("BG22_HERO_004", 4.2, 1000, tribeImpacts: new[]
    {
        new TribeImpact("UNDEAD", -0.30, 400, 500),
        new TribeImpact("MURLOC", 0.20, 300, 350),
        new TribeImpact("DEMON", -0.50, 40, 900),
        new TribeImpact("PIRATE", 0.10, 200, 5),
    });

    [Theory]
    [InlineData(new[] { "UNDEAD", "MURLOC", "DEMON", "PIRATE", "BEAST" }, 4.1)]
    [InlineData(new[] { "UNDEAD", "BEAST", "DRAGON", "MECHANICAL", "NAGA" }, 3.9)]
    [InlineData(new[] { "MURLOC", "BEAST", "DRAGON", "MECHANICAL", "NAGA" }, 4.4)]
    public void AdjustedPlacement_AddsTheImpactsOfTheLobbysTribes(string[] lobby, double expected)
    {
        Assert.Equal(expected, LobbyTribes.AdjustedPlacement(Hero, lobby)!.Value, precision: 10);
    }

    [Fact]
    public void AdjustedPlacement_OnlyThinFiguresForThisLobby_DropsTheHero()
    {
        Assert.Null(LobbyTribes.AdjustedPlacement(Hero, new[] { "DEMON", "PIRATE", "BEAST", "DRAGON", "ELEMENTAL" }));
    }

    [Fact]
    public void AdjustedPlacement_AllTribesOrUnknownLobby_LeavesThePlacement()
    {
        Assert.Equal(4.2, LobbyTribes.AdjustedPlacement(Hero, Tribes.All.ToArray()));
        Assert.Equal(4.2, LobbyTribes.AdjustedPlacement(Hero, Array.Empty<string>()));
    }

    [Fact]
    public void Apply_AdjustsDropsAndKeepsOrder_ButLeavesAFileWithoutTribeFigures()
    {
        var other = new HeroStat("TB_BaconShop_HERO_39", 3.8, 800, tribeImpacts: new[] { new TribeImpact("BEAST", 0.25, 300, 400) });
        var noFigure = new HeroStat("BG31_HERO_802", 5.1, 600, tribeImpacts: new[] { new TribeImpact("NAGA", 0.05, 100, 200) });
        var file = new HeroStatsFile(StatsSources.Firestone, new[] { Hero, other, noFigure }, mmrPercentile: 25);

        var adjusted = LobbyTribes.Apply(file, new[] { "UNDEAD", "BEAST", "DRAGON", "MECHANICAL", "MURLOC" });

        Assert.Equal(new[] { ("BG22_HERO_004", 4.1), ("TB_BaconShop_HERO_39", 4.05) },
            adjusted.Heroes.Select(h => (h.HeroCardId, Math.Round(h.AveragePlacement, 10))));
        Assert.Equal(25, adjusted.MmrPercentile);

        var manual = new HeroStatsFile(StatsSources.HsReplayManual, new[] { new HeroStat("BG22_HERO_004", 3.7, 0) });
        Assert.Same(manual, LobbyTribes.Apply(manual, new[] { "UNDEAD", "BEAST", "DRAGON", "MECHANICAL", "MURLOC" }));
    }

    [Fact]
    public void Import_MapsFirestoneTribeStats_AndTheLoaderRoundTripsThem()
    {
        var tribeStats = "[{\"tribe\":11,\"dataPoints\":479,\"dataPointsOnMissingTribe\":646,\"impactAveragePosition\":0.01}," +
                         "{\"tribe\":126,\"dataPoints\":210,\"dataPointsOnMissingTribe\":300,\"impactAveragePosition\":-0.12}," +
                         "{\"tribe\":999,\"dataPoints\":10,\"dataPointsOnMissingTribe\":10,\"impactAveragePosition\":0.5}]";
        var payload = FirestonePayload(FirestoneHero("TB_BaconShop_HERO_39", 4.29, 1125, 13291, 1121)
            .Replace("\"mmrPercentile\"", "\"tribeStats\":" + tribeStats + ",\"mmrPercentile\""));

        var hero = FirestoneHeroStatsImporter.Import(payload, "https://example.invalid/", DateTimeOffset.UnixEpoch).Heroes.Single();

        Assert.Equal(new[] { ("UNDEAD", 0.01, 479, 646), ("ABERRATION", -0.12, 210, 300) },
            hero.TribeImpacts.Select(i => (i.Tribe, i.Impact, i.DataPoints, i.DataPointsOnMissingTribe)));
        var copy = HeroStatsLoader.Parse(HeroStatsLoader.Serialize(new HeroStatsFile(StatsSources.Firestone, new[] { hero }))).Heroes.Single();
        Assert.Equal(hero.TribeImpacts.Select(i => (i.Tribe, i.Impact)), copy.TribeImpacts.Select(i => (i.Tribe, i.Impact)));
    }

    [Fact]
    public void Loader_UnknownTribeInImpacts_IsRejected()
    {
        var json = "{\"schema\":1,\"source\":\"firestone\",\"heroes\":[{\"heroCardId\":\"BG22_HERO_004\",\"averagePlacement\":3.6,\"dataPoints\":640," +
                   "\"tribeImpacts\":[{\"tribe\":\"GNOME\",\"impact\":0.1,\"dataPoints\":10,\"dataPointsOnMissingTribe\":10}]}]}";
        Assert.StartsWith("heroes[0].tribeImpacts", Assert.Throws<StatsFormatException>(() => HeroStatsLoader.Parse(json)).Message);
    }
}
