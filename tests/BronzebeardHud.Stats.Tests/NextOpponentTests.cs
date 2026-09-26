namespace BronzebeardHud.Stats.Tests;

public class NextOpponentTests
{
    private static EntitySnapshot Hero(int id, string cardId, int playerId, int place, int health = 30, int damage = 0) =>
        new(id, cardId, isHero: true, new Dictionary<string, int>
        {
            ["PLAYER_ID"] = playerId, ["PLAYER_LEADERBOARD_PLACE"] = place, ["HEALTH"] = health, ["DAMAGE"] = damage,
        });

    private static readonly EntitySnapshot[] Lobby =
    {
        Hero(101, "BG22_HERO_004", playerId: 3, place: 2),
        Hero(102, "TB_BaconShop_HERO_39", playerId: 5, place: 6, health: 30, damage: 31),
        Hero(103, "BG31_HERO_802", playerId: 7, place: 4),
        // Player 7's hero was replaced mid-game: the newer entity (higher id) is the one that counts.
        Hero(140, "BG31_HERO_802t", playerId: 7, place: 3),
        new(104, "BG28_300", isHero: false, new Dictionary<string, int> { ["PLAYER_ID"] = 3, ["PLAYER_LEADERBOARD_PLACE"] = 8 }),
    };

    [Fact]
    public void Find_TheHeroOfTheNextOpponent_WithItsTile()
    {
        var next = NextOpponent.Find(3, Lobby)!;
        Assert.Equal((2, "BG22_HERO_004", false), (next.LeaderboardPlace, next.HeroCardId, next.IsGhost));
    }

    [Fact]
    public void Find_ADeadOpponent_IsAGhostFight()
    {
        var next = NextOpponent.Find(5, Lobby)!;
        Assert.Equal((6, true), (next.LeaderboardPlace, next.IsGhost));
    }

    [Fact]
    public void Find_TheMostRecentHeroEntityWins()
    {
        Assert.Equal((3, "BG31_HERO_802t"), (NextOpponent.Find(7, Lobby)!.LeaderboardPlace, NextOpponent.Find(7, Lobby)!.HeroCardId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4)]
    public void Find_UnknownOrAbsentId_GivesNothing(int nextId)
    {
        Assert.Null(NextOpponent.Find(nextId, Lobby));
    }

    [Fact]
    public void LeaderboardTile_StacksEightSquareTilesFromTheTop()
    {
        const double width = 2291, height = 1360;
        var tile = 0.69 / 8 * height;
        for (var place = 1; place <= 8; place++)
        {
            var rect = LeaderboardLayout.Tile(width, height, place);
            Assert.Equal(0.15 * height + tile * (place - 1), rect.Top, precision: 6);
            Assert.Equal((tile, tile), (rect.Width, rect.Height));
            Assert.True(rect.Right < LeaderboardLayout.MmrLabel(width, height, place).Left, "MMR label overlaps the tile");
        }
    }
}
