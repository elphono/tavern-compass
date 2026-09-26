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
    private TrinketPickPanel? _trinkets;
    private GameHistoryPanel? _history;
    private readonly GameTimeline _timeline = new();
    private int _historyKey = -1;

    // One guard per feature: an unexpected exception disables that feature alone (see FeatureGuard).
    private readonly FeatureGuard _heroSelectionGuard;
    private readonly FeatureGuard _tavernGuard;
    private readonly FeatureGuard _opponentMmrGuard;
    private readonly FeatureGuard _trinketGuard;
    private readonly FeatureGuard _historyGuard;

    public Plugin()
    {
        _heroSelectionGuard = new FeatureGuard("hero-selection", (n, e) => Disable(n, e, () => _panel?.Hide()));
        _tavernGuard = new FeatureGuard("tavern-advice", (n, e) => Disable(n, e, () => { _tavern?.HideMarkers(); _tavern?.HidePanel(); }));
        _opponentMmrGuard = new FeatureGuard("opponent-mmr", (n, e) => Disable(n, e, () => _opponentMmr?.Hide()));
        _trinketGuard = new FeatureGuard("trinket-choice", (n, e) => Disable(n, e, () => _trinkets?.Hide()));
        _historyGuard = new FeatureGuard("history", (n, e) => Disable(n, e, () => _history?.Hide()));
    }

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
    private string _trinketKey = string.Empty;
    private StatsService? _stats;
    private CompService? _comps;
    private bool _inHeroSelection;
    private bool _compsLoadedThisGame;
    private string _shownKey = string.Empty;
    private string _tavernKey = string.Empty;
    private int _diagnosticRound = -1;
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
        _panel = new HeroPickPanel(Core.OverlayCanvas);
        _tavern = new TavernAdvicePanel(Core.OverlayCanvas, _mover);
        _opponentMmr = new OpponentMmrPanel(Core.OverlayCanvas);
        _trinkets = new TrinketPickPanel(Core.OverlayCanvas, StatsDirectory);
        _history = new GameHistoryPanel(Core.OverlayCanvas, _mover);
    }

    private void ResetSessionState()
    {
        _inHeroSelection = false;
        _compsLoadedThisGame = false;
        _shownKey = string.Empty;
        _tavernKey = string.Empty;
        _trinketKey = string.Empty;
        _opponentKey = string.Empty;
        _historyKey = -1;
        _diagnosticRound = -1;
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
        _trinkets?.Detach();
        _trinkets?.Dispose();
        _trinkets = null;
        _history?.Detach();
        _history = null;
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
        _trinketGuard.Run(() => UpdateTrinketChoice(game));
        _historyGuard.Run(() => UpdateHistory(game));
    }

    private void UpdateHistory(GameV2 game)
    {
        if (_history == null || !game.IsBattlegroundsMatch || !game.IsBattlegroundsHeroPickingDone)
        {
            _history?.Hide();
            return;
        }

        var heroes = HeroHealth.InGame(HdtEntityAdapter.Heroes(game));
        var nextOpponent = HdtEntityAdapter.NextOpponentPlayerId(game);
        _timeline.Observe(game.GetTurnNumber(), game.IsBattlegroundsCombatPhase, game.Player.Id, nextOpponent, heroes);
        if (!game.IsBattlegroundsCombatPhase)
        {
            _historyKey = -1;
            _history.Hide();
            return;
        }

        var key = _timeline.Combats.Count * 1000 + _timeline.HealthByPlayer.Sum(c => c.Value.Count) + heroes.Sum(h => h.Health) * 7;
        if (key != _historyKey)
        {
            _historyKey = key;
            _history.Show(_timeline, heroes, game.Player.Id, nextOpponent);
        }
    }

    private void UpdateTrinketChoice(GameV2 game)
    {
        if (_trinkets == null || _stats == null)
        {
            return;
        }

        var offered = game.IsBattlegroundsMatch
            ? TrinketChoice.Offered(HdtEntityAdapter.OfferedEntities(game))
            : new List<EntitySnapshot>();
        if (offered.Count == 0)
        {
            _trinketKey = string.Empty;
            _trinkets.Hide();
            return;
        }

        var loaded = _trinkets.Poll();
        var key = string.Join(",", offered.Select(e => e.Id)) + "|" + _stats.Bracket;
        if (key != _trinketKey || loaded)
        {
            _trinketKey = key;
            _trinkets.Show(offered.Select(e => e.CardId!).ToList(), _stats.Bracket);
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
            _diagnosticRound = -1;
            _timeline.Reset();
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
        var key = string.Join(",", offered.Select(h => $"{h.EntityId}:{h.CardId}")) + "|" + _stats.Version + "|" + string.Join(",", tribes);
        if (key != _shownKey)
        {
            _shownKey = key;
            var sources = _stats.Sources().Select(file => LobbyTribes.Apply(file, tribes)).ToList();
            _panel.Show(HeroPickAdvisor.BuildRows(offered, sources), _stats.Status);
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
            var tavern = HdtEntityAdapter.TavernCardIds(game);
            var key = string.Join(",", owned.Select(c => c.CardId)) + "|" + string.Join(",", tavern) + "|" + _comps.Version;
            if (key != _tavernKey || _lastAdvice == null)
            {
                _tavernKey = key;
                changed = true;
                _lastCards = cards;
                _lastAdvice = TavernAdvisor.Advise(tavern, owned, _comps.Compositions(), HdtEntityAdapter.LobbyTribeNames());
                _tavern.ShowMarkers(_lastAdvice, owned.Select(c => c.CardId), _comps.Pins);

                // One diagnostic line per shop round, once the tavern has cards: enough to tell from HDT's log
                // whether compositions were loaded, what was targeted and where the first marker went.
                var round = game.GetTurnNumber();
                if (round != _diagnosticRound && tavern.Count > 0)
                {
                    _diagnosticRound = round;
                    Log.Info(TavernAdvisor.DiagnosticLine(round, _comps.Compositions().Count, _comps.State, _lastAdvice, cards, _tavern.FirstMarker,
                        Core.OverlayCanvas.ActualWidth, Core.OverlayCanvas.ActualHeight));
                }
            }
        }

        var wasVisible = _compPanel.PanelVisible;
        var advice = _lastAdvice;
        var ownedNow = _lastCards.All;
        _compPanel.Update(phase, () => advice != null && (changed || _compPanel.Rows.Count == 0)
            ? CompositionRows.Build(advice.Targets, advice.Playable, ownedNow)
            : _compPanel.Rows);
        if (!_compPanel.MarkersVisible)
        {
            _tavernKey = string.Empty;
            _tavern.HideMarkers();
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
            _tavern.ShowPanel(_compPanel.Rows, _comps.Status);
        }
    }
}
