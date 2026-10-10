namespace TavernCompass.NoDance.Core.Tests;

public class RowReconcilerTests
{
    private static BoardCard Card(int id, int shown, int server) => new(id, shown, shown, onServerBoard: true, server);

    /// <summary>Shown 1 2 3, the server has 2 3 1.</summary>
    private static readonly BoardCard[] Swapped = { Card(1, 1, 3), Card(2, 2, 1), Card(3, 3, 2) };

    [Fact]
    public void Nothing_is_written_until_something_marks_the_row_dirty()
    {
        var r = new RowReconciler(3.0);
        Assert.Null(r.EndOfFrame(Swapped, blocked: false));
        r.MarkDirty("packet");
        var done = r.EndOfFrame(Swapped, blocked: false);
        Assert.NotNull(done);
        Assert.Equal("reconcile (packet): shown [1 2 3] -> [2 3 1], 3 position(s) changed", done!.LogLine());
        Assert.False(r.IsDirty);
        Assert.Null(r.EndOfFrame(Swapped, blocked: false));
    }

    [Fact]
    public void A_held_card_defers_the_write_and_keeps_the_row_dirty()
    {
        var r = new RowReconciler(3.0);
        r.MarkDirty("packet");
        r.MarkDirty("server list");
        r.MarkDirty("packet");
        Assert.Null(r.EndOfFrame(Swapped, blocked: true));
        Assert.True(r.IsDirty);
        var done = r.EndOfFrame(Swapped, blocked: false);
        Assert.Equal("packet+server list", done!.Reason);
    }

    [Fact]
    public void A_flight_keeps_the_order_until_the_answer_then_the_server_order_is_taken()
    {
        var r = new RowReconciler(3.0);
        r.StartFlight(entityId: 2, position: 1, now: 10.0);
        r.MarkDirty("packet");
        Assert.Null(r.EndOfFrame(Swapped, blocked: false));

        var end = r.EndFlight(FlightOutcome.Answered, now: 10.287);
        Assert.Equal("option answered after 287 ms", end!.LogLine());
        var done = r.EndOfFrame(Swapped, blocked: false);
        Assert.Equal("options", done!.Reason);
        Assert.True(done.OrderChanged);
        Assert.Equal(new[] { 2, 3, 1 }, done.Target.Select(c => c.EntityId));
    }

    [Fact]
    public void A_renumbering_in_flight_is_counted_as_kept_and_says_so()
    {
        var r = new RowReconciler(3.0);
        r.StartFlight(2, 1, now: 0);
        r.MarkDirty("packet");
        var mixed = new[] { Card(1, 1, 3), Card(2, 3, 1), Card(3, 3, 2) };
        var done = r.EndOfFrame(mixed, blocked: false);
        Assert.Equal("reconcile (packet): shown [1 2 3] -> [1 2 3], 1 position(s) changed, kept visual order (in flight)",
            done!.LogLine());
        Assert.Equal(1, r.Counters.Kept);
        Assert.Equal(0, r.Counters.OrderChanges);
    }

    [Fact]
    public void A_flight_without_an_answer_times_out_and_unfreezes_the_row()
    {
        var r = new RowReconciler(3.0);
        r.StartFlight(2, 1, now: 100.0);
        Assert.Null(r.CheckTimeout(102.9));
        Assert.True(r.InFlight);
        var end = r.CheckTimeout(103.1);
        Assert.Equal(FlightOutcome.Timeout, end!.Outcome);
        Assert.Equal("flight timeout after 3100 ms", end.LogLine());
        Assert.False(r.InFlight);
        Assert.Equal("timeout", r.EndOfFrame(Swapped, blocked: false)!.Reason);
        Assert.Null(r.CheckTimeout(110.0));
    }

    [Fact]
    public void A_rejection_ends_the_flight()
    {
        var r = new RowReconciler(3.0);
        r.StartFlight(2, 1, now: 0);
        Assert.Equal(FlightOutcome.Rejected, r.EndFlight(FlightOutcome.Rejected, 0.3)!.Outcome);
        Assert.False(r.InFlight);
        Assert.Equal(1, r.Counters.Rejected);
    }

    [Fact]
    public void Options_without_a_flight_still_mark_the_row_dirty()
    {
        var r = new RowReconciler(3.0);
        Assert.Null(r.EndFlight(FlightOutcome.Answered, 0));
        Assert.True(r.IsDirty);
    }

    [Fact]
    public void A_card_entering_or_leaving_the_row_marks_it_dirty_from_the_second_frame_on()
    {
        var r = new RowReconciler(3.0);
        var row = new[] { Card(1, 1, 1), Card(2, 2, 2) };
        Assert.Null(r.EndOfFrame(row, blocked: false));
        Assert.False(r.IsDirty);

        // Same cards, other positions: not a change of the row's cards.
        Assert.Null(r.EndOfFrame(new[] { Card(2, 1, 2), Card(1, 2, 1) }, blocked: false));
        Assert.False(r.IsDirty);

        var entered = new[] { Card(3, 1, 1), Card(1, 1, 2), Card(2, 2, 3) };
        var done = r.EndOfFrame(entered, blocked: false);
        Assert.Equal("row changed", done!.Reason);
        Assert.Equal(new[] { 3, 1, 2 }, done.Target.Select(c => c.EntityId));
    }

    /// <summary>Three flights in a row: the flight state does not trap itself.</summary>
    [Fact]
    public void Three_flights_in_a_row_each_start_and_end()
    {
        var r = new RowReconciler(3.0);
        for (int i = 0; i < 3; i++)
        {
            r.StartFlight(i, 1, now: i * 10.0);
            Assert.True(r.InFlight);
            Assert.NotNull(r.EndFlight(FlightOutcome.Answered, i * 10.0 + 0.2));
            Assert.False(r.InFlight);
        }

        Assert.Equal(3, r.Counters.Flights);
        Assert.Equal(3, r.Counters.Answered);
    }

    [Fact]
    public void The_game_summary_names_every_counter()
    {
        var r = new RowReconciler(3.0);
        r.StartFlight(1, 1, 0);
        r.EndFlight(FlightOutcome.Answered, 0.1);
        r.Counters.Neutralized += 2;
        r.MarkDirty("options");
        r.EndOfFrame(Swapped, blocked: false);
        Assert.Equal(
            "game summary: flights=1 answered=1 rejected=0 timeouts=0 reconciles=1 order changes=1 kept=0 renumbered=0 neutralized=2",
            r.Counters.Summary());
    }

    [Fact]
    public void Renumbering_never_reorders()
    {
        var r = new RowReconciler(3.0);
        var moves = r.Renumber(new[] { Card(1, 1, 3), Card(2, 4, 1), Card(3, 4, 2) });
        Assert.Equal(new[] { "2: 4->2", "3: 4->3" }, moves.Select(m => m.ToString()));
        Assert.Equal(1, r.Counters.Renumbered);
        Assert.Empty(r.Renumber(new[] { Card(1, 1, 3), Card(2, 2, 1) }));
        Assert.Equal(1, r.Counters.Renumbered);
    }

    [Fact]
    public void The_flight_timeout_must_be_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RowReconciler(0));
    }
}
