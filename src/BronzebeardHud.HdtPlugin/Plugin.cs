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
/// Entry point HDT discovers in its Plugins folder. Hero selection: a stats badge under each offered
/// hero. Shop: markers under the tavern minions that fit a target composition, and the target
/// composition panel. The plugin never reads game memory; it only uses what HDT exposes, plus
/// local files and the Firestone downloads managed by <see cref="StatsService"/> and <see cref="CompService"/>.
/// </summary>
public sealed class Plugin : IPlugin
{
    private HeroPickPanel? _panel;
    private PanelMover? _mover;
    private MenuItem? _menu;
    private MenuItem? _moveItem;
    private TavernAdvicePanel? _tavern;
    private OpponentMmrPanel? _opponentMmr;
    private ChoiceAdvicePanel? _choices;

    // One guard per feature: an unexpected exception disables that feature alone (see FeatureGuard).
    private readonly FeatureGuard _heroSelectionGuard;
    private readonly FeatureGuard _tavernGuard;
    private readonly FeatureGuard _opponentMmrGuard;
    private readonly FeatureGuard _choiceGuard;
    private readonly FeatureGuard _selectionGuard;
    private readonly FeatureGuard _warbandGuard;
    private readonly FeatureGuard _heroCompsGuard;
    private readonly FeatureGuard _heroAffinityGuard;
    private readonly FeatureGuard _compCountGuard;
    private readonly FeatureGuard _pinsGuard;
    private readonly FeatureGuard _transitionsGuard;
    private readonly FeatureGuard _lineupsGuard;
    private readonly FeatureGuard _metaGuard;
    private readonly FeatureGuard _compDetailGuard;
    private readonly FeatureGuard _skipCombatGuard;
    private readonly FeatureGuard _highlightsGuard;

    // "How top boards field it", opened by the "?" above one of Bob's minions.
    private LineupsPanel? _lineupsPanel;

    // The "Skip combat" button: shown in combat, acts once per combat (SkipCombatState).
    private SkipCombatPanel? _skipCombat;
    private readonly SkipCombatState _skipState = new();

    // Pins made by click (Tavern Pinning): kept across a plugin reload within a game, forgotten at the next game.
    private readonly GamePins _gamePins = new();
    private int _pinsVersion;
    private HudSettings _settings = HudSettings.Default;

    // The hero being played on each composition (HeroCompAffinity), recomputed when the hero or the compositions change.
    private IReadOnlyDictionary<string, HeroCompPick> _heroEffects = new Dictionary<string, HeroCompPick>();
    private string? _heroEffectsHero;
    private int _heroEffectsVersion = -1;
    private string? _warbandLine;
    private int _warbandRound = -1;
    private int _warbandLoggedRound = -1;

    // The compositions Ali ticks: kept across a plugin reload within a game, forgotten at the next game.
    private readonly CompositionSelection _selection = new();
    private int _selectionVersion;
    private int _rowsSelectionVersion = -1;
    private int _gameNumber;

    public Plugin()
    {
        _heroSelectionGuard = new FeatureGuard("hero-selection", (n, e) => Disable(n, e, () => _panel?.Hide()));
        _tavernGuard = new FeatureGuard("tavern-advice", (n, e) => Disable(n, e, () => { _tavern?.HideMarkers(); _tavern?.HidePanel(); }));
        _opponentMmrGuard = new FeatureGuard("opponent-mmr", (n, e) => Disable(n, e, () => _opponentMmr?.Hide()));
        // Replaces "trinket-choice": trinkets are now one kind of choice among discovers and Dark Gifts.
        _choiceGuard = new FeatureGuard("discover-advice", (n, e) => Disable(n, e, () => _choices?.Hide()));
        _warbandGuard = new FeatureGuard("warband-curve", (n, e) => Disable(n, e, () => _tavern?.SetFooter(null)));
        // Its lines are computed inside the hero panel's update; once switched off, they are simply not added.
        _heroCompsGuard = new FeatureGuard("hero-comps", (n, e) => Disable(n, e, () => _shownKey = string.Empty));
        _compCountGuard = new FeatureGuard("comp-count", (n, e) => Disable(n, e, () => { }));
        _metaGuard = new FeatureGuard("meta-snapshot", (n, e) => Disable(n, e, () =>
        {
            if (_tavern != null)
            {
                _tavern.MetaEnabled = false;
            }

            _shownCompStatus = "\u0000";
        }));
        _lineupsGuard = new FeatureGuard("minion-lineups", (n, e) => Disable(n, e, () =>
        {
            if (_tavern != null)
            {
                _tavern.LineupsEnabled = false;
            }

            _lineupsPanel?.Hide();
            _pinsVersion++;
        }));
        _transitionsGuard = new FeatureGuard("comp-transitions", (n, e) => Disable(n, e, () => _shownCompStatus = "\u0000"));
        // Its highlights are drawn with the tavern markers; once switched off, the markers are drawn as before.
        _highlightsGuard = new FeatureGuard("tavern-highlights", (n, e) => Disable(n, e, () => _tavernKey = string.Empty));
        _compDetailGuard = new FeatureGuard("comp-detail", (n, e) => Disable(n, e, () =>
        {
            if (_tavern != null)
            {
                _tavern.DetailEnabled = false;
            }

            _shownCompStatus = "\u0000";
        }));
        _skipCombatGuard = new FeatureGuard("skip-combat", (n, e) => Disable(n, e, () => _skipCombat?.Hide()));
        _pinsGuard =new FeatureGuard("tavern-pins", (n, e) => Disable(n, e, () =>
        {
            if (_tavern != null)
            {
                _tavern.PinButtonsEnabled = false;
            }

            _pinsVersion++;
        }));
        _heroAffinityGuard = new FeatureGuard("hero-affinity", (n, e) => Disable(n, e, () =>
        {
            _heroEffects = new Dictionary<string, HeroCompPick>();
            _tavernKey = string.Empty;
        }));
        _selectionGuard = new FeatureGuard("comp-selection", (n, e) => Disable(n, e, () =>
        {
            _selection.Clear();
            _selectionVersion++;
            if (_tavern != null)
            {
                _tavern.SelectionEnabled = false;
            }
        }));
    }

    /// <summary>%LocalAppData%\BronzebeardHud\settings.json, next to layout.json.</summary>
    private static string SettingsPath => Path.Combine(Path.GetDirectoryName(StatsDirectory)!, "settings.json");

    /// <summary>The − or + of the target panel: one suggestion less or more, 1 to 8, kept in settings.json.</summary>
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
        _selectionVersion++; // redraws the markers, the choices and the panel, in the shop and in combat
    });

    /// <summary>The "Meta" button of the target panel: Firestone's composition tier list in the default browser.</summary>
    private void OpenMetaSnapshot() => _metaGuard.Run(() =>
    {
        Process.Start(new ProcessStartInfo(MetaSnapshot.Url) { UseShellExecute = true });
        Log.Info($"Bronzebeard HUD: meta snapshot opened {MetaSnapshot.Url}");
    });

    /// <summary>How top players field a minion, from the compositions playable in this lobby; null if the feature failed.</summary>
    private MinionLineups? LineupsFor(string cardId)
    {
        MinionLineups? lineups = null;
        _lineupsGuard.Run(() =>
        {
            var playable = _lastAdvice?.Playable ?? _comps?.Compositions() ?? Array.Empty<Composition>();
            lineups = MinionLineups.For(cardId, playable);
            Log.Info($"Bronzebeard HUD: lineups card={lineups.CardId} position={lineups.UsualPosition?.ToString() ?? "none"} " +
                     $"comps=[{string.Join("; ", lineups.Compositions.Select(c => c.Label))}]");
        });
        return lineups;
    }

    /// <summary>
    /// A composition's detail block (its ▸ in the target panel), derived from its card lists and final boards;
    /// null if the feature failed. Tiers come from HearthDb through HDT (0 = no TECH_LEVEL, counted as unknown).
    /// </summary>
    private CompDetail? DetailFor(Composition composition)
    {
        if (_details.TryGetValue(composition, out var known))
        {
            return known;
        }

        CompDetail? detail = null;
        _compDetailGuard.Run(() =>
        {
            detail = CompDetail.For(composition, id => Database.GetCardFromId(id)?.TechLevel);
            _details[composition] = detail;
            static string Cards(IEnumerable<CompDetailCard> cards) =>
                string.Join(",", cards.Select(c => $"{c.CardId}:T{c.TechLevel?.ToString() ?? "?"}x{c.FinalBoards}"));
            Log.Info($"Bronzebeard HUD: comp detail id={composition.Id} boards={composition.FinalBoards.Count} " +
                     $"enablers=[{Cards(detail.EarlyEnablers)}] commit=[{Cards(detail.CommitCards)}] " +
                     $"turn={detail.TypicalFinalTurn?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}");
        });
        return detail;
    }

    // Each composition's detail, computed and logged once (compositions are the same objects until the cache is reloaded).
    private readonly Dictionary<Composition, CompDetail> _details = new();
    private string _loggedHighlights = string.Empty;

    /// <summary>
    /// Bob's cards that matter for the compositions aimed at (TavernHighlights), under their own guard; null if the
    /// feature failed. One log line whenever the highlights change.
    /// </summary>
    private IReadOnlyList<TavernHighlight>? HighlightsFor(IReadOnlyList<string> bob, IReadOnlyList<Composition> ticked, IReadOnlyList<Composition> suggestions)
    {
        IReadOnlyList<TavernHighlight>? highlights = null;
        _highlightsGuard.Run(() =>
        {
            highlights = TavernHighlights.For(bob, ticked, suggestions, DetailFor, _selection.ColourOf);
            var line = string.Join(",", bob.Zip(highlights, (card, h) => (card, h))
                .Where(x => x.h.Kind != HighlightKind.None)
                .Select(x => $"{x.card}:{x.h.Tag}:{x.h.Composition!.Id}"));
            if (line != _loggedHighlights)
            {
                _loggedHighlights = line;
                Log.Info($"Bronzebeard HUD: tavern highlights=[{line}] from {(ticked.Count > 0 ? "ticked" : "suggested")} compositions");
            }
        });
        return highlights;
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

    /// <summary>The "?" above one of Bob's minions: its own panel shows how top boards field that minion.</summary>
    private void OpenLineups(string cardId)
    {
        if (_lineupsPanel != null && LineupsFor(cardId) is { } lineups)
        {
            _lineupsGuard.Run(() => _lineupsPanel.Show(lineups, _lastCards.All.Select(c => c.CardId)));
        }
    }

    /// <summary>A pin button was clicked above one of Bob's cards.</summary>
    private void TogglePin(string cardId) => _pinsGuard.Run(() =>
    {
        var file = _comps?.Pins ?? TavernPins.Empty;
        _gamePins.Toggle(cardId, file);
        Log.Info($"Bronzebeard HUD: pinned=[{string.Join(",", _gamePins.Merge(file).CardIds)}]");
        _pinsVersion++;
    });

    /// <summary>A composition's box was clicked in the target panel.</summary>
    private void ToggleComposition(string compositionId) => _selectionGuard.Run(() =>
    {
        if (_selection.Toggle(compositionId))
        {
            Log.Info($"Bronzebeard HUD: ticked compositions=[{string.Join(",", _selection.Checked)}]");
        }
        else
        {
            Log.Info($"Bronzebeard HUD: {compositionId} not ticked, four compositions already are");
        }

        _selectionVersion++;
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
    private string _shownKey = string.Empty;
    private string _tavernKey = string.Empty;
    private TavernRowTracker _rowTracker = new();
    private int _lastMinions;
    private LayoutRect? _lastFirstMarker;
    private CompositionPanelState _compPanel = new();
    private TavernAdvice? _lastAdvice;
    private PlayerCards _lastCards = BronzebeardHud.Stats.PlayerCards.None;
    private string? _shownCompStatus;
    private string _opponentKey = string.Empty;

    /// <summary>%LocalAppData%\BronzebeardHud\stats; hand-typed HSReplay files go in its "manual" subfolder.</summary>
    internal static string StatsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BronzebeardHud", "stats");

    public string Name => "Bronzebeard HUD";

    public string Description =>
        "Battlegrounds hero-pick stats and composition advice (Firestone public aggregates, hand-typed HSReplay data) on top of HDT's overlay.";

    public string ButtonText => "Move panels (on/off)";
    public string Author => "elphono";
    public Version Version => new(0, 2, 0);
    /// <summary>HDT's Plugins menu: move mode on/off, reset panel places, open the data folder.</summary>
    public MenuItem MenuItem => _menu ??= BuildMenu();

    private MenuItem BuildMenu()
    {
        var menu = new MenuItem { Header = "Bronzebeard HUD" };
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
        _tavern = new TavernAdvicePanel(Core.OverlayCanvas, _mover, _selection, ToggleComposition, () => _settings.SuggestedCompositions, ChangeSuggested, TogglePin, OpenLineups, OpenMetaSnapshot, DetailFor, HighlightsFor);
        _lineupsPanel = new LineupsPanel(Core.OverlayCanvas, _mover); // added after the target panel: drawn over it
        _opponentMmr = new OpponentMmrPanel(Core.OverlayCanvas);
        _choices = new ChoiceAdvicePanel(Core.OverlayCanvas, StatsDirectory, _selection);
        _skipCombat = new SkipCombatPanel(Core.OverlayCanvas, _mover, SkipCombat);
    }

    private void ResetSessionState()
    {
        _inHeroSelection = false;
        _compsLoadedThisGame = false;
        _shownKey = string.Empty;
        _tavernKey = string.Empty;
        _choiceKey = string.Empty;
        _loggedChoice = string.Empty;
        _opponentKey = string.Empty;
        _rowTracker = new TavernRowTracker();
        _warbandLine = null;
        _warbandRound = -1;
        _heroEffects = new Dictionary<string, HeroCompPick>();
        _heroEffectsHero = null;
        _heroEffectsVersion = -1;
        _warbandLoggedRound = -1;
        _compPanel = new CompositionPanelState();
        _lastAdvice = null;
        _lastCards = BronzebeardHud.Stats.PlayerCards.None;
        _shownCompStatus = null;
    }

    public void OnUnload()
    {
        _panel?.Detach();
        _tavern?.Detach();
        _opponentMmr?.Detach();
        _opponentMmr?.Dispose();
        _opponentMmr = null;
        _choices?.Detach();
        _choices?.Dispose();
        _choices = null;
        _skipCombat?.Detach();
        _skipCombat = null;
        _lineupsPanel?.Detach();
        _lineupsPanel = null;
        _panel = null;
        _tavern = null;
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

        _heroSelectionGuard.Run(() => UpdateHeroSelection(game));
        _tavernGuard.Run(() => UpdateTavern(game));
        _opponentMmrGuard.Run(() => UpdateOpponentMmr(game));
        _choiceGuard.Run(() => UpdateChoice(game));
        _warbandGuard.Run(() => UpdateWarband(game));
        _skipCombatGuard.Run(() => UpdateSkipCombat(game));
    }

    /// <summary>
    /// Under the target compositions, in the shop and in combat: the board's attack plus health against the
    /// average of the same hero at the same turn (Firestone warbandStats), and one line per round in HDT's log.
    /// </summary>
    private void UpdateWarband(GameV2 game)
    {
        if (_tavern == null || _stats == null)
        {
            return;
        }

        var phase = HdtEntityAdapter.Phase(game);
        var hero = phase is OverlayPhase.Shop or OverlayPhase.Combat ? HdtEntityAdapter.PlayerHeroId(game) : null;
        if (hero == null)
        {
            _warbandLine = null;
            _tavern.SetFooter(null);
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
        _warbandLine = comparison.Line;
        _tavern.SetFooter(comparison.Line);
    }

    /// <summary>
    /// Any choice of the player (discover, Dark Gift, trinket): a label above each option. One line in HDT's
    /// log per choice, including the ones without a known layout, so that uncovered kinds show up.
    /// </summary>
    private void UpdateChoice(GameV2 game)
    {
        if (_choices == null || _stats == null || _comps == null)
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

        _comps.Poll();
        var loaded = kind == ChoiceKind.Trinket && _choices.PollTrinketStats();
        var ids = string.Join(",", options.Select(o => o.EntityId));
        var key = $"{ids}|{_comps.Version}|{_stats.Bracket}|{_choices.TrinketStatsLoaded}|{_selectionVersion}";
        if (key == _choiceKey && !loaded)
        {
            return;
        }

        _choiceKey = key;
        var advice = ChoiceAdvisor.Advise(options, HdtEntityAdapter.PlayerCards(game).All, _comps.Compositions(), HdtEntityAdapter.LobbyTribeNames(),
            _choices.TrinketStat, _stats.Bracket, _selection.Checked, _heroEffects, _settings.SuggestedCompositions);
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
            Log.Info(ChoiceAdvisor.DiagnosticLine(options, advice, _comps.Compositions().Count, _comps.State, _choices.LastLines, _choices.FirstLabel,
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
            _selection.BeginGame(++_gameNumber);
            _gamePins.BeginGame(_gameNumber);
            _selectionVersion++;
            _rowTracker = new TavernRowTracker();
            _stats.BeginHeroSelection(game.CurrentBattlegroundsRating);
        }

        _stats.Poll();
        var offered = OfferedHeroes.Select(HdtEntityAdapter.PlayerEntities(game));
        if (offered.Count == 0)
        {
            _panel.Hide();
            return;
        }

        var tribes = HdtEntityAdapter.LobbyTribeNames();
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

    private void UpdateTavern(GameV2 game)
    {
        if (_tavern == null || _comps == null)
        {
            return;
        }

        var phase = HdtEntityAdapter.Phase(game);
        if (phase == OverlayPhase.Shop && !_compsLoadedThisGame)
        {
            _compsLoadedThisGame = true;
            _comps.BeginGame();
        }

        _comps.Poll();
        var changed = false;
        if (phase == OverlayPhase.Shop)
        {
            var cards = HdtEntityAdapter.PlayerCards(game);
            var owned = cards.All;
            // Bob's whole row, the tavern spell included: the game centres minions and spell together.
            var row = HdtEntityAdapter.TavernRow(game);
            // Followed by entity: a purchase, a reroll or an added card redraws the markers at once.
            var rowChanged = _rowTracker.Observe(game.GetTurnNumber(), row.Select(s => s.EntityId).ToList());
            var hero = HdtEntityAdapter.PlayerHeroId(game);
            if (hero != _heroEffectsHero || _comps.Version != _heroEffectsVersion)
            {
                _heroEffectsHero = hero;
                _heroEffectsVersion = _comps.Version;
                _heroEffects = new Dictionary<string, HeroCompPick>();
                _heroAffinityGuard.Run(() =>
                {
                    _heroEffects = HeroCompAffinity.Effects(hero, _comps.Compositions());
                    if (hero != null && _comps.State == "ok")
                    {
                        var inv = System.Globalization.CultureInfo.InvariantCulture;
                        Log.Info($"Bronzebeard HUD: hero affinity hero={hero} comps=[{string.Join("; ", _heroEffects.Values.OrderByDescending(e => e.Gain).Select(e => $"{e.Composition.Name} {e.ShopText} {(CompAdvisor.HeroPlacementWeight * e.Gain).ToString("+0.00;-0.00", inv)}pt"))}]");
                    }
                });
            }

            var key = string.Join(",", owned.Select(c => c.CardId)) + "|" + _comps.Version + "|" + _selectionVersion + "|" + hero + ":" + _heroEffects.Count
                      + "|" + _pinsVersion;
            if (rowChanged || key != _tavernKey || _lastAdvice == null)
            {
                _tavernKey = key;
                changed = true;
                _lastCards = cards;
                _lastMinions = row.Count(s => s.IsMinion);
                _lastAdvice = TavernAdvisor.Advise(row.Select(s => s.CardId).ToList(), owned, _comps.Compositions(), HdtEntityAdapter.LobbyTribeNames(),
                    _selection.Checked, _heroEffects, _settings.SuggestedCompositions);
                _tavern.ShowMarkers(_lastAdvice, owned.Select(c => c.CardId), _gamePins.Merge(_comps.Pins), row.Select(s => s.IsMinion).ToList());
                _lastFirstMarker = _tavern.FirstMarker;
            }
        }
        else if (_rowTracker.IsOpen)
        {
            // One diagnostic line per shop round, written when it ends: whether compositions were loaded,
            // what was targeted, where the first marker last was, and how often Bob's row changed.
            if (_lastAdvice is { Cards.Count: > 0 } lastAdvice)
            {
                Log.Info(TavernAdvisor.DiagnosticLine(_rowTracker.Round, _comps.Compositions().Count, _comps.State, lastAdvice, _lastCards,
                    _lastMinions, _rowTracker.Changes, _rowTracker.Refreshes, _lastFirstMarker, Core.OverlayCanvas.ActualWidth, Core.OverlayCanvas.ActualHeight));
            }

            _rowTracker.Close();
        }

        var wasVisible = _compPanel.PanelVisible;
        var advice = _lastAdvice;
        var ownedNow = _lastCards.All;
        var chosen = _selection.Checked;
        IReadOnlyList<CompositionRow> BuildRows()
        {
            _rowsSelectionVersion = _selectionVersion;
            // The same focus as the markers, recomputed so that a box ticked or a count changed in combat shows at once.
            var shown = TavernAdvisor.Aim(_comps.Compositions(), advice!.Playable, ownedNow, chosen, _settings.SuggestedCompositions, _heroEffects).Shown;
            return CompositionRows.Build(shown, ownedNow, chosen, _heroEffects);
        }

        _compPanel.Update(phase, () => advice != null && (changed || _compPanel.Rows.Count == 0) ? BuildRows() : _compPanel.Rows);
        if (phase == OverlayPhase.Combat && advice != null && _rowsSelectionVersion != _selectionVersion)
        {
            // A box ticked during combat: the rows follow at once, the markers at the next shop.
            _compPanel.Replace(BuildRows());
            changed = true;
        }
        if (!_compPanel.MarkersVisible)
        {
            _tavernKey = string.Empty;
            _tavern.HideMarkers();
            _lineupsPanel?.Hide(); // about Bob's minions: gone with the shop
        }

        if (!_compPanel.PanelVisible)
        {
            _tavern.HidePanel();
            if (phase != OverlayPhase.Combat)
            {
                _lastAdvice = null;
            }
        }
        else if (changed || !wasVisible || _comps.Status != _shownCompStatus)
        {
            _shownCompStatus = _comps.Status;
            // Pivots for each composition shown, among the playable ones: guarded on their own.
            IReadOnlyDictionary<string, IReadOnlyList<CompTransition>> transitions = new Dictionary<string, IReadOnlyList<CompTransition>>();
            var pool = advice?.Playable ?? _comps.Compositions();
            _transitionsGuard.Run(() => transitions = _compPanel.Rows
                .GroupBy(r => r.Composition.Id)
                .ToDictionary(g => g.Key, g => CompTransitions.For(g.First().Composition, pool)));
            _tavern.ShowPanel(_compPanel.Rows, _comps.Status, transitions);
        }
    }
}
