using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// A game entity reduced to what the stats logic needs, so that this library never depends on
/// HDT. Tags are keyed by their GameTag name (e.g. <c>BACON_SKIN</c>).
/// </summary>
public sealed class EntitySnapshot
{
    public EntitySnapshot(int id, string? cardId, bool isHero, IReadOnlyDictionary<string, int> tags, string? parentCardId = null)
    {
        Id = id;
        CardId = cardId;
        IsHero = isHero;
        Tags = tags;
        ParentCardId = parentCardId;
    }

    public int Id { get; }
    public string? CardId { get; }
    public bool IsHero { get; }
    public IReadOnlyDictionary<string, int> Tags { get; }

    /// <summary>Base hero declared by the card data for a skin, when known.</summary>
    public string? ParentCardId { get; }

    public int GetTag(string name) => Tags.TryGetValue(name, out var value) ? value : 0;

    /// <summary>Same meaning as HDT's Entity.HasTag: the tag is set to a positive value.</summary>
    public bool HasTag(string name) => GetTag(name) > 0;
}

/// <summary>A hero the player can pick, in on-screen order.</summary>
public sealed class OfferedHero
{
    public OfferedHero(int entityId, string cardId, string baseCardId, int position)
    {
        EntityId = entityId;
        CardId = cardId;
        BaseCardId = baseCardId;
        Position = position;
    }

    public int EntityId { get; }

    /// <summary>The card id as offered, possibly a skin.</summary>
    public string CardId { get; }

    /// <summary>The id stats are keyed by.</summary>
    public string BaseCardId { get; }

    /// <summary>ZONE_POSITION of the entity, i.e. left to right on the hero picker.</summary>
    public int Position { get; }
}

public static class OfferedHeroes
{
    /// <summary>
    /// The heroes currently offered, with HDT's own rule (GameEventHandler.RefreshBattlegroundsHeroPickStats):
    /// a hero entity of the player carrying BACON_HERO_CAN_BE_DRAFTED or BACON_SKIN, and not
    /// BACON_LOCKED_MULLIGAN_HERO. Ordered as they appear on screen.
    /// </summary>
    public static IReadOnlyList<OfferedHero> Select(IEnumerable<EntitySnapshot> playerEntities) =>
        playerEntities
            .Where(e => e.IsHero && !string.IsNullOrEmpty(e.CardId))
            .Where(e => e.HasTag("BACON_HERO_CAN_BE_DRAFTED") || e.HasTag("BACON_SKIN"))
            .Where(e => !e.HasTag("BACON_LOCKED_MULLIGAN_HERO"))
            .Select(e => new OfferedHero(
                e.Id, e.CardId!, HeroIdNormalizer.Normalize(e.CardId!, e.ParentCardId), e.GetTag("ZONE_POSITION")))
            .OrderBy(h => h.Position)
            .ThenBy(h => h.EntityId)
            .ToList();
}
