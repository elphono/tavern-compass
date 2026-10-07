namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// Reading an opponent's last board from HDT (2026-10-07: in a whole game, not one board was ever read). HDT 1.58.9 files its
/// snapshot under a PLAYER_ID and keeps the minions only, never the hero (BattlegroundsBoardState.SnapshotCurrentBoard); the
/// plugin looked for the hero in the snapshot. Several heroes carry one PLAYER_ID, each with its own entity id and card id,
/// so that the one asked and the one measured against show.
/// </summary>
public class OpponentBoardsTests
{
    // Player 3: the leaderboard's tile (entity 40) and, in play during a combat against a ghost, Kel'Thuzad carrying the
    // same PLAYER_ID (entity 12: a lower id, so that "the first by entity id" is not "the leaderboard's"). Player 5: its tile
    // (entity 41). Player 7: in play only (entity 55), no tile.
    private static readonly HeroEntity Lead3 = new(40, 3, "TB_LEAD3", onLeaderboard: true);
    private static readonly HeroEntity Ghost3 = new(12, 3, "TB_GHOST", onLeaderboard: false);
    private static readonly HeroEntity Lead5 = new(41, 5, "TB_LEAD5", onLeaderboard: true);
    private static readonly HeroEntity InPlay7 = new(55, 7, "TB_PLAY7", onLeaderboard: false);
    private static readonly IReadOnlyList<HeroEntity> Heroes = new[] { Ghost3, Lead3, Lead5, InPlay7 };

    // Minions only, as HDT keeps them: 22 for player 3 at turn 8, 40 for player 5 at turn 6, 7 for player 7 at turn 4.
    private static readonly IReadOnlyDictionary<int, SnapshotRead> Snapshots = new Dictionary<int, SnapshotRead>
    {
        [3] = new(8, 4, new[] { (2, 3), (4, 1), (5, 5), (1, 1) }),
        [5] = new(6, 2, new[] { (10, 10), (10, 10) }),
        [7] = new(4, 1, new[] { (3, 4) }),
    };

    /// <summary>
    /// GameV2.GetBattlegroundsBoardStateFor as HDT 1.58.9 answers it (BattlegroundsBoardState.GetSnapshot): the entity looked
    /// up by id, null when unknown; then the snapshot filed under its PLAYER_ID, null when none. Every entity id asked is kept.
    /// </summary>
    private static Func<int, SnapshotRead?> Hdt(List<int> asked, IReadOnlyDictionary<int, SnapshotRead>? snapshots = null) => entityId =>
    {
        asked.Add(entityId);
        var entity = Heroes.FirstOrDefault(h => h.EntityId == entityId);
        return entity != null && (snapshots ?? Snapshots).TryGetValue(entity.PlayerId, out var snapshot) ? snapshot : null;
    };

    [Fact]
    public void ABoardOfMinionsOnly_AsHdtKeepsIt_IsRead_AgainstTheLeaderboardsHero_NotTheGhostInPlay()
    {
        var asked = new List<int>();

        var read = OpponentBoards.Read(3, Heroes, Hdt(asked));

        Assert.NotNull(read.Board);
        Assert.Equal(("TB_LEAD3", 8, 22, 4), (read.Board!.HeroCardId, read.Board.Turn, read.Board.Stats, read.Board.Minions.Count));
        Assert.Equal(new[] { 40 }, asked);
        Assert.Equal("heroes 12,40 asked 40 → turn 8, 4 entities, 4 minions", read.Probe);
    }

    [Fact]
    public void EachPlayerId_ItsOwnBoard()
    {
        var asked = new List<int>();

        var five = OpponentBoards.Read(5, Heroes, Hdt(asked));
        var seven = OpponentBoards.Read(7, Heroes, Hdt(asked));

        Assert.Equal(("TB_LEAD5", 6, 40), (five.Board!.HeroCardId, five.Board.Turn, five.Board.Stats));
        Assert.Equal("heroes 41 asked 41 → turn 6, 2 entities, 2 minions", five.Probe);
        // No leaderboard tile: the hero that carries the PLAYER_ID, whatever it is.
        Assert.Equal(("TB_PLAY7", 4, 7), (seven.Board!.HeroCardId, seven.Board.Turn, seven.Board.Stats));
        Assert.Equal(new[] { 41, 55 }, asked);
    }

    [Fact]
    public void NeverFought_NoBoard_AndTheLogSaysHdtHadNone()
    {
        var asked = new List<int>();
        var onlyThree = new Dictionary<int, SnapshotRead> { [3] = Snapshots[3] };

        var read = OpponentBoards.Read(5, Heroes, Hdt(asked, onlyThree));

        Assert.Null(read.Board);
        Assert.Equal("heroes 41 asked 41 → no snapshot", read.Probe);
        Assert.Equal(new[] { 41 }, asked);
    }

    [Fact]
    public void NoHeroCarriesThePlayerId_NothingAsked()
    {
        var asked = new List<int>();

        var read = OpponentBoards.Read(6, Heroes, Hdt(asked));

        Assert.Null(read.Board);
        Assert.Equal("no hero entity", read.Probe);
        Assert.Empty(asked);
    }

    [Fact]
    public void TheEntityToAsk_TheLeaderboardsTile_ElseTheFirstByEntityId()
    {
        Assert.Same(Lead3, OpponentBoards.Pick(Heroes, 3));
        Assert.Same(InPlay7, OpponentBoards.Pick(Heroes, 7));
        Assert.Same(Ghost3, OpponentBoards.Pick(new[] { new HeroEntity(30, 3, "TB_OTHER", false), Ghost3 }, 3));
        Assert.Null(OpponentBoards.Pick(Heroes, 6));
    }

    [Fact]
    public void TheLeaderboard_TilesOnly_NeverTheGhostInPlay()
    {
        Assert.Equal(new Dictionary<int, string> { [3] = "TB_LEAD3", [5] = "TB_LEAD5" }, OpponentBoards.Leaderboard(Heroes));
    }
}
