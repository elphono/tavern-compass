using System;
using System.Collections.Generic;
using System.Linq;
using BronzebeardHud.Stats;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using HdtEntity = Hearthstone_Deck_Tracker.Hearthstone.Entities.Entity;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// The only place that reads HDT's game model. Everything it hands over is a plain
/// <see cref="EntitySnapshot"/>, so the logic behind it stays testable without HDT.
/// </summary>
internal static class HdtEntityAdapter
{
    /// <summary>Battlegrounds hero selection is the game's mulligan step.</summary>
    public static bool IsHeroSelection(GameV2 game) =>
        game.IsBattlegroundsMatch
        && game.GameEntity is { } gameEntity
        && gameEntity.GetTag(GameTag.STEP) == (int)Step.BEGIN_MULLIGAN;

    /// <summary>
    /// The player's entities, or an empty list when HDT is mutating them at this very moment
    /// (its log reader runs on another thread): the next update will simply try again.
    /// </summary>
    public static IReadOnlyList<EntitySnapshot> PlayerEntities(GameV2 game)
    {
        try
        {
            return game.Player.PlayerEntities.Select(ToSnapshot).ToList();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<EntitySnapshot>();
        }
    }

    /// <summary>Battlegrounds shopping: hero picked, not in combat.</summary>
    public static bool IsShopPhase(GameV2 game) =>
        game.IsBattlegroundsMatch && game.IsBattlegroundsHeroPickingDone && !game.IsBattlegroundsCombatPhase;

    /// <summary>Minions on the player's board and every card in hand, golden copies mapped to their base card.</summary>
    public static IReadOnlyList<OwnedCard> OwnedCards(GameV2 game)
    {
        try
        {
            return game.Player.Board.Where(e => e.IsMinion)
                .Concat(game.Player.Hand)
                .Where(e => !string.IsNullOrEmpty(e.CardId))
                .Select(e => new OwnedCard(BaseCardId(e.CardId!), TribeOf(e)))
                .ToList();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<OwnedCard>();
        }
    }

    /// <summary>The minions Bob offers, left to right. In the shop, the opponent is Bob.</summary>
    public static IReadOnlyList<string> TavernCardIds(GameV2 game)
    {
        try
        {
            return game.Opponent.Board.Where(e => e.IsMinion && !string.IsNullOrEmpty(e.CardId))
                .OrderBy(e => e.GetTag(GameTag.ZONE_POSITION))
                .Select(e => BaseCardId(e.CardId!))
                .ToList();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Old-style golden ids (TB_BaconUps_…) through HearthDb; "_G" ids by <see cref="BronzebeardHud.Stats.CardIds"/>.</summary>
    private static string BaseCardId(string cardId) =>
        BronzebeardHud.Stats.CardIds.Normalize(HearthDb.Cards.TripleToNormalCardIds.TryGetValue(cardId, out var normal) ? normal : cardId);

    private static string? TribeOf(HdtEntity entity)
    {
        var race = (Race)entity.GetTag(GameTag.CARDRACE);
        return race == Race.INVALID ? null : race.ToString();
    }

    /// <summary>
    /// Lobby players (name and hero), as HDT already read them (GameMetaData.BattlegroundsLobbyInfo),
    /// the local player excluded. Empty until HDT has the lobby.
    /// </summary>
    public static IReadOnlyList<(string Name, string HeroCardId)> LobbyOpponents(GameV2 game)
    {
        try
        {
            var players = game.MetaData.BattlegroundsLobbyInfo?.Players;
            if (players == null)
            {
                return Array.Empty<(string, string)>();
            }

            var ownHero = game.Player.Board.FirstOrDefault(e => e.IsHero)?.CardId;
            return players
                .Where(p => !string.IsNullOrEmpty(p.Name) && !string.IsNullOrEmpty(p.HeroCardId) && p.HeroCardId != ownHero)
                .Select(p => (p.Name, p.HeroCardId))
                .ToList();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<(string, string)>();
        }
    }

    /// <summary>Hero card id → PLAYER_LEADERBOARD_PLACE, for every hero that has one.</summary>
    public static IReadOnlyDictionary<string, int> LeaderboardPlaces(GameV2 game)
    {
        try
        {
            var places = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var hero in game.Entities.Values.Where(e => e.IsHero && e.HasTag(GameTag.PLAYER_LEADERBOARD_PLACE) && !string.IsNullOrEmpty(e.CardId)))
            {
                places[hero.CardId!] = hero.GetTag(GameTag.PLAYER_LEADERBOARD_PLACE);
            }

            return places;
        }
        catch (InvalidOperationException)
        {
            return new Dictionary<string, int>();
        }
    }

    private static Dictionary<string, string>? _cardIdsByName;

    /// <summary>Card name (English or French, any case) → card id, Battlegrounds pool minions first.</summary>
    public static string? ResolveCardName(string name)
    {
        if (_cardIdsByName == null)
        {
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in HearthDb.Cards.BaconPoolMinions.Values.Concat(HearthDb.Cards.All.Values))
            {
                foreach (var cardName in new[] { card.Name, card.GetLocName(Locale.frFR) })
                {
                    if (!string.IsNullOrEmpty(cardName) && !index.ContainsKey(cardName))
                    {
                        index[cardName] = card.Id;
                    }
                }
            }

            _cardIdsByName = index;
        }

        return _cardIdsByName.TryGetValue(name.Trim(), out var id) ? id : null;
    }

    private static EntitySnapshot ToSnapshot(HdtEntity entity)
    {
        var tags = entity.Tags.ToDictionary(t => t.Key.ToString(), t => t.Value);
        var parentDbfId = entity.Card?.BattlegroundsSkinParentId ?? 0;
        var parentCardId = parentDbfId > 0 && HearthDb.Cards.AllByDbfId.TryGetValue(parentDbfId, out var parent)
            ? parent.Id
            : null;
        return new EntitySnapshot(entity.Id, entity.CardId, entity.IsHero, tags, parentCardId);
    }
}
