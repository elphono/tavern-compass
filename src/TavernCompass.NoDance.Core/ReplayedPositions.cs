using System.Collections.Generic;
using System.Linq;

namespace TavernCompass.NoDance.Core;

/// <summary>One change of a server task list (ZoneChange), as the mod reads it when the list is about to be played.</summary>
public readonly struct ServerChange
{
    public ServerChange(int entityId, bool inPlayerRow, bool changesZoneOrController, bool hasDestinationPosition,
        int destinationPosition)
    {
        EntityId = entityId;
        InPlayerRow = inPlayerRow;
        ChangesZoneOrController = changesZoneOrController;
        HasDestinationPosition = hasDestinationPosition;
        DestinationPosition = destinationPosition;
    }

    public int EntityId { get; }

    /// <summary>The entity's card is in the player's row when the list is about to be played.</summary>
    public bool InPlayerRow { get; }

    /// <summary>The change names a destination zone, zone tag or controller: an entry, an exit, a transfer.</summary>
    public bool ChangesZoneOrController { get; }

    public bool HasDestinationPosition { get; }

    public int DestinationPosition { get; }
}

/// <summary>
/// Which positions a server task list replays late on the player's row. In the shop, the server's ZONE_POSITION tag
/// changes are applied twice: on reception (real time) and again when the task list is played, sometimes seconds later,
/// with the places of before. The real-time path and the single writer have already put the row in order; replaying
/// the old places is what makes cards jump back.
/// </summary>
public static class ReplayedPositions
{
    /// <summary>
    /// Indexes of the changes to neutralize: pure position changes (no zone, no controller) of cards in the player's
    /// row. An entity that enters, leaves or changes hands in the same list keeps all its changes: its position there
    /// belongs to its new zone, and the client needs it.
    /// </summary>
    public static IReadOnlyList<int> ToNeutralize(IReadOnlyList<ServerChange> changes)
    {
        var transferred = new HashSet<int>(changes.Where(c => c.ChangesZoneOrController).Select(c => c.EntityId));
        var result = new List<int>();
        for (int i = 0; i < changes.Count; i++)
        {
            var change = changes[i];
            if (change.InPlayerRow
                && change.HasDestinationPosition
                && change.DestinationPosition > 0
                && !change.ChangesZoneOrController
                && !transferred.Contains(change.EntityId))
            {
                result.Add(i);
            }
        }

        return result;
    }
}
