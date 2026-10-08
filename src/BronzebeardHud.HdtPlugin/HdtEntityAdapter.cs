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

    /// <summary>The Battlegrounds tribe name by value, never Race.ToString() alone: 20 is both BEAST and PET (GuideTribes.NameOrEnum).</summary>
    private static string? TribeOf(HdtEntity entity)
    {
        var race = (Race)entity.GetTag(GameTag.CARDRACE);
        return race == Race.INVALID ? null : GuideTribes.NameOrEnum((int)race, race.ToString());
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
    /// What the opponent's gauge reads (OpponentPower): the turn, the player's own hero (only logged), the PLAYER_ID of the
    /// player fought in this combat — the hero in play that game.Opponent controls, as HDT's BattlegroundsBoardState finds it
    /// —, the player's NEXT_OPPONENT_PLAYER_ID, each player's hero as the leaderboard shows it, and HDT's last known board of
    /// those two (GameV2.GetBattlegroundsBoardStateFor, snapshotted when a combat begins: TagChangeActions
    /// .OnBattlegroundsCombatSetupChange), with how each was asked (OpponentBoards.Read: said in the log line). Decompiled
    /// from HDT 1.58.9 (BattlegroundsBoardState.SnapshotCurrentBoard, GetSnapshot): the snapshot is filed by PLAYER_ID and
    /// holds the minions only, never the hero, which OpponentBoards takes from the entities (the leaderboard's).
    /// HDT mutating its entities meanwhile throws InvalidOperationException: the feature guard skips that update.
    /// </summary>
    public static OpponentFacts OpponentFacts(GameV2 game, OverlayPhase phase)
    {
        var entities = game.Entities.Values.ToList();
        var combatId = 0;
        if (phase == OverlayPhase.Combat)
        {
            var foe = entities.FirstOrDefault(e => e.IsHero && e.IsInZone(Zone.PLAY) && e.IsControlledBy(game.Opponent.Id));
            combatId = foe?.GetTag(GameTag.PLAYER_ID) ?? 0;
        }

        var nextId = game.PlayerEntity?.GetTag(GameTag.NEXT_OPPONENT_PLAYER_ID) ?? 0;
        var heroes = entities
            .Where(e => e.IsHero && !string.IsNullOrEmpty(e.CardId) && e.GetTag(GameTag.PLAYER_ID) > 0)
            .Select(e => new HeroEntity(e.Id, e.GetTag(GameTag.PLAYER_ID), BaseHeroId(e), e.HasTag(GameTag.PLAYER_LEADERBOARD_PLACE)))
            .ToList();

        var boards = new Dictionary<int, BoardSeen>();
        var reads = new Dictionary<int, string>();
        foreach (var id in new[] { combatId, nextId }.Where(id => id > 0).Distinct())
        {
            var read = OpponentBoards.Read(id, heroes, entityId => Snapshot(game, entityId));
            reads[id] = read.Probe;
            if (read.Board != null)
            {
                boards[id] = read.Board;
            }
        }

        return new OpponentFacts(phase, game.GetTurnNumber(), PlayerHeroId(game), combatId, nextId, boards, OpponentBoards.Leaderboard(heroes), reads);
    }

    /// <summary>HDT's last known board of the player whose hero is <paramref name="entityId"/> (any hero of that PLAYER_ID); null if none.</summary>
    private static SnapshotRead? Snapshot(GameV2 game, int entityId)
    {
        var snapshot = game.GetBattlegroundsBoardStateFor(entityId);
        if (snapshot?.Entities == null)
        {
            return null;
        }

        return new SnapshotRead(snapshot.Turn, snapshot.Entities.Length,
            snapshot.Entities.Where(e => e.IsMinion).Select(e => (e.GetTag(GameTag.ATK), e.GetTag(GameTag.HEALTH))).ToList());
    }

    /// <summary>A hero's base id, skins mapped to their parent (HeroIdNormalizer), as the stats are keyed.</summary>
    private static string BaseHeroId(HdtEntity hero)
    {
        var snapshot = ToSnapshot(hero);
        return HeroIdNormalizer.Normalize(snapshot.CardId!, snapshot.ParentCardId);
    }

    /// <summary>A hero's English name from HearthDb ("no curve for Rakanishu"); its id when HearthDb does not know it.</summary>
    public static string HeroName(string heroCardId) =>
        HearthDb.Cards.All.TryGetValue(heroCardId, out var card) && !string.IsNullOrEmpty(card.Name) ? card.Name : heroCardId;

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

    /// <summary>
    /// Tribes in this lobby as HDT knows them (BattlegroundsUtils.GetAvailableRaces); empty when unknown. Named by value
    /// (GuideTribes.NameOrEnum): Race 20 is both BEAST and PET, and a lobby read as "PET" would drop every beast guide.
    /// </summary>
    public static IReadOnlyList<string> LobbyTribeNames()
    {
        var races = BattlegroundsUtils.GetAvailableRaces();
        return races == null
            ? Array.Empty<string>()
            : races.Select(r => GuideTribes.NameOrEnum((int)r, r.ToString())).Where(Tribes.All.Contains).Distinct().OrderBy(r => r, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// A card's tribes from HearthDb (Card.Race and Card.SecondaryRace, a dual type's second tribe), by value
    /// (GuideTribes.NameOrEnum: "BEAST", never "PET"), "ALL" for an amalgam; empty for a card without tribe; null for a card
    /// HearthDb does not know (LobbyGuides never holds an unknown card against a guide). For LobbyGuides: which key cards can
    /// show up in this lobby.
    /// </summary>
    /// <summary>The player's tavern tier (PLAYER_TECH_LEVEL on their hero); 0 when unknown.</summary>
    public static int PlayerTavernTier(GameV2 game)
    {
        try
        {
            return game.Player.Board.FirstOrDefault(e => e.IsHero && !string.IsNullOrEmpty(e.CardId))?.GetTag(GameTag.PLAYER_TECH_LEVEL) ?? 0;
        }
        catch (Exception e) when (e is InvalidOperationException or NullReferenceException)
        {
            return 0; // the board changed while it was read
        }
    }

    /// <summary>Battlegrounds' minion pool as HearthDb knows it: id, tavern tier (HDT's Database), tribes.</summary>
    public static IReadOnlyList<(string Id, int Tier, IReadOnlyCollection<string>? Tribes)> BaconPoolMinions() =>
        HearthDb.Cards.BaconPoolMinions.Keys
            .Select(id => (Id: id, Tier: Database.GetCardFromId(id)?.TechLevel ?? 0, Tribes: CardTribes(id)))
            .ToList();

    public static IReadOnlyCollection<string>? CardTribes(string cardId)
    {
        if (!HearthDb.Cards.All.TryGetValue(cardId, out var card))
        {
            return null;
        }

        return new[] { card.Race, card.SecondaryRace }
            .Where(r => r != Race.INVALID && r != Race.BLANK)
            .Select(r => GuideTribes.NameOrEnum((int)r, r.ToString()))
            .Distinct(StringComparer.Ordinal)
            .ToList();
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
