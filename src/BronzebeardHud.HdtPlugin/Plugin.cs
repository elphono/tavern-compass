using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Plugins;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// Entry point HDT discovers in its Plugins folder. Phase 1: hero-pick stats under the offered
/// heroes. The plugin never reads game memory; it only uses what HDT exposes, plus the local
/// stats files and the Firestone download managed by <see cref="StatsService"/>.
/// </summary>
public sealed class Plugin : IPlugin
{
    private HeroPickPanel? _panel;
    private StatsService? _stats;
    private bool _inHeroSelection;
    private string _shownKey = string.Empty;

    /// <summary>%LocalAppData%\BronzebeardHud\stats; hand-typed HSReplay files go in its "manual" subfolder.</summary>
    internal static string StatsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BronzebeardHud", "stats");

    public string Name => "Bronzebeard HUD";

    public string Description =>
        "Battlegrounds hero-pick stats (Firestone public aggregates and hand-typed HSReplay figures) on top of HDT's overlay.";

    public string ButtonText => "Open stats folder";
    public string Author => "elphono";
    public Version Version => new(0, 1, 0);
    public MenuItem MenuItem => null!;

    public void OnLoad()
    {
        _stats = new StatsService(StatsDirectory);
        _panel = new HeroPickPanel();
        Core.OverlayCanvas.Children.Add(_panel);
    }

    public void OnUnload()
    {
        if (_panel != null)
        {
            Core.OverlayCanvas.Children.Remove(_panel);
            _panel = null;
        }

        _stats?.Dispose();
        _stats = null;
    }

    public void OnButtonPress()
    {
        Directory.CreateDirectory(Path.Combine(StatsDirectory, "manual"));
        Process.Start("explorer.exe", StatsDirectory);
    }

    public void OnUpdate()
    {
        if (_panel == null || _stats == null)
        {
            return;
        }

        var game = Core.Game;
        if (game == null || !HdtEntityAdapter.IsHeroSelection(game))
        {
            _inHeroSelection = false;
            _shownKey = string.Empty;
            _panel.Hide();
            return;
        }

        if (!_inHeroSelection)
        {
            _inHeroSelection = true;
            _stats.BeginHeroSelection();
        }

        _stats.Poll();
        var offered = OfferedHeroes.Select(HdtEntityAdapter.PlayerEntities(game));
        if (offered.Count == 0)
        {
            _panel.Hide();
            return;
        }

        var key = string.Join(",", offered.Select(h => $"{h.EntityId}:{h.CardId}")) + "|" + _stats.Version;
        if (key != _shownKey)
        {
            _shownKey = key;
            _panel.Show(HeroPickAdvisor.BuildRows(offered, _stats.Sources()), _stats.Status);
        }

        _panel.Reposition(Core.OverlayCanvas);
    }
}
