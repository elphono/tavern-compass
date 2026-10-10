namespace TavernCompass.NoDance.Core.Tests;

public class BoardOrderTests
{
    private static BoardCard On(int id, int shown, int server, int processed = 0) =>
        new(id, shown, processed == 0 ? shown : processed, onServerBoard: true, server);

    private static BoardCard Gone(int id, int shown, int staleServer) =>
        new(id, shown, shown, onServerBoard: false, staleServer);

    private static int[] Ids(IEnumerable<BoardCard> cards) => cards.Select(c => c.EntityId).ToArray();

    [Fact]
    public void Shown_sorts_by_position_then_processed_position_then_list_order()
    {
        var cards = new[] { On(1, 2, 1, processed: 5), On(2, 1, 2), On(3, 2, 3, processed: 3), On(4, 2, 4, processed: 3) };
        Assert.Equal(new[] { 2, 3, 4, 1 }, Ids(BoardOrder.Shown(cards)));
    }

    [Fact]
    public void Out_of_flight_the_row_takes_the_server_order()
    {
        // Shown 10 20 30 40, the server has 30 40 10 20.
        var cards = new[] { On(10, 1, 3), On(20, 2, 4), On(30, 3, 1), On(40, 4, 2) };
        var target = BoardOrder.Target(cards, keepVisualOrder: false);
        Assert.Equal(new[] { 30, 40, 10, 20 }, Ids(target));
        Assert.Equal(4, BoardOrder.Moves(target).Count);
    }

    [Fact]
    public void In_flight_the_order_shown_is_kept_whatever_the_server_says()
    {
        var cards = new[] { On(10, 1, 3), On(20, 2, 4), On(30, 3, 1), On(40, 4, 2) };
        var target = BoardOrder.Target(cards, keepVisualOrder: true);
        Assert.Equal(new[] { 10, 20, 30, 40 }, Ids(target));
        Assert.Empty(BoardOrder.Moves(target));
    }

    [Fact]
    public void In_flight_the_positions_are_made_1_to_n_without_reordering()
    {
        // Positions mixed by the client's writes: 2 2 5 7, in this order on screen.
        var cards = new[] { On(10, 2, 1, processed: 1), On(20, 2, 2, processed: 2), On(30, 5, 3), On(40, 7, 4) };
        var target = BoardOrder.Target(cards, keepVisualOrder: true);
        Assert.Equal(new[] { 10, 20, 30, 40 }, Ids(target));
        Assert.Equal(new[] { "10: 2->1", "30: 5->3", "40: 7->4" }, BoardOrder.Moves(target).Select(m => m.ToString()));
    }

    /// <summary>
    /// The missing anchoring test of the research: a card the server has removed but the client still shows, whose
    /// stale real-time place would make it jump (here to the front), stays right behind its left neighbour.
    /// </summary>
    [Fact]
    public void A_card_gone_on_the_server_keeps_its_place_behind_its_left_neighbour()
    {
        // Shown: A X B C. X left the server with its stale place 1; the player moved A last: the server has B C A.
        var cards = new[] { On('A', 1, 3), Gone('X', 2, staleServer: 1), On('B', 3, 1), On('C', 4, 2) };
        var target = BoardOrder.Target(cards, keepVisualOrder: false);
        Assert.Equal(new[] { 'B', 'C', 'A', 'X' }.Select(c => (int)c), Ids(target));
    }

    [Fact]
    public void A_card_gone_on_the_server_at_the_front_stays_at_the_front()
    {
        var cards = new[] { Gone('X', 1, staleServer: 4), On('A', 2, 2), On('B', 3, 1), On('C', 4, 3) };
        var target = BoardOrder.Target(cards, keepVisualOrder: false);
        Assert.Equal(new[] { 'X', 'B', 'A', 'C' }.Select(c => (int)c), Ids(target));
    }

    [Fact]
    public void Three_gone_cards_in_a_row_all_stay_behind_the_same_neighbour()
    {
        // A X Y Z B, X Y Z gone with stale places 2 3 4; the server has A 1, B 2. A half-step key per gone card would
        // put Z (1 + 3 × 0.5 = 2.5) behind B.
        var cards = new[]
        {
            On('A', 1, 1), Gone('X', 2, 2), Gone('Y', 3, 3), Gone('Z', 4, 4), On('B', 5, 2),
        };
        var target = BoardOrder.Target(cards, keepVisualOrder: false);
        Assert.Equal(new[] { 'A', 'X', 'Y', 'Z', 'B' }.Select(c => (int)c), Ids(target));
    }

    [Fact]
    public void A_card_on_the_server_board_without_a_place_keeps_its_place_too()
    {
        var cards = new[] { On('A', 1, 2), On('N', 2, 0), On('B', 3, 1) };
        var target = BoardOrder.Target(cards, keepVisualOrder: false);
        Assert.Equal(new[] { 'B', 'A', 'N' }.Select(c => (int)c), Ids(target));
    }

    [Fact]
    public void A_dropped_card_goes_before_the_server_card_at_its_slot_and_gone_cards_keep_their_anchor()
    {
        // Shown A N X B (N just dropped, drawn 2nd); X gone on the server behind A; the server has A 1, B 2; N's slot: 2.
        var cards = new[] { On('A', 1, 1), new BoardCard('N', 2, 0, onServerBoard: false, 0), Gone('X', 3, 2), On('B', 4, 2) };
        Assert.Equal(new[] { 'A', 'X', 'N', 'B' }.Select(c => (int)c), Ids(BoardOrder.TargetWithPlaced(cards, 'N', 2)));
        Assert.Equal(new[] { 'N', 'A', 'X', 'B' }.Select(c => (int)c), Ids(BoardOrder.TargetWithPlaced(cards, 'N', 1)));
        Assert.Equal(new[] { 'A', 'X', 'B', 'N' }.Select(c => (int)c), Ids(BoardOrder.TargetWithPlaced(cards, 'N', 3)));
        Assert.Equal(new[] { 'A', 'N', 'X', 'B' }.Select(c => (int)c), Ids(BoardOrder.TargetWithPlaced(cards, 'N', 0)));
    }

    [Fact]
    public void Moves_name_only_the_cards_not_at_their_slot()
    {
        var target = new[] { On(1, 1, 1), On(2, 3, 2), On(3, 2, 3) };
        Assert.Equal(new[] { "2: 3->2", "3: 2->3" }, BoardOrder.Moves(target).Select(m => m.ToString()));
        Assert.Equal("[1 2 3]", BoardOrder.Ids(target));
    }

    [Fact]
    public void An_empty_row_needs_nothing()
    {
        Assert.Empty(BoardOrder.Target(Array.Empty<BoardCard>(), keepVisualOrder: false));
        Assert.Empty(BoardOrder.Moves(Array.Empty<BoardCard>()));
    }
}
