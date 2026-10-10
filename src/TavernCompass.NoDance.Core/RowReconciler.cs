using System;
using System.Collections.Generic;
using System.Linq;

namespace TavernCompass.NoDance.Core;

public enum FlightOutcome
{
    /// <summary>The server sent its next options: the history of the action has been received before them.</summary>
    Answered,

    /// <summary>The server refused the option; the client puts the card back.</summary>
    Rejected,

    /// <summary>No answer within the flight timeout: the order is unfrozen anyway.</summary>
    Timeout,
}

/// <summary>An option sent with a position (a placement or a move) whose answer has not arrived yet.</summary>
public sealed class Flight
{
    public Flight(int entityId, int position, double since)
    {
        EntityId = entityId;
        Position = position;
        Since = since;
    }

    public int EntityId { get; }

    public int Position { get; }

    /// <summary>Seconds, on the caller's clock (Time.realtimeSinceStartup in the game).</summary>
    public double Since { get; }
}

/// <summary>How a flight ended, for the log.</summary>
public sealed class FlightEnd
{
    public FlightEnd(Flight flight, FlightOutcome outcome, double elapsedMs)
    {
        Flight = flight;
        Outcome = outcome;
        ElapsedMs = elapsedMs;
    }

    public Flight Flight { get; }

    public FlightOutcome Outcome { get; }

    public double ElapsedMs { get; }

    public string LogLine() => Outcome switch
    {
        FlightOutcome.Answered => $"option answered after {ElapsedMs:0} ms",
        FlightOutcome.Rejected => $"option rejected after {ElapsedMs:0} ms",
        _ => $"flight timeout after {ElapsedMs:0} ms",
    };
}

/// <summary>One pass of the single writer that changes at least one position.</summary>
public sealed class Reconciliation
{
    public Reconciliation(string reason, IReadOnlyList<BoardCard> shown, IReadOnlyList<BoardCard> target,
        IReadOnlyList<SlotMove> moves, bool keptVisualOrder, string detail = "")
    {
        Detail = detail;
        Reason = reason;
        Shown = shown;
        Target = target;
        Moves = moves;
        KeptVisualOrder = keptVisualOrder;
    }

    public string Reason { get; }

    public IReadOnlyList<BoardCard> Shown { get; }

    public IReadOnlyList<BoardCard> Target { get; }

    public IReadOnlyList<SlotMove> Moves { get; }

    public bool KeptVisualOrder { get; }

    /// <summary>Appended to the log line: ", placed at server slot 3 (drawn slot 2)" for a drop.</summary>
    public string Detail { get; }

    public bool OrderChanged => !BoardOrder.SameOrder(Shown, Target);

    public string LogLine() =>
        $"reconcile ({Reason}): shown {BoardOrder.Ids(Shown)} -> {BoardOrder.Ids(Target)}, {Moves.Count} position(s) changed"
        + (KeptVisualOrder ? ", kept visual order (in flight)" : string.Empty)
        + (Detail.Length == 0 ? string.Empty : ", " + Detail);
}

/// <summary>
/// The driver's state, without the game: what makes the row "dirty", whether an action of the player is in flight,
/// and what to write at the end of a frame. The mod's NoDanceDriver is a thin adapter around it; the tests drive it
/// through a model of the client's rules.
/// </summary>
public sealed class RowReconciler
{
    private readonly List<string> _reasons = new();
    private int[]? _lastRow;
    private Dictionary<int, int>? _known;
    private bool _predicted;

    public RowReconciler(double flightTimeoutSeconds)
    {
        if (flightTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(flightTimeoutSeconds), flightTimeoutSeconds, "must be positive");
        }

        FlightTimeoutSeconds = flightTimeoutSeconds;
    }

    public double FlightTimeoutSeconds { get; }

    public NoDanceCounters Counters { get; } = new();

    public Flight? CurrentFlight { get; private set; }

    public bool InFlight => CurrentFlight != null;

    public bool IsDirty => _reasons.Count > 0;

    /// <summary>Something may have moved the row: reconcile at the end of the frame. Reasons are kept for the log.</summary>
    public void MarkDirty(string reason)
    {
        if (!_reasons.Contains(reason))
        {
            _reasons.Add(reason);
        }
    }

    /// <summary>An option with a position was sent (GameState.SendOption): keep the order shown until it is answered.</summary>
    public void StartFlight(int entityId, int position, double now)
    {
        CurrentFlight = new Flight(entityId, position, now);
        Counters.Flights++;
    }

    /// <summary>
    /// The server answered (next options), refused, or did not answer in time. The row is marked dirty in every case,
    /// with or without a flight: new options follow every history the server sends.
    /// </summary>
    public FlightEnd? EndFlight(FlightOutcome outcome, double now)
    {
        MarkDirty(outcome switch
        {
            FlightOutcome.Answered => "options",
            FlightOutcome.Rejected => "rejected",
            _ => "timeout",
        });
        if (CurrentFlight == null)
        {
            return null;
        }

        var end = new FlightEnd(CurrentFlight, outcome, (now - CurrentFlight.Since) * 1000.0);
        CurrentFlight = null;
        switch (outcome)
        {
            case FlightOutcome.Answered:
                Counters.Answered++;
                break;
            case FlightOutcome.Rejected:
                Counters.Rejected++;
                break;
            default:
                Counters.Timeouts++;
                break;
        }

        return end;
    }

    /// <summary>Ends the flight if it has lasted more than the timeout.</summary>
    public FlightEnd? CheckTimeout(double now) =>
        CurrentFlight != null && now - CurrentFlight.Since > FlightTimeoutSeconds ? EndFlight(FlightOutcome.Timeout, now) : null;

    /// <summary>
    /// What the last <see cref="EndOfFrame"/> found changed behind the writer: positions of the row that differ from the
    /// ones it left at the previous frame, while a server list was active and neither a real-time packet nor a
    /// prediction of the player happened in between (from: what the writer left, to: what it found). Empty otherwise.
    /// </summary>
    public IReadOnlyList<SlotMove> LateWrites { get; private set; } = Array.Empty<SlotMove>();

    /// <summary>
    /// After the client's prediction of a card dropped from another zone (the hand), in the same frame: the order frozen
    /// for the flight is the one the server will have, the card at the server's slot the client predicted, instead of
    /// the drawn one. Null when no position has to change.
    /// </summary>
    public Reconciliation? PlaceDropped(IReadOnlyList<BoardCard> row, int entityId, int serverSlot, int drawnSlot)
    {
        _predicted = true;
        var shown = BoardOrder.Shown(row);
        var target = BoardOrder.TargetWithPlaced(row, entityId, serverSlot);
        var moves = BoardOrder.Moves(target);
        if (moves.Count == 0)
        {
            return null;
        }

        var reconciliation = new Reconciliation("drop", shown, target, moves, keptVisualOrder: false,
            detail: $"placed at server slot {serverSlot} (drawn slot {drawnSlot})");
        Counters.Reconciles++;
        Counters.Drops++;
        if (reconciliation.OrderChanged)
        {
            Counters.OrderChanges++;
        }

        return reconciliation;
    }

    /// <summary>
    /// Before the client's own prediction of a placement or a move (ZoneMgr.AddPredictedLocalZoneChange): the row's
    /// positions made 1..n again in the order shown, so that the client's arithmetic, which mixes positions and ranks,
    /// computes in one system. Never reorders.
    /// </summary>
    public IReadOnlyList<SlotMove> Renumber(IReadOnlyList<BoardCard> row)
    {
        _predicted = true;
        var moves = BoardOrder.Moves(BoardOrder.Target(row, keepVisualOrder: true));
        if (moves.Count > 0)
        {
            Counters.Renumbered++;
        }

        return moves;
    }

    /// <summary>
    /// The single writer, once per frame after everything else (LateUpdate), before the frame is drawn. A change of the
    /// cards in the row (one entered or left) marks the row dirty by itself: a task list can bring a card in or out
    /// frames after it started, once its neighbours' replayed positions have been neutralized. While a server list is
    /// active (<paramref name="serverListActive"/>), every frame is dirty: its play can write positions frames after it
    /// started, when the mark it left is already consumed. Nothing is written while <paramref name="blocked"/> (a card
    /// held by the player, a choice the client waits for): the row stays dirty. Returns null when no position has to
    /// change.
    /// </summary>
    public Reconciliation? EndOfFrame(IReadOnlyList<BoardCard> row, bool blocked, bool serverListActive)
    {
        var late = new List<SlotMove>();
        if (!blocked && serverListActive && _known != null && !_predicted && !_reasons.Contains("packet"))
        {
            foreach (var card in row)
            {
                if (_known.TryGetValue(card.EntityId, out int left) && left != card.VisualPos)
                {
                    late.Add(new SlotMove(card.EntityId, left, card.VisualPos));
                }
            }
        }

        LateWrites = late;
        if (late.Count > 0)
        {
            Counters.LateWrites++;
        }

        _predicted = false;
        var ids = row.Select(c => c.EntityId).OrderBy(id => id).ToArray();
        if (_lastRow != null && !_lastRow.SequenceEqual(ids))
        {
            MarkDirty("row changed");
        }

        _lastRow = ids;
        if (serverListActive)
        {
            MarkDirty("server list active");
        }

        var reconciliation = Reconcile(row, blocked);
        _known = reconciliation == null
            ? row.ToDictionary(c => c.EntityId, c => c.VisualPos)
            : reconciliation.Target.Select((c, i) => (c.EntityId, Slot: i + 1)).ToDictionary(x => x.EntityId, x => x.Slot);
        return reconciliation;
    }

    private Reconciliation? Reconcile(IReadOnlyList<BoardCard> row, bool blocked)
    {
        if (!IsDirty || blocked)
        {
            return null;
        }

        string reason = string.Join("+", _reasons);
        _reasons.Clear();
        bool keep = InFlight;
        var shown = BoardOrder.Shown(row);
        var target = BoardOrder.Target(row, keep);
        var moves = BoardOrder.Moves(target);
        if (moves.Count == 0)
        {
            return null;
        }

        var reconciliation = new Reconciliation(reason, shown, target, moves, keep);
        Counters.Reconciles++;
        if (reconciliation.OrderChanged)
        {
            Counters.OrderChanges++;
        }

        if (keep)
        {
            Counters.Kept++;
        }

        return reconciliation;
    }
}

/// <summary>What the mod did during one game, logged when the game ends.</summary>
public sealed class NoDanceCounters
{
    /// <summary>Options sent with a position, in scope.</summary>
    public int Flights { get; set; }

    public int Answered { get; set; }

    public int Rejected { get; set; }

    public int Timeouts { get; set; }

    /// <summary>End-of-frame passes that wrote at least one position.</summary>
    public int Reconciles { get; set; }

    /// <summary>Of those, the passes that changed the order shown: each one is an order the client would have kept wrong.</summary>
    public int OrderChanges { get; set; }

    /// <summary>Of those, the passes made while an action was in flight: positions renumbered, order kept.</summary>
    public int Kept { get; set; }

    /// <summary>Renumberings before the client's prediction.</summary>
    public int Renumbered { get; set; }

    /// <summary>Positions replayed late by a server task list and neutralized on the player's row.</summary>
    public int Neutralized { get; set; }

    /// <summary>Of those, the positions the client made up while merging a server list with a pending prediction.</summary>
    public int MergedNeutralized { get; set; }

    /// <summary>Cards dropped from the hand and shown at once at the server's slot.</summary>
    public int Drops { get; set; }

    /// <summary>Frames where a position of the row had changed behind the writer while a server list was active.</summary>
    public int LateWrites { get; set; }

    public string Summary() =>
        $"game summary: flights={Flights} answered={Answered} rejected={Rejected} timeouts={Timeouts} "
        + $"reconciles={Reconciles} order changes={OrderChanges} kept={Kept} renumbered={Renumbered} neutralized={Neutralized} "
        + $"(merged {MergedNeutralized}) drops={Drops} late writes={LateWrites}";
}
