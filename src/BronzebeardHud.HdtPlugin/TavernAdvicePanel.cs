using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// In the shop: a marker under each tavern minion that would add a piece to a target composition,
/// and the "target compositions" panel. Positions come from <see cref="TavernLayout"/> and follow
/// the overlay canvas size. Built in code, without XAML, so that it compiles under WSL.
/// </summary>
internal sealed class TavernAdvicePanel
{
    private static readonly Brush KeyBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x00));
    private static readonly Brush AddonBrush = new SolidColorBrush(Color.FromRgb(0x4D, 0xA6, 0xFF));
    private static readonly Brush PinBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0x4D, 0xFF));

    private readonly Canvas _canvas;
    private readonly List<(Border Marker, int Position)> _markers = new();
    private readonly Border _targets;
    private IReadOnlyList<ShopAdvice> _advice = new List<ShopAdvice>();
    private IReadOnlyList<CompProgress> _progress = new List<CompProgress>();
    private string? _status;
    private TavernPins _pins = TavernPins.Empty;
    private bool _visible;

    public TavernAdvicePanel(Canvas canvas)
    {
        _canvas = canvas;
        _targets = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x1E)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x48, 0x0F)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        _canvas.Children.Add(_targets);
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    public void Show(IReadOnlyList<ShopAdvice> advice, IReadOnlyList<CompProgress> progress, string? status, TavernPins pins)
    {
        _pins = pins;
        _advice = advice;
        _progress = progress;
        _status = status;
        _visible = true;
        Relayout();
    }

    public void Hide()
    {
        _visible = false;
        foreach (var (marker, _) in _markers)
        {
            marker.Visibility = Visibility.Collapsed;
        }

        _targets.Visibility = Visibility.Collapsed;
    }

    public void Detach()
    {
        _canvas.SizeChanged -= OnCanvasSizeChanged;
        ClearMarkers();
        _canvas.Children.Remove(_targets);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => Relayout();

    private void ClearMarkers()
    {
        foreach (var (marker, _) in _markers)
        {
            _canvas.Children.Remove(marker);
        }

        _markers.Clear();
    }

    private void Relayout()
    {
        ClearMarkers();
        if (!_visible || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            return;
        }

        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;
        var scale = height / 1080;
        var rects = TavernLayout.Markers(width, height, _advice.Count);
        foreach (var advice in _advice.Where(a => a.Advances.Count > 0 || _pins.IsPinned(a.CardId)))
        {
            var rect = rects[advice.Position];
            var isKey = advice.Advances.Any(a => a.IsKeyPiece);
            var isPinned = _pins.IsPinned(advice.CardId);
            var parts = advice.Advances.Select(a => (a.IsKeyPiece ? "★ " : "+ ") + a.Composition.Name).ToList();
            if (isPinned)
            {
                parts.Insert(0, "◆ pinned");
            }

            var text = string.Join(" · ", parts);
            var marker = new Border
            {
                Width = rect.Width,
                Height = rect.Height,
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x1E)),
                BorderBrush = isPinned ? PinBrush : isKey ? KeyBrush : AddonBrush,
                BorderThickness = new Thickness(2 * scale),
                CornerRadius = new CornerRadius(4 * scale),
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 11 * scale,
                    Foreground = isKey ? KeyBrush : Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            Canvas.SetLeft(marker, rect.Left);
            Canvas.SetTop(marker, rect.Top);
            _canvas.Children.Add(marker);
            _markers.Add((marker, advice.Position));
        }

        var panel = TavernLayout.TargetPanel(width, height);
        var lines = new StackPanel { Margin = new Thickness(6 * scale) };
        lines.Children.Add(new TextBlock { Text = "Target compositions", FontSize = 12 * scale, Foreground = Brushes.LightGray });
        if (_progress.Count == 0)
        {
            lines.Children.Add(new TextBlock { Text = "none yet: no key piece, add-on or tribe held", FontSize = 12 * scale, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
        }

        foreach (var progress in _progress)
        {
            var comp = progress.Composition;
            var average = comp.AveragePlacement is { } avg ? $" · avg {avg.ToString("0.00", CultureInfo.InvariantCulture)}" : string.Empty;
            lines.Children.Add(new TextBlock
            {
                Text = $"{comp.Name}: {progress.CoreOwned.Count}/{comp.CoreCards.Count} key pieces{average}",
                FontSize = 14 * scale,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }

        if (_progress.Count > 0 && _progress[0].Composition.InspirationBoards.Count > 0)
        {
            var board = _progress[0].Composition.InspirationBoards[0];
            var names = board.Select(id => Hearthstone_Deck_Tracker.Hearthstone.Database.GetCardFromId(id)?.LocalizedName ?? id);
            lines.Children.Add(new TextBlock
            {
                Text = "Inspiration: " + string.Join(", ", names),
                FontSize = 11 * scale,
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (!string.IsNullOrEmpty(_status))
        {
            lines.Children.Add(new TextBlock { Text = _status, FontSize = 10 * scale, Foreground = Brushes.Gold, TextWrapping = TextWrapping.Wrap });
        }

        _targets.Child = lines;
        _targets.Width = panel.Width;
        _targets.MinHeight = panel.Height;
        Canvas.SetLeft(_targets, panel.Left);
        Canvas.SetTop(_targets, panel.Top);
        _targets.Visibility = Visibility.Visible;
    }
}
