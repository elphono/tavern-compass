using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>Where the next opponent sits on the in-game leaderboard.</summary>
public sealed class NextOpponentTile
{
    public NextOpponentTile(int leaderboardPlace, string heroCardId, bool isGhost)
    {
        LeaderboardPlace = leaderboardPlace;
        HeroCardId = heroCardId;
        IsGhost = isGhost;
    }

    public int LeaderboardPlace { get; }
    public string HeroCardId { get; }

    /// <summary>The opponent is already dead: the fight is against its ghost.</summary>
    public bool IsGhost { get; }
}

public static class NextOpponent
{
    /// <summary>
    /// The hero whose PLAYER_ID is the player's NEXT_OPPONENT_PLAYER_ID (HDT reads that tag on the
    /// player entity: LogReader/Handlers/TagChangeActions.cs:1679-1683) and that has a leaderboard place.
    /// When several hero entities share the id (a hero replaced during the game), the most recent wins.
    /// Null when the id is unknown or no such hero is on the leaderboard.
    /// </summary>
    public static NextOpponentTile? Find(int nextOpponentPlayerId, IEnumerable<EntitySnapshot> heroes)
    {
        if (nextOpponentPlayerId <= 0)
        {
            return null;
        }

        var hero = heroes
            .Where(h => h.IsHero && !string.IsNullOrEmpty(h.CardId))
            .Where(h => h.GetTag("PLAYER_ID") == nextOpponentPlayerId && h.GetTag("PLAYER_LEADERBOARD_PLACE") > 0)
            .OrderByDescending(h => h.Id)
            .FirstOrDefault();
        if (hero == null)
        {
            return null;
        }

        var isGhost = hero.GetTag("HEALTH") - hero.GetTag("DAMAGE") <= 0;
        return new NextOpponentTile(hero.GetTag("PLAYER_LEADERBOARD_PLACE"), hero.CardId!, isGhost);
    }
}
