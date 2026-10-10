namespace TavernCompass.NoDance.Core.Tests;

/// <summary>
/// The three scenarios of the research's model, replayed with the client's rules alone and with the mod. Each one
/// checks the whole sequence of orders drawn: the client's rules must show an order the server never had (otherwise
/// the scenario proves nothing about the mod), and the mod must not.
/// </summary>
public class ClientModelScenarioTests
{
    private static void ExitThenMove(ClientModel b)
    {
        b.Layout("start");
        b.RtLeave('X');
        b.RtPos('B', 2);
        b.RtPos('C', 3);
        b.EndOfPacket();
        b.Layout("packet: X left on the server, still shown");
        b.Grab('A');
        b.DropAndPredict(3);
        b.Layout("drop A as 3rd shown card: local prediction");
        b.RtPos('A', 2);
        b.RtPos('B', 1);
        b.EndOfPacket();
        b.Layout("packet: server answer");
        b.OptionsReceived();
        b.Layout("options received");
        b.PlayServerList("X", new[] { ('B', 2), ('C', 3) });
        b.Layout("X's task list played");
        b.PlayServerList("", new[] { ('A', 2), ('B', 1) }, confirmed: true);
        b.Layout("answer task list played (confirmed)");
    }

    private static void ExitDuringFlight(ClientModel b)
    {
        b.Layout("start");
        b.RtLeave('X');
        b.RtPos('B', 2);
        b.RtPos('C', 3);
        b.EndOfPacket();
        b.Layout("packet: X left on the server, still shown");
        b.Grab('A');
        b.DropAndPredict(3);
        b.Layout("drop A as 3rd shown card: local prediction");
        b.PlayServerList("X", new[] { ('B', 2), ('C', 3) });
        b.Layout("X's task list played, answer not received yet");
        b.RtPos('A', 2);
        b.RtPos('B', 1);
        b.EndOfPacket();
        b.Layout("packet: server answer");
        b.OptionsReceived();
        b.Layout("options received");
        b.PlayServerList("", new[] { ('A', 2), ('B', 1) }, confirmed: true);
        b.Layout("answer task list played (confirmed)");
    }

    private static void SellThenMove(ClientModel b)
    {
        b.Layout("start");
        b.Grab('A');
        b.DropOnBob();
        b.Layout("A dragged to Bob");
        b.RtLeave('A');
        b.RtPos('B', 1);
        b.RtPos('C', 2);
        b.RtPos('D', 3);
        b.EndOfPacket();
        b.Layout("packet: sale answered");
        b.OptionsReceived();
        b.Grab('D');
        b.DropAndPredict(1);
        b.Layout("drop D in front: local prediction");
        b.RtPos('D', 1);
        b.RtPos('B', 2);
        b.RtPos('C', 3);
        b.EndOfPacket();
        b.Layout("packet: move answered");
        b.OptionsReceived();
        b.Layout("options received");
        b.PlayServerList("A", new[] { ('B', 1), ('C', 2), ('D', 3) });
        b.Layout("sale's task list played");
        b.PlayServerList("", new[] { ('D', 1), ('B', 2), ('C', 3) }, confirmed: true);
        b.Layout("move task list played (confirmed)");
        b.Layout("next layout of the zone");
    }

    private static string[] Orders(ClientModel b) => b.History.Select(h => h.Order).ToArray();

    [Fact]
    public void Exit_then_move_the_client_dances_and_the_mod_does_not()
    {
        var client = new ClientModel("AXBC", withMod: false);
        ExitThenMove(client);
        Assert.Equal(new[] { "AXBC", "AXBC", "XBCA", "XBAC", "XBAC", "ABC", "BAC" }, Orders(client));

        var mod = new ClientModel("AXBC", withMod: true);
        ExitThenMove(mod);
        Assert.Equal(new[] { "AXBC", "AXBC", "XBAC", "XBAC", "XBAC", "BAC", "BAC" }, Orders(mod));
    }

    [Fact]
    public void Exit_animated_during_the_flight_the_client_dances_and_the_mod_does_not()
    {
        var client = new ClientModel("AXBC", withMod: false);
        ExitDuringFlight(client);
        Assert.Equal(new[] { "AXBC", "AXBC", "XBCA", "BAC", "BAC", "BAC", "BAC" }, Orders(client));

        var mod = new ClientModel("AXBC", withMod: true);
        ExitDuringFlight(mod);
        Assert.Equal(new[] { "AXBC", "AXBC", "XBAC", "BAC", "BAC", "BAC", "BAC" }, Orders(mod));
    }

    [Fact]
    public void Sell_then_move_the_client_dances_and_the_mod_does_not()
    {
        var client = new ClientModel("ABCD", withMod: false);
        SellThenMove(client);
        Assert.Equal(new[] { "ABCD", "BCD", "BCD", "DBC", "DBC", "DBC", "BDC", "DBC", "DBC" }, Orders(client));

        var mod = new ClientModel("ABCD", withMod: true);
        SellThenMove(mod);
        Assert.Equal(new[] { "ABCD", "BCD", "BCD", "DBC", "DBC", "DBC", "DBC", "DBC", "DBC" }, Orders(mod));
        Assert.Equal(new[] { 1, 2, 3 }, mod.Positions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Freeze_in_flight_keeps_the_prediction_while_a_queued_exit_plays(bool skipPerCardRealTimeWrites)
    {
        // A is dropped as 3rd shown card; before the server answers, X's exit is animated. The real-time places the
        // mod knows are still those of before the move (A first): reordering now would send A back to the front.
        var b = new ClientModel("AXBC", withMod: true, skipPerCardRealTimeWrites);
        b.Layout("start");
        b.RtLeave('X');
        b.RtPos('B', 2);
        b.RtPos('C', 3);
        b.EndOfPacket();
        b.Grab('A');
        b.DropAndPredict(3);
        Assert.True(b.Mod!.InFlight);
        Assert.Equal("XBAC", b.Layout("drop"));

        b.PlayServerList("X", new[] { ('B', 2), ('C', 3) });
        Assert.Equal("BAC", b.Layout("exit played in flight: the prediction is kept"));
        Assert.Equal(new[] { 1, 2, 3 }, b.Positions);
        Assert.True(b.Mod.Counters.Kept > 0, "the exit should have been reconciled in flight, keeping the order");

        b.RtPos('A', 2);
        b.RtPos('B', 1);
        b.EndOfPacket();
        b.OptionsReceived();
        Assert.False(b.Mod.InFlight);
        Assert.Equal("BAC", b.Layout("answered"));
    }

    /// <summary>
    /// C1, the defect the reading of Nomi's mod revealed. A B C D; A is sold and its task list waits; D is moved in
    /// front. The sale's list starts while D's prediction is still pending: the client merges it (a change naming the
    /// zone, without a task, for every card of the row), and writes the sale's places a frame after the list started.
    /// </summary>
    [Fact]
    public void Sell_then_move_with_the_sale_list_merged_and_written_a_frame_later_never_shows_BDC()
    {
        var b = new ClientModel("ABCD", withMod: true);
        b.Layout("start");
        b.Grab('A');
        b.DropOnBob();
        b.RtLeave('A');
        b.RtPos('B', 1);
        b.RtPos('C', 2);
        b.RtPos('D', 3);
        b.EndOfPacket();
        b.OptionsReceived();
        Assert.Equal("BCD", b.Layout("sale answered"));
        b.Grab('D');
        b.DropAndPredict(1);
        Assert.Equal("DBC", b.Layout("drop D in front"));
        b.RtPos('D', 1);
        b.RtPos('B', 2);
        b.RtPos('C', 3);
        b.EndOfPacket();
        b.OptionsReceived();
        Assert.Equal("DBC", b.Layout("move answered"));

        b.PlayServerList("A", new[] { ('B', 1), ('C', 2), ('D', 3) });
        Assert.Equal("DBC", b.Layout("sale's list merged, its places written a frame later"));
        b.PlayServerList("", new[] { ('D', 1), ('B', 2), ('C', 3) }, confirmed: true);
        Assert.Equal("DBC", b.Layout("move list played (confirmed)"));
        Assert.DoesNotContain(b.History, h => h.Order == "BDC");
    }

    /// <summary>
    /// C2, Nomi's "drop" idea in our terms. A T B, T summoned next to A. N is played from the hand and drawn 2nd; the
    /// client's server slot for it is 3 (the token stays by its maker). The frozen order is the one the server will
    /// have, from the drop on, instead of the drawn one for the whole flight.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_card_played_from_the_hand_is_shown_at_its_server_slot_from_the_drop(bool skipPerCardRealTimeWrites)
    {
        var b = new ClientModel("ATB", withMod: true, skipPerCardRealTimeWrites);
        b.Layout("start");
        b.PlayFromHand('N', drawnSlot: 2, serverSlot: 3);
        Assert.Equal("ATNB", b.Layout("drop N, drawn 2nd, server slot 3"));
        b.RtArrive('N', 3);
        b.RtPos('B', 4);
        b.EndOfPacket();
        Assert.Equal("ATNB", b.Layout("play answered"));
        b.OptionsReceived();
        Assert.Equal("ATNB", b.Layout("options"));
        Assert.Equal(new[] { 1, 2, 3, 4 }, b.Positions);
    }

    /// <summary>
    /// The prediction is wrong (the server put D second, not first): once answered, the row must take the server's
    /// order, with or without the client's card-by-card real-time writes (H6). In the search above the server always
    /// agrees with what the player meant, so it cannot tell "takes the server order" from "keeps what is shown".
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_wrong_prediction_ends_on_the_server_place(bool skipPerCardRealTimeWrites)
    {
        var b = new ClientModel("ABCD", withMod: true, skipPerCardRealTimeWrites);
        b.Layout("start");
        b.Grab('D');
        b.DropAndPredict(1);
        Assert.Equal("DABC", b.Layout("drop D in front"));
        b.RtPos('A', 1);
        b.RtPos('D', 2);
        b.RtPos('B', 3);
        b.RtPos('C', 4);
        b.EndOfPacket();
        b.OptionsReceived();
        // The client confirms its prediction only when the server's place equals it: here the list is not confirmed.
        b.PlayServerList("", new[] { ('A', 1), ('D', 2), ('B', 3), ('C', 4) });
        Assert.Equal("ADBC", b.Layout("answered: D is second on the server"));
        Assert.Equal(new[] { 1, 2, 3, 4 }, b.Positions);
    }

    /// <summary>
    /// Not in the research's model: two tokens summoned in front of the row. The client alone is right here, because it
    /// applies the neighbours' replayed places; the mod neutralizes them, so it must put the row in order again when
    /// the tokens enter, frames after the task list started. Positions applied before or after the entries.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Tokens_summoned_in_front_enter_at_their_server_place(bool positionsFirst)
    {
        foreach (bool withMod in new[] { false, true })
        {
            var b = new ClientModel("AB", withMod);
            b.Layout("start");
            b.RtEnter('S', 1);
            b.RtEnter('T', 2);
            b.RtPos('A', 3);
            b.RtPos('B', 4);
            b.EndOfPacket();
            Assert.Equal("AB", b.Layout("packet: two tokens in front, not shown yet"));
            b.PlayServerList("", new[] { ('A', 3), ('B', 4) }, entering: new[] { ('S', 1), ('T', 2) },
                positionsFirst: positionsFirst);
            Assert.Equal("STAB", b.Layout($"summon played ({(withMod ? "mod" : "client")})"));
            Assert.Equal(new[] { 1, 2, 3, 4 }, b.Positions);
        }
    }
}
