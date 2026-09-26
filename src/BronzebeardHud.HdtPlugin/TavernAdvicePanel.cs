using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.Utility.Extensions;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// In the shop: a frame and a label on each tavern card that fits a composition (key piece of any
/// composition playable in the lobby, add-on of a target, or a pin), and the target composition panel.
/// Positions come from <see cref="TavernLayout"/> (HDT's shop constants) and follow the canvas size.
/// Labels are built to fit by <see cref="MarkerText"/>; a DownOnly Viewbox shrinks, never clips, as a safety net.
/// </summary>
internal sealed class TavernAdvicePanel
{
    private readonly CompositionSelection _selection;
    private readonly Action<string> _toggle;
    private readonly Func<int> _suggested;
    private readonly Action<string> _togglePin;
    private IReadOnlyList<bool> _minionSlots = Array.Empty<bool>();
    private readonly Action<int> _changeSuggested;

    private readonly Canvas _canvas;
    private readonly PanelMover _mover;
    private readonly List<UIElement> _markers = new();
    private readonly Border _targets;
    private TavernAdvice? _advice;
    private HashSet<string> _owned = new();
    private string? _status;
    private string? _footer;
    private IReadOnlyDictionary<string, IReadOnlyList<CompTransition>> _transitions = new Dictionary<string, IReadOnlyList<CompTransition>>();
    private TavernPins _pins = TavernPins.Empty;
    private bool _visible;
    private bool _panelVisible;
    private IReadOnlyList<CompositionRow> _rows = new List<CompositionRow>();

    /// <param name="toggle">Called with a composition id when its box is clicked.</param>
    /// <param name="suggested">How many suggestions the panel shows (settings.json).</param>
    /// <param name="changeSuggested">Called with −1 or +1 when the − or + of the panel is clicked.</param>
    /// <param name="togglePin">Called with a card id when its pin button is clicked.</param>
    public TavernAdvicePanel(Canvas canvas, PanelMover mover, CompositionSelection selection, Action<string> toggle, Func<int> suggested, Action<int> changeSuggested,
        Action<string> togglePin)
    {
        _togglePin = togglePin;
        _suggested = suggested;
        _changeSuggested = changeSuggested;
        _canvas = canvas;
        _mover = mover;
        _selection = selection;
        _toggle = toggle;
        _targets = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x1E)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x48, 0x0F)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Visibility = Visibility.Collapsed,
        };
        OverlayLayer.Add(_canvas, _targets);
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    /// <summary>The first marker drawn by the last layout, for the diagnostic line; null when none.</summary>
    public LayoutRect? FirstMarker { get; private set; }

    /// <summary>When false, no pin buttons are drawn (the pinning feature was switched off by its guard).</summary>
    public bool PinButtonsEnabled { get; set; } = true;

    /// <param name="minionSlots">One entry per card of Bob's row: true for a minion (it gets a pin button), false for the spell.</param>
    public void ShowMarkers(TavernAdvice advice, IEnumerable<string> ownedCardIds, TavernPins pins, IReadOnlyList<bool> minionSlots)
    {
        _minionSlots = minionSlots;
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

    /// <param name="transitions">Composition id → where it can pivot to (CompTransitions); none when absent.</param>
    public void ShowPanel(IReadOnlyList<CompositionRow> rows, string? status, IReadOnlyDictionary<string, IReadOnlyList<CompTransition>>? transitions = null)
    {
        _transitions = transitions ?? new Dictionary<string, IReadOnlyList<CompTransition>>();
        _rows = rows;
        _status = status;
        _panelVisible = true;
        RelayoutPanel();
    }

    /// <summary>A last line under the compositions (the warband against its curve); null for none.</summary>
    public void SetFooter(string? footer)
    {
        if (footer == _footer)
        {
            return;
        }

        _footer = footer;
        if (_panelVisible)
        {
            RelayoutPanel();
        }
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
            // One colour per ticked composition, white when nothing is ticked (CompositionSelection); every
            // colour of the palette is light, so the text is black. Pins, typed by hand, are white too.
            var colour = Brush(_selection.MarkerColour(card.Advances.Select(a => a.Composition.Id)));
            var textColour = Brushes.Black;

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

            OverlayLayer.Add(_canvas, frame);
            OverlayLayer.Add(_canvas, label);
            _markers.Add(frame);
            _markers.Add(label);
            FirstMarker ??= rect;
        }

        AddPinButtons(width, height, scale);
    }

    /// <summary>
    /// A small ◇ above each of Bob's minions (◆ once pinned): a click pins the card for the game. Clickable
    /// while the overlay stays locked, like the boxes (IsOverlayHitTestVisible); placed above the card, never
    /// on it, so buying is never caught (TavernLayout.PinButtons).
    /// </summary>
    private void AddPinButtons(double width, double height, double scale)
    {
        if (!PinButtonsEnabled || _advice == null)
        {
            return;
        }

        var buttons = TavernLayout.PinButtons(width, height, _advice.Cards.Count);
        for (var i = 0; i < buttons.Count && i < _minionSlots.Count; i++)
        {
            if (!_minionSlots[i])
            {
                continue;
            }

            var cardId = _advice.Cards[i].CardId;
            var pinned = _pins.IsPinned(cardId);
            var button = new Border
            {
                Width = buttons[i].Width,
                Height = buttons[i].Height,
                CornerRadius = new CornerRadius(buttons[i].Width / 2),
                Background = pinned ? Brushes.White : new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x1E)),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1.5 * scale),
                Cursor = System.Windows.Input.Cursors.Hand,
                IsHitTestVisible = true,
                Child = new TextBlock
                {
                    Text = pinned ? "◆" : "◇",
                    FontSize = 14 * scale,
                    FontWeight = FontWeights.Bold,
                    Foreground = pinned ? Brushes.Black : Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            button.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _togglePin(cardId);
            };
            OverlayExtensions.SetIsOverlayHitTestVisible(button, true);
            Canvas.SetLeft(button, buttons[i].Left);
            Canvas.SetTop(button, buttons[i].Top);
            OverlayLayer.Add(_canvas, button);
            _markers.Add(button);
        }
    }

    public static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    private Border StepButton(string text, int step, double scale)
    {
        var button = new Border
        {
            Width = 20 * scale,
            Height = 20 * scale,
            CornerRadius = new CornerRadius(4 * scale),
            Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x44)),
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = new TextBlock { Text = text, FontSize = 14 * scale, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        button.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            _changeSuggested(step);
        };
        OverlayExtensions.SetIsOverlayHitTestVisible(button, true);
        return button;
    }

    /// <summary>
    /// Beside the panel wherever it was moved, inside the window (TavernLayout.PreviewRect), handed to HDT's
    /// tooltip as a placement and offsets (TavernLayout.HdtTooltipOffsets).
    /// </summary>
    private void PlacePreview(FrameworkElement vignette)
    {
        if (_canvas.ActualWidth <= 0 || double.IsNaN(Canvas.GetLeft(_targets)) || double.IsNaN(Canvas.GetTop(_targets)))
        {
            return;
        }

        // Bounds as drawn: the panel's content may be shrunk to fit the window, and HDT measures the scaled size.
        var bounds = vignette.TransformToAncestor(_canvas).TransformBounds(new Rect(0, 0, vignette.ActualWidth, vignette.ActualHeight));
        var target = new LayoutRect(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, bounds.Width, bounds.Height);
        var panel = new LayoutRect(Canvas.GetLeft(_targets) + _targets.ActualWidth / 2, Canvas.GetTop(_targets) + _targets.ActualHeight / 2,
            _targets.ActualWidth, _targets.ActualHeight);
        var (side, preview) = TavernLayout.PreviewRect(panel, target, _canvas.ActualWidth, _canvas.ActualHeight);
        var (offsetX, offsetY) = TavernLayout.HdtTooltipOffsets(side, preview, target);
        ToolTipService.SetPlacement(vignette, side switch
        {
            PreviewSide.Left => System.Windows.Controls.Primitives.PlacementMode.Left,
            PreviewSide.Right => System.Windows.Controls.Primitives.PlacementMode.Right,
            PreviewSide.Above => System.Windows.Controls.Primitives.PlacementMode.Top,
            _ => System.Windows.Controls.Primitives.PlacementMode.Bottom,
        });
        ToolTipService.SetHorizontalOffset(vignette, offsetX);
        ToolTipService.SetVerticalOffset(vignette, offsetY);
    }

    /// <summary>When false, the panel draws no boxes (the selection feature was switched off by its guard).</summary>
    public bool SelectionEnabled { get; set; } = true;

    /// <summary>
    /// One line per composition: a box to tick it (clickable while the overlay stays locked: HDT makes its
    /// window catch the mouse only while the cursor is over an element declared with IsOverlayHitTestVisible,
    /// Windows/OverlayWindow.MouseOverDetection.cs:489-491, registered at OverlayWindow.xaml.cs:194-200), its
    /// name in its colour once ticked, average placement, key pieces n/m, then the final board as card
    /// vignettes left to right: held cards in colour with a tick, missing ones greyed out, the whole card
    /// shown on hover.
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
        // Title, and how many suggestions to show: − n +, clickable while the overlay stays locked, like the boxes.
        var title = new DockPanel();
        var count = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(count, Dock.Right);
        count.Children.Add(StepButton("−", -1, scale));
        count.Children.Add(new TextBlock
        {
            Text = $"{_suggested()} suggested",
            FontSize = 12 * scale,
            Foreground = Brushes.LightGray,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4 * scale, 0, 4 * scale, 0),
        });
        count.Children.Add(StepButton("+", +1, scale));
        title.Children.Add(count);
        title.Children.Add(new TextBlock { Text = "Target compositions", FontSize = 12 * scale, Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center });
        lines.Children.Add(title);
        if (_rows.Count == 0)
        {
            lines.Children.Add(new TextBlock { Text = "No composition reachable yet", FontSize = 12 * scale, Foreground = Brushes.LightGray, Margin = new Thickness(0, 4 * scale, 0, 0) });
        }
        foreach (var row in _rows)
        {
            var colour = _selection.ColourOf(row.Composition.Id) is { } hex ? Brush(hex) : null;
            var header = $"{row.Composition.Name} · {row.PlacementText}"
                         + (row.HeroEffect is { } heroEffect ? $" ({heroEffect.ShopText})" : string.Empty)
                         + $" · {row.KeyOwned}/{row.KeyTotal} key"
                         + (row.IsChecked || row.IsTarget ? string.Empty : " · suggestion")
                         + (row.OrderKnown ? string.Empty : " · order unknown");
            var headerLine = new DockPanel { Margin = new Thickness(0, 4 * scale, 0, 2 * scale) };
            if (SelectionEnabled)
            {
                var id = row.Composition.Id;
                var box = new CheckBox
                {
                    IsChecked = row.IsChecked,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4 * scale, 0),
                    LayoutTransform = new ScaleTransform(scale, scale),
                    Cursor = System.Windows.Input.Cursors.Hand,
                };
                box.Click += (_, _) => _toggle(id);
                OverlayExtensions.SetIsOverlayHitTestVisible(box, true);
                headerLine.Children.Add(box);
            }

            headerLine.Children.Add(new TextBlock
            {
                Text = header,
                FontSize = 13 * scale,
                FontWeight = row.IsChecked || row.IsTarget ? FontWeights.Bold : FontWeights.Normal,
                Foreground = colour ?? (row.IsTarget ? Brushes.White : Brushes.LightGray),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            });
            lines.Children.Add(headerLine);
            var board = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var card in row.Vignettes)
            {
                board.Children.Add(CardImages.Vignette(card.CardId, card.Owned, vignette, scale, TavernLayout.PreviewHeight * height, PlacePreview));
            }

            // A ticked composition's colour also runs down the left of its board.
            lines.Children.Add(colour == null
                ? board
                : new Border { BorderBrush = colour, BorderThickness = new Thickness(4 * scale, 0, 0, 0), Padding = new Thickness(4 * scale, 0, 0, 0), Child = board });

            // Where this composition can pivot to, and the cards in common (hoverable like the others).
            if (_transitions.TryGetValue(row.Composition.Id, out var transitions) && CompTransitions.Text(transitions) is { } pivot)
            {
                var line = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2 * scale, 0, 0) };
                line.Children.Add(new TextBlock
                {
                    Text = pivot,
                    FontSize = 11 * scale,
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.LightGray,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4 * scale, 0),
                });
                foreach (var shared in transitions.SelectMany(t => t.Shared).Distinct().Take(7))
                {
                    line.Children.Add(CardImages.Vignette(shared, _owned.Contains(shared), vignette * 0.6, scale, TavernLayout.PreviewHeight * height, PlacePreview));
                }

                lines.Children.Add(line);
            }
        }

        if (!string.IsNullOrEmpty(_footer))
        {
            lines.Children.Add(new TextBlock { Text = _footer, FontSize = 12 * scale, Foreground = Brushes.White, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4 * scale, 0, 0) });
        }

        if (!string.IsNullOrEmpty(_status))
        {
            lines.Children.Add(new TextBlock { Text = _status, FontSize = 10 * scale, Foreground = Brushes.Gold, TextWrapping = TextWrapping.Wrap });
        }

        _targets.Width = panel.Width;
        _targets.MinHeight = panel.Height;
        _mover.Place(_targets, "target-compositions", panel, interactive: true);

        // More lines (up to 8 suggestions, their pivots) must not run off the bottom of the window: the
        // content shrinks to the room left under the panel's top, never gets cut.
        lines.Width = panel.Width - 2 * 6 * scale - 4;
        var room = Math.Max(panel.Height, height - Canvas.GetTop(_targets) - 0.005 * height);
        _targets.MaxHeight = room;
        _targets.Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxHeight = room - 4, VerticalAlignment = VerticalAlignment.Top, Child = lines };
        _targets.Visibility = Visibility.Visible;
    }
}
