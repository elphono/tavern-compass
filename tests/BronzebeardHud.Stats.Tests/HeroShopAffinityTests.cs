namespace BronzebeardHud.Stats.Tests;

public class HeroShopAffinityTests
{
    // Three compositions equally advanced (one key piece held in each), ranked by placement without a hero: X, Y, Z.
    private static readonly Composition X = new("x_comp", "X Comp", Array.Empty<string>(), new[] { "KEY_X" }, Array.Empty<string>(), averagePlacement: 3.8,
        heroStats: new[] { new CompHeroStat("HERO_B", 50, 4.9), new CompHeroStat("HERO_C", 8, 1.0) });
    private static readonly Composition Y = new("y_comp", "Y Comp", Array.Empty<string>(), new[] { "KEY_Y" }, Array.Empty<string>(), averagePlacement: 3.9);
    private static readonly Composition Z = new("z_comp", "Z Comp", Array.Empty<string>(), new[] { "KEY_Z" }, Array.Empty<string>(), averagePlacement: 4.0,
        heroStats: new[] { new CompHeroStat("HERO_A", 60, 2.8), new CompHeroStat("HERO_C", 9, 1.5) });
    private static readonly Composition Lonely = new("w_comp", "W Comp", Array.Empty<string>(), new[] { "KEY_W" }, Array.Empty<string>(), averagePlacement: 4.5,
        heroStats: new[] { new CompHeroStat("HERO_A", 60, 1.2) });
    private static readonly Composition[] All = { X, Y, Z, Lonely };
    private static readonly OwnedCard[] OneKeyEach = { new("KEY_X"), new("KEY_Y"), new("KEY_Z") };

    private static IEnumerable<string> Order(string? hero) =>
        CompAdvisor.Rank(OneKeyEach, All, heroEffects: HeroCompAffinity.Effects(hero, All)).Select(t => t.Composition.Id);

    [Fact]
    public void ThreeHeroes_ReorderThreeEquallyAdvancedComps()
    {
        Assert.Equal(new[] { "x_comp", "y_comp", "z_comp" }, Order(null));

        // Hero A: Z much better with it, (60 × 2.8 + 30 × 4.0) / 90 = 3.2, a gain of 0.8 place, +1.6 points.
        Assert.Equal(new[] { "z_comp", "x_comp", "y_comp" }, Order("HERO_A"));
        var z = CompAdvisor.Rank(OneKeyEach, All, heroEffects: HeroCompAffinity.Effects("HERO_A", All))[0];
        Assert.Equal((4.6, 1.6), (Math.Round(z.Score, 6), Math.Round(z.HeroBonus, 6)));

        // Hero B: X worse with it, (50 × 4.9 + 30 × 3.8) / 80 = 4.4875, a loss of 0.6875 place.
        Assert.Equal(new[] { "y_comp", "z_comp", "x_comp" }, Order("HERO_B"));

        // Hero C: brilliant on X and Z, but on 8 and 9 games only: no effect at all.
        Assert.Empty(HeroCompAffinity.Effects("HERO_C", All));
        Assert.Equal(new[] { "x_comp", "y_comp", "z_comp" }, Order("HERO_C"));
    }

    [Fact]
    public void TheLargestHeroEffectMeasured_NeverOutweighsAKeyPiece()
    {
        // 0.76 place, the largest hero effect on last-patch (2026-09-26): (65 × 2.8892 + 30 × 4.0) / 95 ≈ 3.24.
        var q = new Composition("q_comp", "Q Comp", Array.Empty<string>(), new[] { "KEY_Q" }, Array.Empty<string>(), averagePlacement: 4.0,
            heroStats: new[] { new CompHeroStat("HERO_D", 65, 2.8892) });
        var p = new Composition("p_comp", "P Comp", Array.Empty<string>(), new[] { "KEY_P1", "KEY_P2" }, Array.Empty<string>(), averagePlacement: 4.5);
        var owned = new[] { new OwnedCard("KEY_Q"), new OwnedCard("KEY_P1"), new OwnedCard("KEY_P2") };

        var effects = HeroCompAffinity.Effects("HERO_D", new[] { q, p });
        Assert.Equal(0.76, Math.Round(effects["q_comp"].Gain, 2));
        Assert.Equal(new[] { "p_comp", "q_comp" }, CompAdvisor.Rank(owned, new[] { q, p }, heroEffects: effects).Select(t => t.Composition.Id));
    }

    [Fact]
    public void TheHeroAloneNeverMakesATarget()
    {
        // W is by far the best composition for hero A, but nothing of it is held.
        Assert.DoesNotContain("w_comp", Order("HERO_A"));
    }

    [Fact]
    public void ThePanelLineShowsTheEstimateWithTheHero()
    {
        var effects = HeroCompAffinity.Effects("HERO_A", All);
        var rows = CompositionRows.Build(CompAdvisor.Suggest(OneKeyEach, All, 3, heroEffects: effects), OneKeyEach, heroEffects: effects);
        Assert.Equal("≈ 3,2 with your hero (60)", rows[0].HeroEffect!.ShopText);
        Assert.Null(rows[1].HeroEffect);
    }
}
