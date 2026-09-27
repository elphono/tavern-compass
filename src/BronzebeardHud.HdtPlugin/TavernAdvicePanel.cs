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
    private readonly Action<string> _openLineups;
    private readonly Action _openMeta;
    private readonly Func<Composition, CompDetail?> _detailFor;

    // The list, or one composition's detail in its place (TargetPanelView), and that detail once computed.
    private readonly TargetPanelView _view = new();
    private CompDetail? _detail;
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
    /// <param name="openLineups">Called with a card id when the "?" above one of Bob's minions is clicked (LineupsPanel).</param>
    /// <param name="detailFor">A composition's detail, computed when its line is clicked; null if that feature failed.</param>
    public TavernAdvicePanel(Canvas canvas, PanelMover mover, CompositionSelection selection, Action<string> toggle, Func<int> suggested, Action<int> changeSuggested,
        Action<string> togglePin, Action<string> openLineups, Action openMeta, Func<Composition, CompDetail?> detailFor)
    {
        _detailFor = detailFor;
        _openMeta = openMeta;
        _togglePin = togglePin;
        _openLineups = openLineups;
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

    /// <summary>When false, no "Meta" button (the meta-snapshot feature was switched off by its guard).</summary>
    public bool MetaEnabled { get; set; } = true;

    /// <summary>When false, no "?" buttons above Bob's minions (the lineups feature was switched off by its guard).</summary>
    public bool LineupsEnabled { get; set; } = true;

    /// <summary>When false, a composition's line opens nothing (the comp-detail feature was switched off by its guard).</summary>
    public bool DetailEnabled { get; set; } = true;

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
        _view.Back(); // out of the game: the next game starts on the list
        _detail = null;
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
        var lineupButtons = TavernLayout.LineupButtons(width, height, _advice.Cards.Count);
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

            if (LineupsEnabled)
            {
                var ask = RoundButton("?", lineupButtons[i], scale, filled: false);
                ask.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    _openLineups(cardId);
                };
                Canvas.SetLeft(ask, lineupButtons[i].Left);
                Canvas.SetTop(ask, lineupButtons[i].Top);
                OverlayLayer.Add(_canvas, ask);
                _markers.Add(ask);
            }
        }
    }

    public static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

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
                FontSize = 14 * scale,
                FontWeight = FontWeights.Bold,
                Foreground = filled ? Brushes.Black : Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        OverlayExtensions.SetIsOverlayHitTestVisible(button, true);
        return button;
    }

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
    /// A composition's line was clicked (its name or one of its ovals): its detail replaces the list
    /// (TargetPanelView), computed once here; the list stays if that computation failed.
    /// </summary>
    private void OpenDetail(Composition composition)
    {
        if (!DetailEnabled || !_view.LineClicked(composition.Id))
        {
            return;
        }

        _detail = _detailFor(composition);
        if (_detail == null)
        {
            _view.Back();
        }

        RelayoutPanel();
    }

    /// <summary>"← back": the list again.</summary>
    private void CloseDetail()
    {
        _view.Back();
        _detail = null;
        RelayoutPanel();
    }

    private static TextBlock Text(string text, double size, double scale, Brush brush, bool bold = false, bool italic = false) => new()
    {
        Text = text,
        FontSize = size * scale,
        Foreground = brush,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        FontStyle = italic ? FontStyles.Italic : FontStyles.Normal,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 3 * scale, 0, 1 * scale),
    };

    /// <summary>
    /// One composition, one line: its tick box, its name above its average placement (bold and in its colour once
    /// ticked, plain light grey for a suggestion), then its final board as seven ovals, held ones ringed green
    /// with a tick, missing ones red. The name and every oval open the composition's detail; hover shows the card.
    /// </summary>
    private FrameworkElement CompositionLine(CompositionRow row, double scale, double height)
    {
        var colour = _selection.ColourOf(row.Composition.Id) is { } hex ? Brush(hex) : null;
        var line = new Grid { Margin = new Thickness(0, 3 * scale, 0, 3 * scale) };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TavernLayout.BoxColumn * height) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TavernLayout.NameColumn * height) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (SelectionEnabled)
        {
            var id = row.Composition.Id;
            var box = new CheckBox
            {
                IsChecked = row.IsChecked,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                LayoutTransform = new ScaleTransform(scale, scale),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            box.Click += (_, _) => _toggle(id);
            OverlayExtensions.SetIsOverlayHitTestVisible(box, true);
            line.Children.Add(box);
        }

        Action<string>? open = DetailEnabled ? _ => OpenDetail(row.Composition) : null;
        var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent, Margin = new Thickness(0, 0, 4 * scale, 0) };
        name.Children.Add(new TextBlock
        {
            Text = row.Composition.Name,
            FontSize = 13 * scale,
            FontWeight = row.IsChecked ? FontWeights.Bold : FontWeights.Normal,
            Foreground = colour ?? (row.IsChecked ? Brushes.White : Brushes.LightGray),
            TextWrapping = TextWrapping.Wrap,
        });
        name.Children.Add(new TextBlock { Text = "avg " + row.PlacementText, FontSize = 11 * scale, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap });
        if (open != null)
        {
            name.Cursor = System.Windows.Input.Cursors.Hand;
            name.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                OpenDetail(row.Composition);
            };
            OverlayExtensions.SetIsOverlayHitTestVisible(name, true);
        }

        Grid.SetColumn(name, 1);
        line.Children.Add(name);
        var ovals = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var card in row.Vignettes)
        {
            ovals.Children.Add(CardImages.Vignette(card.CardId, card.Owned, TavernLayout.RowOvalWidth * height, scale, TavernLayout.PreviewHeight * height, PlacePreview, open));
        }

        Grid.SetColumn(ovals, 2);
        line.Children.Add(ovals);
        return line;
    }

    /// <summary>
    /// One composition's detail, in place of the list: "← back" and its name, where it stands (tribes, placement,
    /// games, tier) and where the player stands on it, the early enablers and the key pieces as ovals with their
    /// tier, where it can pivot to and through which cards, the typical final turn, and what all of it is
    /// derived from. Every text wraps within the panel's width; nothing is cut.
    /// </summary>
    private void AddDetail(StackPanel lines, CompDetail detail, double scale, double height)
    {
        var oval = TavernLayout.VignetteSize * 0.85 * height;
        FrameworkElement Cards(IEnumerable<string> cardIds, Func<string, int?> tier)
        {
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var cardId in cardIds)
            {
                wrap.Children.Add(CardImages.Vignette(cardId, _owned.Contains(cardId), oval, scale, TavernLayout.PreviewHeight * height, PlacePreview, tier: tier(cardId)));
            }

            return wrap;
        }

        FrameworkElement DetailCards(IReadOnlyList<CompDetailCard> cards) =>
            cards.Count == 0
                ? Text("none with a known tier", 11, scale, Brushes.LightGray, italic: true)
                : Cards(cards.Select(c => c.CardId), id => cards.First(c => c.CardId == id).TechLevel);

        var id = detail.Composition.Id;
        var colour = _selection.ColourOf(id) is { } hex ? Brush(hex) : null;
        var header = new DockPanel { Margin = new Thickness(0, 4 * scale, 0, 0) };
        var back = new Border
        {
            Height = 22 * scale,
            CornerRadius = new CornerRadius(4 * scale),
            Background = Brushes.White,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6 * scale, 0, 6 * scale, 0),
            Margin = new Thickness(0, 0, 6 * scale, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = new TextBlock { Text = "← back", FontSize = 12 * scale, FontWeight = FontWeights.Bold, Foreground = Brushes.Black, VerticalAlignment = VerticalAlignment.Center },
        };
        back.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            CloseDetail();
        };
        OverlayExtensions.SetIsOverlayHitTestVisible(back, true);
        DockPanel.SetDock(back, Dock.Left);
        header.Children.Add(back);
        header.Children.Add(Text(detail.Composition.Name, 13, scale, colour ?? Brushes.White, bold: true));
        lines.Children.Add(header);
        lines.Children.Add(Text(detail.Header, 11, scale, Brushes.LightGray));
        if (_rows.FirstOrDefault(r => r.Composition.Id == id) is { } row)
        {
            lines.Children.Add(Text(row.Status, 11, scale, Brushes.White));
        }

        lines.Children.Add(Text($"Early enablers: tier {CompDetail.MaxEarlyTier} or lower, most seen on top final boards first", 11, scale, Brushes.LightGray));
        lines.Children.Add(DetailCards(detail.EarlyEnablers));
        lines.Children.Add(Text("When to commit: once these key pieces show up", 11, scale, Brushes.LightGray));
        lines.Children.Add(DetailCards(detail.CommitCards));

        // Where it can pivot to, and through which cards (CompTransitions, among the compositions playable here).
        if (_transitions.TryGetValue(id, out var transitions))
        {
            lines.Children.Add(Text(transitions.Count == 0 ? "Pivots: none among this lobby's compositions" : "Pivots: where it can turn, through the cards they share",
                11, scale, Brushes.LightGray));
            foreach (var transition in transitions)
            {
                lines.Children.Add(Text($"→ {transition.To.Name} ({transition.Shared.Count} shared)", 11, scale, Brushes.White));
                lines.Children.Add(Cards(transition.Shared, _ => null));
            }
        }

        if (detail.TypicalFinalTurnText is { } turn)
        {
            lines.Children.Add(Text(turn, 11, scale, Brushes.White));
        }

        lines.Children.Add(Text(detail.SourceNote, 10, scale, Brushes.LightGray, italic: true));
    }

    /// <summary>The whole-card preview beside this panel (PreviewPlacer).</summary>
    private void PlacePreview(FrameworkElement vignette) => PreviewPlacer.Place(_canvas, _targets, vignette);

    /// <summary>When false, the panel draws no boxes (the selection feature was switched off by its guard).</summary>
    public bool SelectionEnabled { get; set; } = true;

    /// <summary>
    /// The title (− n +, Meta), then either one line per composition (CompositionLine) or, once a line was
    /// clicked, that composition's detail in place of the list (AddDetail). The tick boxes, the name and the
    /// ovals are clickable while the overlay stays locked: HDT makes its window catch the mouse only while the
    /// cursor is over an element declared with IsOverlayHitTestVisible (Windows/OverlayWindow.MouseOverDetection.cs:489-491,
    /// registered at OverlayWindow.xaml.cs:194-200). Under them, the warband line and the loading status.
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
        var lines = new StackPanel { Margin = new Thickness(6 * scale) };
        // Title, and how many suggestions to show: − n +, clickable while the overlay stays locked, like the boxes.
        var title = new DockPanel();
        var count = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(count, Dock.Right);
        if (MetaEnabled)
        {
            // The meta tier list in the browser (MetaSnapshot.Url), clickable while the overlay stays locked.
            var meta = new Border
            {
                Height = 20 * scale,
                CornerRadius = new CornerRadius(4 * scale),
                Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x44)),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5 * scale, 0, 5 * scale, 0),
                Margin = new Thickness(0, 0, 8 * scale, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = new TextBlock { Text = "Meta ↗", FontSize = 12 * scale, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center },
            };
            meta.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _openMeta();
            };
            OverlayExtensions.SetIsOverlayHitTestVisible(meta, true);
            count.Children.Add(meta);
        }

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

        if (DetailEnabled && _view.ShowsDetail && _detail != null)
        {
            AddDetail(lines, _detail, scale, height);
        }
        else
        {
            _view.Back();
            _detail = null;
            if (_rows.Count == 0)
            {
                lines.Children.Add(new TextBlock { Text = "No composition reachable yet", FontSize = 12 * scale, Foreground = Brushes.LightGray, Margin = new Thickness(0, 4 * scale, 0, 0) });
            }

            foreach (var row in _rows)
            {
                lines.Children.Add(CompositionLine(row, scale, height));
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

        // More lines (up to 8 suggestions, or a long detail) must not run off the bottom of the window: the
        // content shrinks to the room left under the panel's top, never gets cut.
        lines.Width = panel.Width - 2 * 6 * scale - 4;
        var room = Math.Max(panel.Height, height - Canvas.GetTop(_targets) - 0.005 * height);
        _targets.MaxHeight = room;
        _targets.Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxHeight = room - 4, VerticalAlignment = VerticalAlignment.Top, Child = lines };
        _targets.Visibility = Visibility.Visible;
    }
}
