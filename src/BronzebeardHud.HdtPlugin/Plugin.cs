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
    private TavernAdvicePanel? _tavern;
    private OpponentMmrPanel? _opponentMmr;
    private TrinketPickPanel? _trinkets;
    private NextOpponentMarker? _nextOpponent;
    private string _nextOpponentKey = string.Empty;
    private string _trinketKey = string.Empty;
    private StatsService? _stats;
    private CompService? _comps;
    private bool _inHeroSelection;
    private bool _compsLoadedThisGame;
    private string _shownKey = string.Empty;
    private string _tavernKey = string.Empty;
    private string _opponentKey = string.Empty;

    /// <summary>%LocalAppData%\BronzebeardHud\stats; hand-typed HSReplay files go in its "manual" subfolder.</summary>
    internal static string StatsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BronzebeardHud", "stats");

    public string Name => "Bronzebeard HUD";

    public string Description =>
        "Battlegrounds hero-pick stats and composition advice (Firestone public aggregates, hand-typed HSReplay data) on top of HDT's overlay.";

    public string ButtonText => "Open stats folder";
    public string Author => "elphono";
    public Version Version => new(0, 2, 0);
    public MenuItem MenuItem => null!;

    public void OnLoad()
    {
        Directory.CreateDirectory(Path.Combine(StatsDirectory, "manual"));
        _stats = new StatsService(StatsDirectory);
        _comps = new CompService(StatsDirectory);
        _panel = new HeroPickPanel(Core.OverlayCanvas);
        _tavern = new TavernAdvicePanel(Core.OverlayCanvas);
        _opponentMmr = new OpponentMmrPanel(Core.OverlayCanvas);
        _trinkets = new TrinketPickPanel(Core.OverlayCanvas, StatsDirectory);
        _nextOpponent = new NextOpponentMarker(Core.OverlayCanvas);
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
        _nextOpponent?.Detach();
        _nextOpponent = null;
        _panel = null;
        _tavern = null;
        _stats?.Dispose();
        _comps?.Dispose();
        _stats = null;
        _comps = null;
    }

    public void OnButtonPress()
    {
        Directory.CreateDirectory(Path.Combine(StatsDirectory, "manual"));
        Process.Start("explorer.exe", StatsDirectory);
    }

    public void OnUpdate()
    {
        var game = Core.Game;
        if (game == null)
        {
            return;
        }

        UpdateHeroSelection(game);
        UpdateTavern(game);
        UpdateOpponentMmr(game);
        UpdateTrinketChoice(game);
        UpdateNextOpponent(game);
    }

    private void UpdateNextOpponent(GameV2 game)
    {
        if (_nextOpponent == null)
        {
            return;
        }

        var tile = HdtEntityAdapter.IsShopPhase(game)
            ? NextOpponent.Find(HdtEntityAdapter.NextOpponentPlayerId(game), HdtEntityAdapter.Heroes(game))
            : null;
        var key = tile == null ? string.Empty : $"{tile.LeaderboardPlace}:{tile.IsGhost}";
        if (key == _nextOpponentKey)
        {
            return;
        }

        _nextOpponentKey = key;
        if (tile == null)
        {
            _nextOpponent.Hide();
        }
        else
        {
            _nextOpponent.Show(tile);
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

        if (!HdtEntityAdapter.IsShopPhase(game))
        {
            _tavernKey = string.Empty;
            _tavern.Hide();
            return;
        }

        if (!_compsLoadedThisGame)
        {
            _compsLoadedThisGame = true;
            _comps.BeginGame();
        }

        _comps.Poll();
        var owned = HdtEntityAdapter.OwnedCards(game);
        var tavern = HdtEntityAdapter.TavernCardIds(game);
        var key = string.Join(",", owned.Select(c => c.CardId)) + "|" + string.Join(",", tavern) + "|" + _comps.Version;
        if (key == _tavernKey)
        {
            return;
        }

        _tavernKey = key;
        var targets = CompAdvisor.Rank(owned, _comps.Compositions());
        _tavern.Show(CompAdvisor.AdviseShop(tavern, targets, owned), targets, _comps.Status, _comps.Pins);
    }
}
