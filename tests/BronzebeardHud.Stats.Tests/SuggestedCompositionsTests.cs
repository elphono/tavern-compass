namespace BronzebeardHud.Stats.Tests;

public class SuggestedCompositionsTests
{
    private static Composition Comp(string id, double placement, params string[] keys) =>
        new(id, id.ToUpperInvariant(), Array.Empty<string>(), keys, Array.Empty<string>(), averagePlacement: placement);

    // Three reachable compositions with distinct placements, B the most advanced; D the best of all but unreachable.
    private static readonly Composition A = Comp("a", 3.1, "KEY_A1", "KEY_A2");
    private static readonly Composition B = Comp("b", 3.8, "KEY_B1", "KEY_B2");
    private static readonly Composition C = Comp("c", 3.5, "KEY_C1", "KEY_C2");
    private static readonly Composition D = Comp("d", 2.5, "KEY_D1");
    private static readonly Composition[] All = { A, B, C, D };
    private static readonly OwnedCard[] Held = { new("KEY_A1"), new("KEY_B1"), new("KEY_B2"), new("KEY_C1") };

    private static IEnumerable<string> Ids(IEnumerable<CompProgress> shown) => shown.Select(p => p.Composition.Id);

    [Fact]
    public void BestPlacementFirst_ProgressOnlyBreaksTies_UnreachableCompsAreNotSuggested()
    {
        Assert.Equal(new[] { "a", "c", "b" }, Ids(CompAdvisor.Suggest(Held, All, 3)));

        // Equal placement: the more advanced one first.
        var e = Comp("e", 3.5, "KEY_E1", "KEY_E2");
        var held = Held.Append(new OwnedCard("KEY_E1")).Append(new OwnedCard("KEY_E2")).ToList();
        Assert.Equal(new[] { "a", "e", "c", "b" }, Ids(CompAdvisor.Suggest(held, All.Append(e).ToList(), 8)));
    }

    [Theory]
    [InlineData(1, new[] { "a" })]
    [InlineData(3, new[] { "a", "c", "b" })]
    [InlineData(8, new[] { "a", "c", "b" })] // only three are reachable
    public void TheChosenNumberOfSuggestions(int count, string[] expected)
    {
        Assert.Equal(expected, Ids(TavernAdvisor.Aim(All, All, Held, null, count, null).Shown));
    }

    [Fact]
    public void ATickedCompStaysFirst_EvenWithAWorsePlacement()
    {
        Assert.Equal(new[] { "b", "a", "c" }, Ids(TavernAdvisor.Aim(All, All, Held, new[] { "b" }, 3, null).Shown));
    }

    [Fact]
    public void ThePlacementWithTheHeroBeingPlayed_WhenThereIsOne()
    {
        // With this hero, B is estimated at (90 × 2.8 + 30 × 3.8) / 120 = 3.05: better than A's 3.1.
        var b = new Composition("b", "B", Array.Empty<string>(), new[] { "KEY_B1", "KEY_B2" }, Array.Empty<string>(), averagePlacement: 3.8,
            heroStats: new[] { new CompHeroStat("HERO", 90, 2.8) });
        var all = new[] { A, b, C, D };
        Assert.Equal(new[] { "b", "a", "c" }, Ids(CompAdvisor.Suggest(Held, all, 3, heroEffects: HeroCompAffinity.Effects("HERO", all))));
        Assert.Equal(3.05, Math.Round(CompAdvisor.PlacementFor(b, HeroCompAffinity.Effects("HERO", all)), 6));
        Assert.Equal(3.8, CompAdvisor.PlacementFor(b, null));
    }

    [Fact]
    public void TheMarkersFollowTheShownComps()
    {
        var tavern = new[] { "KEY_A2", "KEY_C2", "KEY_D1" };
        var one = TavernAdvisor.Advise(tavern, Held, All, Array.Empty<string>(), suggested: 1);
        Assert.Equal(new[] { 1, 0, 0 }, one.Cards.Select(c => c.Advances.Count)); // only A is shown
        var three = TavernAdvisor.Advise(tavern, Held, All, Array.Empty<string>(), suggested: 3);
        Assert.Equal(new[] { 1, 1, 0 }, three.Cards.Select(c => c.Advances.Count)); // D is never reachable here
    }

    [Theory]
    [InlineData("{\"schema\": 1, \"suggestedCompositions\": 2}", 2, false)]
    [InlineData("{\"schema\": 1, \"suggestedCompositions\": 4}", 4, false)]
    [InlineData("{\"schema\": 1, \"suggestedCompositions\": 5}", 4, false)] // written when the bound was 8: one target per colour now
    [InlineData("{\"schema\": 1, \"suggestedCompositions\": 8}", 4, false)]
    [InlineData("{\"schema\": 1, \"suggestedCompositions\": 0}", 1, false)]
    [InlineData("{\"schema\": 1, \"suggestedCompositions\": 12}", 4, false)]
    [InlineData("{\"schema\": 1, \"suggestedCompositions\": \"five\"}", 3, true)]
    [InlineData("{ not json", 3, true)]
    [InlineData("", 3, false)]
    public void Settings_BoundsDefaultsAndErrors(string json, int expected, bool error)
    {
        var (settings, problem) = HudSettings.Parse(json);
        Assert.Equal(expected, settings.SuggestedCompositions);
        Assert.Equal(error, problem != null);
    }

    [Fact]
    public void Settings_RoundTrip_AndStepsStayInside1To4()
    {
        var settings = HudSettings.Default;
        Assert.Equal(3, settings.SuggestedCompositions);
        foreach (var expected in new[] { 4, 4, 4 })
        {
            settings = settings.WithSuggested(settings.SuggestedCompositions + 1);
            Assert.Equal(expected, HudSettings.Parse(settings.Serialize()).Settings.SuggestedCompositions);
        }

        for (var i = 0; i < 10; i++)
        {
            settings = settings.WithSuggested(settings.SuggestedCompositions - 1);
        }

        Assert.Equal(1, HudSettings.Parse(settings.Serialize()).Settings.SuggestedCompositions);
    }
}
