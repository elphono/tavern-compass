using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// The options of the player's pending choice, in the order the game shows them.
///
/// That order is the order of the choice in the log ("Entities[0]", "Entities[1]"…): HDT's ChoicesHandler
/// appends each offered id in that order (LogReader/Handlers/ChoicesHandler.cs:61-71, 161-164), stores the
/// list as Player.OfferedEntityIds (Hearthstone/GameEventHandler.cs:1522), and its own trinket overlay lays its
/// stats out in that order (GameEventHandler.cs:1526-1544). Player.OfferedEntities, on the other hand, filters
/// the game's entity table (Hearthstone/Player.cs:87-88), so it comes in creation order: in Ali's game of
/// 2026-09-26 the trinkets were created 3434 to 3437 and offered 3436, 3434, 3437, 3435. The offered
/// entities carry no position of their own (zonePos=0 in the log).
/// </summary>
public static class ChoiceOrder
{
    /// <summary>
    /// The entities in the order of <paramref name="offeredIds"/>. Empty when one offered id has no entity
    /// yet (HDT hides entities whose tags are still changing): a partial list would put advice on the wrong card.
    /// </summary>
    public static IReadOnlyList<EntitySnapshot> Arrange(IReadOnlyList<int> offeredIds, IReadOnlyList<EntitySnapshot> entities) =>
        Arrange(offeredIds, entities, e => e.Id);

    /// <summary>The same rule for any item that carries an entity id.</summary>
    public static IReadOnlyList<T> Arrange<T>(IReadOnlyList<int> offeredIds, IReadOnlyList<T> items, Func<T, int> idOf)
    {
        var byId = new Dictionary<int, T>();
        foreach (var item in items)
        {
            byId[idOf(item)] = item;
        }

        var ordered = new List<T>(offeredIds.Count);
        foreach (var id in offeredIds)
        {
            if (!byId.TryGetValue(id, out var item))
            {
                return Array.Empty<T>();
            }

            ordered.Add(item);
        }

        return ordered;
    }

    /// <summary>"[3436,3434,3437,3435]", for the diagnostic lines.</summary>
    public static string Describe(IEnumerable<EntitySnapshot> options) => "[" + string.Join(",", options.Select(o => o.Id)) + "]";
}
