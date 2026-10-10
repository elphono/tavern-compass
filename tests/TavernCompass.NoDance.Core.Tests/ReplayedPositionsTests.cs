namespace TavernCompass.NoDance.Core.Tests;

public class ReplayedPositionsTests
{
    private static ServerChange Pos(int id, int pos, bool inRow = true) => new(id, inRow, false, true, pos);

    private static ServerChange Zone(int id, bool inRow = true) => new(id, inRow, true, false, 0);

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
        var changes = new[] { Zone(1), Pos(1, 4), new ServerChange(2, false, true, true, 1), Pos(2, 1), Pos(3, 2) };
        Assert.Equal(new[] { 4 }, ReplayedPositions.ToNeutralize(changes));
    }

    [Fact]
    public void A_zero_or_missing_position_is_left_alone()
    {
        var changes = new[] { Pos(1, 0), new ServerChange(2, true, false, false, 0) };
        Assert.Empty(ReplayedPositions.ToNeutralize(changes));
    }
}
