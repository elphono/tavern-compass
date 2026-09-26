using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// In the shop: a frame and a label on each tavern card that fits a composition (key piece of any
/// composition playable in the lobby, add-on of a target, or a pin), and the target composition panel.
/// Positions come from <see cref="TavernLayout"/> (HDT's shop constants) and follow the canvas size.
/// Labels are built to fit by <see cref="MarkerText"/>; a DownOnly Viewbox shrinks, never clips, as a safety net.
/// </summary>
internal sealed class TavernAdvicePanel
{
    private static readonly Brush KeyBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x00));
    private static readonly Brush AddonBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x90, 0xFF));
    private static readonly Brush PinBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0x4D, 0xFF));

    private readonly Canvas _canvas;
    private readonly PanelMover _mover;
    private readonly List<UIElement> _markers = new();
    private readonly Border _targets;
    private TavernAdvice? _advice;
    private HashSet<string> _owned = new();
    private string? _status;
    private TavernPins _pins = TavernPins.Empty;
    private bool _visible;
    private bool _panelVisible;
    private IReadOnlyList<CompositionRow> _rows = new List<CompositionRow>();

    public TavernAdvicePanel(Canvas canvas, PanelMover mover)
    {
        _canvas = canvas;
        _mover = mover;
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

    /// <summary>The first marker drawn by the last layout, for the diagnostic line; null when none.</summary>
    public LayoutRect? FirstMarker { get; private set; }

    public void ShowMarkers(TavernAdvice advice, IEnumerable<string> ownedCardIds, TavernPins pins)
    {
        _advice = advice;
        _owned = new HashSet<string>(ownedCardIds);
        _pins = pins;
        _visible = true;
        RelayoutMarkers();
    }

    public void HideMarkers()
    {
        _visible = false;
        ClearMarkers();
    }

    public void ShowPanel(IReadOnlyList<CompositionRow> rows, string? status)
    {
        _rows = rows;
        _status = status;
        _panelVisible = true;
        RelayoutPanel();
    }

    public void HidePanel()
    {
        _panelVisible = false;
        _targets.Visibility = Visibility.Collapsed;
    }

    public void Detach()
    {
        _canvas.SizeChanged -= OnCanvasSizeChanged;
        ClearMarkers();
        _canvas.Children.Remove(_targets);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RelayoutMarkers();
        RelayoutPanel();
    }

    private void ClearMarkers()
    {
        foreach (var marker in _markers)
        {
            _canvas.Children.Remove(marker);
        }

        _markers.Clear();
    }

    private List<string> LinesFor(ShopAdvice card, int maxChars)
    {
        var comps = card.Advances
            .Select(a => (a.Composition.Name, a.Composition.CoreCards.Count(_owned.Contains), a.Composition.CoreCards.Count, a.IsKeyPiece))
            .ToList();
        var lines = MarkerText.Lines(comps, maxChars).ToList();
        if (_pins.IsPinned(card.CardId))
        {
            lines.Insert(0, "◆ pinned");
        }

        return lines.Take(2).ToList();
    }

    private void RelayoutMarkers()
    {
        ClearMarkers();
        FirstMarker = null;
        if (!_visible || _advice == null || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            return;
        }

        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;
        var scale = TavernLayout.Scale(height);
        var fontSize = TavernLayout.MarkerFontSize * scale;
        var slotWidth = TavernLayout.Markers(width, height, _advice.Cards.Count).FirstOrDefault().Width;
        var maxChars = MarkerText.MaxChars(slotWidth, fontSize, TavernLayout.MarkerPadding * scale);
        var marked = _advice.Cards
            .Where(c => c.Advances.Count > 0 || _pins.IsPinned(c.CardId))
            .Select(c => (Card: c, Lines: LinesFor(c, maxChars)))
            .ToList();
        var lineCount = marked.Count == 0 ? 1 : marked.Max(m => m.Lines.Count);
        var markers = TavernLayout.Markers(width, height, _advice.Cards.Count, lineCount);
        var slots = TavernLayout.CardSlots(width, height, _advice.Cards.Count);

        foreach (var (card, lines) in marked)
        {
            var colour = _pins.IsPinned(card.CardId) ? PinBrush : card.Advances.Any(a => a.IsKeyPiece) ? KeyBrush : AddonBrush;
            var textColour = colour == KeyBrush ? Brushes.Black : Brushes.White;

            // A thick frame around the card itself, so the marked card stands out at a glance.
            var slot = slots[card.Position];
            var frame = new Border
            {
                Width = slot.Width,
                Height = slot.Height,
                BorderBrush = colour,
                BorderThickness = new Thickness(4 * scale),
                CornerRadius = new CornerRadius(10 * scale),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(frame, slot.Left);
            Canvas.SetTop(frame, slot.Top);

            var rect = markers[card.Position];
            var text = new StackPanel();
            foreach (var line in lines)
            {
                text.Children.Add(new TextBlock
                {
                    Text = line,
                    FontSize = fontSize,
                    FontWeight = FontWeights.Bold,
                    Foreground = textColour,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            }

            var label = new Border
            {
                Width = rect.Width,
                Height = rect.Height,
                Background = colour,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1.5 * scale),
                CornerRadius = new CornerRadius(5 * scale),
                Padding = new Thickness(TavernLayout.MarkerPadding * scale, 0, TavernLayout.MarkerPadding * scale, 0),
                IsHitTestVisible = false,
                // Safety net: if the glyph-width estimate is ever short, shrink rather than clip.
                Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = text },
            };
            Canvas.SetLeft(label, rect.Left);
            Canvas.SetTop(label, rect.Top);

            _canvas.Children.Add(frame);
            _canvas.Children.Add(label);
            _markers.Add(frame);
            _markers.Add(label);
            FirstMarker ??= rect;
        }
    }

    /// <summary>
    /// One line per composition: name, average placement, key pieces n/m, then the final board as card
    /// vignettes left to right: held cards in colour with a tick, missing ones greyed out.
    /// </summary>
    private void RelayoutPanel()
    {
        if (!_panelVisible || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            _targets.Visibility = Visibility.Collapsed;
            return;
        }

        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;
        var scale = TavernLayout.Scale(height);
        var panel = TavernLayout.TargetPanel(width, height);
        var vignette = TavernLayout.VignetteSize * height;
        var lines = new StackPanel { Margin = new Thickness(6 * scale) };
        lines.Children.Add(new TextBlock { Text = "Target compositions", FontSize = 12 * scale, Foreground = Brushes.LightGray });
        foreach (var row in _rows)
        {
            var header = $"{row.Composition.Name} · {row.PlacementText} · {row.KeyOwned}/{row.KeyTotal} key"
                         + (row.IsTarget ? string.Empty : " · suggestion")
                         + (row.OrderKnown ? string.Empty : " · order unknown");
            lines.Children.Add(new TextBlock
            {
                Text = header,
                FontSize = 13 * scale,
                FontWeight = row.IsTarget ? FontWeights.Bold : FontWeights.Normal,
                Foreground = row.IsTarget ? Brushes.White : Brushes.LightGray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4 * scale, 0, 2 * scale),
            });
            var board = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var card in row.Vignettes)
            {
                board.Children.Add(CardImages.Vignette(card.CardId, card.Owned, vignette, scale));
            }

            lines.Children.Add(board);
        }

        if (!string.IsNullOrEmpty(_status))
        {
            lines.Children.Add(new TextBlock { Text = _status, FontSize = 10 * scale, Foreground = Brushes.Gold, TextWrapping = TextWrapping.Wrap });
        }

        _targets.Child = lines;
        _targets.Width = panel.Width;
        _targets.MinHeight = panel.Height;
        _mover.Place(_targets, "target-compositions", panel);
        _targets.Visibility = Visibility.Visible;
    }
}
