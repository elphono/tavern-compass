using System.Collections.Generic;
using System.Linq;

namespace TavernCompass.NoDance.Core;

/// <summary>A card to move to a slot of the row (1-based), from the position it has now.</summary>
public readonly struct SlotMove
{
    public SlotMove(int entityId, int from, int to)
    {
        EntityId = entityId;
        From = from;
        To = to;
    }

    public int EntityId { get; }

    public int From { get; }

    public int To { get; }

    public override string ToString() => $"{EntityId}: {From}->{To}";
}

/// <summary>
/// The single writer's decision: in which order the player's row must be shown, as a pure function of what the client
/// shows and what the server says in real time (docs/journal/2026-10-10-anti-danse.md).
/// </summary>
public static class BoardOrder
{
    /// <summary>
    /// What the client's layout shows: sorted by the card's position, ties broken by the processed position (the
    /// client's own sort), then by the order of the list (a stable sort).
    /// </summary>
    public static IReadOnlyList<BoardCard> Shown(IReadOnlyList<BoardCard> cards) =>
        cards.Select((card, index) => (card, index))
            .OrderBy(x => x.card.VisualPos)
            .ThenBy(x => x.card.ProcessedPos)
            .ThenBy(x => x.index)
            .Select(x => x.card)
            .ToList();

    /// <summary>
    /// The order the row must take. While the player's action is in flight, the order shown is kept (the client's
    /// prediction): only the numbers are made 1..n again. Otherwise the server's real-time order, for the cards it still
    /// has; a card the server has already removed, still shown because its exit is not animated yet, keeps its place
    /// right behind the nearest card on its left that the server still has (in front of the row if none), whatever
    /// stale real-time place it still carries.
    /// </summary>
    public static IReadOnlyList<BoardCard> Target(IReadOnlyList<BoardCard> cards, bool keepVisualOrder)
    {
        var shown = Shown(cards);
        if (keepVisualOrder)
        {
            return shown;
        }

        int anchor = 0;
        var keyed = new List<(int Major, int Minor, int Index, BoardCard Card)>(shown.Count);
        for (int i = 0; i < shown.Count; i++)
        {
            var card = shown[i];
            if (card.HasServerPlace)
            {
                anchor = card.ServerPos;
                keyed.Add((card.ServerPos, 0, i, card));
            }
            else
            {
                keyed.Add((anchor, 1, i, card));
            }
        }

        return keyed
            .OrderBy(k => k.Major)
            .ThenBy(k => k.Minor)
            .ThenBy(k => k.Index)
            .Select(k => k.Card)
            .ToList();
    }

    /// <summary>The cards whose position is not their slot (1-based) in <paramref name="target"/>.</summary>
    public static IReadOnlyList<SlotMove> Moves(IReadOnlyList<BoardCard> target)
    {
        var moves = new List<SlotMove>();
        for (int i = 0; i < target.Count; i++)
        {
            int slot = i + 1;
            if (target[i].VisualPos != slot)
            {
                moves.Add(new SlotMove(target[i].EntityId, target[i].VisualPos, slot));
            }
        }

        return moves;
    }

    public static bool SameOrder(IReadOnlyList<BoardCard> a, IReadOnlyList<BoardCard> b) =>
        a.Select(c => c.EntityId).SequenceEqual(b.Select(c => c.EntityId));

    /// <summary>The entity ids in order, as the log writes them: [4911 4912 4915].</summary>
    public static string Ids(IEnumerable<BoardCard> cards) => "[" + string.Join(" ", cards.Select(c => c.EntityId)) + "]";
}
