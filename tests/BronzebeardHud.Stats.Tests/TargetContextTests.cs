using static BronzebeardHud.Stats.Tests.BridgeTestData;
using static BronzebeardHud.Stats.Tests.GuideTestData;

namespace BronzebeardHud.Stats.Tests;

public class TargetContextTests
{
    private static readonly CompGuide Undead = BridgeFixture.Undead;

    // Average placement 3.8 (never shown); HERO_H: 23 games at 2.9 → (23 × 2.9 + 30 × 3.8) / 53 = 3.409…; HERO_K: 9 games,
    // under the minimum. Final turns 12 14 13 15 11: median 13.
    private static readonly CompHeroStat[] Heroes = { new("HERO_H", 23, 2.9), new("HERO_K", 9, 1.0) };

    private static Composition WithBoards(params int?[] turns) => Comp("undead_ctx", new[] { "U1", "U2", "U3" }, addons: new[] { "UA" }, games: 1500,
        placement: 3.8, heroes: Heroes, boards: turns.Select(t => Board(t, "U1", "U2")).ToArray());

    private static IReadOnlyDictionary<string, GuideEvidence> BridgeTo(Composition comp) => GuideBridge.For(Set(Undead), new[] { comp });

    [Fact]
    public void EveryPiece_HeroThenFinalTurnThenBoards_NeverTheCompsOwnPlacement()
    {
        var comp = WithBoards(12, 14, 13, 15, 11);

        var text = TargetContext.For(Undead, BridgeTo(comp), HeroCompAffinity.Effects("HERO_H", new[] { comp }));

        Assert.Equal("≈ 3,4 with your hero (23) · final turn ≈ 13 · 5 top boards", text);
        Assert.DoesNotContain("3,8", text);
    }

    [Fact]
    public void EachPieceIsLeftOutWhenItIsMissing()
    {
        var comp = WithBoards(12, 14, null, 15);
        var bridge = BridgeTo(comp);

        Assert.Equal("final turn ≈ 14 · 4 top boards", TargetContext.For(Undead, bridge));                       // no hero effects
        Assert.Equal("final turn ≈ 14 · 4 top boards", TargetContext.For(Undead, bridge, HeroCompAffinity.Effects("HERO_K", new[] { comp }))); // too few games
        Assert.Equal("≈ 3,4 with your hero (23) · 1 top board", TargetContext.For(Undead, BridgeTo(WithBoards(new int?[] { null })),
            HeroCompAffinity.Effects("HERO_H", new[] { WithBoards(new int?[] { null }) })));                           // a board without turn

        var noBoards = WithBoards();
        Assert.Equal("≈ 3,4 with your hero (23)", TargetContext.For(Undead, BridgeTo(noBoards), HeroCompAffinity.Effects("HERO_H", new[] { noBoards })));
        Assert.Null(TargetContext.For(Undead, BridgeTo(noBoards)));
    }

    [Fact]
    public void AGuideWithoutABridge_HasNoContext()
    {
        var comp = WithBoards(12);

        Assert.Null(TargetContext.For(BridgeFixture.Pirates, BridgeTo(comp), HeroCompAffinity.Effects("HERO_H", new[] { comp })));
        Assert.Null(TargetContext.For(Undead, null, HeroCompAffinity.Effects("HERO_H", new[] { comp })));
    }

    [Fact]
    public void TheFinalTurnIsCompDetailsMedian()
    {
        var comp = WithBoards(12, 14, 13, 15);

        Assert.Equal(13.5, CompDetail.MedianFinalTurn(comp));
        Assert.Equal(CompDetail.For(comp, _ => null).TypicalFinalTurn, CompDetail.MedianFinalTurn(comp));
        Assert.Equal("final turn ≈ 13,5 · 4 top boards", TargetContext.For(Undead, BridgeTo(comp)));
    }
}
