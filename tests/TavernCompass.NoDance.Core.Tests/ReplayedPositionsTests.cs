namespace TavernCompass.NoDance.Core.Tests;

public class ReplayedPositionsTests
{
    private static ServerChange Pos(int id, int pos, bool inRow = true) => new(id, inRow, false, true, pos, hasPowerTask: true);

    private static ServerChange Zone(int id, bool inRow = true) => new(id, inRow, true, false, 0, hasPowerTask: true);

    [Fact]
    public void Pure_positions_of_cards_in_the_row_are_neutralized()
    {
        var changes = new[] { Pos(1, 2), Pos(2, 1), Pos(3, 3, inRow: false) };
        Assert.Equal(new[] { 0, 1 }, ReplayedPositions.ToNeutralize(changes));
    }

    [Fact]
    public void A_card_that_leaves_or_enters_in_the_same_list_keeps_its_position()
    {
        // 1 leaves the row with a position in its new zone; 2 enters the row; 3 stays and is neutralized.
        var changes = new[] { Zone(1), Pos(1, 4), new ServerChange(2, false, true, true, 1, hasPowerTask: true), Pos(2, 1), Pos(3, 2) };
        Assert.Equal(new[] { 4 }, ReplayedPositions.ToNeutralize(changes));
    }

    /// <summary>
    /// C1. While a prediction of the player is pending, the client merges the list: it adds, for every card of the row,
    /// a change naming the row's zone, without a task (ZoneMgr.MergeServerChangeList). Those cards do not change zone:
    /// the server's replayed places must still be neutralized, and the client's invented ones with them.
    /// </summary>
    [Fact]
    public void Positions_of_a_list_the_client_merged_are_still_neutralized()
    {
        static ServerChange Merged(int id, int pos) => new(id, inPlayerRow: true, changesZoneOrController: true,
            hasDestinationPosition: true, destinationPosition: pos, hasPowerTask: false);
        var changes = new[] { Pos(2, 1), Pos(3, 2), Pos(4, 3), Merged(4, 1), Merged(2, 2), Merged(3, 3) };
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, ReplayedPositions.ToNeutralize(changes));
    }

    [Fact]
    public void A_server_change_of_zone_still_makes_a_transfer_even_in_a_merged_list()
    {
        // 1 leaves the row (a server change with its task); the client's merge names the zone for 2.
        var changes = new[]
        {
            Zone(1), Pos(1, 4), Pos(2, 1),
            new ServerChange(2, true, true, true, 1, hasPowerTask: false),
        };
        Assert.Equal(new[] { 2, 3 }, ReplayedPositions.ToNeutralize(changes));
    }

    [Fact]
    public void A_zero_or_missing_position_is_left_alone()
    {
        var changes = new[] { Pos(1, 0), new ServerChange(2, true, false, false, 0, hasPowerTask: true) };
        Assert.Empty(ReplayedPositions.ToNeutralize(changes));
    }
}
