using TavernCompass.NoDance.Core;

namespace TavernCompass.NoDance.Core.Tests;

/// <summary>
/// A model of the client's rules for the player's row in the Battlegrounds shop, as the research read them in the
/// client (docs/journal/2026-10-10-anti-danse.md), with or without the mod. Cards are letters; the entity id is the
/// letter's code. This is a model of rules read in the code, not an observation of the game.
///
/// Rules of the client (always on):
///  R1 a real-time ZONE_POSITION is copied into that one card's position, neighbours untouched (ZoneMgr's real-time
///     position change), if the server still has the card on the board;
///  R2 the prediction of a move shifts the cards between the source POSITION and the destination SLOT;
///  R3 the layout sorts by position, ties by the processed position;
///  R4 a server task list replays its positions when it is played; a card the player moved goes to its real-time
///     place instead; the player's own confirmed list moves nothing.
///
/// The mod, when <c>withMod</c>: its hooks call the <see cref="RowReconciler"/> where the mod does (H2 SendOption,
/// H3 real-time packet, H4 before the prediction, H5 when a server list starts, the options listeners), and
/// <see cref="Frame"/> is the mod's LateUpdate, run before every <see cref="Layout"/>.
/// </summary>
internal sealed class ClientModel
{
    private readonly Dictionary<char, ModelCard> _cards = new();
    private readonly RowReconciler? _mod;
    private readonly bool _skipPerCardRealTimeWrites;
    private List<ModelCard> _zone;
    private ModelCard? _held;
    private bool _serverListActive;

    public ClientModel(string ids, bool withMod, bool skipPerCardRealTimeWrites = false)
    {
        for (int i = 0; i < ids.Length; i++)
        {
            _cards[ids[i]] = new ModelCard(ids[i], i + 1);
        }

        _zone = ids.Select(id => _cards[id]).ToList();
        _mod = withMod ? new RowReconciler(3.0) : null;
        _skipPerCardRealTimeWrites = skipPerCardRealTimeWrites;
    }

    public RowReconciler? Mod => _mod;

    public List<(string Label, string Order)> History { get; } = new();

    /// <summary>The row's list, in the order of the last layout (what the player sees).</summary>
    public string ZoneOrder => new(_zone.Select(c => c.Id).ToArray());

    public IReadOnlyList<int> Positions => _zone.Select(c => c.VPos).OrderBy(p => p).ToList();

    /// <summary>R3, after the mod's LateUpdate: the order drawn on screen.</summary>
    public string Layout(string label)
    {
        Frame();
        _zone = _zone.OrderBy(c => c.VPos).ThenBy(c => c.PPos).ToList();
        string order = ZoneOrder;
        History.Add((label, order));
        return order;
    }

    /// <summary>The mod's LateUpdate: the single writer, if something marked the row dirty.</summary>
    public void Frame()
    {
        if (_mod == null)
        {
            return;
        }

        var reconciliation = _mod.EndOfFrame(Row(), blocked: _held != null, serverListActive: _serverListActive);
        if (reconciliation != null)
        {
            Apply(reconciliation.Moves);
        }
    }

    // ---- real-time path ------------------------------------------------------------------------------------------

    /// <summary>The server removed the card (graveyard, set aside): it is still shown until its exit is animated.</summary>
    public void RtLeave(char id) => _cards[id].RtOnBoard = false;

    /// <summary>The server put a new card on the board; its card is not in the row until its task list is played.</summary>
    public void RtEnter(char id, int pos) =>
        _cards[id] = new ModelCard(id, pos) { InRowYet = false };

    /// <summary>The server put a card of the row on its board in real time (the answer to a play from hand).</summary>
    public void RtArrive(char id, int pos)
    {
        _cards[id].RtOnBoard = true;
        RtPos(id, pos);
    }

    /// <summary>R1, unless the mod's optional H6 skips it for the player's row.</summary>
    public void RtPos(char id, int value)
    {
        var card = _cards[id];
        card.RtPos = value;
        if (!card.InRowYet || (_mod != null && _skipPerCardRealTimeWrites))
        {
            return;
        }

        if (card.RtOnBoard && card.VPos != value)
        {
            card.VPos = value;
        }
    }

    /// <summary>End of a real-time packet: H3 marks the row dirty, the frame ends.</summary>
    public void EndOfPacket()
    {
        _mod?.MarkDirty("packet");
        Frame();
    }

    // ---- player --------------------------------------------------------------------------------------------------

    public void Grab(char id)
    {
        _held = _cards[id];
        _zone.Remove(_held);
    }

    /// <summary>A sale: the card is not put back; the option has no position, so no flight.</summary>
    public void DropOnBob() => _held = null;

    /// <summary>The client's drop of a held minion of the row on a slot, with the mod's H2 and H4 before R2.</summary>
    public void DropAndPredict(int slot)
    {
        var card = _held ?? throw new InvalidOperationException("nothing held");
        _held = null;
        _zone.Add(card);
        if (_mod != null)
        {
            _mod.StartFlight(card.EntityId, slot, now: 0);
            Apply(_mod.Renumber(Row()));
        }

        int src = card.VPos;
        int dst = slot;
        var moves = new List<(ModelCard Card, int Pos)>();
        foreach (var other in _zone)
        {
            int p = other.VPos;
            if (src < dst && src <= p && p <= dst)
            {
                moves.Add((other, other == card ? dst : p - 1));
            }
            else if (src > dst && dst <= p && p <= src)
            {
                moves.Add((other, other == card ? dst : p + 1));
            }
        }

        foreach (var (other, pos) in moves)
        {
            other.VPos = pos;
        }

        card.Moved = true;
        card.Predicted = slot;
    }

    /// <summary>
    /// The client's drop of a card from the hand on the row: H2 and H4's prefix, then the client's arithmetic for a change
    /// of zone (every card at or after the drawn slot moves one up), then H4's postfix.
    /// </summary>
    public void PlayFromHand(char id, int drawnSlot, int serverSlot)
    {
        var card = new ModelCard(id, 0) { RtOnBoard = false };
        _cards[id] = card;
        if (_mod != null)
        {
            _mod.StartFlight(card.EntityId, serverSlot, now: 0);
            Apply(_mod.Renumber(Row()));
        }

        foreach (var other in _zone.Where(o => o.VPos >= drawnSlot))
        {
            other.VPos++;
        }

        card.VPos = drawnSlot;
        card.Predicted = serverSlot;
        _zone.Add(card);
        var placed = _mod?.PlaceDropped(Row(), card.EntityId, serverSlot, drawnSlot);
        if (placed != null)
        {
            Apply(placed.Moves);
        }
    }

    /// <summary>The next options arrive (after the history of the action): the mod's listener ends the flight.</summary>
    public void OptionsReceived()
    {
        if (_mod == null)
        {
            return;
        }

        _mod.EndFlight(FlightOutcome.Answered, now: 0);
        Frame();
    }

    // ---- processed path (task lists, played later) ---------------------------------------------------------------

    /// <summary>
    /// A server task list is played: H5 runs when it starts (the mod neutralizes the pure positions it would replay on
    /// the row and marks the row dirty, a frame ends), then exits and entries are animated (a frame each), then the
    /// positions are applied (R4), then a frame ends.
    /// </summary>
    public void PlayServerList(string leaving, IReadOnlyList<(char Id, int Pos)> positions, bool confirmed = false,
        IReadOnlyList<(char Id, int Pos)>? entering = null, bool positionsFirst = false)
    {
        entering ??= Array.Empty<(char, int)>();
        var neutralized = new HashSet<char>();
        if (_mod != null)
        {
            _serverListActive = true;
            if (!confirmed)
            {
                var changes = leaving.Select(id => new ServerChange(id, InRow(id), true, false, 0, true))
                    .Concat(entering.Select(e => new ServerChange(e.Id, InRow(e.Id), true, true, e.Pos, true)))
                    .Concat(positions.Select(p => new ServerChange(p.Id, InRow(p.Id), false, true, p.Pos, true)))
                    .ToList();
                // ZoneMgr.MergeServerChangeList: while a prediction of the player is pending, the client adds, for every
                // card of the row, a change naming the zone, without a task (ignored in the shop when played).
                if (_zone.Any(c => c.Predicted != 0))
                {
                    changes.AddRange(_zone.Select((c, i) => new ServerChange(c.EntityId, true, true, true, i + 1, false)));
                }

                foreach (int index in ReplayedPositions.ToNeutralize(changes))
                {
                    neutralized.Add((char)changes[index].EntityId);
                    _mod.Counters.Neutralized++;
                }
            }

            _mod.MarkDirty("server list");
            Frame();
        }

        if (positionsFirst)
        {
            ApplyPositions(positions, confirmed, neutralized);
        }

        foreach (char id in leaving)
        {
            _zone.Remove(_cards[id]);
            Frame();
        }

        foreach (var (id, _) in entering)
        {
            _cards[id].InRowYet = true;
            _zone.Add(_cards[id]);
            Frame();
        }

        if (!positionsFirst)
        {
            ApplyPositions(positions, confirmed, neutralized);
        }

        Frame();
        _serverListActive = false;
    }

    private void ApplyPositions(IReadOnlyList<(char Id, int Pos)> positions, bool confirmed, HashSet<char> neutralized)
    {
        foreach (var (id, value) in positions)
        {
            var card = _cards[id];
            card.PPos = value;
            if (confirmed)
            {
                card.Moved = false;
                card.Predicted = 0;
                continue;
            }

            if (neutralized.Contains(id))
            {
                continue;
            }

            card.VPos = card.Moved ? card.RtPos : value;
        }
    }

    private bool InRow(char id) => _zone.Contains(_cards[id]);

    private IReadOnlyList<BoardCard> Row() =>
        _zone.Select(c => new BoardCard(c.EntityId, c.VPos, c.PPos, c.RtOnBoard, c.RtPos)).ToList();

    private void Apply(IEnumerable<SlotMove> moves)
    {
        foreach (var move in moves)
        {
            _cards[(char)move.EntityId].VPos = move.To;
        }
    }

    private sealed class ModelCard
    {
        public ModelCard(char id, int pos)
        {
            Id = id;
            VPos = pos;
            PPos = pos;
            RtPos = pos;
        }

        public char Id { get; }

        public int EntityId => Id;

        /// <summary>Card.m_zonePosition.</summary>
        public int VPos { get; set; }

        /// <summary>The entity's processed ZONE_POSITION.</summary>
        public int PPos { get; set; }

        /// <summary>The entity's real-time ZONE_POSITION.</summary>
        public int RtPos { get; set; }

        public bool RtOnBoard { get; set; } = true;

        /// <summary>The client's "minion was moved" mark on the card, cleared by the confirming list.</summary>
        public bool Moved { get; set; }

        public bool InRowYet { get; set; } = true;

        /// <summary>Card.m_predictedZonePosition: a prediction of the player not yet confirmed.</summary>
        public int Predicted { get; set; }
    }
}
