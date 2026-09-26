using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>One card of Bob's row: a minion, or the tavern spell the game lays out in the same row.</summary>
public sealed class TavernSlot
{
    public TavernSlot(int entityId, string cardId, bool isMinion, int zonePosition)
    {
        EntityId = entityId;
        CardId = cardId;
        IsMinion = isMinion;
        ZonePosition = zonePosition;
    }

    public int EntityId { get; }
    public string CardId { get; }
    public bool IsMinion { get; }
    public int ZonePosition { get; }
}

/// <summary>
/// The cards Bob shows, in on-screen order. The game puts the tavern spell in the same row as the minions,
/// at ZONE_POSITION n + 1 (Ali's Power.log, 2026-09-26: every shop of the 20:27 game, e.g. round 14 = six
/// minions at 1-6 and BG28_573 at 7), and centres the whole row: laying the markers out for the minions
/// alone put each of them half a card to the right (+87 px at 2290 × 1359, the offset Ali saw).
/// </summary>
public static class TavernRow
{
    /// <summary>Cards with a position, left to right; positions are only used for the order.</summary>
    public static IReadOnlyList<TavernSlot> Arrange(IEnumerable<TavernSlot> cards) =>
        cards.Where(c => c.ZonePosition > 0 && !string.IsNullOrEmpty(c.CardId))
            .OrderBy(c => c.ZonePosition)
            .ThenBy(c => c.EntityId)
            .ToList();
}

/// <summary>
/// Follows Bob's row during one shop round, so that the markers are redrawn as soon as a card leaves or
/// arrives (a purchase, a reroll, a card added by an effect) rather than once per round, and so that the
/// round's diagnostic line can say how often that happened.
/// </summary>
public sealed class TavernRowTracker
{
    private readonly HashSet<int> _seen = new();
    private IReadOnlyList<int> _last = Array.Empty<int>();
    private IReadOnlyList<int> _lastNonEmpty = Array.Empty<int>();

    /// <summary>The round being followed; -1 when none.</summary>
    public int Round { get; private set; } = -1;

    public bool IsOpen => Round >= 0;

    /// <summary>Row changes after the round's first cards: purchases, rerolls, cards added or moved.</summary>
    public int Changes { get; private set; }

    /// <summary>
    /// Changes that brought cards not seen this round while some of the previous cards were gone: a reroll.
    /// A row that only grows (the first cards arriving one by one, a card added by an effect) is not one.
    /// </summary>
    public int Refreshes { get; private set; }

    /// <summary>True when the row differs from the last one seen: the markers must be redrawn.</summary>
    public bool Observe(int round, IReadOnlyList<int> entityIds)
    {
        if (round != Round)
        {
            Round = round;
            _seen.Clear();
            _last = Array.Empty<int>();
            _lastNonEmpty = Array.Empty<int>();
            Changes = 0;
            Refreshes = 0;
        }

        if (entityIds.SequenceEqual(_last))
        {
            return false;
        }

        if (_seen.Count > 0)
        {
            Changes++;
            var fresh = entityIds.Any(id => !_seen.Contains(id));
            var lost = _lastNonEmpty.Any(id => !entityIds.Contains(id));
            if (fresh && lost)
            {
                Refreshes++;
            }
        }

        foreach (var id in entityIds)
        {
            _seen.Add(id);
        }

        _last = entityIds.ToList();
        if (entityIds.Count > 0)
        {
            _lastNonEmpty = _last;
        }

        return true;
    }

    /// <summary>End of the shop round: the next observation starts a new one.</summary>
    public void Close() => Round = -1;
}
