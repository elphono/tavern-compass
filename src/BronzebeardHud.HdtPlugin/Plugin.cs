using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Plugins;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// Entry point HDT discovers in its Plugins folder. Hero selection: a stats badge under each offered hero. Shop and
/// combat: the "Compositions" panel (HDT's own comp guides that the lobby can play; the ticked ones and those in progress,
/// or the most probable ones, are the targets, each in its colour; the board's power under the list), the frames on Bob's
/// cards that serve the targets, the labels of choices. The plugin never reads
/// game memory; it only uses what HDT exposes, plus local files and the Firestone downloads managed by
/// <see cref="StatsService"/> and <see cref="CompService"/>.
/// </summary>
public sealed class Plugin : IPlugin
{
    private HeroPickPanel? _panel;
    private PanelMover? _mover;
    private MenuItem? _menu;
    private MenuItem? _moveItem;
    private CompsPanel? _compsPanel;
    private TavernMarkers? _markers;
    private OpponentMmrPanel? _opponentMmr;
    private ChoiceAdvicePanel? _choices;
    private CardStatsService? _cardStats;

    // One guard per feature: an unexpected exception disables that feature alone (see FeatureGuard).
    private readonly FeatureGuard _heroSelectionGuard;
    private readonly FeatureGuard _dataGuard;
    private readonly FeatureGuard _compsGuard;
    private readonly FeatureGuard _markersGuard;
    private readonly FeatureGuard _opponentMmrGuard;
    private readonly FeatureGuard _choiceGuard;
    private readonly FeatureGuard _cardStatsGuard;
    private readonly FeatureGuard _selectionGuard;
    private readonly FeatureGuard _warbandGuard;
    private readonly FeatureGuard _opponentPowerGuard;
    private readonly FeatureGuard _heroCompsGuard;
    private readonly FeatureGuard _compCountGuard;
    private readonly FeatureGuard _pinsGuard;
    private readonly FeatureGuard _pivotsGuard;
    private readonly FeatureGuard _metaGuard;
    private readonly FeatureGuard _compDetailGuard;
    private readonly FeatureGuard _skipCombatGuard;
    private readonly FeatureGuard _bridgeGuard;
    private readonly FeatureGuard _coverGuard;
    private readonly FeatureGuard _lobbyGuard;

    // While a choice is open in the shop, the markers on Bob's cards and the panel are off the screen (ChoiceCover).
    private readonly ChoiceCover _cover = new();

    // HDT's own comp guides (HdtCompGuides), as last read.
    private string? _guidesState;
    private object? _guidesList;
    private HdtCompGuidesSnapshot? _guides;
    private int _guidesVersion;

    // The guides this lobby can play (LobbyGuides): the only list the panel, the targets, the frames, the choices and the
    // pivots are given. Null without guides from HDT. The lobby's tribes are read once known, for the game (ReadLobbyTribes).
    private LobbyGuides? _lobby;
    private string _lobbyKey = string.Empty;
    private int _lobbyVersion;
    private IReadOnlyList<string> _lobbyTribes = Array.Empty<string>();
    private int _lobbyTribesGame = -1;
    private Stopwatch? _lobbyRead;
    private bool _lobbyUnknownLogged;

    // Firestone's compositions bridged to HDT's guides (GuideBridge), and the hero being played on each composition
    // (HeroCompAffinity): what the labels of choices, the frames on Bob's cards and a guide's context line draw on. Null
    // bridge: the labels and frames of before the bridge, word for word. _bridgeVersion changes whenever either changes.
    private IReadOnlyDictionary<string, GuideEvidence>? _bridge;
    private IReadOnlyDictionary<string, HeroCompPick>? _heroEffects;
    private string _bridgeKey = string.Empty;
    private string _heroEffectsKey = string.Empty;
    private int _bridgeVersion;

    // The targets: ticks and colours kept across a plugin reload within a game, forgotten at the next game.
    private readonly CompTargetTracker _tracker = new();
    private CompGuideBoard _compsBoard = CompGuideBoard.Empty;
    private PlayerCards _compsCards = BronzebeardHud.Stats.PlayerCards.None;
    private string _compsKey = string.Empty;
    private int _compsRound = -1;
    private OverlayPhase _compsPhase = OverlayPhase.OutOfGame;
    private int _targetsVersion;
    private int _selectionVersion;
    private int _gameNumber;

    // The "Skip combat" button: shown in combat, acts once per combat (SkipCombatState).
    private SkipCombatPanel? _skipCombat;
    private readonly SkipCombatState _skipState = new();

    // Pins made by click (Tavern Pinning): kept across a plugin reload within a game, forgotten at the next game.
    private readonly GamePins _gamePins = new();
    private int _pinsVersion;
    private HudSettings _settings = HudSettings.Default;

    private string? _warbandLine;
    private int _warbandRound = -1;
    private int _warbandLoggedRound = -1;

    public Plugin()
    {
        _heroSelectionGuard = new FeatureGuard("hero-selection", (n, e) => Disable(n, e, () => _panel?.Hide()));
        // Without it the features still load their data themselves, only later (at hero pick, trinket choice).
        _dataGuard = new FeatureGuard("data-refresh", (n, e) => Disable(n, e, () => { }));
        // The panel, HDT's guides and the targets: one feature since the two composition panels became one (2026-10-04).
        _compsGuard = new FeatureGuard("compositions", (n, e) => Disable(n, e, () =>
        {
            _compsPanel?.Hide();
            _tracker.Reset(); // no target any more: the frames and the labels of choices stop following stale ones
            _targetsVersion++;
        }));
        // The frames and labels on Bob's cards, and the pin buttons above them.
        _markersGuard = new FeatureGuard("tavern-markers", (n, e) => Disable(n, e, () => _markers?.Hide()));
        _opponentMmrGuard = new FeatureGuard("opponent-mmr", (n, e) => Disable(n, e, () => _opponentMmr?.Hide()));
        // Replaces "trinket-choice": trinkets are now one kind of choice among discovers and Dark Gifts.
        _choiceGuard = new FeatureGuard("discover-advice", (n, e) => Disable(n, e, () => _choices?.Hide()));
        // The values of the cards at this turn (Bob's row, a choice's "—"): off alone, the frames and labels stay as before.
        _cardStatsGuard = new FeatureGuard("card-stats", (n, e) => Disable(n, e, () =>
        {
            _tavernKey = string.Empty;
            _choiceKey = string.Empty;
        }));
        // The two rows of the power inset under the panel, one guard each: a row that throws (computing it or drawing it)
        // leaves the inset alone, the other row and the panel keep running.
        _warbandGuard = new FeatureGuard("warband-curve", (n, e) => Disable(n, e, () => _compsPanel?.SetPower(null)));
        _opponentPowerGuard = new FeatureGuard("opponent-power", (n, e) => Disable(n, e, () => _compsPanel?.SetOpponentPower(null)));
        // Its lines are computed inside the hero panel's update; once switched off, they are simply not added.
        _heroCompsGuard = new FeatureGuard("hero-comps", (n, e) => Disable(n, e, () => _shownKey = string.Empty));
        _compCountGuard = new FeatureGuard("comp-count", (n, e) => Disable(n, e, () => { }));
        // Once switched off, the panel and the targets are those of the whole list (LobbyGuides.Unknown, UpdateComps), as
        // before the lobby was read: guides of absent tribes come back, rather than no panel at all.
        _lobbyGuard = new FeatureGuard("lobby-filter", (n, e) => Disable(n, e, () => _lobbyKey = string.Empty));
        _metaGuard = new FeatureGuard("meta-snapshot", (n, e) => Disable(n, e, () =>
        {
            if (_compsPanel != null)
            {
                _compsPanel.MetaEnabled = false;
            }

            _compsKey = string.Empty;
        }));
        // Once switched off, a guide's detail simply shows no pivots.
        _pivotsGuard = new FeatureGuard("comp-pivots", (n, e) => Disable(n, e, () => _compsKey = string.Empty));
        _compDetailGuard = new FeatureGuard("comp-detail", (n, e) => Disable(n, e, () =>
        {
            if (_compsPanel != null)
            {
                _compsPanel.DetailEnabled = false;
            }

            _compsKey = string.Empty;
        }));
        _skipCombatGuard = new FeatureGuard("skip-combat", (n, e) => Disable(n, e, () => _skipCombat?.Hide()));
        // Once switched off: no bridge, so the labels of choices and the frames are those of before it, and no context line.
        _bridgeGuard = new FeatureGuard("guide-bridge", (n, e) => Disable(n, e, () =>
        {
            _bridge = null;
            _heroEffects = null;
            _bridgeVersion++;
        }));
        // Once switched off, nothing stays hidden behind a choice that the feature can no longer see close.
        _coverGuard = new FeatureGuard("choice-cover", (n, e) => Disable(n, e, () =>
        {
            _cover.Reset();
            _markers?.Suspend(false);
            _compsPanel?.Suspend(false);
        }));
        _pinsGuard = new FeatureGuard("tavern-pins", (n, e) => Disable(n, e, () =>
        {
            if (_markers != null)
            {
                _markers.PinButtonsEnabled = false;
            }

            _pinsVersion++;
        }));
        _selectionGuard = new FeatureGuard("comp-selection", (n, e) => Disable(n, e, () =>
        {
            _tracker.Reset();
            _selectionVersion++;
            if (_compsPanel != null)
            {
                _compsPanel.SelectionEnabled = false;
            }
        }));
    }

    /// <summary>%LocalAppData%\BronzebeardHud\settings.json, next to layout.json.</summary>
    private static string SettingsPath => Path.Combine(Path.GetDirectoryName(StatsDirectory)!, "settings.json");

    /// <summary>The − or + of the panel: one target less or more, 1 to 4, kept in settings.json.</summary>
    private void ChangeSuggested(int step) => _compCountGuard.Run(() =>
    {
        _settings = _settings.WithSuggested(_settings.SuggestedCompositions + step);
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, _settings.Serialize());
        if (File.Exists(SettingsPath))
        {
            File.Replace(temp, SettingsPath, null);
        }
        else
        {
            File.Move(temp, SettingsPath);
        }

        Log.Info($"Bronzebeard HUD: suggested compositions={_settings.SuggestedCompositions}");
        _selectionVersion++; // the targets, the panel, the frames and the choices follow, in the shop and in combat
    });

    /// <summary>The "Meta" button of the panel: Firestone's composition tier list in the default browser.</summary>
    private void OpenMetaSnapshot() => _metaGuard.Run(() =>
    {
        Process.Start(new ProcessStartInfo(MetaSnapshot.Url) { UseShellExecute = true });
        Log.Info($"Bronzebeard HUD: meta snapshot opened {MetaSnapshot.Url}");
    });

    /// <summary>
    /// A guide's tick box was clicked in the panel: a ticked guide is a target whatever its score (four at most); while one
    /// is ticked, the other targets are the guides in progress only (CompTargets.Choose).
    /// </summary>
    private void ToggleGuide(string guideId) => _selectionGuard.Run(() =>
    {
        var accepted = _tracker.Toggle(guideId);
        Log.Info(_tracker.ToggleLine(guideId, accepted));
        _selectionVersion++;
    });

    /// <summary>The cards the targets were last ranked on (board and hand), as base card ids.</summary>
    private HashSet<string> HeldCards() => new(_compsCards.All.Select(c => c.CardId), StringComparer.Ordinal);

    /// <summary>A guide's pivots (GuidePivots) among the guides HDT lists, under their own guard; null if that feature failed.</summary>
    private IReadOnlyList<GuidePivot>? PivotsFor(CompGuide guide)
    {
        IReadOnlyList<GuidePivot>? pivots = null;
        _pivotsGuard.Run(() =>
        {
            if (_lobby?.Playable is { } all)
            {
                pivots = GuidePivots.For(guide, all, HeldCards()); // never towards a guide the lobby cannot play
            }
        });
        return pivots;
    }

    /// <summary>One line each time a guide's detail is opened: what the guide lists, and how much of it the panel shows.</summary>
    private void DetailShown(CompGuide guide, SectionFit fit) => _compDetailGuard.Run(() =>
    {
        var pivots = PivotsFor(guide) ?? Array.Empty<GuidePivot>();
        Log.Info($"Bronzebeard HUD: comp detail id={guide.Id} tier={guide.TierLetter} difficulty={CompGuideDifficulty.Text(guide.Difficulty)} " +
                 $"core=[{string.Join(",", guide.CoreCards)}] addons=[{string.Join(",", guide.AddonCards)}] enablers=[{string.Join(",", guide.Enablers)}] " +
                 $"commit lines={guide.WhenToCommitLines.Count} pivots=[{string.Join("; ", pivots.Select(p => $"{p.To.Name} {p.Shared.Count} shared"))}] " +
                 $"sections={fit.Shown.Count} of {fit.Total}");
    });

    private string _loggedHighlights = string.Empty;
    private string _loggedValues = string.Empty;

    /// <summary>
    /// In the shop: Bob's cards that serve the targets (TavernHighlights: core card → solid frame, enabler or add-on →
    /// dotted frame, in the target's colour; with the bridge, a card on at least two top boards of a target's Firestone
    /// comp → dotted "+ T 3/5"; with a guide ticked, the ticked guides alone), the pins, and a pin button above each minion.
    /// Redrawn when Bob's row, the targets, the pins or the bridge change; one log line whenever the highlights change
    /// (TavernHighlights.LogLine).
    /// </summary>
    private void UpdateTavern(GameV2 game)
    {
        if (_markers == null)
        {
            return;
        }

        if (HdtEntityAdapter.Phase(game) != OverlayPhase.Shop)
        {
            _tavernKey = string.Empty;
            _markers.Hide();
            return;
        }

        // Bob's whole row, the tavern spell included: the game centres minions and spell together. Followed by entity:
        // a purchase, a reroll or an added card redraws the markers at once.
        var row = HdtEntityAdapter.TavernRow(game);
        var turn = game.GetTurnNumber();
        var key = string.Join(",", row.Select(s => $"{s.EntityId}:{s.CardId}")) + "|" + _targetsVersion + "|" + _pinsVersion + "|" + _bridgeVersion
                  + "|" + _cardStats?.Version + "|" + turn;
        if (key == _tavernKey)
        {
            return;
        }

        _tavernKey = key;
        var bob = row.Select(s => s.CardId).ToList();
        var targets = _tracker.Targets;
        // With a guide ticked, the ticked guides alone frame Bob's cards (TavernHighlights.Framing).
        var highlights = TavernHighlights.For(bob, targets, _bridge);
        // The top-board frames too ("card:boards 3/5:guide"): written by role alone, they were invisible in the log.
        var line = TavernHighlights.Summary(bob, highlights);
        if (line != _loggedHighlights)
        {
            _loggedHighlights = line;
            Log.Info(TavernHighlights.LogLine(line, targets));
        }

        // The value of each card at this turn, against every card played then (CardTurnValue): the last line left.
        var notes = bob.Select(id => CardNote(id, turn)).ToList();
        var valueLine = string.Join(",", bob.Zip(notes, (id, n) => CardTurnValue.Label(n, int.MaxValue) is { } v ? $"{id}:{v} ({n!.Played})" : null)
            .Where(v => v != null));
        if (valueLine != _loggedValues)
        {
            _loggedValues = valueLine;
            Log.Info($"Bronzebeard HUD: tavern values turn={turn} bracket=mmr-{_cardStats?.Bracket?.ToString() ?? "none"} [{valueLine}]");
        }

        _markers.Show(bob, highlights, _gamePins.Merge(_comps?.Pins ?? TavernPins.Empty), row.Select(s => s.IsMinion).ToList(), notes);
    }

    /// <summary>Shows the "Skip combat" button in combat only, and not again in a combat already skipped.</summary>
    private void UpdateSkipCombat(GameV2 game)
    {
        if (_skipCombat == null)
        {
            return;
        }

        if (_skipState.Observe(HdtEntityAdapter.Phase(game)))
        {
            _skipCombat.Show();
        }
        else
        {
            _skipCombat.Hide();
        }
    }

    /// <summary>
    /// The "Skip combat" button: closes Hearthstone and has Battle.net start it again at once, the reconnection
    /// landing after the combat animation (a known Battlegrounds trick). A local action on processes: no game
    /// memory is read. The client is always started again through Battle.net (SkipCombatPlan.Relaunch): measured
    /// on 2026-09-27, a client started directly cannot log in and shows "could not connect to Blizzard services".
    /// Battle.net is found BEFORE the kill (the client's parent, else a running Battle.net.exe); without it nothing
    /// is killed. After Kill(), at most <see cref="ExitWaitMs"/> for the old client to be gone (it took 100 to
    /// 416 ms on Ali's machine), then Battle.net is asked, again every second until it starts the client
    /// (AskBattleNet, off the UI thread). Log lines with the measures: pid, parent, relaunch command and where it
    /// came from, Kill() time, exit time, the asks and when the new client appeared, whether it is still alive
    /// 3 s later, or the exception.
    /// </summary>
    private void SkipCombat() => _skipCombatGuard.Run(() =>
    {
        if (!_skipState.TryBegin())
        {
            Log.Info("Bronzebeard HUD: skip combat ignored: not in combat, or this combat was already skipped");
            return;
        }

        _skipCombat?.Hide();
        var processes = Array.Empty<Process>();
        var logged = false; // the steps below log their own failure with its context
        try
        {
            processes = Process.GetProcessesByName("Hearthstone");
            if (processes.Length == 0)
            {
                Log.Warn("Bronzebeard HUD: skip combat: GetProcessesByName(\"Hearthstone\") found no process; nothing done");
                return;
            }

            var target = processes.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero) ?? processes[0];
            var pid = target.Id;
            var (parentName, parentPath, parentNote) = ParentOf(pid);
            var plan = SkipCombatPlan.Relaunch(parentName, parentPath, RunningBattleNet());
            var what = $"pid={pid} ({processes.Length} Hearthstone process{(processes.Length > 1 ? "es" : string.Empty)}) parent={parentName ?? "?"}{parentNote} " +
                       $"relaunch=[{plan.File ?? "none"} {plan.Arguments}] (from {plan.Source})";
            if (plan.Refusal != null || !File.Exists(plan.File))
            {
                Log.Warn($"Bronzebeard HUD: skip combat {what}: {plan.Refusal ?? "Battle.net.exe not found on disk; Hearthstone left running"}");
                return;
            }

            var watch = Stopwatch.StartNew();
            try
            {
                target.Kill();
            }
            catch (Exception e)
            {
                Log.Error($"Bronzebeard HUD: skip combat {what}: Kill() failed after {watch.ElapsedMilliseconds} ms: {e.GetType().Name}: {e.Message}");
                logged = true;
                throw;
            }

            var killMs = watch.ElapsedMilliseconds;
            var exited = target.WaitForExit(ExitWaitMs);
            var timing = $"Kill() returned in {killMs} ms, old client exited: {exited} (at {watch.ElapsedMilliseconds} ms, bound {ExitWaitMs} ms)";
            Log.Info($"Bronzebeard HUD: skip combat {what}: {timing}; asking Battle.net");
            AskBattleNet(plan.File!, plan.Arguments, pid);
        }
        catch (Exception e) when (!logged)
        {
            // Anything else (a process gone while it was being read, say): still one line with what is known.
            Log.Error($"Bronzebeard HUD: skip combat failed ({processes.Length} Hearthstone process(es) found): {e.GetType().Name}: {e.Message}");
            throw;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    });

    /// <summary>How long to wait for the killed client to be gone before asking Battle.net (measured: 350 to 416 ms).</summary>
    private const int ExitWaitMs = 1500;

    /// <summary>
    /// Asking Battle.net again while no new client appears: every <see cref="AskEveryMs"/>, <see cref="MaxAsks"/>
    /// times at most (12 s). Measured 2026-09-27: Battle.net ignored the asks made 0.1, 2.2 and 4.2 s after the
    /// kill and took the one at 6.2 s, the client appearing 0.5 s later; asking every second gets it at the first
    /// second Battle.net accepts.
    /// </summary>
    private const int AskEveryMs = 1000;
    private const int MaxAsks = 12;

    /// <summary>After the new client appeared, how long before checking it is still alive.</summary>
    private const int AliveCheckMs = 3000;

    /// <summary>The running client's parent process (WMI: ParentProcessId, then that process's name and path).</summary>
    private static (string? Name, string? Path, string Note) ParentOf(int pid)
    {
        try
        {
            using var child = new System.Management.ManagementObjectSearcher($"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId={pid}");
            var parentId = child.Get().Cast<System.Management.ManagementObject>().Select(o => (uint?)o["ParentProcessId"]).FirstOrDefault();
            if (parentId == null)
            {
                return (null, null, " (no WMI entry)");
            }

            using var parent = new System.Management.ManagementObjectSearcher($"SELECT Name, ExecutablePath FROM Win32_Process WHERE ProcessId={parentId}");
            var row = parent.Get().Cast<System.Management.ManagementObject>().FirstOrDefault();
            return row == null ? (null, null, $" (parent {parentId} gone)") : ((string?)row["Name"], (string?)row["ExecutablePath"], string.Empty);
        }
        catch (Exception e) when (e is System.Management.ManagementException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            return (null, null, $" (WMI: {e.GetType().Name}: {e.Message})");
        }
    }

    /// <summary>The executable of a running Battle.net.exe, when one can be read; null otherwise.</summary>
    private static string? RunningBattleNet()
    {
        foreach (var process in Process.GetProcessesByName("Battle.net"))
        {
            using (process)
            {
                try
                {
                    return process.MainModule?.FileName;
                }
                catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Next one, if any.
                }
            }
        }

        return null;
    }

    /// <summary>A Hearthstone process other than the killed one; null when there is none yet.</summary>
    private static int? NewClient(int oldPid)
    {
        var processes = Process.GetProcessesByName("Hearthstone");
        try
        {
            return processes.Select(p => p.Id).Where(id => id != oldPid).Select(id => (int?)id).FirstOrDefault();
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    /// <summary>
    /// From a background task, off HDT's UI thread: asks Battle.net to start Hearthstone, and again every
    /// <see cref="AskEveryMs"/> while no new client appears (measured 2026-09-27: asked 112 ms after the kill,
    /// Battle.net started nothing within 50 s; asked again every 2 s, it started it after the ask at 6.2 s; asked
    /// when idle, it started it in 582 ms). One log line with the number
    /// of asks and when the new client appeared, or that it did not; then whether it is still alive
    /// <see cref="AliveCheckMs"/> later.
    /// </summary>
    private static void AskBattleNet(string battleNet, string arguments, int oldPid) => System.Threading.Tasks.Task.Run(async () =>
    {
        var clock = Stopwatch.StartNew();
        var asks = 0;
        int? fresh = null;
        try
        {
            while (fresh == null && asks < MaxAsks)
            {
                using (Process.Start(new ProcessStartInfo(battleNet, arguments) { WorkingDirectory = Path.GetDirectoryName(battleNet), UseShellExecute = false }))
                {
                }

                asks++;
                var until = clock.ElapsedMilliseconds + AskEveryMs;
                while (clock.ElapsedMilliseconds < until && (fresh = NewClient(oldPid)) == null)
                {
                    await System.Threading.Tasks.Task.Delay(100).ConfigureAwait(false);
                }
            }

            Log.Info($"Bronzebeard HUD: skip combat: Battle.net asked {asks} time(s); " + (fresh is { } pid
                ? $"new client pid={pid} appeared {clock.ElapsedMilliseconds} ms after the first ask"
                : $"no new client after {clock.ElapsedMilliseconds} ms — start Hearthstone from Battle.net"));
            if (fresh is { } started)
            {
                await System.Threading.Tasks.Task.Delay(AliveCheckMs).ConfigureAwait(false);
                Log.Info($"Bronzebeard HUD: skip combat: restarted pid={started} still alive after {AliveCheckMs} ms: {NewClient(oldPid) == started}");
            }
        }
        catch (Exception e)
        {
            Log.Error($"Bronzebeard HUD: skip combat: asking Battle.net failed after {asks} ask(s), {clock.ElapsedMilliseconds} ms: {e.GetType().Name}: {e.Message}; start Hearthstone from Battle.net");
        }
    });

    /// <summary>A pin button was clicked above one of Bob's cards.</summary>
    private void TogglePin(string cardId) => _pinsGuard.Run(() =>
    {
        var file = _comps?.Pins ?? TavernPins.Empty;
        _gamePins.Toggle(cardId, file);
        Log.Info($"Bronzebeard HUD: pinned=[{string.Join(",", _gamePins.Merge(file).CardIds)}]");
        _pinsVersion++;
    });

    /// <summary>Report a disabled feature once in HDT's log, then take its panel off the screen.</summary>
    private static void Disable(string feature, Exception error, Action hide)
    {
        Log.Error($"Bronzebeard HUD: feature \"{feature}\" disabled for this session after {error.GetType().FullName}: {error.Message}");
        Log.Error(error);
        try
        {
            hide();
        }
        catch (Exception)
        {
            // The panel is already broken; the other features keep running.
        }
    }

    private string _choiceKey = string.Empty;
    private string _loggedChoice = string.Empty;
    private StatsService? _stats;
    private CompService? _comps;
    private bool _inHeroSelection;
    private bool _compsLoadedThisGame;
    private string? _loggedCompStatus;
    private string _shownKey = string.Empty;
    private string _tavernKey = string.Empty;
    private string _opponentKey = string.Empty;

    /// <summary>%LocalAppData%\BronzebeardHud\stats; hand-typed HSReplay files go in its "manual" subfolder.</summary>
    internal static string StatsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BronzebeardHud", "stats");

    public string Name => "Tavern Compass";

    public string Description =>
        "Battlegrounds comp targets from HDT's comp guides, frames on Bob's cards, help with choices, a board-power gauge, and hero-pick stats (Firestone public aggregates), on top of HDT's overlay.";

    public string ButtonText => "Move panels (on/off)";
    public string Author => "elphono";
    public Version Version => new(0, 3, 0);
    /// <summary>HDT's Plugins menu: move mode on/off, reset panel places, open the data folder.</summary>
    public MenuItem MenuItem => _menu ??= BuildMenu();

    private MenuItem BuildMenu()
    {
        var menu = new MenuItem { Header = "Tavern Compass" };
        _moveItem = new MenuItem { Header = "Move panels", IsCheckable = true };
        _moveItem.Click += (_, _) => ToggleMoveMode();
        var reset = new MenuItem { Header = "Reset panel positions" };
        reset.Click += (_, _) => _mover?.Reset();
        var folder = new MenuItem { Header = "Open data folder" };
        folder.Click += (_, _) => OpenDataFolder();
        menu.Items.Add(_moveItem);
        menu.Items.Add(reset);
        menu.Items.Add(folder);
        return menu;
    }

    private void ToggleMoveMode()
    {
        if (_mover == null)
        {
            return;
        }

        _mover.ToggleMoveMode();
        if (_moveItem != null)
        {
            _moveItem.IsChecked = _mover.MoveMode;
        }

        Log.Info($"Bronzebeard HUD: move mode {(_mover.MoveMode ? "on" : "off")}");
    }

    private static void OpenDataFolder()
    {
        Directory.CreateDirectory(Path.Combine(StatsDirectory, "manual"));
        Process.Start("explorer.exe", Path.GetDirectoryName(StatsDirectory)!);
    }

    public void OnLoad()
    {
        // HDT calls OnLoad again on this same instance when the plugin is disabled then re-enabled
        // (Plugins/PluginWrapper.cs:58-94): what was remembered for the previous panels must go with them.
        ResetSessionState();
        Directory.CreateDirectory(Path.Combine(StatsDirectory, "manual"));
        _stats = new StatsService(StatsDirectory);
        _comps = new CompService(StatsDirectory);
        _mover = new PanelMover(Core.OverlayCanvas, Path.Combine(Path.GetDirectoryName(StatsDirectory)!, "layout.json"));
        try
        {
            string? settingsError;
            (_settings, settingsError) = HudSettings.Parse(File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : null);
            if (settingsError != null)
            {
                Log.Warn("Bronzebeard HUD: " + settingsError);
            }
        }
        catch (IOException e)
        {
            Log.Warn($"Bronzebeard HUD: cannot read {SettingsPath}: {e.Message}");
        }
        _panel = new HeroPickPanel(Core.OverlayCanvas);
        _compsPanel = new CompsPanel(Core.OverlayCanvas, _mover, ToggleGuide, () => _settings.SuggestedCompositions, ChangeSuggested, OpenMetaSnapshot,
            PivotsFor, DetailShown, guide => TargetContext.For(guide, _bridge, _heroEffects), action => _compsGuard.Run(action),
            action => _warbandGuard.Run(action), action => _opponentPowerGuard.Run(action), line => Log.Info(line));
        _markers = new TavernMarkers(Core.OverlayCanvas, TogglePin);
        _opponentMmr = new OpponentMmrPanel(Core.OverlayCanvas);
        _choices = new ChoiceAdvicePanel(Core.OverlayCanvas, StatsDirectory);
        _cardStats = new CardStatsService(StatsDirectory);
        _skipCombat = new SkipCombatPanel(Core.OverlayCanvas, _mover, SkipCombat);
    }

    private void ResetSessionState()
    {
        _inHeroSelection = false;
        _compsLoadedThisGame = false;
        _loggedCompStatus = null;
        _shownKey = string.Empty;
        _tavernKey = string.Empty;
        _loggedHighlights = string.Empty;
        _choiceKey = string.Empty;
        _loggedChoice = string.Empty;
        _opponentKey = string.Empty;
        _warbandLine = null;
        _warbandRound = -1;
        _warbandLoggedRound = -1;
        _opponentPowerLine = string.Empty;
        _guidesState = null;
        _guidesList = null;
        _guides = null;
        _lobby = null;
        _lobbyKey = string.Empty;
        _lobbyTribes = Array.Empty<string>();
        _lobbyTribesGame = -1;
        _lobbyRead = null;
        _lobbyUnknownLogged = false;
        _compsKey = string.Empty;
        _compsBoard = CompGuideBoard.Empty;
        _compsCards = BronzebeardHud.Stats.PlayerCards.None;
        _compsRound = -1;
        _compsPhase = OverlayPhase.OutOfGame;
        _bridge = null;
        _heroEffects = null;
        _bridgeKey = string.Empty;
        _heroEffectsKey = string.Empty;
        _cover.Reset(); // the panels are new: nothing of them is hidden yet
    }

    public void OnUnload()
    {
        _panel?.Detach();
        _compsPanel?.Detach();
        _compsPanel = null;
        _markers?.Detach();
        _markers = null;
        _opponentMmr?.Detach();
        _opponentMmr?.Dispose();
        _opponentMmr = null;
        _choices?.Detach();
        _choices?.Dispose();
        _cardStats?.Dispose();
        _cardStats = null;
        _choices = null;
        _skipCombat?.Detach();
        _skipCombat = null;
        _mover?.Detach();
        _panel = null;
        _stats?.Dispose();
        _comps?.Dispose();
        _stats = null;
        _comps = null;
    }

    public void OnButtonPress() => ToggleMoveMode();

    public void OnUpdate()
    {
        var game = Core.Game;
        if (game == null)
        {
            return;
        }

        _dataGuard.Run(() => RefreshData(game));
        _heroSelectionGuard.Run(() => UpdateHeroSelection(game));
        _compsGuard.Run(() => UpdateComps(game)); // the targets first: the frames and the choices follow them
        _markersGuard.Run(() => UpdateTavern(game));
        _coverGuard.Run(() => UpdateChoiceCover(game)); // in the same update: a choice that opens never shows them over it
        _opponentMmrGuard.Run(() => UpdateOpponentMmr(game));
        _choiceGuard.Run(() => UpdateChoice(game));
        _warbandGuard.Run(() => UpdateWarband(game));
        _opponentPowerGuard.Run(() => UpdateOpponentPower(game));
        _skipCombatGuard.Run(() => UpdateSkipCombat(game));
    }

    /// <summary>
    /// The "Compositions" panel, in the shop and in combat. HDT's guides are read whenever its list or its state changes
    /// (one log line: where from, how many, which tiers), and narrowed to the ones the lobby can play (UpdateLobby,
    /// LobbyGuides: one log line); in the shop those are ranked against the board and the hand whenever those change
    /// (CompTargets.Round, CompGuideMatch), and the targets follow (CompTargetTracker: the ticked guides, then the guides in
    /// progress; with nothing ticked the most probable ones, up to the number set by − n +; each keeping its colour while it
    /// stays a target). In combat the shop's cards stay (minions die there); a tick or a change of the number still redraws
    /// at once. One log line per shop round, when it ends, with the targets (CompTargets.RoundLine).
    /// </summary>
    private void UpdateComps(GameV2 game)
    {
        if (_compsPanel == null)
        {
            return;
        }

        var phase = HdtEntityAdapter.Phase(game);
        if (phase != _compsPhase)
        {
            _compsPhase = phase;
            _compsPanel.HideGuidePopup(); // the tavern closes (or opens): a guide's popup does not outlive it
        }

        if (phase is not (OverlayPhase.Shop or OverlayPhase.Combat))
        {
            LogCompsRound();
            _compsKey = string.Empty;
            _compsPanel.Hide();
            return;
        }

        var (state, list) = HdtCompGuides.Peek();
        if (_guides == null || state != _guidesState || !ReferenceEquals(list, _guidesList))
        {
            _guidesState = state;
            _guidesList = list;
            _guides = HdtCompGuides.Read();
            _guidesVersion++;
            Log.Info(_guides.Guides is { } loaded
                ? $"Bronzebeard HUD: comp guides loaded from HDT ({loaded.Source}, state {_guides.State}): {loaded.Count} comps, tiers [{loaded.TierSummary}], unknown cards {loaded.UnknownCards}"
                : $"Bronzebeard HUD: comp guides: none from HDT (state {_guides.State}){(_guides.Error != null ? ": " + _guides.Error : string.Empty)}");
        }

        _bridgeGuard.Run(() => UpdateBridge(game));
        _lobbyGuard.Run(() => UpdateLobby(game, phase));
        if (_guides.Guides is { } listed && (_lobby == null || !ReferenceEquals(_lobby.All, listed)))
        {
            _lobby = LobbyGuides.Unknown(listed); // the filter switched off by its guard: every guide, as before it
            _lobbyVersion++;
        }
        else if (_guides.Guides == null && _lobby != null)
        {
            _lobby = null;
            _lobbyVersion++;
        }

        var cards = _compsCards;
        if (phase == OverlayPhase.Shop)
        {
            var round = game.GetTurnNumber();
            if (round != _compsRound)
            {
                LogCompsRound();
                _compsRound = round;
            }

            cards = HdtEntityAdapter.PlayerCards(game);
        }
        else
        {
            LogCompsRound(); // the shop round is over: its last targets are the ones logged
        }

        var count = _settings.SuggestedCompositions;
        var bracket = _stats?.Bracket ?? MmrBracket.EveryPlayer;
        _compsPanel.Bracket = _stats == null ? null : (BracketChoice.Label(bracket), NextBracket);
        var key = string.Join(",", cards.All.Select(c => c.CardId).OrderBy(id => id, StringComparer.Ordinal)) + "|" + _guidesVersion + "|" + _selectionVersion + "|" + count
                  + "|" + bracket       // the bracket button says which one is shown
                  + "|" + _bridgeVersion   // a guide's context line (detail, popup) follows the bridge and the hero
                  + "|" + _lobbyVersion;   // the lobby's tribes, once known, take guides out of the list and the targets
        if (key == _compsKey && _compsPanel.IsVisible)
        {
            return;
        }

        _compsKey = key;
        _compsCards = cards;
        if (_lobby != null)
        {
            // Only the guides the lobby can play are ranked, listed and targeted: CompTargets.Round takes LobbyGuides, never
            // a bare list (2026-10-06: guides of absent tribes were targets in 15 of Ali's 69 shop rounds).
            var round = CompTargets.Round(_lobby, cards, _tracker, count);
            foreach (var line in round.Unticked)
            {
                Log.Info(line);
            }

            _compsBoard = round.Board;
        }
        else
        {
            _compsBoard = CompGuideBoard.Empty;
            _tracker.Next(_compsBoard, count);
        }

        _targetsVersion++;
        var note = _lobby is { Known: false } && _lobby.All.Count > 0 ? "Lobby tribes unknown: every guide listed" : null;
        _compsPanel.Show(_compsBoard, _tracker.Targets, cards.All.Select(c => c.CardId), _guides.Guides?.Source, CompGuidesStatus(_guides), note);
    }

    /// <summary>
    /// The lobby's tribes (HdtEntityAdapter.LobbyTribeNames: HDT's BattlegroundsUtils.GetAvailableRaces, which HDT reads from
    /// the game's memory and keeps per game), asked for until known, once a second at most, then kept for the game. One log
    /// line when they are first known, saying when ("hero selection", "shop turn 1"), so that when HDT has them can be
    /// measured; one line if the shop opens without them.
    /// </summary>
    private IReadOnlyList<string> ReadLobbyTribes(string when)
    {
        if (_lobbyTribesGame != _gameNumber)
        {
            _lobbyTribesGame = _gameNumber;
            _lobbyTribes = Array.Empty<string>();
            _lobbyRead = null;
            _lobbyUnknownLogged = false;
        }

        if (_lobbyTribes.Count == 0 && (_lobbyRead == null || _lobbyRead.ElapsedMilliseconds >= LobbyReadEveryMs))
        {
            _lobbyRead = Stopwatch.StartNew();
            _lobbyTribes = HdtEntityAdapter.LobbyTribeNames();
            if (_lobbyTribes.Count > 0)
            {
                Log.Info($"Bronzebeard HUD: lobby tribes=[{string.Join(",", _lobbyTribes)}] read at {when}");
            }
        }

        if (_lobbyTribes.Count == 0 && !_lobbyUnknownLogged && when.StartsWith("shop", StringComparison.Ordinal))
        {
            _lobbyUnknownLogged = true;
            Log.Info($"Bronzebeard HUD: lobby tribes unknown at {when}: no guide left out until HDT has them");
        }

        return _lobbyTribes;
    }

    /// <summary>While the lobby's tribes are unknown, HDT is asked again after this long (each ask may read the game's memory).</summary>
    private const int LobbyReadEveryMs = 1000;

    /// <summary>
    /// The guides the lobby can play (LobbyGuides.Of, with HearthDb's tribes of each key card), again whenever HDT's list or
    /// the lobby's tribes change, with one log line: the tribes, how many guides are playable, and why each other one is left
    /// out ("a beast guide (no BEAST)").
    /// </summary>
    private void UpdateLobby(GameV2 game, OverlayPhase phase)
    {
        var when = $"{(phase == OverlayPhase.Shop ? "shop" : "combat")} turn {game.GetTurnNumber()}";
        var tribes = ReadLobbyTribes(when);
        var key = _guidesVersion + "|" + string.Join(",", tribes);
        if (key == _lobbyKey)
        {
            return;
        }

        _lobbyKey = key;
        _lobby = _guides?.Guides is { } guides ? LobbyGuides.Of(guides, tribes, HdtEntityAdapter.CardTribes) : null;
        _lobbyVersion++;
        if (_lobby != null)
        {
            Log.Info(_lobby.Line(when));
        }
    }

    /// <summary>
    /// Bridges HDT's guides to the known compositions (GuideBridge: Firestone's, and hand-typed ones if any) whenever either
    /// changes — a new list of guides, a composition load —, with one log line each time: every guide and its composition or
    /// "no match", then how many were bridged and against what ("(2/23 guides bridged, against 24 compositions; Firestone
    /// ok)"), so that the match can be judged on real data. Without guides from HDT, no bridge (null). The hero being played
    /// on each composition (HeroCompAffinity, for the context line) follows the hero and the compositions. Either change
    /// bumps _bridgeVersion: the panel, the frames and the labels of choices are drawn again.
    /// </summary>
    private void UpdateBridge(GameV2 game)
    {
        if (_comps == null)
        {
            return;
        }

        var key = _guidesVersion + "|" + _comps.Version;
        if (key != _bridgeKey)
        {
            _bridgeKey = key;
            if (_guides?.Guides is { } guides)
            {
                var comps = _comps.Compositions();
                _bridge = GuideBridge.For(guides, comps);
                var bridged = guides.All.Count(g => _bridge.ContainsKey(g.Id));
                Log.Info(GuideBridge.Line(guides, _bridge) + $" ({bridged}/{guides.Count} guides bridged, against {comps.Count} compositions; Firestone {_comps.State})");
            }
            else
            {
                _bridge = null;
            }

            _bridgeVersion++;
        }

        var hero = HdtEntityAdapter.PlayerHeroId(game);
        var heroKey = hero + "|" + _comps.Version;
        if (heroKey != _heroEffectsKey)
        {
            _heroEffectsKey = heroKey;
            _heroEffects = HeroCompAffinity.Effects(hero, _comps.Compositions());
            _bridgeVersion++;
        }
    }

    /// <summary>The line the panel shows when HDT gives no guides, in HDT's own terms; null when it gives some.</summary>
    private static string? CompGuidesStatus(HdtCompGuidesSnapshot snapshot)
    {
        if (snapshot.Error != null)
        {
            return "HDT's comp guides could not be read: " + snapshot.Error;
        }

        if (snapshot.Guides != null)
        {
            return snapshot.Guides.Count == 0 ? "HDT lists no comp guide" : null;
        }

        return snapshot.State switch
        {
            "Loading" => "HDT is loading its comp guides…",
            "Error" => "HDT could not load its comp guides",
            "Empty" => "HDT lists no comp guide",
            _ => $"HDT shows no comp guide ({snapshot.State})",
        };
    }

    /// <summary>One line when a shop round ends: the targets for the cards held at its end.</summary>
    private void LogCompsRound()
    {
        if (_compsRound < 0)
        {
            return;
        }

        Log.Info(CompTargets.RoundLine(_compsRound, _guides?.Guides?.Source, _compsBoard.Count, _compsCards, _tracker.Targets));
        _compsRound = -1;
    }

    /// <summary>
    /// From the first update after the plugin starts, in or out of a game: load every Firestone file (hero stats,
    /// compositions, trinkets). Each one's first load in a plugin session asks Firestone's server whatever the
    /// age of the cache (StatsCache), so starting HDT brings the freshest data; later loads keep the age rules; the
    /// compositions are asked again at each game's first shop. One line in HDT's log per finished load (DataRefresh).
    /// Firestone's compositions are no longer shown (HDT's guides replaced them on 2026-10-04): the hero badges still use
    /// them, the bridge to HDT's guides draws on them (UpdateBridge), and the hand-typed pins of stats\manual\pins.txt
    /// come with them.
    /// </summary>
    private void RefreshData(GameV2 game)
    {
        if (_stats == null || _comps == null || _choices == null)
        {
            return;
        }

        if (HdtEntityAdapter.Phase(game) == OverlayPhase.Shop && !_compsLoadedThisGame)
        {
            _compsLoadedThisGame = true;
            _comps.BeginGame();
        }

        _stats.EnsureStarted(game.CurrentBattlegroundsRating);
        _stats.Poll();
        _comps.Poll();
        _choices.PollTrinketStats();
        _cardStatsGuard.Run(() => PollCardStats());
        foreach (var line in new[] { _stats.PendingLogLine, _comps.PendingLogLine, _choices.PendingLogLine })
        {
            if (line != null)
            {
                Log.Info(line);
            }
        }

        _stats.PendingLogLine = null;
        _comps.PendingLogLine = null;
        _choices.PendingLogLine = null;

        // A hand-typed file that cannot be read (pins.txt, *.comps.txt) is said once: no panel shows it any more.
        var status = _comps.State == "loading" ? _loggedCompStatus : _comps.Status;
        if (status != _loggedCompStatus)
        {
            _loggedCompStatus = status;
            if (status != null)
            {
                Log.Warn($"Bronzebeard HUD: compositions data: {status}");
            }
        }
    }

    /// <summary>
    /// The first row of the power inset under the panel, in the shop and in combat: the board's power, its attack plus health
    /// against the average of the same hero at the same turn (Firestone warbandStats), drawn as four lamps, the lit one
    /// glowing, and a coloured badge (BoardPowerView, WarbandCurve.Compare's level), and one line per round in HDT's log, with
    /// the level ("… · +18% power=even", "power=none (too early)").
    /// </summary>
    private void UpdateWarband(GameV2 game)
    {
        if (_compsPanel == null || _stats == null)
        {
            return;
        }

        var phase = HdtEntityAdapter.Phase(game);
        var hero = phase is OverlayPhase.Shop or OverlayPhase.Combat ? HdtEntityAdapter.PlayerHeroId(game) : null;
        if (hero == null)
        {
            _warbandLine = null;
            _compsPanel.SetPower(null);
            return;
        }

        _stats.EnsureStarted(game.CurrentBattlegroundsRating);
        _stats.Poll();
        var round = game.GetTurnNumber();
        if (phase == OverlayPhase.Combat && _warbandLine != null)
        {
            // In combat minions die: the line stays as the shop left it, the board the combat started with.
            if (_warbandRound == round && _warbandLoggedRound != round)
            {
                _warbandLoggedRound = round;
                Log.Info($"Bronzebeard HUD: warband round={round} hero={hero} {_warbandLine}");
            }

            return;
        }

        var comparison = WarbandCurve.Compare(round, WarbandCurve.BoardStats(HdtEntityAdapter.BoardMinionStats(game)), hero, _stats.Sources());
        _warbandRound = round;
        _warbandLine = comparison.Line + " " + comparison.PowerText;
        _compsPanel.SetPower(comparison);
    }

    private string _opponentPowerLine = string.Empty;
    private readonly CombatOpponentKeeper _combatOpponent = new();

    /// <summary>
    /// The second row of the power inset: the opponent's board against THEIR hero's average (OpponentPower) — in combat the
    /// board being fought, as HDT snapshotted it when the combat began, kept to the end of the combat once HDT no longer
    /// has their hero in play (CombatOpponentKeeper); in the shop the next opponent's last board HDT saw, against their hero's
    /// average at the turn it was seen; grey with the reason without one. One line in HDT's log each time it changes, with
    /// the measure it rests on (OpponentPower.LogLine).
    /// </summary>
    private void UpdateOpponentPower(GameV2 game)
    {
        if (_compsPanel == null || _stats == null)
        {
            return;
        }

        var phase = HdtEntityAdapter.Phase(game);
        if (phase is not (OverlayPhase.Shop or OverlayPhase.Combat))
        {
            _opponentPowerLine = string.Empty;
            _combatOpponent.Forget();
            _compsPanel.SetOpponentPower(null);
            return;
        }

        var facts = _combatOpponent.Observe(HdtEntityAdapter.OpponentFacts(game, phase));
        var comparison = OpponentPower.Compare(facts, _stats.Sources(), HdtEntityAdapter.HeroName);
        var line = OpponentPower.LogLine(facts, comparison);
        if (line != _opponentPowerLine)
        {
            _opponentPowerLine = line;
            Log.Info(line);
        }

        _compsPanel.SetOpponentPower(comparison);
    }

    /// <summary>
    /// While a choice is open in the shop (ChoiceCover: any kind ChoiceClassifier tells from None), the markers on Bob's
    /// cards and the "Compositions" panel with its popup are off the screen, and back as they were once it closes: nothing
    /// is computed again for that, the panel and the markers keep what they were last given. One log line per transition.
    /// A decision to be confirmed in game by Ali (docs/journal/2026-10-04-panneau-unique.md).
    /// </summary>
    private void UpdateChoiceCover(GameV2 game)
    {
        var options = game.IsBattlegroundsMatch ? HdtEntityAdapter.OfferedOptions(game) : Array.Empty<OfferedOption>();
        if (_cover.Observe(HdtEntityAdapter.Phase(game), ChoiceClassifier.Kind(options)) is { } line)
        {
            Log.Info(line);
        }

        _markers?.Suspend(_cover.Hidden);
        _compsPanel?.Suspend(_cover.Hidden);
    }

    /// <summary>
    /// Any choice of the player (discover, Dark Gift, trinket): a label above each option, from the targets
    /// (ChoiceAdvisor, with the bridge: "· 4/5 boards", "+ T 3/5 boards", "pivot → G (S)"), in the colour of the first
    /// target the option serves. One line in HDT's log per choice, including the ones without a known layout, so that
    /// uncovered kinds show up.
    /// </summary>
    /// <summary>
    /// The panel's bracket button: the next bracket Firestone publishes, for the rest of the game (BracketChoice). Hero
    /// stats, the power gauge, trinkets and card stats all follow it; one log line.
    /// </summary>
    private void NextBracket()
    {
        if (_stats == null)
        {
            return;
        }

        var next = BracketChoice.Next(_stats.Bracket);
        var rating = Core.Game?.CurrentBattlegroundsRating;
        Log.Info($"Bronzebeard HUD: bracket {BracketChoice.Label(next)} chosen in the overlay (was {BracketChoice.Label(_stats.Bracket)}, rating {rating?.ToString() ?? "unknown"})");
        _stats.ChooseBracket(next, rating);
    }

    /// <summary>
    /// Card stats follow the bracket of the hero stats once it is resolved (the player's own), so that the first load is
    /// not spent on the every-player file; one log line per finished load.
    /// </summary>
    private void PollCardStats()
    {
        if (_cardStats == null || _stats == null || !_stats.BracketKnown)
        {
            return;
        }

        _cardStats.SetBracket(_stats.Bracket);
        _cardStats.Poll();
        if (_cardStats.PendingLogLine is { } line)
        {
            Log.Info(line);
            _cardStats.PendingLogLine = null;
        }
    }

    /// <summary>The value of a card at this turn, under the card-stats guard: null when it is off or says nothing.</summary>
    private CardTurnNote? CardNote(string cardId, int turn)
    {
        CardTurnNote? note = null;
        _cardStatsGuard.Run(() => note = _cardStats?.Note(cardId, turn));
        return note;
    }

    private void UpdateChoice(GameV2 game)
    {
        if (_choices == null || _stats == null)
        {
            return;
        }

        var options = game.IsBattlegroundsMatch ? HdtEntityAdapter.OfferedOptions(game) : Array.Empty<OfferedOption>();
        var kind = ChoiceClassifier.Kind(options);
        if (kind == ChoiceKind.None)
        {
            _choiceKey = string.Empty;
            _choices.Hide();
            return;
        }

        var ids = string.Join(",", options.Select(o => o.EntityId));
        if (kind == ChoiceKind.Trinket)
        {
            // Entity ids restart with each game: the game number keeps two games' choices apart.
            _choices.BeginTrinketChoice($"{_gameNumber}:{ids}");
        }

        var loaded = kind == ChoiceKind.Trinket && _choices.PollTrinketStats();
        var turn = game.GetTurnNumber();
        var key = $"{ids}|{_targetsVersion}|{_stats.Bracket}|{_choices.TrinketStatsVersion}|{_bridgeVersion}|{_cardStats?.Version}|{turn}";
        if (key == _choiceKey && !loaded)
        {
            return;
        }

        _choiceKey = key;
        // The lobby's guides only: the fallback "core G (S)" and the pivots never name a guide of an absent tribe.
        var guides = _lobby?.Playable;
        var advice = ChoiceAdvisor.Advise(options, HdtEntityAdapter.PlayerCards(game).All, _tracker.Targets, guides, _lobby?.Tribes ?? Array.Empty<string>(),
            _choices.TrinketStat, _stats.Bracket, _bridge, id => CardNote(id, turn));
        if (advice.HasMarkers)
        {
            _choices.Show(advice);
        }
        else
        {
            _choices.Hide();
        }

        if (ids != _loggedChoice)
        {
            _loggedChoice = ids;
            var guideState = guides?.Source ?? $"none from HDT, state {_guides?.State ?? "unread"}";
            Log.Info(ChoiceAdvisor.DiagnosticLine(options, advice, guides?.Count ?? 0, guideState, _choices.LastLines, _choices.FirstLabel,
                Core.OverlayCanvas.ActualWidth, Core.OverlayCanvas.ActualHeight));
        }
    }

    private void UpdateOpponentMmr(GameV2 game)
    {
        if (_opponentMmr == null)
        {
            return;
        }

        if (!game.IsBattlegroundsMatch || HdtEntityAdapter.IsHeroSelection(game))
        {
            _opponentKey = string.Empty;
            _opponentMmr.Hide();
            return;
        }

        _opponentMmr.EnsureDownloadStarted();
        if (_opponentMmr.Index is not { } index)
        {
            return;
        }

        var opponents = OpponentMmr.Build(HdtEntityAdapter.LobbyOpponents(game), HdtEntityAdapter.LeaderboardPlaces(game), index);
        var key = string.Join(",", opponents.Select(o => $"{o.LeaderboardPlace}:{o.Name}:{o.Row?.Rating}"));
        if (key != _opponentKey)
        {
            _opponentKey = key;
            _opponentMmr.Show(opponents);
        }
    }

    private void UpdateHeroSelection(GameV2 game)
    {
        if (_panel == null || _stats == null)
        {
            return;
        }

        if (!HdtEntityAdapter.IsHeroSelection(game))
        {
            _inHeroSelection = false;
            _shownKey = string.Empty;
            _panel.Hide();
            return;
        }

        if (!_inHeroSelection)
        {
            _inHeroSelection = true;
            _compsLoadedThisGame = false;
            _tracker.BeginGame(++_gameNumber); // a new game: no tick, no colour, no target
            _cardStats?.BeginGame(_gameNumber);
            _gamePins.BeginGame(_gameNumber);
            _selectionVersion++;
            _stats.BeginHeroSelection(game.CurrentBattlegroundsRating);
        }

        _stats.Poll();
        var offered = OfferedHeroes.Select(HdtEntityAdapter.PlayerEntities(game));
        if (offered.Count == 0)
        {
            _panel.Hide();
            return;
        }

        var tribes = ReadLobbyTribes("hero selection");
        _comps?.Poll();
        var key = string.Join(",", offered.Select(h => $"{h.EntityId}:{h.CardId}")) + "|" + _stats.Version + "|" + string.Join(",", tribes)
                  + "|" + (_comps?.Version ?? 0);
        if (key != _shownKey)
        {
            _shownKey = key;
            var sources = _stats.Sources().Select(file => LobbyTribes.Apply(file, tribes)).ToList();

            // Under each hero, the composition it does best with (plan, phase 5.3), guarded on its own.
            IReadOnlyDictionary<int, string> compLines = new Dictionary<int, string>();
            _heroCompsGuard.Run(() =>
            {
                var playable = TavernAdvisor.Playable(_comps?.Compositions() ?? Array.Empty<Composition>(), tribes);
                compLines = offered
                    .Select(h => (h.EntityId, Pick: HeroCompAffinity.Best(h.BaseCardId, playable)))
                    .Where(x => x.Pick != null)
                    .ToDictionary(x => x.EntityId, x => x.Pick!.Label);
                if (_comps?.State == "ok")
                {
                    Log.Info($"Bronzebeard HUD: hero comps [{string.Join("; ", offered.Select(h => $"{h.BaseCardId}: {(compLines.TryGetValue(h.EntityId, out var l) ? l : "none")}"))}]");
                }
            });
            _panel.Show(HeroPickAdvisor.BuildRows(offered, sources), _stats.Status, compLines);
        }
    }
}
