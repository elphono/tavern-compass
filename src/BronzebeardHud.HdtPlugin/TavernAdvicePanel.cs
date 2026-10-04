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
/// In the shop: a frame and a label on each tavern card that matters (a key piece or an early enabler of a
/// composition aimed at, TavernHighlights; key piece of any playable composition or add-on of a target; a pin),
/// and the target composition panel. Positions come from <see cref="TavernLayout"/> (HDT's shop constants) and
/// follow the canvas size. Labels are built to fit by <see cref="MarkerText"/>. Nothing is ever shrunk: every
/// text is at least PanelTypography.Floor at 1080p, and what does not fit is left out (PanelFit).
/// </summary>
internal sealed class TavernAdvicePanel
{
    private static readonly Brush PanelBrush = new SolidColorBrush(Color.FromArgb(0xEB, 0x14, 0x14, 0x1E));
    private static readonly Brush ButtonBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x44));
    private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xCD, 0xD8));
    private static readonly Brush EnablerBrush = new SolidColorBrush(Color.FromRgb(0x5C, 0xE1, 0xFF));
    private static readonly Brush CommitBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x3F));
    private static readonly Brush PivotBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x43));

    private readonly CompositionSelection _selection;
    private readonly Action<string> _toggle;
    private readonly Func<int> _suggested;
    private readonly Action<string> _togglePin;
    private readonly Action<string> _openLineups;
    private readonly Action _openMeta;
    private readonly Func<Composition, CompDetail?> _detailFor;
    private readonly Func<IReadOnlyList<string>, IReadOnlyList<Composition>, IReadOnlyList<Composition>, IReadOnlyList<TavernHighlight>?> _highlightsFor;

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

    // The composition lines actually on screen (the list shows as many as fit): the suggestions the tavern highlights follow.
    private IReadOnlyList<string> _visibleIds = Array.Empty<string>();

    /// <param name="toggle">Called with a composition id when its box is clicked.</param>
    /// <param name="suggested">How many suggestions the panel shows (settings.json).</param>
    /// <param name="changeSuggested">Called with −1 or +1 when the − or + of the panel is clicked.</param>
    /// <param name="togglePin">Called with a card id when its pin button is clicked.</param>
    /// <param name="openLineups">Called with a card id when the "?" above one of Bob's minions is clicked (LineupsPanel).</param>
    /// <param name="detailFor">A composition's detail, computed when its line is clicked; null if that feature failed.</param>
    /// <param name="highlightsFor">Bob's cards, ticked and shown suggested compositions → one highlight per card (TavernHighlights); null if that feature failed.</param>
    public TavernAdvicePanel(Canvas canvas, PanelMover mover, CompositionSelection selection, Action<string> toggle, Func<int> suggested, Action<int> changeSuggested,
        Action<string> togglePin, Action<string> openLineups, Action openMeta, Func<Composition, CompDetail?> detailFor,
        Func<IReadOnlyList<string>, IReadOnlyList<Composition>, IReadOnlyList<Composition>, IReadOnlyList<TavernHighlight>?> highlightsFor)
    {
        _highlightsFor = highlightsFor;
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
            Background = PanelBrush,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x48, 0x0F)),
            BorderThickness = new Thickness(PanelFit.Border),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
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

    private int KeyOwned(Composition composition) => composition.CoreCards.Count(_owned.Contains);

    /// <summary>One highlight per card of Bob's row: the ticked compositions first, else the suggestions on screen.</summary>
    private IReadOnlyList<TavernHighlight> Highlights()
    {
        var none = _advice!.Cards.Select(_ => TavernHighlight.None).ToList();
        var ticked = _rows.Where(r => r.IsChecked).Select(r => r.Composition).ToList();
        var suggestions = _rows.Where(r => !r.IsChecked && _visibleIds.Contains(r.Composition.Id)).Select(r => r.Composition).ToList();
        var highlights = _highlightsFor(_advice.Cards.Select(c => c.CardId).ToList(), ticked, suggestions);
        return highlights != null && highlights.Count == none.Count ? highlights : none;
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
        var fontSize = PanelTypography.Marker * scale;
        var slotWidth = TavernLayout.Markers(width, height, _advice.Cards.Count).FirstOrDefault().Width;
        var maxChars = MarkerText.MaxChars(slotWidth, fontSize, TavernLayout.MarkerPadding * scale);
        var highlights = Highlights();
        var marked = _advice.Cards
            .Select((c, i) => (Card: c, Highlight: highlights[i],
                Lines: TavernHighlights.MarkerLines(highlights[i], c.Advances, KeyOwned, _pins.IsPinned(c.CardId), maxChars)))
            .Where(m => m.Lines.Count > 0)
            .ToList();
        var lineCount = marked.Count == 0 ? 1 : marked.Max(m => m.Lines.Count);
        var markers = TavernLayout.Markers(width, height, _advice.Cards.Count, lineCount);
        var slots = TavernLayout.CardSlots(width, height, _advice.Cards.Count);

        foreach (var (card, highlight, lines) in marked)
        {
            // A highlighted card takes its composition's colour (white for a suggestion); any other marked card one
            // colour per ticked composition, white when nothing is ticked (CompositionSelection). Every colour of
            // the palette is light, so the text is black.
            var colour = Brush(highlight.Kind != HighlightKind.None ? highlight.Colour ?? CompositionSelection.AutoColour
                : _selection.MarkerColour(card.Advances.Select(a => a.Composition.Id)));
            var slot = slots[card.Position];
            var frame = Frame(highlight.Kind, slot, colour, scale);
            Canvas.SetLeft(frame, slot.Left);
            Canvas.SetTop(frame, slot.Top);

            var rect = markers[card.Position];
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            foreach (var line in lines)
            {
                text.Children.Add(new TextBlock
                {
                    Text = line,
                    FontSize = fontSize,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Black,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            }

            // Built to fit by MarkerText (a generous glyph width): no shrinking, no cutting.
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
                Child = text,
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
    /// The frame around a marked card: a key piece to commit on, thick and solid; an early enabler, dashed; any
    /// other marked card (an add-on, a pin, a key piece of a composition not aimed at), the thin frame of before.
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
    /// A small ◇ above each of Bob's minions (◆ once pinned): a click pins the card for the game. Clickable
    /// while the overlay stays locked, like the boxes (IsOverlayHitTestVisible); placed above the card, never
    /// on it, so buying is never caught (TavernLayout.PinButtons). The "?" right of it opens the lineups panel.
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
                FontSize = PanelTypography.RoundButton * scale,
                FontWeight = FontWeights.Bold,
                Foreground = filled ? Brushes.Black : Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        OverlayExtensions.SetIsOverlayHitTestVisible(button, true);
        return button;
    }

    /// <summary>A small clickable button of the panel (−, +, Meta ↗, ← back), clickable while the overlay stays locked.</summary>
    private static Border PanelButton(string text, double scale, Action onClick, bool light = false, double? width = null)
    {
        var button = new Border
        {
            Height = 22 * scale,
            MinWidth = width ?? 22 * scale,
            CornerRadius = new CornerRadius(4 * scale),
            Background = light ? Brushes.White : ButtonBrush,
            BorderBrush = light ? Brushes.White : MutedBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(5 * scale, 0, 5 * scale, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = new TextBlock
            {
                Text = text,
                FontSize = PanelTypography.Button * scale,
                FontWeight = FontWeights.Bold,
                Foreground = light ? Brushes.Black : Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        button.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            onClick();
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

    private static TextBlock Text(string text, double size, double scale, Brush brush, bool bold = false, double top = 0) => new()
    {
        Text = text,
        FontSize = size * scale,
        Foreground = brush,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, top * scale, 0, 0),
    };

    /// <summary>
    /// One composition, one line: its tick box, its name above its average placement (bold and in its colour once
    /// ticked, plain light grey for a suggestion), then its final board as seven ovals, held ones ringed green
    /// with a tick. The name and every oval open the composition's detail; hover shows the card.
    /// </summary>
    private FrameworkElement CompositionLine(CompositionRow row, double scale, double height)
    {
        var colour = _selection.ColourOf(row.Composition.Id) is { } hex ? Brush(hex) : null;
        var line = new Grid { Height = PanelFit.RowHeight * scale, Margin = new Thickness(0, PanelFit.RowGap * scale, 0, 0) };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelFit.BoxColumn * scale) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelFit.NameColumn * scale) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (SelectionEnabled)
        {
            var id = row.Composition.Id;
            var box = new CheckBox
            {
                IsChecked = row.IsChecked,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                LayoutTransform = new ScaleTransform(scale * 1.2, scale * 1.2),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            box.Click += (_, _) => _toggle(id);
            OverlayExtensions.SetIsOverlayHitTestVisible(box, true);
            line.Children.Add(box);
        }

        Action<string>? open = DetailEnabled ? _ => OpenDetail(row.Composition) : null;
        var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent, Margin = new Thickness(0, 0, 6 * scale, 0) };
        // Fixed line heights (16.5 + 16.5 + 15 = 48): a name on two lines and its placement fit the 50 px line.
        var nameText = Text(row.Composition.Name, PanelTypography.CompositionName, scale, colour ?? (row.IsChecked ? Brushes.White : MutedBrush), bold: row.IsChecked);
        nameText.LineHeight = 16.5 * scale;
        nameText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        name.Children.Add(nameText);
        var placeText = Text("avg " + row.PlacementText, PanelTypography.Small, scale, MutedBrush);
        placeText.LineHeight = 15 * scale;
        placeText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        name.Children.Add(placeText);
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
        foreach (var card in row.Vignettes.Take(7))
        {
            ovals.Children.Add(CardImages.Vignette(card.CardId, card.Owned, PanelFit.OvalWidth * scale, scale, TavernLayout.PreviewHeight * height, PlacePreview, open));
        }

        Grid.SetColumn(ovals, 2);
        line.Children.Add(ovals);
        return line;
    }

    /// <summary>A line of up to seven ovals (the detail's sections and pivots); hover shows the card, a click does nothing.</summary>
    private FrameworkElement Ovals(IEnumerable<string> cardIds, Func<string, int?> tier, double scale, double height)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var cardId in cardIds.Take(7))
        {
            line.Children.Add(CardImages.Vignette(cardId, _owned.Contains(cardId), PanelFit.OvalWidth * scale, scale, TavernLayout.PreviewHeight * height, PlacePreview, tier: tier(cardId)));
        }

        return line;
    }

    /// <summary>A section title in its colour, then a short hint, on one line that wraps.</summary>
    private static TextBlock SectionTitle(string title, string hint, Brush colour, double scale)
    {
        // 3 + 16 + 2 = 21, plus a 48 px line of ovals: PanelFit.DetailSection (69).
        var text = new TextBlock
        {
            FontSize = PanelTypography.Small * scale,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 16 * scale,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            Margin = new Thickness(0, 3 * scale, 0, 2 * scale),
        };
        text.Inlines.Add(new System.Windows.Documents.Run(title.ToUpperInvariant()) { Foreground = colour, FontWeight = FontWeights.Bold });
        text.Inlines.Add(new System.Windows.Documents.Run("  " + hint) { Foreground = MutedBrush });
        return text;
    }

    /// <summary>
    /// One composition's detail, in place of the list, in short sections: "← back", its name and placement; one
    /// meta line (tribes, games, tier, final turn, the played hero's figure) with an ⓘ that says, on hover, what
    /// all of it is derived from; EARLY ENABLERS and WHEN TO COMMIT, one line of ovals each with their tier; then
    /// as many pivots as fit (PanelFit.DetailPivots), each a label and the cards shared.
    /// </summary>
    private void AddDetail(StackPanel lines, CompDetail detail, double scale, double height, double top, double? bottom)
    {
        var id = detail.Composition.Id;
        var row = _rows.FirstOrDefault(r => r.Composition.Id == id);
        var colour = _selection.ColourOf(id) is { } hex ? Brush(hex) : Brushes.White;

        var header = new DockPanel { Height = PanelFit.DetailHeader * scale, LastChildFill = true };
        var back = PanelButton("← back", scale, CloseDetail, light: true);
        back.Margin = new Thickness(0, 0, 8 * scale, 0);
        DockPanel.SetDock(back, Dock.Left);
        header.Children.Add(back);
        var place = Text("avg " + (row?.PlacementText ?? (detail.Composition.AveragePlacement is { } p ? p.ToString("0.0", CultureInfo.GetCultureInfo("fr-FR")) : "–")),
            PanelTypography.Small, scale, Brushes.White, bold: true);
        place.VerticalAlignment = VerticalAlignment.Center;
        place.Margin = new Thickness(6 * scale, 0, 0, 0);
        DockPanel.SetDock(place, Dock.Right);
        header.Children.Add(place);
        var name = Text(detail.Composition.Name, PanelTypography.CompositionName, scale, colour, bold: true);
        name.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(name);
        lines.Children.Add(header);

        // One meta line; what it is derived from behind the ⓘ (HDT's overlay tooltip, like the cards).
        var meta = new DockPanel { MinHeight = 16 * scale, LastChildFill = true };
        var info = new Border
        {
            Width = 18 * scale,
            Height = 18 * scale,
            CornerRadius = new CornerRadius(9 * scale),
            BorderBrush = MutedBrush,
            BorderThickness = new Thickness(1.2 * scale),
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(6 * scale, 0, 0, 0),
            Child = new TextBlock { Text = "i", FontSize = PanelTypography.Small * scale, FontWeight = FontWeights.Bold, Foreground = MutedBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        OverlayExtensions.SetIsOverlayHoverVisible(info, true);
        OverlayExtensions.SetToolTip(info, new Border
        {
            Background = PanelBrush,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6 * scale),
            MaxWidth = 320 * scale,
            Child = Text(detail.SourceNote, PanelTypography.Small, scale, Brushes.White),
        });
        ToolTipService.SetInitialShowDelay(info, 0);
        DockPanel.SetDock(info, Dock.Right);
        meta.Children.Add(info);
        var metaText = detail.Meta + (row?.HeroEffect is { } hero ? " · " + hero.ShopText : string.Empty);
        meta.Children.Add(Text(metaText, PanelTypography.Small, scale, MutedBrush));
        lines.Children.Add(meta);

        lines.Children.Add(SectionTitle("Early enablers", $"tier ≤ {CompDetail.MaxEarlyTier}, most seen on top boards first", EnablerBrush, scale));
        lines.Children.Add(detail.EarlyEnablers.Count == 0
            ? Text("none with a known tier", PanelTypography.Small, scale, MutedBrush)
            : Ovals(detail.EarlyEnablers.Select(c => c.CardId), c => detail.EarlyEnablers.First(x => x.CardId == c).TechLevel, scale, height));
        lines.Children.Add(SectionTitle("When to commit", "once these key pieces show up", CommitBrush, scale));
        lines.Children.Add(Ovals(detail.CommitCards.Select(c => c.CardId), c => detail.CommitCards.First(x => x.CardId == c).TechLevel, scale, height));

        // Pivots: as many as fit under the two sections (PanelFit.DetailPivots), each a label and its shared cards.
        if (_transitions.TryGetValue(id, out var transitions))
        {
            var shown = PanelFit.DetailPivots(height, top, transitions.Count, bottom);
            foreach (var transition in transitions.Take(shown))
            {
                var pivot = new Grid { Height = (PanelFit.PivotLine - 4) * scale, Margin = new Thickness(0, 4 * scale, 0, 0) };
                pivot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength((PanelFit.BoxColumn + PanelFit.NameColumn) * scale) });
                pivot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var label = new TextBlock { FontSize = PanelTypography.Small * scale, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6 * scale, 0) };
                label.Inlines.Add(new System.Windows.Documents.Run("PIVOT → ") { Foreground = PivotBrush, FontWeight = FontWeights.Bold });
                label.Inlines.Add(new System.Windows.Documents.Run(transition.To.Name) { Foreground = Brushes.White, FontWeight = FontWeights.Bold });
                label.Inlines.Add(new System.Windows.Documents.Run($" · {transition.Shared.Count} shared") { Foreground = MutedBrush });
                pivot.Children.Add(label);
                var shared = Ovals(transition.Shared, _ => null, scale, height);
                Grid.SetColumn(shared, 1);
                pivot.Children.Add(shared);
                lines.Children.Add(pivot);
            }
        }
    }

    /// <summary>The whole-card preview beside this panel (PreviewPlacer).</summary>
    private void PlacePreview(FrameworkElement vignette) => PreviewPlacer.Place(_canvas, _targets, vignette);

    /// <summary>When false, the panel draws no boxes (the selection feature was switched off by its guard).</summary>
    public bool SelectionEnabled { get; set; } = true;

    /// <summary>
    /// The title (Meta, − n +), then either as many composition lines as fit (PanelFit.Rows; "4 of 8 shown" when
    /// some are left out) or, once a line was clicked, that composition's detail in place of the list. The tick
    /// boxes, the names and the ovals are clickable while the overlay stays locked: HDT makes its window catch the
    /// mouse only while the cursor is over an element declared with IsOverlayHitTestVisible
    /// (Windows/OverlayWindow.MouseOverDetection.cs:489-491, registered at OverlayWindow.xaml.cs:194-200). Under
    /// the list, the warband line and the loading status. Nothing is shrunk; the panel never reaches the gold.
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
        var placed = _mover.Place(_targets, "target-compositions", panel, interactive: true,
            new PanelResize(PanelFit.TargetMinWidth * scale, PanelFit.TargetMinHeight * scale, RelayoutPanel));
        var resized = _mover.IsResized("target-compositions");
        _targets.Width = placed.Width;
        _targets.MinHeight = resized ? Math.Min(panel.Height, placed.Height) : panel.Height;
        var top = Canvas.GetTop(_targets);

        // The room the content has: the box the player gave the panel, otherwise down to the gold.
        double? bottom = resized ? top + placed.Height : null;
        var hasFooter = !string.IsNullOrEmpty(_footer);
        var hasStatus = !string.IsNullOrEmpty(_status);
        var statusLine = hasStatus ? PanelFit.StatusLine : 0;
        var showDetail = DetailEnabled && _view.ShowsDetail && _detail != null;
        var shown = showDetail ? 0 : PanelFit.Rows(height, top, _rows.Count, hasFooter, hasStatus, bottom);

        var lines = new StackPanel { Margin = new Thickness(PanelFit.Padding * scale), Width = placed.Width - 2 * (PanelFit.Padding + PanelFit.Border) * scale };
        var title = new DockPanel { Height = (PanelFit.TitleBar - 6) * scale, LastChildFill = true };
        var count = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(count, Dock.Right);
        if (MetaEnabled)
        {
            // The meta tier list in the browser (MetaSnapshot.Url), clickable while the overlay stays locked.
            var meta = PanelButton("Meta ↗", scale, _openMeta);
            meta.Margin = new Thickness(0, 0, 8 * scale, 0);
            count.Children.Add(meta);
        }

        count.Children.Add(PanelButton("−", scale, () => _changeSuggested(-1)));
        count.Children.Add(Text(_rows.Count > shown && !showDetail ? $"{shown} of {_rows.Count} shown" : $"{_suggested()} suggested", PanelTypography.Small, scale, MutedBrush));
        ((TextBlock)count.Children[count.Children.Count - 1]).VerticalAlignment = VerticalAlignment.Center;
        ((TextBlock)count.Children[count.Children.Count - 1]).Margin = new Thickness(5 * scale, 0, 5 * scale, 0);
        count.Children.Add(PanelButton("+", scale, () => _changeSuggested(+1)));
        title.Children.Add(count);
        var titleText = Text("Target compositions", PanelTypography.PanelTitle, scale, Brushes.White, bold: true);
        titleText.VerticalAlignment = VerticalAlignment.Center;
        title.Children.Add(titleText);
        lines.Children.Add(new Border { BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 4 * scale), Child = title });

        IReadOnlyList<string> visible;
        if (showDetail)
        {
            AddDetail(lines, _detail!, scale, height, top, bottom - statusLine * scale); // the status line, under the detail, takes its room
            visible = _visibleIds; // the tavern keeps following the list's compositions while a detail is open
        }
        else
        {
            _view.Back();
            _detail = null;
            if (_rows.Count == 0)
            {
                lines.Children.Add(Text("No composition reachable yet", PanelTypography.Body, scale, MutedBrush, top: 6));
            }

            foreach (var row in _rows.Take(shown))
            {
                lines.Children.Add(CompositionLine(row, scale, height));
            }

            visible = _rows.Take(shown).Select(r => r.Composition.Id).ToList();
            if (hasFooter)
            {
                var footer = Text(_footer!, PanelTypography.Small, scale, Brushes.White, bold: true, top: 4); // 4 + 16 = PanelFit.FooterLine
                footer.LineHeight = 16 * scale;
                footer.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                lines.Children.Add(footer);
            }
        }

        if (hasStatus)
        {
            lines.Children.Add(Text(_status!, PanelTypography.Small, scale, Brushes.Gold));
        }

        if (!resized)
        {
            _targets.MaxHeight = Math.Max(panel.Height, PanelFit.BottomLimit * height - top);
        }
        else
        {
            // Never less than what is shown: a list always shows one line, and the detail its fixed part, even in a box
            // too small for them.
            var needed = showDetail ? PanelFit.DetailMinHeight + statusLine : PanelFit.ListHeight(shown, hasFooter, hasStatus);
            _targets.MaxHeight = Math.Max(placed.Height, needed * scale);
        }

        _targets.Child = lines;
        _targets.Visibility = Visibility.Visible;

        if (!visible.SequenceEqual(_visibleIds))
        {
            // The suggestions on screen changed: the tavern highlights follow them.
            _visibleIds = visible;
            RelayoutMarkers();
        }
    }
}
