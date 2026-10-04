using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;
using BronzebeardHud.HdtPlugin;
using BronzebeardHud.Stats;
using BronzebeardHud.Stats.Tests;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Assets;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace BronzebeardHud.Harness;

/// <summary>
/// The plugin's four movable panels on a canvas the size of a Hearthstone window, fed with synthetic data, as HDT's
/// overlay would hold them. Move mode is on at the start. The zones a panel must not cover (the boards, the
/// leaderboard, the hero) are drawn under the panels in red. The plugin's own log lines show in the pane on the right.
/// The layout is saved in the harness's own file, never in the plugin's layout.json.
/// </summary>
internal sealed class HarnessWindow : Window
{
    private static readonly (string Label, int Width, int Height)[] Sizes =
    {
        ("1920 × 1080", 1920, 1080),
        ("1600 × 900", 1600, 900),
        ("1440 × 1080 (4:3)", 1440, 1080),
        ("2560 × 1080 (21:9)", 2560, 1080),
        ("2291 × 1360", 2291, 1360),
    };

    private readonly PanelMover _mover;
    private readonly TavernAdvicePanel _tavern;
    private readonly LineupsPanel _lineups;
    private readonly CompGuidesPanel _guides;
    private readonly SkipCombatPanel _skip;
    private readonly CompositionSelection _selection = new();
    private readonly TextBox _log = new() { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap };
    private readonly List<string> _lines = new();
    private readonly string _folder;
    private int _suggested = 5;
    private int _scenario = 2;
    private bool _skipShown = true;

    public Canvas Overlay { get; }
    public bool MoveMode => _mover.MoveMode;
    public string LayoutPath { get; }
    public IReadOnlyList<string> LogLines => _lines;

    public HarnessWindow(Options options)
    {
        _folder = Path.Combine(Path.GetTempPath(), "BronzebeardHarness");
        Directory.CreateDirectory(_folder);
        LayoutPath = options.Layout ?? Path.Combine(_folder, "layout.json");

        Log.Written += line => Dispatcher.BeginInvoke(new Action(() => AppendLog(line)));
        AssetDownloaders.Initialize(Path.Combine(_folder, "images"));
        HarnessCards.Install();

        Overlay = new Canvas { Width = options.Size.Width, Height = options.Size.Height, Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x22, 0x30)), ClipToBounds = true };
        Overlay.SizeChanged += (_, _) => DrawZones();

        _mover = new PanelMover(Overlay, LayoutPath);
        _tavern = new TavernAdvicePanel(Overlay, _mover, _selection, ToggleComposition, () => _suggested, ChangeSuggested,
            id => Log.Info($"pin toggled: {id}"), ShowLineups, () => Log.Info("Meta clicked"),
            composition => CompDetail.For(composition, id => Database.GetCardFromId(id)?.TechLevel),
            (_, _, _) => null);
        _lineups = new LineupsPanel(Overlay, _mover); // added after the target panel: drawn over it, as in the plugin
        _guides = new CompGuidesPanel(Overlay, _mover);
        _skip = new SkipCombatPanel(Overlay, _mover, () => Log.Info("Skip combat clicked (nothing is killed here)"));
        _mover.ToggleMoveMode(); // move mode on from the start: the harness is for moving and resizing

        Title = "Bronzebeard HUD — simulation (not HDT)";
        Width = 1750;
        Height = 960;
        Content = BuildContent(options);
        Refresh();
        ShowLineups(HarnessData.Pool[3]);
        if (!options.SelfTest)
        {
            HarnessCards.Load(Path.Combine(_folder, "cache"), Refresh);
        }
    }

    /// <summary>Shows the three panels again from the current board, count and ticks: what a game update does in the plugin.</summary>
    private void Refresh()
    {
        var cards = HarnessData.Scenarios[_scenario].Cards;
        var owned = cards.All;
        var chosen = _selection.Checked;
        var shown = TavernAdvisor.Aim(HarnessData.Lobby, HarnessData.Lobby, owned, chosen, _suggested, null).Shown;
        var rows = CompositionRows.Build(shown, owned, chosen, null);
        var transitions = rows.GroupBy(r => r.Composition.Id)
            .ToDictionary(g => g.Key, g => CompTransitions.For(g.First().Composition, HarnessData.Lobby));
        _tavern.ShowPanel(rows, null, transitions);
        _tavern.SetFooter("Warband 19 · hero avg 17 at turn 3 · +10%");
        _guides.Show(CompGuideMatch.Rank(HarnessData.Guides, cards), CompGuideSources.HdtFree, null);
        if (_lineups.IsOpen)
        {
            ShowLineups(HarnessData.Pool[3]);
        }

        if (_skipShown)
        {
            _skip.Show();
        }
    }

    private void ShowLineups(string cardId) =>
        _lineups.Show(MinionLineups.For(cardId, HarnessData.Lobby, 3), HarnessData.Scenarios[_scenario].Cards.All.Select(c => c.CardId));

    private void ToggleComposition(string id)
    {
        _selection.Toggle(id);
        Refresh();
    }

    private void ChangeSuggested(int step)
    {
        _suggested = Math.Max(1, Math.Min(8, _suggested + step));
        Refresh();
    }

    private UIElement BuildContent(Options options)
    {
        var move = new CheckBox { Content = "Move panels", IsChecked = true, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        move.Click += (_, _) => _mover.ToggleMoveMode();
        var reset = new Button { Content = "Reset panel positions", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 14, 0) };
        reset.Click += (_, _) => _mover.Reset();

        var size = new ComboBox { ItemsSource = Sizes.Select(s => s.Label).ToList(), Width = 150, Margin = new Thickness(0, 0, 14, 0) };
        size.SelectedIndex = Math.Max(0, Array.FindIndex(Sizes, s => s.Width == options.Size.Width && s.Height == options.Size.Height));
        size.SelectionChanged += (_, _) =>
        {
            Overlay.Width = Sizes[size.SelectedIndex].Width;
            Overlay.Height = Sizes[size.SelectedIndex].Height;
        };

        var board = new ComboBox { ItemsSource = HarnessData.Scenarios.Select(s => s.Name).ToList(), Width = 220, Margin = new Thickness(0, 0, 14, 0), SelectedIndex = _scenario };
        board.SelectionChanged += (_, _) =>
        {
            _scenario = board.SelectedIndex;
            Refresh();
        };

        var skip = new CheckBox { Content = "Skip combat button", IsChecked = true, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        skip.Click += (_, _) =>
        {
            _skipShown = skip.IsChecked == true;
            if (_skipShown)
            {
                _skip.Show();
            }
            else
            {
                _skip.Hide();
            }
        };

        var lineups = new Button { Content = "Open / close lineups", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 14, 0) };
        lineups.Click += (_, _) =>
        {
            if (_lineups.IsOpen)
            {
                _lineups.Hide();
            }
            else
            {
                ShowLineups(HarnessData.Pool[3]);
            }
        };

        var clear = new Button { Content = "Clear log", Padding = new Thickness(8, 2, 8, 2) };
        clear.Click += (_, _) =>
        {
            _lines.Clear();
            _log.Clear();
        };

        var bar = new WrapPanel { Margin = new Thickness(8) };
        foreach (var element in new UIElement[] { move, reset, size, board, skip, lineups, clear })
        {
            bar.Children.Add(element);
        }

        var path = new TextBlock { Text = "layout: " + LayoutPath, Margin = new Thickness(8, 0, 8, 6), Foreground = Brushes.Gray, TextTrimming = TextTrimming.CharacterEllipsis };
        var viewport = new Border { Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x11, 0x18)), Child = new Viewbox { Child = Overlay, Margin = new Thickness(6) } };

        var left = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(path, Dock.Bottom);
        left.Children.Add(bar);
        left.Children.Add(path);
        left.Children.Add(viewport);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(430) });
        Grid.SetColumn(_log, 1);
        root.Children.Add(left);
        root.Children.Add(_log);
        return root;
    }

    private void AppendLog(string line)
    {
        _lines.Add(line);
        try
        {
            File.AppendAllText(Path.Combine(_folder, "harness.log"), line + Environment.NewLine);
        }
        catch (IOException)
        {
            // The pane has it; the file is a convenience.
        }

        _log.AppendText(line + Environment.NewLine);
        _log.ScrollToEnd();
    }

    /// <summary>The zones a default panel must not cover, in red under the panels, redrawn at every canvas size.</summary>
    private void DrawZones()
    {
        foreach (var old in Overlay.Children.OfType<FrameworkElement>().Where(e => Equals(e.Tag, "zone")).ToList())
        {
            Overlay.Children.Remove(old);
        }

        foreach (var (name, zone) in NoGoZones.For(Overlay.ActualWidth, Overlay.ActualHeight))
        {
            var area = new Rectangle { Width = zone.Width, Height = zone.Height, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0x40, 0x40)), Stroke = new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0x40, 0x40)), StrokeThickness = 2, Tag = "zone", IsHitTestVisible = false };
            var label = new TextBlock { Text = name, Foreground = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0x90, 0x90)), FontSize = 16, Tag = "zone", IsHitTestVisible = false };
            Panel.SetZIndex(area, -10);
            Panel.SetZIndex(label, -10);
            Canvas.SetLeft(area, zone.Left);
            Canvas.SetTop(area, zone.Top);
            Canvas.SetLeft(label, zone.Left + 8);
            Canvas.SetTop(label, zone.Top + 6);
            Overlay.Children.Add(area);
            Overlay.Children.Add(label);
        }
    }
}
