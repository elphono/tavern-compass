using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>A "#RRGGBB" colour as a brush.</summary>
internal static class HexBrush
{
    public static SolidColorBrush Of(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
}

/// <summary>
/// In the shop, on Bob's cards: a frame and a label on each card that matters for the targets (TavernHighlights: a core
/// card of a target, solid frame; an enabler or add-on, dotted frame; in the target's colour, CompTargetTracker.Palette),
/// or that is pinned; and a ◇ above each minion to pin it. Positions come from <see cref="TavernLayout"/> (HDT's shop
/// constants) and follow the canvas size. Labels are built to fit by <see cref="MarkerText"/>: nothing is shrunk, every
/// text is at least PanelTypography.Floor at 1080p. All of it is taken off while a choice is open (<see cref="Suspend"/>).
/// </summary>
internal sealed class TavernMarkers
{
    private const string PinnedColour = "#FFFFFF";

    /// <summary>A card that only has its value at this turn: no frame, the neutral label of the choices (ChoiceAdvicePanel).</summary>
    private static readonly Brush ValueBrush = new SolidColorBrush(Color.FromArgb(0xE6, 0x3A, 0x3A, 0x44));

    private readonly Canvas _canvas;
    private readonly Action<string> _togglePin;
    private readonly List<UIElement> _markers = new();
    private IReadOnlyList<string> _cards = Array.Empty<string>();
    private IReadOnlyList<TavernHighlight> _highlights = Array.Empty<TavernHighlight>();
    private IReadOnlyList<CardTurnNote?> _values = Array.Empty<CardTurnNote?>();
    private IReadOnlyList<bool> _minionSlots = Array.Empty<bool>();
    private TavernPins _pins = TavernPins.Empty;
    private bool _visible;

    /// <param name="togglePin">Called with a card id when its pin button is clicked.</param>
    public TavernMarkers(Canvas canvas, Action<string> togglePin)
    {
        _canvas = canvas;
        _togglePin = togglePin;
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    /// <summary>The first marker drawn by the last layout, for the diagnostic line; null when none.</summary>
    public LayoutRect? FirstMarker { get; private set; }

    /// <summary>When false, no pin buttons are drawn (the pinning feature was switched off by its guard).</summary>
    public bool PinButtonsEnabled { get; set; } = true;

    /// <summary>True while a choice is open (ChoiceCover): nothing is drawn, what <see cref="Show"/> gave is kept.</summary>
    public bool Suspended { get; private set; }

    /// <summary>
    /// While a choice is open the game draws its options over Bob's row: frames, labels and ◇ buttons are taken off (a ◇
    /// inside an option would pin instead of choosing), and drawn again from what was last shown once it closes, with
    /// nothing computed again. A <see cref="Show"/> meanwhile is kept for then.
    /// </summary>
    public void Suspend(bool suspended)
    {
        if (suspended == Suspended)
        {
            return;
        }

        Suspended = suspended;
        Relayout();
    }

    /// <param name="cards">Bob's row, left to right, the tavern spell included.</param>
    /// <param name="highlights">One per card (TavernHighlights.For); any other count draws no frame, the pins only.</param>
    /// <param name="minionSlots">One entry per card of Bob's row: true for a minion (it gets a pin button), false for the spell.</param>
    /// <param name="values">
    /// One per card: its value at this turn (CardTurnValue), or null; drawn in the last line left, in the form that fits
    /// (CardTurnValue.Label, TavernHighlights.MarkerLines).
    /// Any other count draws no value.
    /// </param>
    public void Show(IReadOnlyList<string> cards, IReadOnlyList<TavernHighlight>? highlights, TavernPins pins, IReadOnlyList<bool> minionSlots,
        IReadOnlyList<CardTurnNote?>? values = null)
    {
        _cards = cards;
        _highlights = highlights != null && highlights.Count == cards.Count ? highlights : cards.Select(_ => TavernHighlight.None).ToList();
        _values = values != null && values.Count == cards.Count ? values : cards.Select(_ => (CardTurnNote?)null).ToList();
        _pins = pins;
        _minionSlots = minionSlots;
        _visible = true;
        Relayout();
    }

    public void Hide()
    {
        _visible = false;
        Clear();
    }

    public void Detach()
    {
        _canvas.SizeChanged -= OnCanvasSizeChanged;
        Clear();
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => Relayout();

    private void Clear()
    {
        foreach (var marker in _markers)
        {
            _canvas.Children.Remove(marker);
        }

        _markers.Clear();
        FirstMarker = null;
    }

    private void Relayout()
    {
        Clear();
        if (!_visible || Suspended || _cards.Count == 0 || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            return;
        }

        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;
        var scale = TavernLayout.Scale(height);
        var fontSize = PanelTypography.Marker * scale;
        var slotWidth = TavernLayout.Markers(width, height, _cards.Count).FirstOrDefault().Width;
        var maxChars = MarkerText.MaxChars(slotWidth, fontSize, TavernLayout.MarkerPadding * scale);
        var marked = _cards
            .Select((card, i) => (Position: i, Highlight: _highlights[i], Pinned: _pins.IsPinned(card),
                Lines: TavernHighlights.MarkerLines(_highlights[i], _pins.IsPinned(card), maxChars, value: CardTurnValue.Label(_values[i], maxChars))))
            .Where(m => m.Lines.Count > 0)
            .ToList();
        var lineCount = marked.Count == 0 ? 1 : marked.Max(m => m.Lines.Count);
        var markers = TavernLayout.Markers(width, height, _cards.Count, lineCount);
        var slots = TavernLayout.CardSlots(width, height, _cards.Count);

        foreach (var (position, highlight, pinned, lines) in marked)
        {
            // The target's colour; a card only pinned is white. Every colour of the palette is light, so the text is black.
            // A card that only has its value gets no frame: a frame says "this card matters for a target".
            var valueOnly = highlight.Kind == HighlightKind.None && !pinned;
            var colour = HexBrush.Of(highlight.Colour ?? PinnedColour);
            var slot = slots[position];
            var frame = valueOnly ? null : Frame(highlight.Kind, slot, colour, scale);
            if (frame != null)
            {
                Canvas.SetLeft(frame, slot.Left);
                Canvas.SetTop(frame, slot.Top);
            }

            var rect = markers[position];
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            foreach (var line in lines)
            {
                text.Children.Add(new TextBlock
                {
                    Text = line,
                    FontSize = fontSize,
                    FontWeight = FontWeights.Bold,
                    Foreground = valueOnly ? Brushes.White : Brushes.Black,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            }

            // Built to fit by MarkerText (a generous glyph width): no shrinking, no cutting.
            var label = new Border
            {
                Width = rect.Width,
                Height = rect.Height,
                Background = valueOnly ? ValueBrush : colour,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1.5 * scale),
                CornerRadius = new CornerRadius(5 * scale),
                Padding = new Thickness(TavernLayout.MarkerPadding * scale, 0, TavernLayout.MarkerPadding * scale, 0),
                IsHitTestVisible = false,
                Child = text,
            };
            Canvas.SetLeft(label, rect.Left);
            Canvas.SetTop(label, rect.Top);

            if (frame != null)
            {
                OverlayLayer.Add(_canvas, frame);
                _markers.Add(frame);
            }

            OverlayLayer.Add(_canvas, label);
            _markers.Add(label);
            FirstMarker ??= rect;
        }

        AddPinButtons(width, height, scale);
    }

    /// <summary>
    /// The frame around a marked card: a core card of a target, thick and solid; an enabler or add-on, dashed; a card only
    /// pinned, a thin solid frame.
    /// </summary>
    private static UIElement Frame(HighlightKind kind, LayoutRect slot, Brush colour, double scale)
    {
        if (kind == HighlightKind.Enabler)
        {
            return new System.Windows.Shapes.Rectangle
            {
                Width = slot.Width,
                Height = slot.Height,
                Stroke = colour,
                StrokeThickness = 5 * scale,
                StrokeDashArray = new DoubleCollection { 2, 1 },
                RadiusX = 10 * scale,
                RadiusY = 10 * scale,
                IsHitTestVisible = false,
            };
        }

        return new Border
        {
            Width = slot.Width,
            Height = slot.Height,
            BorderBrush = colour,
            BorderThickness = new Thickness((kind == HighlightKind.Commit ? 6 : 4) * scale),
            CornerRadius = new CornerRadius(10 * scale),
            IsHitTestVisible = false,
        };
    }

    /// <summary>
    /// A small ◇ above each of Bob's minions (◆ once pinned): a click pins the card for the game. Clickable while the
    /// overlay stays locked (IsOverlayHitTestVisible); placed above the card, never on it, so buying is never caught
    /// (TavernLayout.PinButtons).
    /// </summary>
    private void AddPinButtons(double width, double height, double scale)
    {
        if (!PinButtonsEnabled)
        {
            return;
        }

        var buttons = TavernLayout.PinButtons(width, height, _cards.Count);
        for (var i = 0; i < buttons.Count && i < _minionSlots.Count; i++)
        {
            if (!_minionSlots[i])
            {
                continue;
            }

            var cardId = _cards[i];
            var pinned = _pins.IsPinned(cardId);
            var button = RoundButton(pinned ? "◆" : "◇", buttons[i], scale, filled: pinned);
            button.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _togglePin(cardId);
            };
            Canvas.SetLeft(button, buttons[i].Left);
            Canvas.SetTop(button, buttons[i].Top);
            OverlayLayer.Add(_canvas, button);
            _markers.Add(button);
        }
    }

    private static Border RoundButton(string text, LayoutRect rect, double scale, bool filled)
    {
        var button = new Border
        {
            Width = rect.Width,
            Height = rect.Height,
            CornerRadius = new CornerRadius(rect.Width / 2),
            Background = filled ? Brushes.White : new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x1E)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1.5 * scale),
            Cursor = System.Windows.Input.Cursors.Hand,
            IsHitTestVisible = true,
            Child = new TextBlock
            {
                Text = text,
                FontSize = PanelTypography.RoundButton * scale,
                FontWeight = FontWeights.Bold,
                Foreground = filled ? Brushes.Black : Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        OverlayClickable.Declare(button);
        return button;
    }
}
