namespace TavernCompass.NoDance.Core.Tests;

/// <summary>
/// The research's exhaustive search over small boards, ported: boards of 4 to 7 cards, every card that leaves (a shop
/// effect, or the player selling it), every card moved, every slot, the exit animated during the flight or after the
/// answer. Counts the cases where, from the drop on, an order the server never had is drawn. The research found 1 724
/// of 2 072 with the client's rules and 0 with the fix; the port must find the same, which also checks the port.
/// </summary>
public class ClientModelSearchTests
{
    private const string Letters = "ABCDEFG";

    /// <summary>
    /// The server's board after the move: the moved card right after its nearest shown left neighbour that the server
    /// still has (what the client's prediction of the server place intends, modelled).
    /// </summary>
    private static List<char> ServerAfterMove(IReadOnlyList<char> server, IReadOnlyList<char> shownAfterDrop, char moved)
    {
        var rest = server.Where(c => c != moved).ToList();
        int i = shownAfterDrop.ToList().IndexOf(moved);
        var left = shownAfterDrop.Take(i).Where(rest.Contains).ToList();
        int at = left.Count > 0 ? rest.IndexOf(left[^1]) + 1 : 0;
        var result = new List<char>(rest);
        result.Insert(at, moved);
        return result;
    }

    /// <summary>One case; returns the wrong orders drawn after the drop.</summary>
    private static List<string> Play(ClientModel b, string ids, char gone, char moved, int slot, bool exitDuringFlight, bool sell)
    {
        var wrong = new List<string>();
        var accepted = new HashSet<string>();
        void Check(string label)
        {
            string order = b.Layout(label);
            if (!accepted.Contains(order.Replace(gone.ToString(), string.Empty)))
            {
                wrong.Add($"{order} ({label})");
            }
        }

        if (sell)
        {
            b.Grab(gone);
            b.DropOnBob();
            b.Layout("sold");
        }

        var server = ids.Where(c => c != gone).ToList();
        b.RtLeave(gone);
        for (int i = 0; i < server.Count; i++)
        {
            b.RtPos(server[i], i + 1);
        }

        b.EndOfPacket();
        b.Layout("exit received");
        b.OptionsReceived();

        var shown = b.ZoneOrder.ToList();
        shown.Remove(moved);
        var afterDrop = shown.Take(slot - 1).Append(moved).Concat(shown.Skip(slot - 1)).ToList();
        var final = ServerAfterMove(server, afterDrop, moved);
        accepted.Add(new string(final.ToArray()));
        accepted.Add(new string(afterDrop.Where(c => c != gone).ToArray()));

        b.Grab(moved);
        b.DropAndPredict(slot);
        Check("drop");

        void ExitPlayed()
        {
            b.PlayServerList(gone.ToString(), server.Select((c, i) => (c, i + 1)).ToList());
            Check("exit played");
        }

        if (exitDuringFlight)
        {
            ExitPlayed();
        }

        for (int i = 0; i < final.Count; i++)
        {
            b.RtPos(final[i], i + 1);
        }

        b.EndOfPacket();
        Check("answer");
        b.OptionsReceived();
        Check("options");
        if (!exitDuringFlight)
        {
            ExitPlayed();
        }

        b.PlayServerList("", final.Select((c, i) => (c, i + 1)).ToList(), confirmed: true);
        Check("answer played");
        return wrong;
    }

    private static (int Cases, int Wrong, string FirstWrong) Search(bool withMod, bool skipPerCardRealTimeWrites)
    {
        int cases = 0;
        int wrongCases = 0;
        string firstWrong = string.Empty;
        for (int n = 4; n <= 7; n++)
        {
            string ids = Letters.Substring(0, n);
            foreach (char gone in ids)
            {
                foreach (char moved in ids.Where(c => c != gone))
                {
                    for (int slot = 1; slot < n; slot++)
                    {
                        foreach (bool duringFlight in new[] { false, true })
                        {
                            foreach (bool sell in new[] { false, true })
                            {
                                var b = new ClientModel(ids, withMod, skipPerCardRealTimeWrites);
                                b.Layout("start");
                                var wrong = Play(b, ids, gone, moved, slot, duringFlight, sell);
                                cases++;
                                if (wrong.Count > 0)
                                {
                                    wrongCases++;
                                    if (firstWrong.Length == 0)
                                    {
                                        firstWrong = $"{ids} gone={gone} moved={moved} slot={slot} duringFlight={duringFlight} "
                                            + $"sell={sell}: {string.Join(", ", wrong)}";
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return (cases, wrongCases, firstWrong);
    }

    [Fact]
    public void The_client_alone_shows_an_order_the_server_never_had_in_1724_cases_of_2072()
    {
        var (cases, wrong, _) = Search(withMod: false, skipPerCardRealTimeWrites: false);
        Assert.Equal(2072, cases);
        Assert.Equal(1724, wrong);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void With_the_mod_no_case_shows_an_order_the_server_never_had(bool skipPerCardRealTimeWrites)
    {
        var (cases, wrong, first) = Search(withMod: true, skipPerCardRealTimeWrites);
        Assert.Equal(2072, cases);
        Assert.True(wrong == 0, $"{wrong} case(s) with a wrong order, first: {first}");
    }

    /// <summary>A repeating mechanism is tested on three iterations: three moves in a row, random boards and slots.</summary>
    [Fact]
    public void Random_sequences_of_three_moves_end_on_the_server_order_with_positions_1_to_n()
    {
        var random = new Random(7);
        for (int run = 0; run < 2000; run++)
        {
            int n = random.Next(4, 8);
            string ids = Letters.Substring(0, n);
            var b = new ClientModel(ids, withMod: true);
            b.Layout("start");
            var server = ids.ToList();
            for (int step = 0; step < 3; step++)
            {
                var live = b.ZoneOrder.ToList();
                char moved = live[random.Next(live.Count)];
                int slot = random.Next(1, live.Count + 1);
                b.Grab(moved);
                b.DropAndPredict(slot);
                string afterDrop = b.Layout("drop");
                server = ServerAfterMove(server, afterDrop.ToList(), moved);
                for (int i = 0; i < server.Count; i++)
                {
                    b.RtPos(server[i], i + 1);
                }

                b.EndOfPacket();
                b.OptionsReceived();
                b.PlayServerList("", server.Select((c, i) => (c, i + 1)).ToList(), confirmed: true);
                string shown = b.Layout("after move");
                Assert.True(shown == new string(server.ToArray()),
                    $"run {run} step {step}: shown {shown}, server {new string(server.ToArray())}");
                Assert.Equal(Enumerable.Range(1, n), b.Positions);
            }
        }
    }

    /// <summary>Three moves, written out, with an exit animated in the middle of the second flight.</summary>
    [Fact]
    public void Three_successive_moves_end_on_the_server_order_with_positions_1_to_n()
    {
        var b = new ClientModel("ABCDE", withMod: true);
        b.Layout("start");

        // 1. E to the front.
        b.Grab('E');
        b.DropAndPredict(1);
        Assert.Equal("EABCD", b.Layout("drop E"));
        Answer(b, "EABCD");
        Assert.Equal("EABCD", b.Layout("E answered"));

        // 2. A shop effect removes C on the server; before its exit is animated, A goes last; the exit is animated
        //    during the flight.
        b.RtLeave('C');
        b.RtPos('D', 4);
        b.EndOfPacket();
        Assert.Equal("EABCD", b.Layout("C gone on the server, still shown"));
        b.Grab('A');
        b.DropAndPredict(5);
        Assert.Equal("EBCDA", b.Layout("drop A last"));
        b.PlayServerList("C", new[] { ('E', 1), ('A', 2), ('B', 3), ('D', 4) });
        Assert.Equal("EBDA", b.Layout("C's exit played in flight"));
        Answer(b, "EBDA");
        Assert.Equal("EBDA", b.Layout("A answered"));
        Assert.Equal(new[] { 1, 2, 3, 4 }, b.Positions);

        // 3. D between E and B.
        b.Grab('D');
        b.DropAndPredict(2);
        Assert.Equal("EDBA", b.Layout("drop D"));
        Answer(b, "EDBA");
        Assert.Equal("EDBA", b.Layout("D answered"));
        Assert.Equal(new[] { 1, 2, 3, 4 }, b.Positions);
        Assert.False(b.Mod!.InFlight);
        Assert.Equal(3, b.Mod.Counters.Flights);
        Assert.Equal(3, b.Mod.Counters.Answered);
    }

    private static void Answer(ClientModel b, string server)
    {
        for (int i = 0; i < server.Length; i++)
        {
            b.RtPos(server[i], i + 1);
        }

        b.EndOfPacket();
        b.OptionsReceived();
        b.PlayServerList("", server.Select((c, i) => (c, i + 1)).ToList(), confirmed: true);
    }
}
