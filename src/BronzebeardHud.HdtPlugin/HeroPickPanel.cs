using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// One badge per offered hero, under the game's reroll button below that hero's portrait, plus a
/// status line under the row. Positions come from <see cref="HeroPickLayout"/> and are recomputed
/// whenever the overlay canvas changes size. Built in code rather than XAML so it compiles under WSL.
/// </summary>
internal sealed class HeroPickPanel
{
    private readonly Canvas _canvas;
    private readonly List<Border> _badges = new();
    private readonly TextBlock _status = new() { Foreground = Brushes.Gold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, IsHitTestVisible = false };
    private IReadOnlyList<HeroPickRow> _rows = new List<HeroPickRow>();
    private IReadOnlyDictionary<int, string> _compLines = new Dictionary<int, string>();
    private bool _visible;

    public HeroPickPanel(Canvas canvas)
    {
        _canvas = canvas;
        _status.Visibility = Visibility.Collapsed;
        OverlayLayer.Add(_canvas, _status);
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    /// <param name="compLines">Hero entity id → its best composition line (HeroCompAffinity), when known.</param>
    public void Show(IReadOnlyList<HeroPickRow> rows, string? status, IReadOnlyDictionary<int, string>? compLines = null)
    {
        _rows = rows;
        _compLines = compLines ?? new Dictionary<int, string>();
        foreach (var badge in _badges)
        {
            _canvas.Children.Remove(badge);
        }

        _badges.Clear();
        foreach (var row in rows)
        {
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x1E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x48, 0x0F)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                IsHitTestVisible = false,
                Tag = row,
            };
            _badges.Add(badge);
            OverlayLayer.Add(_canvas, badge);
        }

        _status.Text = status ?? string.Empty;
        _visible = true;
        Relayout();
    }

    public void Hide()
    {
        _visible = false;
        foreach (var badge in _badges)
        {
            badge.Visibility = Visibility.Collapsed;
        }

        _status.Visibility = Visibility.Collapsed;
    }

    public void Detach()
    {
        _canvas.SizeChanged -= OnCanvasSizeChanged;
        foreach (var badge in _badges)
        {
            _canvas.Children.Remove(badge);
        }

        _canvas.Children.Remove(_status);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => Relayout();

    /// <summary>Place every badge for the current canvas size; the content is rebuilt so fonts follow the scale.</summary>
    private void Relayout()
    {
        if (!_visible)
        {
            return;
        }

        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;
        var rects = HeroPickLayout.Compute(width, height, _badges.Count);
        if (rects.Count != _badges.Count)
        {
            return;
        }

        var scale = HeroPickLayout.Scale(height);
        for (var i = 0; i < _badges.Count; i++)
        {
            var badge = _badges[i];
            var rect = rects[i];
            badge.Width = rect.Width;
            badge.Height = rect.Height;
            badge.Padding = new Thickness(4 * scale, 2 * scale, 4 * scale, 2 * scale);
            var row = (HeroPickRow)badge.Tag;
            badge.Child = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Child = BuildContent(row, scale, _compLines.TryGetValue(row.Hero.EntityId, out var compLine) ? compLine : null),
            };
            Canvas.SetLeft(badge, rect.Left);
            Canvas.SetTop(badge, rect.Top);
            badge.Visibility = Visibility.Visible;
        }

        if (string.IsNullOrEmpty(_status.Text) || rects.Count == 0)
        {
            _status.Visibility = Visibility.Collapsed;
            return;
        }

        var status = HeroPickLayout.Status(width, height, rects);
        _status.FontSize = 12 * scale;
        _status.Width = status.Width;
        Canvas.SetLeft(_status, status.Left);
        Canvas.SetTop(_status, status.Top);
        _status.Visibility = Visibility.Visible;
    }

    private static UIElement BuildContent(HeroPickRow row, double scale, string? compLine)
    {
        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        if (!row.HasData)
        {
            lines.Children.Add(Text("no data", 14 * scale, Brushes.LightGray));
            AddCompLine(lines, compLine, scale);
            return lines;
        }

        // One compact line per source (two at most, the plate is small), and under the first source its
        // top-4 and first-place shares: MMR is won in the top 4.
        foreach (var (figures, index) in row.Figures.Take(2).Select((f, i) => (f, i)))
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            line.Children.Add(Text(figures.Tier ?? "–", 22 * scale, TierBrush(figures.Tier), FontWeights.Bold));
            var detail = figures.AveragePlacement.ToString("0.00", CultureInfo.InvariantCulture);
            if (figures.PickRate is { } pickRate)
            {
                detail += $" · {(pickRate * 100).ToString("0", CultureInfo.InvariantCulture)}%";
            }

            var bracket = figures.MmrPercentile is { } percentile && percentile < MmrBracket.EveryPlayer ? $" {percentile}%" : string.Empty;
            var label = Text($" {detail} {SourceLabel(figures.Source)}{bracket}", 13 * scale, Brushes.White);
            label.VerticalAlignment = VerticalAlignment.Center;
            line.Children.Add(label);
            lines.Children.Add(line);
            if (index == 0 && figures.OddsText is { } odds)
            {
                var oddsLine = Text(odds, 13 * scale, Brushes.LightGray);
                oddsLine.HorizontalAlignment = HorizontalAlignment.Center;
                lines.Children.Add(oddsLine);
            }
        }

        AddCompLine(lines, compLine, scale);
        return lines;
    }

    private static void AddCompLine(StackPanel lines, string? compLine, double scale)
    {
        if (compLine == null)
        {
            return;
        }

        var text = Text(compLine, 12 * scale, Brushes.White);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        lines.Children.Add(text);
    }

    private static string SourceLabel(string source) => source == StatsSources.HsReplayManual ? "HSR" : "FS";

    private static TextBlock Text(string text, double size, Brush brush, FontWeight? weight = null) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = brush,
        FontWeight = weight ?? FontWeights.Normal,
        IsHitTestVisible = false,
    };

    private static Brush TierBrush(string? tier) => tier switch
    {
        "S" => new SolidColorBrush(Color.FromRgb(0xFF, 0x4D, 0x4D)),
        "A" => new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x1C)),
        "B" => new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x3D)),
        "C" => new SolidColorBrush(Color.FromRgb(0x7C, 0xD9, 0x5C)),
        "D" => new SolidColorBrush(Color.FromRgb(0x4D, 0xA6, 0xFF)),
        _ => Brushes.LightGray,
    };
}
