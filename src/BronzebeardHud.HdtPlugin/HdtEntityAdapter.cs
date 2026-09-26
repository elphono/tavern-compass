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
