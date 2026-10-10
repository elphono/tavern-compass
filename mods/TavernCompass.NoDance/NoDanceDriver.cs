using System;
using System.Collections.Generic;
using System.Reflection;
using TavernCompass.NoDance.Core;
using UnityEngine;

namespace TavernCompass.NoDance;

/// <summary>
/// The single writer of the player's row in the Battlegrounds shop: one per game, on the zone manager's object. A thin
/// adapter around <see cref="RowReconciler"/>: it reads the row, hands it over, and writes back what comes out. Every
/// call into the client is listed in Core/ClientMembers and checked when the mod loads.
/// </summary>
internal sealed class NoDanceDriver : MonoBehaviour
{
    private static int s_games;
    private static PropertyInfo? s_blockTypeProperty;
    private static bool s_blockTypeLookedUp;

    private RowReconciler _reconciler = null!;
    private GameState.OptionsReceivedCallback _onOptionsReceived = null!;
    private GameState.OptionRejectedCallback _onOptionRejected = null!;
    private GameState? _listenersOn;
    private bool _realTimeCombat;
    private int _renumbered;
    private int _game;

    /// <summary>The driver of the current game, or null between games.</summary>
    internal static NoDanceDriver? Current { get; private set; }

    private static double Now => Time.realtimeSinceStartup;

    internal static void Attach(ZoneMgr zoneMgr) => zoneMgr.gameObject.AddComponent<NoDanceDriver>();

    private void Awake()
    {
        _reconciler = new RowReconciler(NoDancePlugin.FlightTimeoutSeconds);
        _onOptionsReceived = OnOptionsReceived;
        _onOptionRejected = OnOptionRejected;
        _game = ++s_games;
        Current = this;
        NoDancePlugin.Log?.LogInfo($"attached to ZoneMgr (game {_game})");
        EnsureListeners();
    }

    private void OnDestroy()
    {
        try
        {
            Unregister();
            NoDancePlugin.Log?.LogInfo($"{_reconciler.Counters.Summary()} (game {_game})");
        }
        catch (Exception e)
        {
            Hooks.Fail("driver (end of game)", e, required: true);
        }

        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }

    // ---- scope -----------------------------------------------------------------------------------------------------

    /// <summary>
    /// Battlegrounds (not Duos, not spectating), shop phase as animated, real-time step MAIN_ACTION, and no combat in
    /// real time: the only time the row is the player's to arrange.
    /// </summary>
    internal bool InScope()
    {
        if (_realTimeCombat)
        {
            return false;
        }

        var gameMgr = GameMgr.Get();
        if (gameMgr == null || !gameMgr.IsBattlegrounds() || gameMgr.IsBattlegroundDuoGame() || gameMgr.IsSpectator())
        {
            return false;
        }

        var zoneMgr = ZoneMgr.Get();
        return zoneMgr != null && zoneMgr.IsBattlegroundShoppingPhase() && zoneMgr.ShouldIgnorePosChange();
    }

    internal void OnRealTimeCombatPhase(int entityId, int value)
    {
        var gameState = GameState.Get();
        var gameEntity = gameState == null ? null : gameState.GetGameEntity();
        if (gameEntity != null && gameEntity.GetEntityId() == entityId)
        {
            _realTimeCombat = value > 0;
        }
    }

    private static ZonePlay? PlayerRow()
    {
        var zoneMgr = ZoneMgr.Get();
        if (zoneMgr == null)
        {
            return null;
        }

        var zone = zoneMgr.FindZoneOfType<ZonePlay>(Player.Side.FRIENDLY);
        return zone == null ? null : zone;
    }

    // ---- what the hooks report ------------------------------------------------------------------------------------

    internal void MarkDirty(string reason) => _reconciler.MarkDirty(reason);

    internal void StartFlight(int entityId, int position)
    {
        _reconciler.StartFlight(entityId, position, Now);
        NoDancePlugin.Log?.LogInfo($"option sent: entity={entityId} position={position} (in flight)");
    }

    private void OnOptionsReceived(object userData) => EndFlight(FlightOutcome.Answered);

    private void OnOptionRejected(Network.Options.Option option, object userData) => EndFlight(FlightOutcome.Rejected);

    private void EndFlight(FlightOutcome outcome)
    {
        if (!Hooks.Active)
        {
            return;
        }

        try
        {
            var end = _reconciler.EndFlight(outcome, Now);
            if (end != null)
            {
                NoDancePlugin.Log?.LogInfo(end.LogLine());
            }
        }
        catch (Exception e)
        {
            Hooks.Fail("options listener", e, required: true);
        }
    }

    internal void RenumberBeforePrediction()
    {
        _renumbered = 0;
        if (!InScope())
        {
            return;
        }

        var zone = PlayerRow();
        if (zone == null)
        {
            return;
        }

        var cards = zone.GetCards();
        var moves = _reconciler.Renumber(Describe(cards));
        foreach (var move in moves)
        {
            var card = Find(cards, move.EntityId);
            if (card != null)
            {
                card.SetZonePosition(move.To);
            }
        }

        _renumbered = moves.Count;
    }

    internal void LogPrediction(ZoneChangeList? list)
    {
        int renumbered = _renumbered;
        _renumbered = 0;
        if (list == null || !InScope())
        {
            return;
        }

        var card = list.GetLocalTriggerCard();
        var entity = card == null ? null : card.GetEntity();
        var trigger = list.GetLocalTriggerChange();
        NoDancePlugin.Log?.LogInfo($"prediction: entity={(entity == null ? 0 : entity.GetEntityId())} "
            + $"slot={(trigger == null ? 0 : trigger.GetDestinationPosition())} predicted={list.GetPredictedPosition()} "
            + $"list={list.GetId()}{(renumbered > 0 ? $" (renumbered {renumbered})" : string.Empty)}");
    }

    /// <summary>
    /// A server task list is about to be played. The player's own confirmed list moves nothing; any other one gets its
    /// pure positions on the player's row neutralized (the task still runs and sets the tag; only the card stays put).
    /// </summary>
    internal void OnServerListStarts(ZoneChangeList? list)
    {
        if (list == null || !InScope())
        {
            return;
        }

        _reconciler.MarkDirty("server list");
        if (IsConfirmedByClient(list))
        {
            return;
        }

        var zone = PlayerRow();
        var changes = list.GetChanges();
        var read = new List<ServerChange>(changes.Count);
        foreach (var change in changes)
        {
            var entity = change.GetEntity();
            var card = entity == null ? null : entity.GetCard();
            bool inRow = zone != null && card != null && card.GetZone() == zone;
            read.Add(new ServerChange(
                entity == null ? 0 : entity.GetEntityId(),
                inRow,
                change.HasDestinationZoneChange() || change.HasDestinationZone(),
                change.HasDestinationPosition(),
                change.GetDestinationPosition()));
        }

        var neutralize = ReplayedPositions.ToNeutralize(read);
        foreach (int index in neutralize)
        {
            changes[index].SetDestinationPosition(0);
        }

        if (neutralize.Count > 0)
        {
            _reconciler.Counters.Neutralized += neutralize.Count;
            NoDancePlugin.Log?.LogInfo($"server list {list.GetId()} ({BlockType(list)}): {neutralize.Count} replayed position(s) neutralized");
        }
    }

    /// <summary>H6: the client's card-by-card real-time write is skipped for the player's row, which the driver rewrites.</summary>
    internal bool TakesOverRealTimeWrite(Entity? entity)
    {
        if (entity == null || !InScope())
        {
            return false;
        }

        var card = entity.GetCard();
        var zone = PlayerRow();
        if (card == null || zone == null || card.GetZone() != zone)
        {
            return false;
        }

        _reconciler.MarkDirty("packet");
        return true;
    }

    // ---- the single writer ------------------------------------------------------------------------------------------

    private void LateUpdate()
    {
        if (!Hooks.Active)
        {
            return;
        }

        try
        {
            EnsureListeners();
            var timedOut = _reconciler.CheckTimeout(Now);
            if (timedOut != null)
            {
                NoDancePlugin.Log?.LogInfo(timedOut.LogLine());
            }

            if (!InScope())
            {
                return;
            }

            var zone = PlayerRow();
            var gameState = GameState.Get();
            if (zone == null || gameState == null)
            {
                return;
            }

            var cards = zone.GetCards();
            var inputManager = InputManager.Get();
            bool blocked = (inputManager != null && inputManager.GetHeldCard() != null) || gameState.MustWaitForChoices();
            var reconciliation = _reconciler.EndOfFrame(Describe(cards), blocked);
            if (reconciliation == null)
            {
                return;
            }

            Write(reconciliation.Moves, cards);
            NoDancePlugin.Log?.LogInfo(reconciliation.LogLine());
        }
        catch (Exception e)
        {
            Hooks.Fail("driver (LateUpdate)", e, required: true);
        }
    }

    /// <summary>
    /// One local list of pure position changes, processed at once by the client's own entry point for generated lists
    /// (the one it uses to put a moved minion back at its real-time place): positions applied, row laid out again.
    /// </summary>
    private static void Write(IReadOnlyList<SlotMove> moves, List<Card> cards)
    {
        var zoneMgr = ZoneMgr.Get();
        if (zoneMgr == null)
        {
            return;
        }

        var list = new ZoneChangeList();
        foreach (var move in moves)
        {
            var card = Find(cards, move.EntityId);
            if (card == null)
            {
                continue;
            }

            var change = new ZoneChange();
            change.SetEntity(card.GetEntity());
            change.SetSourcePosition(card.GetZonePosition());
            change.SetDestinationPosition(move.To);
            list.AddChange(change);
        }

        if (list.GetChanges().Count > 0)
        {
            zoneMgr.ProcessGeneratedLocalChangeLists(new List<ZoneChangeList> { list }, zoneMgr.GetCancellationToken());
        }
    }

    private static List<BoardCard> Describe(List<Card> cards)
    {
        var row = new List<BoardCard>(cards.Count);
        foreach (var card in cards)
        {
            var entity = card == null ? null : card.GetEntity();
            if (card == null || entity == null)
            {
                continue;
            }

            bool onServerBoard = entity.GetRealTimeZone() == TAG_ZONE.PLAY && !entity.HasQueuedControllerTagChange();
            row.Add(new BoardCard(entity.GetEntityId(), card.GetZonePosition(), entity.GetZonePosition(), onServerBoard,
                entity.GetRealTimeZonePosition()));
        }

        return row;
    }

    private static Card? Find(List<Card> cards, int entityId)
    {
        foreach (var card in cards)
        {
            var entity = card == null ? null : card.GetEntity();
            if (entity != null && entity.GetEntityId() == entityId)
            {
                return card;
            }
        }

        return null;
    }

    private static bool IsConfirmedByClient(ZoneChangeList list)
    {
        var field = NoDancePlugin.IgnoreCardZoneChangesField;
        return field != null && field.GetValue(list) is bool ignored && ignored;
    }

    /// <summary>The list's block type for the log (PLAY, TRIGGER…), read by name: its enum lives outside Assembly-CSharp.</summary>
    private static string BlockType(ZoneChangeList list)
    {
        var blockStart = list.GetTaskList()?.GetBlockStart();
        if (blockStart == null)
        {
            return "no block";
        }

        if (!s_blockTypeLookedUp)
        {
            s_blockTypeLookedUp = true;
            s_blockTypeProperty = blockStart.GetType().GetProperty("BlockType");
        }

        return s_blockTypeProperty?.GetValue(blockStart, null)?.ToString() ?? "?";
    }

    // ---- the client's option listeners --------------------------------------------------------------------------------

    /// <summary>Registers on the current GameState, which may not exist yet when the zone manager wakes up.</summary>
    private void EnsureListeners()
    {
        var gameState = GameState.Get();
        if (ReferenceEquals(gameState, _listenersOn))
        {
            return;
        }

        Unregister();
        if (gameState == null)
        {
            return;
        }

        gameState.RegisterOptionsReceivedListener(_onOptionsReceived, null);
        gameState.RegisterOptionRejectedListener(_onOptionRejected, null);
        _listenersOn = gameState;
    }

    private void Unregister()
    {
        if (_listenersOn == null)
        {
            return;
        }

        _listenersOn.UnregisterOptionsReceivedListener(_onOptionsReceived, null);
        _listenersOn.UnregisterOptionRejectedListener(_onOptionRejected, null);
        _listenersOn = null;
    }
}
