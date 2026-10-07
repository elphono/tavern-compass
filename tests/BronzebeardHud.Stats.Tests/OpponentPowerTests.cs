namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// The opponent's board against THEIR hero's average (2026-10-06), never the player's: two synthetic curves that put the same
/// board on different levels, so that a gauge computed on the wrong hero shows. No Firestone data in the repository.
/// </summary>
public class OpponentPowerTests
{
    private const string Mine = "TB_ME";
    private const string Theirs = "TB_THEM";
    private const string Uncharted = "TB_NOCURVE";

    private static HeroStat Hero(string id, int games, params (int Turn, double Average)[] curve) =>
        new(id, 4.0, games, warbandCurve: curve.Select(p => new WarbandPoint(p.Turn, p.Average)).ToList());

    /// <summary>Mine: 35 at turn 5, 120 at turn 8. Theirs: 50 at turn 5, 220 at turn 8, 400 at turn 9. The third hero has none.</summary>
    private static readonly IReadOnlyList<HeroStatsFile> Sources = new[]
    {
        new HeroStatsFile(StatsSources.Firestone, new[]
        {
            Hero(Mine, 1000, (2, 6), (5, 35), (8, 120), (9, 216)),
            Hero(Theirs, 1000, (2, 6), (5, 50), (8, 220), (9, 400)),
        }),
    };

    private static string Name(string id) => id switch
    {
        Theirs => "Rakanishu",
        Uncharted => "Ozumat",
        _ => id,
    };

    /// <summary>Minions whose attack plus health sum to <paramref name="stats"/> (three of them, the last taking the rest).</summary>
    private static IReadOnlyList<(int Attack, int Health)> Minions(int stats) => new[] { (2, 3), (4, 1), (stats - 10, 0) };

    private static OpponentFacts Facts(OverlayPhase phase, int turn, int combat = 0, int next = 0, IReadOnlyDictionary<int, BoardSeen>? boards = null,
        IReadOnlyDictionary<int, string>? heroes = null) =>
        new(phase, turn, Mine, combat, next, boards ?? new Dictionary<int, BoardSeen>(), heroes ?? new Dictionary<int, string>());

    [Fact]
    public void InCombat_TheBoardBeingFought_AgainstTheirHerosAverage_NotYours()
    {
        var facts = Facts(OverlayPhase.Combat, 8, combat: 5, boards: new Dictionary<int, BoardSeen> { [5] = new(Theirs, 8, Minions(160)) });

        var power = OpponentPower.Compare(facts, Sources, Name);

        Assert.Equal(220, power.Average);                     // theirs; yours is 120 (+33%, even)
        Assert.Equal(BoardPower.Behind, power.Power);         // 160 / 220
        Assert.Equal("−27%", power.Percent);
        Assert.Equal("Opp. 160 · their hero avg 220 at turn 8", power.Details);
        Assert.Equal("Opp. 160 · their hero avg 220 at turn 8 · −27%", power.Line);
        Assert.Null(power.Note);
    }

    [Fact]
    public void TheirHeroWithoutCurve_GreyAndNamed_NeverYourCurveInstead()
    {
        var facts = Facts(OverlayPhase.Combat, 8, combat: 5, boards: new Dictionary<int, BoardSeen> { [5] = new(Uncharted, 8, Minions(160)) });

        var power = OpponentPower.Compare(facts, Sources, Name);

        Assert.Equal(BoardPower.None, power.Power);
        Assert.Null(power.Average);
        Assert.Null(power.Percent);
        Assert.Equal("Opp. 160 · no curve for Ozumat", power.Details);
    }

    [Fact]
    public void InTheShop_TheNextOpponent_OnTheLastBoardSeen_AgainstTheirAverageAtTheTurnItWasSeen()
    {
        var facts = Facts(OverlayPhase.Shop, 9, next: 5, boards: new Dictionary<int, BoardSeen> { [5] = new(Theirs, 5, Minions(70)) });

        var power = OpponentPower.Compare(facts, Sources, Name);

        Assert.Equal(50, power.Average);                      // theirs at turn 5; theirs at turn 9 (400) would read behind, yours (35) shiny
        Assert.Equal(BoardPower.Ahead, power.Power);          // 70 / 50
        Assert.Equal("Next opp. 70 at turn 5 · their hero avg 50", power.Details);
        Assert.Equal("+40%", power.Percent);
    }

    [Fact]
    public void WithoutABoard_GreyWithTheReason()
    {
        var neverFought = OpponentPower.Compare(Facts(OverlayPhase.Shop, 4, next: 6, heroes: new Dictionary<int, string> { [6] = Theirs }), Sources, Name);
        var unknownNext = OpponentPower.Compare(Facts(OverlayPhase.Shop, 1), Sources, Name);
        var unknownFoe = OpponentPower.Compare(Facts(OverlayPhase.Combat, 3), Sources, Name);
        var stale = OpponentPower.Compare(Facts(OverlayPhase.Combat, 8, combat: 5, boards: new Dictionary<int, BoardSeen> { [5] = new(Theirs, 6, Minions(90)) }), Sources, Name);
        var outOfGame = OpponentPower.Compare(Facts(OverlayPhase.HeroSelection, 1), Sources, Name);

        Assert.All(new[] { neverFought, unknownNext, unknownFoe, stale, outOfGame }, p =>
        {
            Assert.Equal(BoardPower.None, p.Power);
            Assert.Null(p.Percent);
            Assert.Null(p.Average);
        });
        Assert.Equal("Next opp. Rakanishu – not fought yet", neverFought.Details);
        Assert.Equal("Next opp. – not known yet", unknownNext.Details);
        Assert.Equal("Opp. – not known yet", unknownFoe.Details);
        Assert.Equal("Opp. – board not read yet", stale.Details); // a board of turn 6 is not this combat's
        Assert.Equal("Opp. – not in a shop or a combat", outOfGame.Details);
    }

    [Fact]
    public void TheRulesOfYourOwnGauge_TooEarly_FewGames_CurveThatFalls_HoldForTheirsToo()
    {
        var thin = new[] { new HeroStatsFile(StatsSources.Firestone, new[] { Hero(Theirs, 78, (8, 220)) }) };
        var falling = new[] { new HeroStatsFile(StatsSources.Firestone, new[] { Hero(Theirs, 1000, (15, 6300), (16, 7400), (17, 6200)) }) };
        OpponentFacts Fight(int turn, int stats) => Facts(OverlayPhase.Combat, turn, combat: 5, boards: new Dictionary<int, BoardSeen> { [5] = new(Theirs, turn, Minions(stats)) });

        var early = OpponentPower.Compare(Fight(2, 12), Sources, Name);
        var few = OpponentPower.Compare(Fight(8, 400), thin, Name);
        var fallen = OpponentPower.Compare(Fight(17, 9000), falling, Name);
        var noTurn = OpponentPower.Compare(Fight(12, 300), Sources, Name);

        Assert.Equal(("too early", BoardPower.None), (early.Note, early.Power));
        Assert.Equal(("few games (78)", BoardPower.None), (few.Note, few.Power));
        Assert.Equal(("curve falls after turn 16", BoardPower.None), (fallen.Note, fallen.Power));
        Assert.Equal(("Opp. 300 · no average for turn 12", BoardPower.None), (noTurn.Details, noTurn.Power));
        Assert.Equal("power=none (too early)", early.PowerText);
    }

    [Fact]
    public void ABoardReadFromHdt_MinionsOnly_FeedsTheGauge_AgainstTheLeaderboardsHero_AndTheLogSaysHowItWasAsked()
    {
        // Player 5's tile is Theirs (entity 40); in play, a ghost without curve carries the same PLAYER_ID (entity 12). HDT's
        // snapshot of player 5 holds three minions (160) and no hero; player 6's tile (entity 41) has none.
        var heroes = new[] { new HeroEntity(12, 5, Uncharted, false), new HeroEntity(40, 5, Theirs, true), new HeroEntity(41, 6, Theirs, true) };
        SnapshotRead? Hdt(int entityId) =>
            heroes.FirstOrDefault(h => h.EntityId == entityId)?.PlayerId == 5 ? new SnapshotRead(8, 3, Minions(160)) : null;
        var five = OpponentBoards.Read(5, heroes, Hdt);
        var six = OpponentBoards.Read(6, heroes, Hdt);
        var fight = new OpponentFacts(OverlayPhase.Combat, 8, Mine, 5, 6, new Dictionary<int, BoardSeen> { [5] = five.Board! },
            OpponentBoards.Leaderboard(heroes), new Dictionary<int, string> { [5] = five.Probe, [6] = six.Probe });
        var shop = new OpponentFacts(OverlayPhase.Shop, 9, Mine, 0, 6, new Dictionary<int, BoardSeen>(),
            OpponentBoards.Leaderboard(heroes), new Dictionary<int, string> { [6] = six.Probe });

        var power = OpponentPower.Compare(fight, Sources, Name);

        Assert.Equal((BoardPower.Behind, "Opp. 160 · their hero avg 220 at turn 8"), (power.Power, power.Details));
        Assert.Equal("Bronzebeard HUD: opponent power scope=combat id=5 hero=TB_THEM (yours TB_ME) turn=8 seen=8 board=160 (3 minions) "
                     + "read=[heroes 12,40 asked 40 → turn 8, 3 entities, 3 minions] · Opp. 160 · their hero avg 220 at turn 8 · −27% power=behind",
            OpponentPower.LogLine(fight, power));
        Assert.Equal("Bronzebeard HUD: opponent power scope=next id=6 hero=TB_THEM (yours TB_ME) turn=9 seen=none "
                     + "read=[heroes 41 asked 41 → no snapshot] · Next opp. Rakanishu – not fought yet power=none",
            OpponentPower.LogLine(shop, OpponentPower.Compare(shop, Sources, Name)));
    }

    [Fact]
    public void LogLine_SaysTheMeasureItRestsOn()
    {
        var fight = Facts(OverlayPhase.Combat, 8, combat: 5, boards: new Dictionary<int, BoardSeen> { [5] = new(Theirs, 8, Minions(160)) });
        var next = Facts(OverlayPhase.Shop, 4, next: 6, heroes: new Dictionary<int, string> { [6] = Theirs });

        Assert.Equal("Bronzebeard HUD: opponent power scope=combat id=5 hero=TB_THEM (yours TB_ME) turn=8 seen=8 board=160 (3 minions) · "
                     + "Opp. 160 · their hero avg 220 at turn 8 · −27% power=behind",
            OpponentPower.LogLine(fight, OpponentPower.Compare(fight, Sources, Name)));
        Assert.Equal("Bronzebeard HUD: opponent power scope=next id=6 hero=TB_THEM (yours TB_ME) turn=4 seen=none · Next opp. Rakanishu – not fought yet power=none",
            OpponentPower.LogLine(next, OpponentPower.Compare(next, Sources, Name)));
    }
}
