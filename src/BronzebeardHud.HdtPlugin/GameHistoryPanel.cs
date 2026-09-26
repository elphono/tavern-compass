using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.Hearthstone;

namespace BronzebeardHud.HdtPlugin;

/// <summary>During combat: the last combats of the game and every player's health curve.</summary>
internal sealed class GameHistoryPanel
{
    private static readonly Brush[] CurveBrushes =
    {
        new SolidColorBrush(Color.FromRgb(0x4D, 0xA6, 0xFF)), new SolidColorBrush(Color.FromRgb(0x7C, 0xD9, 0x5C)),
        new SolidColorBrush(Color.FromRgb(0xE6, 0x4D, 0xFF)), new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x1C)),
        new SolidColorBrush(Color.FromRgb(0x33, 0xD6, 0xC8)), new SolidColorBrush(Color.FromRgb(0xFF, 0x4D, 0x4D)),
        new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)), new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x3D)),
    };

    private readonly Canvas _canvas;
    private readonly PanelMover _mover;
    private readonly Border _panel;
    private GameTimeline? _timeline;
    private int _playerId;

    public GameHistoryPanel(Canvas canvas, PanelMover mover)
    {
        _canvas = canvas;
        _mover = mover;
        _panel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x1E)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x48, 0x0F)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        _canvas.Children.Add(_panel);
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    public void Show(GameTimeline timeline, int playerId)
    {
        _timeline = timeline;
        _playerId = playerId;
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

    private void Relayout()
    {
        if (_timeline == null || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            _panel.Visibility = Visibility.Collapsed;
            return;
        }

        var scale = _canvas.ActualHeight / 1080;
        var rect = HistoryLayout.Panel(_canvas.ActualWidth, _canvas.ActualHeight);
        var content = new StackPanel { Margin = new Thickness(6 * scale) };
        content.Children.Add(new TextBlock { Text = "Combats", FontSize = 12 * scale, Foreground = Brushes.LightGray });
        foreach (var combat in _timeline.Combats.Reverse().Take(6))
        {
            var sign = combat.Result switch { CombatResult.Win => $"+{combat.DamageDealt}", CombatResult.Loss => $"-{combat.DamageTaken}", _ => "=" };
            content.Children.Add(new TextBlock
            {
                Text = $"T{combat.Turn} {HeroName(combat.OpponentHeroCardId)} {sign}",
                FontSize = 12 * scale,
                Foreground = combat.Result == CombatResult.Loss ? Brushes.OrangeRed : combat.Result == CombatResult.Win ? Brushes.LightGreen : Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }

        var chartWidth = rect.Width - 12 * scale;
        var chartHeight = rect.Height * 0.45;
        var chart = new Canvas { Width = chartWidth, Height = chartHeight, Margin = new Thickness(0, 6 * scale, 0, 0) };
        var curves = _timeline.HealthByPlayer;
        var maxTurn = Math.Max(1, curves.Values.SelectMany(c => c).Select(p => p.Turn).DefaultIfEmpty(1).Max());
        var maxHealth = Math.Max(1, curves.Values.SelectMany(c => c).Select(p => p.Health).DefaultIfEmpty(1).Max());
        var colour = 0;
        foreach (var entry in curves.OrderBy(kv => kv.Key))
        {
            var line = new Polyline
            {
                Stroke = CurveBrushes[colour++ % CurveBrushes.Length],
                StrokeThickness = (entry.Key == _playerId ? 3 : 1.5) * scale,
                Points = new PointCollection(HealthChart.Points(entry.Value, maxTurn, maxHealth, chartWidth, chartHeight).Select(p => new Point(p.X, p.Y))),
            };
            chart.Children.Add(line);
        }

        content.Children.Add(chart);
        _panel.Child = content;
        _panel.Width = rect.Width;
        _panel.Height = rect.Height;
        _mover.Place(_panel, "combats", rect);
        _panel.Visibility = Visibility.Visible;
    }
}
