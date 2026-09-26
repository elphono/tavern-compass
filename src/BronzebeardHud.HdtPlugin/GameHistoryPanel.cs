using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.Hearthstone;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// During combat, a wide panel meant to read without explanation: on the left the eight players by
/// health (portrait, health, change over the last combat, next opponent framed); on the right the last
/// combats in words, and the health curves with numbered axes, each curve ending with its hero's
/// portrait and current health. Default place and logic come from the library (HistoryLayout,
/// Standings, CombatText, ChartAxes); the panel can be moved.
/// </summary>
internal sealed class GameHistoryPanel
{
    private static readonly Color[] CurveColours =
    {
        Color.FromRgb(0x4D, 0xA6, 0xFF), Color.FromRgb(0x7C, 0xD9, 0x5C), Color.FromRgb(0xE6, 0x4D, 0xFF), Color.FromRgb(0xFF, 0x9F, 0x1C),
        Color.FromRgb(0x33, 0xD6, 0xC8), Color.FromRgb(0xFF, 0x4D, 0x4D), Color.FromRgb(0xC8, 0xC8, 0xC8), Color.FromRgb(0xFF, 0xE0, 0x3D),
    };

    private static readonly Brush NextBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x00));

    private readonly Canvas _canvas;
    private readonly PanelMover _mover;
    private readonly Border _panel;
    private GameTimeline? _timeline;
    private IReadOnlyList<HeroHealth> _heroes = new List<HeroHealth>();
    private int _playerId;
    private int _nextOpponentId;

    public GameHistoryPanel(Canvas canvas, PanelMover mover)
    {
        _canvas = canvas;
        _mover = mover;
        _panel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xEB, 0x14, 0x14, 0x1E)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x48, 0x0F)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        _canvas.Children.Add(_panel);
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    public void Show(GameTimeline timeline, IReadOnlyList<HeroHealth> heroes, int playerId, int nextOpponentId)
    {
        _timeline = timeline;
        _heroes = heroes;
        _playerId = playerId;
        _nextOpponentId = nextOpponentId;
        Relayout();
    }

    public void Hide()
    {
        _timeline = null;
        _panel.Visibility = Visibility.Collapsed;
    }

    public void Detach()
    {
        _canvas.SizeChanged -= OnCanvasSizeChanged;
        _canvas.Children.Remove(_panel);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => Relayout();

    private static string HeroName(string? cardId) =>
        string.IsNullOrEmpty(cardId) ? "?" : Database.GetCardFromId(cardId)?.LocalizedName ?? cardId!;

    private static TextBlock Text(string text, double size, Brush brush, FontWeight? weight = null) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = brush,
        FontWeight = weight ?? FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private void Relayout()
    {
        if (_timeline == null || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            _panel.Visibility = Visibility.Collapsed;
            return;
        }

        var height = _canvas.ActualHeight;
        var scale = height / 1080;
        var rect = HistoryLayout.Panel(_canvas.ActualWidth, height);
        var inner = new Grid { Margin = new Thickness(6 * scale) };
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.36, GridUnitType.Star) });
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.64, GridUnitType.Star) });

        var colourByPlayer = _heroes.Select(h => h.PlayerId).OrderBy(id => id).Select((id, i) => (id, i))
            .ToDictionary(x => x.id, x => CurveColours[x.i % CurveColours.Length]);

        var standings = BuildStandings(scale, rect.Height, colourByPlayer);
        Grid.SetColumn(standings, 0);
        inner.Children.Add(standings);

        var right = new DockPanel { Margin = new Thickness(8 * scale, 0, 0, 0), LastChildFill = true };
        var combats = new StackPanel();
        foreach (var combat in _timeline.Combats.Reverse().Take(3))
        {
            var line = new DockPanel { Margin = new Thickness(0, 0, 0, 2 * scale) };
            var portrait = CardImages.Hero(combat.OpponentHeroCardId, 0.022 * height);
            portrait.Margin = new Thickness(0, 0, 4 * scale, 0);
            line.Children.Add(portrait);
            var brush = combat.Result == CombatResult.Win ? Brushes.LightGreen : combat.Result == CombatResult.Loss ? Brushes.OrangeRed : Brushes.White;
            line.Children.Add(Text(CombatText.Label(combat, HeroName(combat.OpponentHeroCardId)), 11 * scale, brush));
            combats.Children.Add(line);
        }

        if (_timeline.Combats.Count == 0)
        {
            combats.Children.Add(Text("No combat yet", 11 * scale, Brushes.LightGray));
        }

        DockPanel.SetDock(combats, Dock.Top);
        right.Children.Add(combats);
        right.Children.Add(BuildChart(scale, rect.Width * 0.64 - 14 * scale, rect.Height * 0.55, colourByPlayer));
        Grid.SetColumn(right, 1);
        inner.Children.Add(right);

        _panel.Child = inner;
        _panel.Width = rect.Width;
        _panel.Height = rect.Height;
        _mover.Place(_panel, "combats", rect);
        _panel.Visibility = Visibility.Visible;
    }

    private UIElement BuildStandings(double scale, double panelHeight, IReadOnlyDictionary<int, Color> colours)
    {
        var column = new StackPanel();
        var rowHeight = (panelHeight - 12 * scale) / 8;
        foreach (var row in Standings.Build(_heroes, _timeline!, _playerId, _nextOpponentId))
        {
            var line = new DockPanel { Height = rowHeight, LastChildFill = true };
            var frame = new Border
            {
                BorderBrush = row.IsNextOpponent ? NextBrush : new SolidColorBrush(colours.TryGetValue(row.Hero.PlayerId, out var c) ? c : Colors.Gray),
                BorderThickness = new Thickness(row.IsNextOpponent ? 2.5 * scale : 1.5 * scale),
                Margin = new Thickness(0, 1 * scale, 4 * scale, 1 * scale),
                Child = CardImages.Hero(row.Hero.HeroCardId, rowHeight - 4 * scale),
                Opacity = row.IsDead ? 0.4 : 1,
            };
            line.Children.Add(frame);
            var delta = row.Delta == 0 ? string.Empty : row.Delta > 0 ? $" +{row.Delta}" : $" {row.Delta}";
            var record = row.RecordText.Length > 0 ? $" · {row.RecordText}" : string.Empty;
            var label = $"{row.Rank}. {row.Hero.Health} HP{delta}{record}" + (row.IsNextOpponent ? " · NEXT" : string.Empty) + (row.IsLocal ? " · you" : string.Empty);
            var brush = row.IsNextOpponent ? NextBrush : row.IsDead ? Brushes.Gray : Brushes.White;
            line.Children.Add(new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock { Text = label, FontSize = 11 * scale, Foreground = brush, FontWeight = row.IsLocal ? FontWeights.Bold : FontWeights.Normal },
            });
            column.Children.Add(line);
        }

        return column;
    }

    private UIElement BuildChart(double scale, double width, double height, IReadOnlyDictionary<int, Color> colours)
    {
        var chart = new Canvas { Width = Math.Max(10, width), Height = Math.Max(10, height), ClipToBounds = false };
        var curves = _timeline!.HealthByPlayer;
        var maxTurn = Math.Max(1, curves.Values.SelectMany(c => c).Select(p => p.Turn).DefaultIfEmpty(1).Max());
        var healthTicks = ChartAxes.HealthTicks(curves.Values.SelectMany(c => c).Select(p => p.Health).DefaultIfEmpty(40).Max());
        var maxHealth = healthTicks[healthTicks.Count - 1];
        var axisLeft = 22 * scale;
        var axisBottom = 14 * scale;
        var plotWidth = chart.Width - axisLeft - 40 * scale;   // room at the right for end-of-curve portraits
        var plotHeight = chart.Height - axisBottom;
        var grid = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));

        foreach (var tick in healthTicks)
        {
            var y = plotHeight - tick / (double)maxHealth * plotHeight;
            chart.Children.Add(new Line { X1 = axisLeft, X2 = axisLeft + plotWidth, Y1 = y, Y2 = y, Stroke = grid, StrokeThickness = 1 });
            var label = new TextBlock { Text = tick.ToString(CultureInfo.InvariantCulture), FontSize = 9 * scale, Foreground = Brushes.LightGray };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, y - 6 * scale);
            chart.Children.Add(label);
        }

        foreach (var turn in ChartAxes.TurnTicks(maxTurn))
        {
            var x = axisLeft + turn / (double)maxTurn * plotWidth;
            var label = new TextBlock { Text = turn.ToString(CultureInfo.InvariantCulture), FontSize = 9 * scale, Foreground = Brushes.LightGray };
            Canvas.SetLeft(label, x - 3 * scale);
            Canvas.SetTop(label, plotHeight + 1 * scale);
            chart.Children.Add(label);
        }

        foreach (var entry in curves.OrderBy(kv => kv.Key))
        {
            var colour = colours.TryGetValue(entry.Key, out var c) ? c : Colors.Gray;
            var points = HealthChart.Points(entry.Value, maxTurn, maxHealth, plotWidth, plotHeight)
                .Select(p => new Point(axisLeft + p.X, p.Y)).ToList();
            if (points.Count == 0)
            {
                continue;
            }

            chart.Children.Add(new Polyline
            {
                Stroke = new SolidColorBrush(colour),
                StrokeThickness = (entry.Key == _playerId ? 3 : 1.5) * scale,
                Points = new PointCollection(points),
            });

            // Legend at the end of the curve: portrait and current health.
            var end = points[points.Count - 1];
            var portrait = CardImages.Hero(_timeline.HeroOf(entry.Key) ?? string.Empty, 14 * scale);
            Canvas.SetLeft(portrait, end.X + 2 * scale);
            Canvas.SetTop(portrait, end.Y - 7 * scale);
            chart.Children.Add(portrait);
            var value = new TextBlock { Text = entry.Value[entry.Value.Count - 1].Health.ToString(CultureInfo.InvariantCulture), FontSize = 9 * scale, Foreground = new SolidColorBrush(colour) };
            Canvas.SetLeft(value, end.X + 17 * scale);
            Canvas.SetTop(value, end.Y - 6 * scale);
            chart.Children.Add(value);
        }

        return chart;
    }
}
