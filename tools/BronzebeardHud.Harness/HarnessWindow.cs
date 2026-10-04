using System.Globalization;
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
/// The plugin's two movable panels (Compositions, Skip combat) and the frames on Bob's cards, on a canvas the size of a
/// Hearthstone window, fed with synthetic data as HDT's overlay would hold them. Bob's seven cards are drawn as grey
/// boxes where the game draws them (TavernLayout.CardSlots), so that the frames and labels land on something. Move mode
/// is on at the start. The zones a panel must not cover (the boards, the leaderboard, the hero) are drawn under the
/// panels in red. The plugin's own log lines show in the pane on the right. The layout is saved in the harness's own
/// file, never in the plugin's layout.json.
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
    private readonly SkipCombatPanel _skip;
    private readonly CompTargetTracker _tracker = new();
    private readonly TextBox _log = new() { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap };
    private readonly List<string> _lines = new();
    private readonly string _folder;
    private CompGuideSet _guides = CompGuideSet.Empty(CompGuideSources.HdtFree);
    private int _count = HudSettings.DefaultSuggested;
    private int _scenario;
    private bool _skipShown = true;

    public Canvas Overlay { get; }
    public CompsPanel Comps { get; }
    public TavernMarkers Markers { get; }
    public bool MoveMode => _mover.MoveMode;
    public string LayoutPath { get; }
    public IReadOnlyList<string> LogLines => _lines;

    /// <summary>The targets of the scene as it stands (the panel's colours).</summary>
    public IReadOnlyList<CompTarget> Targets => _tracker.Targets;

    /// <summary>How many automatic targets are wanted (the panel's − n +): what a click on − or + would change.</summary>
    public int Count => _count;

    public IReadOnlyList<TavernHighlight> Highlights { get; private set; } = Array.Empty<TavernHighlight>();

    public HarnessWindow(Options options)
    {
        _folder = Path.Combine(Path.GetTempPath(), "BronzebeardHarness");
        Directory.CreateDirectory(_folder);
        LayoutPath = options.Layout ?? Path.Combine(_folder, "layout.json");
        _scenario = Math.Max(0, Math.Min(HarnessData.Scenarios.Count - 1, options.Scenario));

        Log.Written += line => Dispatcher.BeginInvoke(new Action(() => AppendLog(line)));
        AssetDownloaders.Initialize(Path.Combine(_folder, "images"));
        HarnessCards.Install();

        Overlay = new Canvas { Width = options.Size.Width, Height = options.Size.Height, Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x22, 0x30)), ClipToBounds = true };
        Overlay.SizeChanged += (_, _) => DrawScene();

        _mover = new PanelMover(Overlay, LayoutPath);
        Comps = new CompsPanel(Overlay, _mover, ToggleGuide, () => _count, ChangeCount, () => Log.Info("Meta clicked"),
            guide => GuidePivots.For(guide, _guides, Held()),
            (guide, fit) => Log.Info($"comp detail id={guide.Id} sections={fit.Shown.Count} of {fit.Total}"),
            action => action());
        Markers = new TavernMarkers(Overlay, id => Log.Info($"pin toggled: {id}"));
        _skip = new SkipCombatPanel(Overlay, _mover, () => Log.Info("Skip combat clicked (nothing is killed here)"));
        _mover.ToggleMoveMode(); // move mode on from the start: the harness is for moving and resizing

        Title = "Bronzebeard HUD — simulation (not HDT)";
        Width = 1750;
        Height = 960;
        Content = BuildContent(options);
        Refresh();
        if (!options.SelfTest)
        {
            HarnessCards.Load(Path.Combine(_folder, "cache"), Refresh);
        }
    }

    /// <summary>How many pivots a guide's detail lists (GuidePivots, as the panel asks them).</summary>
    public int PivotCount(CompGuide guide) => GuidePivots.For(guide, _guides, Held()).Count;

    private HashSet<string> Held() => new(HarnessData.Scenarios[_scenario].Cards.All.Select(c => c.CardId), StringComparer.Ordinal);

    /// <summary>The targets a scenario gives with the current ticks and count, without changing the scene (the self-test asks).</summary>
    public int TargetCount(int scenario)
    {
        var probe = new CompTargetTracker();
        foreach (var id in _tracker.Ticked)
        {
            probe.Toggle(id);
        }

        return probe.Next(CompGuideMatch.Rank(_guides, HarnessData.Scenarios[scenario].Cards, _count), _count).Count;
    }

    /// <summary>Shows the panel and the markers again from the current board, count and ticks: what a game update does in the plugin.</summary>
    private void Refresh()
    {
        _guides = HarnessData.Guides(id => Database.GetCardFromId(id)?.LocalizedName ?? id);
        var cards = HarnessData.Scenarios[_scenario].Cards;
        var board = CompGuideMatch.Rank(_guides, cards, _count);
        var targets = _tracker.Next(board, _count);
        Comps.Show(board, targets, cards.All.Select(c => c.CardId), CompGuideSources.HdtFree, null);
        Comps.SetFooter("Board 142 · hero avg 120 at turn 8 · +18%");
        DrawScene(); // Bob's cards by name, once the names are known
        Highlights = TavernHighlights.For(HarnessData.Shop, targets);
        Markers.Show(HarnessData.Shop, Highlights, HarnessData.Pins, HarnessData.Shop.Select(_ => true).ToList());
        if (_skipShown)
        {
            _skip.Show();
        }
    }

    /// <summary>
    /// Opens a guide's detail: <paramref name="which"/> is a target's rank (1 for the first target) or a guide's name.
    /// Throws when there is no such guide: a capture of the wrong view is worse than none.
    /// </summary>
    public void OpenDetail(string which)
    {
        var guide = int.TryParse(which, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank)
            ? _tracker.Targets.FirstOrDefault(t => t.Rank == rank)?.Guide
            : _guides.All.FirstOrDefault(g => string.Equals(g.Name, which, StringComparison.OrdinalIgnoreCase));
        if (guide == null || !Comps.OpenDetail(guide.Id))
        {
            throw new ArgumentException($"--detail {which}: no such target or guide (targets: {CompTargets.Summary(_tracker.Targets)})");
        }
    }

    /// <summary>Ticks a guide as the player does: <paramref name="which"/> is a target's rank (1 for the first target) or a guide's name.</summary>
    public void Tick(string which)
    {
        var guide = int.TryParse(which, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank)
            ? _tracker.Targets.FirstOrDefault(t => t.Rank == rank)?.Guide
            : _guides.All.FirstOrDefault(g => string.Equals(g.Name, which, StringComparison.OrdinalIgnoreCase));
        if (guide == null)
        {
            throw new ArgumentException($"--tick {which}: no such target or guide (targets: {CompTargets.Summary(_tracker.Targets)})");
        }

        ToggleGuide(guide.Id);
    }

    private void ToggleGuide(string id)
    {
        Log.Info(_tracker.ToggleLine(id, _tracker.Toggle(id)));
        Refresh();
    }

    private void ChangeCount(int step)
    {
        _count = Math.Max(HudSettings.MinSuggested, Math.Min(HudSettings.MaxSuggested, _count + step));
        Log.Info($"suggested compositions={_count}");
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

        var detail = new Button { Content = "Detail of target 1 / list", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 14, 0) };
        detail.Click += (_, _) =>
        {
            if (Comps.ShowsDetail)
            {
                Comps.CloseDetail();
            }
            else if (_tracker.Targets.Count > 0)
            {
                OpenDetail("1");
            }
            else
            {
                Log.Info("no target in this scenario: click a guide's name instead");
            }
        };

        var clear = new Button { Content = "Clear log", Padding = new Thickness(8, 2, 8, 2) };
        clear.Click += (_, _) =>
        {
            _lines.Clear();
            _log.Clear();
        };

        var bar = new WrapPanel { Margin = new Thickness(8) };
        foreach (var element in new UIElement[] { move, reset, size, board, skip, detail, clear })
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

    /// <summary>
    /// Under the panels, redrawn at every canvas size: the zones a default panel must not cover, in red, and Bob's seven
    /// cards as grey boxes with their names, where TavernLayout puts the game's shop cards.
    /// </summary>
    private void DrawScene()
    {
        foreach (var old in Overlay.Children.OfType<FrameworkElement>().Where(e => Equals(e.Tag, "zone") || Equals(e.Tag, "shop")).ToList())
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

        var slots = TavernLayout.CardSlots(Overlay.ActualWidth, Overlay.ActualHeight, HarnessData.Shop.Count);
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var card = new Border
            {
                Width = slot.Width * 0.9,
                Height = slot.Height * 0.9,
                Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x50, 0x5C)),
                CornerRadius = new CornerRadius(12),
                Tag = "shop",
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = Database.GetCardFromId(HarnessData.Shop[i])?.LocalizedName ?? HarnessData.Shop[i],
                    Foreground = Brushes.White,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6),
                },
            };
            Panel.SetZIndex(card, -5);
            Canvas.SetLeft(card, slot.Left + slot.Width * 0.05);
            Canvas.SetTop(card, slot.Top + slot.Height * 0.05);
            Overlay.Children.Add(card);
        }
    }
}
