using System.Collections.Generic;
using System.Linq;

namespace TavernCompass.NoDance.Core;

/// <summary>One change of a server task list (ZoneChange), as the mod reads it when the list is about to be played.</summary>
public readonly struct ServerChange
{
    public ServerChange(int entityId, bool inPlayerRow, bool changesZoneOrController, bool hasDestinationPosition,
        int destinationPosition, bool hasPowerTask)
    {
        EntityId = entityId;
        InPlayerRow = inPlayerRow;
        ChangesZoneOrController = changesZoneOrController;
        HasDestinationPosition = hasDestinationPosition;
        DestinationPosition = destinationPosition;
        HasPowerTask = hasPowerTask;
    }

    public int EntityId { get; }

    /// <summary>The entity's card is in the player's row when the list is about to be played.</summary>
    public bool InPlayerRow { get; }

    /// <summary>The change names a destination zone, zone tag or controller: an entry, an exit, a transfer.</summary>
    public bool ChangesZoneOrController { get; }

    public bool HasDestinationPosition { get; }

    public int DestinationPosition { get; }

    /// <summary>
    /// The change carries a task of the server's history (ZoneChange.GetPowerTask). Without one, the client made it up
    /// itself while merging the list with the player's pending prediction (ZoneMgr.MergeServerChangeList).
    /// </summary>
    public bool HasPowerTask { get; }
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
    /// Indexes of the changes whose position is neutralized, for cards in the player's row: the server's pure position
    /// changes, and the changes the client made up while merging (no task: they never move a card already in the row;
    /// the single writer arranges it). Only a change sent by the server (with its task) that names a zone or a
    /// controller makes the entity a transfer (it enters, leaves or changes hands): all its changes are then kept, its
    /// position there belongs to its new zone and the client needs it.
    /// </summary>
    public static IReadOnlyList<int> ToNeutralize(IReadOnlyList<ServerChange> changes)
    {
        var transferred = new HashSet<int>(changes
            .Where(c => c.ChangesZoneOrController && c.HasPowerTask)
            .Select(c => c.EntityId));
        var result = new List<int>();
        for (int i = 0; i < changes.Count; i++)
        {
            var change = changes[i];
            if (change.InPlayerRow
                && change.HasDestinationPosition
                && change.DestinationPosition > 0
                && !transferred.Contains(change.EntityId))
            {
                result.Add(i);
            }
        }

        return result;
    }
}
