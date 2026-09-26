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

    /// <summary>
    /// The options of the player's pending choice, in on-screen order, with what the advice needs: card type
    /// and English text from HearthDb, tribe, Dark Gift tag (HDT: OverlayWindow.xaml.cs:1714) and zone.
    /// </summary>
    public static IReadOnlyList<OfferedOption> OfferedOptions(GameV2 game)
    {
        try
        {
            var ids = game.Player.OfferedEntityIds.ToList();
            var entities = ChoiceOrder.Arrange(ids, game.Player.OfferedEntities.ToList(), e => e.Id);
            return entities.Select(e =>
            {
                var cardId = e.CardId ?? string.Empty;
                HearthDb.Cards.All.TryGetValue(cardId, out var card);
                var isTrinket = card?.Type == CardType.BATTLEGROUND_TRINKET;
                return new OfferedOption(
                    e.Id,
                    cardId.Length == 0 ? cardId : BaseCardId(cardId),
                    card?.Type.ToString() ?? "INVALID",
                    TribeOf(e),
                    e.GetTag(GameTag.DARK_GIFT_ENTITY) > 0,
                    e.GetTag(GameTag.ZONE),
                    isTrinket ? card!.GetLocText(Locale.enUS) : null);
            }).ToList();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<OfferedOption>();
        }
    }

    /// <summary>The player's NEXT_OPPONENT_PLAYER_ID, read where HDT reads it: on the player entity.</summary>
    public static int NextOpponentPlayerId(GameV2 game) => game.PlayerEntity?.GetTag(GameTag.NEXT_OPPONENT_PLAYER_ID) ?? 0;

    /// <summary>Every hero entity of the game.</summary>
    public static IReadOnlyList<EntitySnapshot> Heroes(GameV2 game)
    {
        try
        {
            return game.Entities.Values.Where(e => e.IsHero).Select(ToSnapshot).ToList();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<EntitySnapshot>();
        }
    }

    /// <summary>The phase the tavern overlay cares about.</summary>
    public static OverlayPhase Phase(GameV2 game)
    {
        if (game.IsInMenu || !game.IsBattlegroundsMatch)
        {
            return OverlayPhase.OutOfGame;
        }

        if (IsHeroSelection(game))
        {
            return OverlayPhase.HeroSelection;
        }

        if (!game.IsBattlegroundsHeroPickingDone)
        {
            return OverlayPhase.OutOfGame;
        }

        return game.IsBattlegroundsCombatPhase ? OverlayPhase.Combat : OverlayPhase.Shop;
    }

    /// <summary>
    /// Minions on the player's board and every card in hand, golden copies mapped to their base card; kept
    /// apart so that the diagnostic line can tell how many of each HDT handed over.
    /// </summary>
    public static PlayerCards PlayerCards(GameV2 game)
    {
        try
        {
            OwnedCard Owned(HdtEntity e) => new(BaseCardId(e.CardId!), TribeOf(e));
            var board = game.Player.Board.Where(e => e.IsMinion && !string.IsNullOrEmpty(e.CardId)).Select(Owned).ToList();
            var hand = game.Player.Hand.Where(e => !string.IsNullOrEmpty(e.CardId)).Select(Owned).ToList();
            return new PlayerCards(board, hand);
        }
        catch (InvalidOperationException)
        {
            return BronzebeardHud.Stats.PlayerCards.None;
        }
    }

    /// <summary>
    /// Bob's row, left to right: his minions and the tavern spell, which the game lays out in the same row
    /// (see <see cref="TavernRow"/>). In the shop, the opponent is Bob.
    /// </summary>
    public static IReadOnlyList<TavernSlot> TavernRow(GameV2 game)
    {
        try
        {
            return BronzebeardHud.Stats.TavernRow.Arrange(game.Opponent.Board
                .Where(e => !string.IsNullOrEmpty(e.CardId)
                            && (e.IsMinion || e.GetTag(GameTag.CARDTYPE) == (int)CardType.BATTLEGROUND_SPELL))
                .Select(e => new TavernSlot(e.Id, e.IsMinion ? BaseCardId(e.CardId!) : e.CardId!, e.IsMinion, e.GetTag(GameTag.ZONE_POSITION)))
                .ToList());
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<TavernSlot>();
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

    /// <summary>The player's hero as a base hero id (skins mapped to their parent); null before one is picked.</summary>
    public static string? PlayerHeroId(GameV2 game)
    {
        try
        {
            var hero = game.Player.Board.FirstOrDefault(e => e.IsHero && !string.IsNullOrEmpty(e.CardId));
            if (hero == null)
            {
                return null;
            }

            var snapshot = ToSnapshot(hero);
            return HeroIdNormalizer.Normalize(snapshot.CardId!, snapshot.ParentCardId);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Attack and health of each minion on the player's board.</summary>
    public static IReadOnlyList<(int Attack, int Health)> BoardMinionStats(GameV2 game)
    {
        try
        {
            return game.Player.Board.Where(e => e.IsMinion)
                .Select(e => (e.GetTag(GameTag.ATK), e.GetTag(GameTag.HEALTH)))
                .ToList();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<(int, int)>();
        }
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

    /// <summary>Tribes in this lobby as HDT knows them (BattlegroundsUtils.GetAvailableRaces); empty when unknown.</summary>
    public static IReadOnlyList<string> LobbyTribeNames()
    {
        var races = BattlegroundsUtils.GetAvailableRaces();
        return races == null
            ? Array.Empty<string>()
            : races.Select(r => r.ToString()).Where(Tribes.All.Contains).OrderBy(r => r, StringComparer.Ordinal).ToList();
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
