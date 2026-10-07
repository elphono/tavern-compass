namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// The combat's opponent kept until the shop (Ali, 2026-10-07). In HDT's log, at the end of every combat, no hero in play is
/// controlled by the opponent any more while the phase is still combat ("scope=combat id=0", 28 of 28 combats). Two opponents
/// with distinct heroes, curves and boards, so that which one is kept shows.
/// </summary>
public class CombatOpponentKeeperTests
{
    private const string Mine = "TB_ME";
    private const string Three = "TB_THREE";
    private const string One = "TB_ONE";

    /// <summary>Three: 220 at turn 8. One: 250 at turn 9.</summary>
    private static readonly IReadOnlyList<HeroStatsFile> Sources = new[]
    {
        new HeroStatsFile(StatsSources.Firestone, new[]
        {
            new HeroStat(Three, 4.0, 1000, warbandCurve: new[] { new WarbandPoint(2, 6), new WarbandPoint(8, 220) }),
            new HeroStat(One, 4.0, 1000, warbandCurve: new[] { new WarbandPoint(2, 6), new WarbandPoint(9, 250) }),
        }),
    };

    private static readonly IReadOnlyDictionary<int, string> Heroes = new Dictionary<int, string> { [3] = Three, [1] = One };

    private static string Name(string id) => id;

    /// <summary>Player 3 at turn 8: 160. Player 1 at turn 9: 300.</summary>
    private static readonly BoardSeen Board3 = new(Three, 8, new[] { (2, 3), (4, 1), (150, 0) });
    private static readonly BoardSeen Board1 = new(One, 9, new[] { (100, 100), (50, 50) });

    private static OpponentFacts Fight(int turn, int id, BoardSeen board) =>
        new(OverlayPhase.Combat, turn, Mine, id, 0, new Dictionary<int, BoardSeen> { [id] = board }, Heroes,
            new Dictionary<int, string> { [id] = $"heroes {id} asked {id} → turn {turn}, {board.Minions.Count} entities, {board.Minions.Count} minions" });

    /// <summary>The end of a combat, as HDT leaves it: no hero in play for the opponent; the next opponent (1) already said.</summary>
    private static OpponentFacts Lost(int turn) =>
        new(OverlayPhase.Combat, turn, Mine, 0, 1, new Dictionary<int, BoardSeen>(), Heroes);

    private static OpponentFacts Shop(int turn, int next) =>
        new(OverlayPhase.Shop, turn, Mine, 0, next, new Dictionary<int, BoardSeen>(), Heroes);

    private static string Line(OpponentFacts facts) => OpponentPower.LogLine(facts, OpponentPower.Compare(facts, Sources, Name));

    [Fact]
    public void TheEndOfACombat_KeepsItsOpponent_TheShopForgetsIt_TheNextCombatKeepsItsOwn()
    {
        var keeper = new CombatOpponentKeeper();

        // 1. Combat of turn 8 against player 3, then the end of it: player 3 kept, its gauge as it was.
        var fight3 = Fight(8, 3, Board3);
        Assert.Same(fight3, keeper.Observe(fight3));
        var kept3 = keeper.Observe(Lost(8));
        var power3 = OpponentPower.Compare(kept3, Sources, Name);
        Assert.Equal((true, 3), (kept3.Kept, kept3.CombatOpponentId));
        Assert.Equal((BoardPower.Behind, "Opp. 160 · their hero avg 220 at turn 8"), (power3.Power, power3.Details));
        Assert.Equal("Bronzebeard HUD: opponent power scope=combat id=3 (kept) hero=TB_THREE (yours TB_ME) turn=8 seen=8 board=160 (3 minions) "
                     + "read=[heroes 3 asked 3 → turn 8, 3 entities, 3 minions] · Opp. 160 · their hero avg 220 at turn 8 · −27% power=behind",
            Line(kept3));
        // Every further tick of the same end of combat: the same line, so no new one in the log.
        Assert.Equal(Line(kept3), Line(keeper.Observe(Lost(8))));

        // 2. The shop of turn 9, next opponent 1: the shop's facts, nothing kept; a combat whose opponent is not found yet
        // keeps nothing either (never player 3 again).
        var shop = Shop(9, 1);
        Assert.Same(shop, keeper.Observe(shop));
        var early = keeper.Observe(Lost(9));
        Assert.Equal((false, 0), (early.Kept, early.CombatOpponentId));
        Assert.Equal("Opp. – not known yet", OpponentPower.Compare(early, Sources, Name).Details);

        // 3. Combat of turn 9 against player 1, then the end of it: player 1 kept, not player 3.
        var fight1 = Fight(9, 1, Board1);
        Assert.Same(fight1, keeper.Observe(fight1));
        var kept1 = keeper.Observe(Lost(9));
        var power1 = OpponentPower.Compare(kept1, Sources, Name);
        Assert.Equal((true, 1), (kept1.Kept, kept1.CombatOpponentId));
        Assert.Equal((BoardPower.Even, "Opp. 300 · their hero avg 250 at turn 9"), (power1.Power, power1.Details));
        Assert.StartsWith("Bronzebeard HUD: opponent power scope=combat id=1 (kept) hero=TB_ONE ", Line(kept1));
    }

    [Fact]
    public void AShop_ForgetsTheOpponent_EvenAtTheSameTurn()
    {
        var keeper = new CombatOpponentKeeper();
        keeper.Observe(Fight(8, 3, Board3));
        keeper.Observe(Shop(8, 1));

        var after = keeper.Observe(Lost(8));

        Assert.Equal((false, 0), (after.Kept, after.CombatOpponentId));
    }

    [Fact]
    public void ACombatOfAnotherTurn_KeepsNothing_EvenWithoutAShopSeenBetween()
    {
        var keeper = new CombatOpponentKeeper();
        keeper.Observe(Fight(8, 3, Board3));

        var later = keeper.Observe(Lost(10));

        Assert.Equal((false, 0, 10), (later.Kept, later.CombatOpponentId, later.Turn));
    }

    [Fact]
    public void ACombatWithAnOpponentFound_ReplacesTheKeptOne_AndForgetEmptiesIt()
    {
        var keeper = new CombatOpponentKeeper();
        keeper.Observe(Fight(8, 3, Board3));
        keeper.Observe(Fight(8, 1, Board1)); // the same turn, another opponent found: it replaces player 3

        Assert.Equal(1, keeper.Observe(Lost(8)).CombatOpponentId);
        keeper.Forget();
        Assert.Equal((false, 0), (keeper.Observe(Lost(8)).Kept, keeper.Observe(Lost(8)).CombatOpponentId));
        Assert.DoesNotContain("(kept)", Line(Fight(8, 3, Board3)));
    }
}
