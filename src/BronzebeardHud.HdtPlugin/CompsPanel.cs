using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Extensions;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// The single "Compositions" panel (Ali, 2026-10-04: the two panels of before said the same thing twice, and HDT's way of
/// showing a guide reads better): HDT's own comp guides that the lobby can play (LobbyGuides: a guide of an absent tribe is
/// not listed), grouped by tier as HDT groups them, the targets first in their tier
/// (CompTargets.Tiers), each guide on one line — its tick box, its name, its core cards as ovals (held: green ring and
/// tick). A target carries its colour (CompTargetTracker.Palette): a 3 px bar on the left, a tint, its rank in a round
/// badge, its name in bold in that colour; the frames on Bob's cards and the labels of choices use the same colour. A click
/// on a name or an oval replaces the list by that guide's detail, as HDT does: "← All comp guides", the name with its tier
/// and difficulty badges, then HOW TO PLAY, CORE CARDS, ADDON CARDS, WHEN TO COMMIT, COMMON ENABLERS and PIVOTS. Hovering a
/// line shows that whole guide in a popup beside the panel, free of the panel's box (GuidePopup, after a short delay); the
/// detail and the popup draw the sections with the same code (GuideView).
///
/// Movable and resizable ("target-compositions", the id the panel always had: a place Ali saved stays valid), in the shop
/// and in combat, off the screen while a choice is open in the shop (<see cref="Suspend"/>). Nothing is shrunk: every piece is built and measured in place, then what fits is shown (CompGuideLayout:
/// the targets never give way to the others, "k of n shown" by the title; a detail section that does not fit is left out whole, "k of n
/// sections"). The tick boxes, the names, the ovals and the buttons are clickable while the overlay stays locked: HDT
/// makes its window catch the mouse only over elements declared with IsOverlayHitTestVisible
/// (Windows/OverlayWindow.MouseOverDetection.cs:489-491).
/// </summary>
internal sealed class CompsPanel
{
    /// <summary>The layout id: unchanged since the panel was "Target compositions" (PanelLayout.KnownPanels).</summary>
    public const string PanelId = "target-compositions";

    /// <summary>
    /// The list's vertical rhythm, in design pixels (× scale): a guide line is an oval tall (PanelFit.RowHeight) plus 1 above
    /// and below, 3 apart; the panel's padding is 6 above and below (PanelFit.Padding, 8, on the sides: the six ovals fill
    /// the width). Measured on 2026-10-04 in the simulation at 1920 × 1080: with PanelFit.RowGap (6), 2 px insets and a
    /// "k of n shown" line under the list, three targets in two tiers did not fit at the default place (373 px for 330), and
    /// the third target was left out; this way they take about 322.
    /// </summary>
    private const double RowSpacing = 3;
    private const double RowInset = 1;
    private const double VerticalPadding = 6;

    /// <summary>A target's line: the bar in its colour, on the left, taken from the tick box column (the names stay aligned).</summary>
    private const double TargetBar = 3;

    /// <summary>The panel's background and frame (the guide popup wears them too).</summary>
    internal static readonly Brush PanelBrush = new SolidColorBrush(Color.FromArgb(0xEB, 0x14, 0x14, 0x1E));
    internal static readonly Brush FrameBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x48, 0x0F));
    private static readonly Brush ButtonBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x44));
    private static readonly Brush MutedBrush = GuideView.MutedBrush;
    private static readonly Brush RuleBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));

    private readonly Canvas _canvas;
    private readonly PanelMover _mover;
    private readonly Border _panel;
    private readonly Action<string> _toggle;
    private readonly Func<int> _count;
    private readonly Action<int> _changeCount;
    private readonly Action _openMeta;
    private readonly Func<CompGuide, IReadOnlyList<GuidePivot>?> _pivotsFor;
    private readonly Action<CompGuide, SectionFit> _detailShown;
    private readonly Func<CompGuide, string?> _contextFor;
    private readonly Action<Action> _run;
    private readonly TargetPanelView _view = new();
    private readonly GuidePopup _popup;
    private Dictionary<string, FrameworkElement> _shownLines = new(StringComparer.Ordinal);

    private CompGuideBoard _board = CompGuideBoard.Empty;
    private IReadOnlyList<CompTarget> _targets = Array.Empty<CompTarget>();
    private HashSet<string> _held = new(StringComparer.Ordinal);
    private string? _source;
    private string? _status;
    private string? _note;
    private WarbandComparison? _footer;
    private string? _footerKey;
    private string? _loggedDetail;

    // A redraw was asked while a choice hid the panel (Suspend): it is redrawn when it comes back, not merely shown again.
    private bool _staleWhileSuspended;

    /// <param name="toggle">Called with a guide id (CompGuide.Id) when its tick box is clicked.</param>
    /// <param name="count">How many targets are wanted (settings.json), shown between − and +.</param>
    /// <param name="changeCount">Called with −1 or +1 when − or + is clicked.</param>
    /// <param name="pivotsFor">A guide's pivots (GuidePivots), under their own guard; null when that feature failed.</param>
    /// <param name="detailShown">Called once each time a guide's detail is opened, with what of it fits (the log line).</param>
    /// <param name="contextFor">
    /// A guide's line of Firestone context (TargetContext.For, on the bridge and the hero being played), under its header in
    /// the detail and the popup; null: no line.
    /// </param>
    /// <param name="run">Runs what a click or a resize triggers, under the panel's feature guard (a WPF handler is under none).</param>
    /// <param name="cursorOver">
    /// Whether the cursor is within an element, for the guide popup's MouseLeave (GuidePopup.IsCursorOver when null; the
    /// simulation's self-test swaps it).
    /// </param>
    public CompsPanel(Canvas canvas, PanelMover mover, Action<string> toggle, Func<int> count, Action<int> changeCount, Action openMeta,
        Func<CompGuide, IReadOnlyList<GuidePivot>?> pivotsFor, Action<CompGuide, SectionFit> detailShown, Func<CompGuide, string?> contextFor,
        Action<Action> run, Func<FrameworkElement, bool>? cursorOver = null)
    {
        _canvas = canvas;
        _mover = mover;
        _toggle = toggle;
        _count = count;
        _changeCount = changeCount;
        _openMeta = openMeta;
        _pivotsFor = pivotsFor;
        _detailShown = detailShown;
        _contextFor = contextFor;
        _run = run;
        _panel = new Border
        {
            Background = PanelBrush,
            BorderBrush = FrameBrush,
            BorderThickness = new Thickness(PanelFit.Border),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Visibility = Visibility.Collapsed,
        };
        OverlayLayer.Add(_canvas, _panel);
        _canvas.SizeChanged += OnCanvasSizeChanged;
        _popup = new GuidePopup(canvas, PopupContent, () => _mover.MoveMode || _view.ShowsDetail || !IsVisible || Suspended, cursorOver ?? GuidePopup.IsCursorOver, run);
        _mover.MoveModeChanged += OnMoveModeChanged;
    }

    /// <summary>Shown by the plugin (in the shop and in combat), even while a choice hides it (<see cref="Suspended"/>).</summary>
    public bool IsVisible { get; private set; }

    /// <summary>True while a choice is open (ChoiceCover): the panel and its popup are off the screen, what it shows is kept.</summary>
    public bool Suspended { get; private set; }

    /// <summary>
    /// While a choice is open the panel is taken off the screen with its guide popup (at its default place it covered the
    /// bottom of the third option), and put back once it closes: the very same elements when nothing changed meanwhile,
    /// redrawn from what it was last given otherwise (a new board, a resize). No popup shows meanwhile.
    /// </summary>
    public void Suspend(bool suspended)
    {
        if (suspended == Suspended)
        {
            return;
        }

        Suspended = suspended;
        if (suspended)
        {
            _panel.Visibility = Visibility.Collapsed;
            _popup.Hide();
            return;
        }

        if (_staleWhileSuspended || !IsVisible)
        {
            _staleWhileSuspended = false;
            Relayout();
            return;
        }

        _panel.Visibility = Visibility.Visible;
        _popup.Listed(_shownLines);
    }

    /// <summary>The popup of a hovered guide line (the simulation's self-test drives it).</summary>
    public GuidePopup Popup => _popup;

    /// <summary>The guide lines the list shows, by guide id: what hover is tracked on (none in a guide's detail).</summary>
    public IReadOnlyDictionary<string, FrameworkElement> ShownLines => _shownLines;

    /// <summary>Hides the guide popup: the shop phase ended or began (Plugin.UpdateComps).</summary>
    public void HideGuidePopup() => _popup.Hide();

    /// <summary>The panel on the canvas (the simulation's self-test reads its texts).</summary>
    public Border Element => _panel;

    /// <summary>True while a guide's detail stands in place of the list.</summary>
    public bool ShowsDetail => _view.ShowsDetail;

    /// <summary>When false, no "Meta ↗" button (the meta-snapshot feature was switched off by its guard).</summary>
    public bool MetaEnabled { get; set; } = true;

    /// <summary>When false, a click on a guide opens nothing (the comp-detail feature was switched off by its guard).</summary>
    public bool DetailEnabled { get; set; } = true;

    /// <summary>When false, no tick boxes (the comp-selection feature was switched off by its guard).</summary>
    public bool SelectionEnabled { get; set; } = true;

    /// <param name="board">The lobby's guides ranked against the player's board and hand (CompTargets.Round: LobbyGuides.Playable).</param>
    /// <param name="targets">The targets and their colours (CompTargetTracker.Next).</param>
    /// <param name="held">Base card ids of the player's board and hand: the green rings.</param>
    /// <param name="source">CompGuideSources.HdtFree or HdtTier7; null when HDT shows no guides.</param>
    /// <param name="status">Why there are no guides (HDT's state), or null.</param>
    /// <param name="note">A muted line under the title ("Lobby tribes unknown: every guide listed"), or null.</param>
    public void Show(CompGuideBoard board, IReadOnlyList<CompTarget> targets, IEnumerable<string> held, string? source, string? status, string? note = null)
    {
        _board = board;
        _targets = targets;
        _held = new HashSet<string>(held, StringComparer.Ordinal);
        _source = source;
        _status = status;
        _note = note;
        IsVisible = true;
        Relayout();
    }

    /// <summary>The board's power under the list (BoardPowerView, from WarbandCurve.Compare); null for none.</summary>
    public void SetFooter(WarbandComparison? footer)
    {
        var key = footer == null ? null : footer.Line + "|" + footer.PowerText;
        if (key == _footerKey)
        {
            return;
        }

        _footerKey = key;
        _footer = footer;
        if (IsVisible)
        {
            Relayout();
        }
    }

    public void Hide()
    {
        IsVisible = false;
        _view.Back(); // out of the game: the next one starts on the list
        _loggedDetail = null;
        _panel.Visibility = Visibility.Collapsed;
        _popup.Hide();
        _popup.NewGame();
    }

    public void Detach()
    {
        _canvas.SizeChanged -= OnCanvasSizeChanged;
        _mover.MoveModeChanged -= OnMoveModeChanged;
        _popup.Detach();
        _canvas.Children.Remove(_panel);
    }

    private void OnMoveModeChanged() => _run(_popup.Hide);

    /// <summary>What the popup draws for a guide of the list, and where the panel and the other panels are; null when it left the list.</summary>
    private GuidePopupContent? PopupContent(string guideId)
    {
        var guide = _board.All.Select(p => p.Guide).FirstOrDefault(g => g.Id == guideId);
        var left = Canvas.GetLeft(_panel);
        var top = Canvas.GetTop(_panel);
        if (guide == null || !IsVisible || Suspended || double.IsNaN(left) || double.IsNaN(top) || _panel.ActualWidth <= 0)
        {
            return null;
        }

        var panel = new LayoutRect(left + _panel.ActualWidth / 2, top + _panel.ActualHeight / 2, _panel.ActualWidth, _panel.ActualHeight);
        return new GuidePopupContent(guide, CompTargets.Find(_targets, guide), _held, _pivotsFor(guide), _contextFor(guide), panel, _mover.VisiblePanels(except: _panel));
    }

    /// <summary>Opens a guide's detail in place of the list, as a click on its name does; false when it is not listed.</summary>
    public bool OpenDetail(string guideId)
    {
        if (!DetailEnabled || _board.All.All(p => p.Guide.Id != guideId) || !_view.LineClicked(guideId))
        {
            return false;
        }

        Relayout();
        return true;
    }

    /// <summary>"← All comp guides": the list again.</summary>
    public void CloseDetail()
    {
        _view.Back();
        _loggedDetail = null;
        Relayout();
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => _run(Relayout);

    private static SolidColorBrush Tint(string hex)
    {
        var colour = (Color)ColorConverter.ConvertFromString(hex);
        return new SolidColorBrush(Color.FromArgb(0x38, colour.R, colour.G, colour.B));
    }

    private static TextBlock Text(string text, double size, double scale, Brush brush, bool bold = false) => GuideView.Text(text, size, scale, brush, bold);

    /// <summary>A card's tavern tier for its badge; null when HDT knows none.</summary>
    internal static int? TierOf(string cardId) => Database.GetCardFromId(cardId)?.TechLevel is > 0 and var tier ? tier : null;

    /// <summary>A tier's bar, as in HDT's Tier 7 list: the letter on the tier's gradient.</summary>
    private static FrameworkElement TierHeader(CompGuideBoardTier tier, double scale) => new Border
    {
        Background = GuideView.TierBrush(tier.Tier),
        CornerRadius = new CornerRadius(3 * scale),
        Padding = new Thickness(6 * scale, 0, 6 * scale, 1 * scale),
        Margin = new Thickness(0, 4 * scale, 0, 0),
        Child = Text(tier.Letter, PanelTypography.Body, scale, Brushes.White, bold: true),
    };

    /// <summary>
    /// A small clickable button of the panel (−, +, Meta ↗, ← All comp guides), clickable while the overlay stays locked.
    /// A button that is not <paramref name="enabled"/> is drawn dim and does nothing, but still catches the click, so that
    /// it does not fall through to the game.
    /// </summary>
    private Border PanelButton(string text, double scale, Action onClick, bool light = false, bool enabled = true)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = PanelTypography.Button * scale,
            FontWeight = FontWeights.Bold,
            Foreground = light ? Brushes.Black : Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var button = new Border
        {
            Height = 22 * scale,
            MinWidth = 22 * scale,
            CornerRadius = new CornerRadius(4 * scale),
            Background = light ? Brushes.White : ButtonBrush,
            BorderBrush = light ? Brushes.White : MutedBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(5 * scale, 0, 5 * scale, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = enabled ? System.Windows.Input.Cursors.Hand : null,
            Opacity = enabled ? 1 : 0.4,
            Child = label,
        };
        button.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (enabled)
            {
                _run(onClick);
            }
        };
        OverlayExtensions.SetIsOverlayHitTestVisible(button, true);
        return button;
    }

    private FrameworkElement TickBox(string guideId, bool ticked, double scale)
    {
        var box = new CheckBox
        {
            IsChecked = ticked,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            LayoutTransform = new ScaleTransform(scale * 1.2, scale * 1.2),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        box.Click += (_, _) => _run(() => _toggle(guideId));
        OverlayExtensions.SetIsOverlayHitTestVisible(box, true);
        return box;
    }

    /// <summary>A target's rank (1 to 4) in a round badge of its colour.</summary>
    private static FrameworkElement RankBadge(CompTarget target, double scale) => new Border
    {
        Width = 18 * scale,
        Height = 18 * scale,
        CornerRadius = new CornerRadius(9 * scale),
        Background = HexBrush.Of(target.Colour),
        HorizontalAlignment = HorizontalAlignment.Left,
        Child = new TextBlock
        {
            Text = target.Rank.ToString(CultureInfo.InvariantCulture),
            FontSize = PanelTypography.Small * scale,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    private FrameworkElement Oval(string cardId, double scale, double height, Action<string>? onClick) =>
        CardImages.Vignette(cardId, _held.Contains(cardId), PanelFit.OvalWidth * scale, scale, TavernLayout.PreviewHeight * height, PlacePreview, onClick, TierOf(cardId));

    /// <summary>The whole-card preview beside this panel (PreviewPlacer).</summary>
    private void PlacePreview(FrameworkElement vignette) => PreviewPlacer.Place(_canvas, _panel, vignette);

    /// <summary>
    /// One guide, one line: its tick box (and, for a target, its rank under it), its name (two lines when it needs them,
    /// never cut: the line grows), its core cards in the guide's order. A target's line carries a bar and a tint of its
    /// colour. The name and the ovals open the detail; hover on an oval shows the card. The ovals are put in by
    /// <c>FillOvals</c>, once the line is known to be shown: they have a fixed size, so the line measures the same without
    /// them, and a line left out loads no picture.
    /// </summary>
    private (FrameworkElement Element, Action FillOvals) Row(CompGuideProgress row, double scale, double height)
    {
        var guide = row.Guide;
        var target = CompTargets.Find(_targets, guide);
        Action<string>? open = DetailEnabled ? _ => _run(() => OpenDetail(guide.Id)) : null;

        var grid = new Grid { MinHeight = PanelFit.RowHeight * scale };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength((PanelFit.BoxColumn - (target != null ? TargetBar : 0)) * scale) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelFit.NameColumn * scale) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var box = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        if (SelectionEnabled)
        {
            box.Children.Add(TickBox(guide.Id, target?.Ticked == true, scale));
        }

        if (target != null)
        {
            var rank = RankBadge(target, scale);
            ((FrameworkElement)rank).Margin = new Thickness(0, SelectionEnabled ? 4 * scale : 0, 0, 0);
            box.Children.Add(rank);
        }

        grid.Children.Add(box);

        var colour = target != null ? HexBrush.Of(target.Colour) : row.Score > 0 ? Brushes.White : MutedBrush;
        var name = Text(guide.Name, PanelTypography.CompositionName, scale, colour, bold: target != null);
        FrameworkElement nameContent = name;
        if (target?.Kind == TargetKind.InProgress)
        {
            // Kept beside a ticked guide because it is being built (two key cards held): said under its name, so that a
            // coloured line nobody ticked does not read as a tick that did not take.
            var names = new StackPanel();
            names.Children.Add(name);
            names.Children.Add(Text("in progress", PanelTypography.Small, scale, MutedBrush));
            nameContent = names;
        }

        var nameCell = new Border { Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(0, 0, 6 * scale, 0), Child = nameContent };
        if (open != null)
        {
            nameCell.Cursor = System.Windows.Input.Cursors.Hand;
            nameCell.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                open(guide.Id);
            };
            OverlayExtensions.SetIsOverlayHitTestVisible(nameCell, true);
        }

        Grid.SetColumn(nameCell, 1);
        grid.Children.Add(nameCell);

        var ovals = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(ovals, 2);
        grid.Children.Add(ovals);
        void FillOvals()
        {
            var (shown, more) = PanelFit.ListOvals(guide.CoreCards.Count);
            foreach (var card in guide.CoreCards.Take(shown))
            {
                ovals.Children.Add(Oval(card, scale, height, open));
            }

            if (more > 0)
            {
                var rest = Text($"+{more.ToString(CultureInfo.InvariantCulture)}", PanelTypography.Body, scale, MutedBrush, bold: true);
                rest.VerticalAlignment = VerticalAlignment.Center;
                ovals.Children.Add(rest);
            }
        }

        // No negative margin to reach into the padding: WPF clips an element to its layout slot, and the bar was cut away.
        var element = new Border
        {
            Background = target != null ? Tint(target.Colour) : Brushes.Transparent,
            BorderBrush = target != null ? HexBrush.Of(target.Colour) : null,
            BorderThickness = new Thickness(target != null ? TargetBar * scale : 0, 0, 0, 0),
            Margin = new Thickness(0, RowSpacing * scale, 0, 0),
            Padding = new Thickness(0, RowInset * scale, 0, RowInset * scale),
            Child = grid,
        };

        // The whole line, its name and its background included, shows the guide's popup on hover (after a delay).
        _popup.Track(element, guide.Id);
        return (element, FillOvals);
    }

    /// <summary>
    /// "Compositions" — or, in a guide's detail, the "← All comp guides" button in its place, as HDT puts it at the top —
    /// then the source, Meta ↗ and − n targets +.
    /// </summary>
    private FrameworkElement TitleBar(double scale, bool detail = false) => TitleBar(scale, detail, out _);

    /// <param name="shown">The "k of n shown" right of the title, written once the list is fitted (the list's own line saves its height).</param>
    private FrameworkElement TitleBar(double scale, bool detail, out TextBlock shown)
    {
        var bar = new DockPanel { LastChildFill = true };
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(right, Dock.Right);
        if (_source != null)
        {
            var tag = Text(_source == CompGuideSources.HdtTier7 ? "Tier 7" : "HDT free", PanelTypography.Small, scale, MutedBrush);
            tag.TextWrapping = TextWrapping.NoWrap;
            tag.VerticalAlignment = VerticalAlignment.Center;
            tag.Margin = new Thickness(0, 0, 8 * scale, 0);
            right.Children.Add(tag);
        }

        if (MetaEnabled)
        {
            var meta = PanelButton("Meta ↗", scale, _openMeta);
            meta.Margin = new Thickness(0, 0, 8 * scale, 0);
            right.Children.Add(meta);
        }

        // A tick silences the automatic guesses, which − n + counts: with a guide ticked, the targets are the ticked guides and
        // the guides in progress (CompTargets.Choose), − and + have nothing to change, so they are dim and the title counts
        // what was chosen.
        var chosen = _targets.Count(t => t.Ticked);
        var n = chosen > 0 ? chosen : _count();
        right.Children.Add(PanelButton("−", scale, () => _changeCount(-1), enabled: chosen == 0));
        var count = Text(chosen > 0
                ? $"{n.ToString(CultureInfo.InvariantCulture)} chosen"
                : $"{n.ToString(CultureInfo.InvariantCulture)} target{(n == 1 ? string.Empty : "s")}",
            PanelTypography.Small, scale, MutedBrush);
        count.TextWrapping = TextWrapping.NoWrap;
        count.VerticalAlignment = VerticalAlignment.Center;
        count.Margin = new Thickness(5 * scale, 0, 5 * scale, 0);
        right.Children.Add(count);
        right.Children.Add(PanelButton("+", scale, () => _changeCount(+1), enabled: chosen == 0));
        bar.Children.Add(right);

        shown = new TextBlock { FontSize = PanelTypography.Small * scale, Foreground = MutedBrush, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6 * scale, 0, 0, 0) };
        if (detail)
        {
            var back = PanelButton("← All comp guides", scale, CloseDetail, light: true);
            back.HorizontalAlignment = HorizontalAlignment.Left;
            bar.Children.Add(back);
        }
        else
        {
            var title = Text("Compositions", PanelTypography.PanelTitle, scale, Brushes.White, bold: true);
            title.TextWrapping = TextWrapping.NoWrap;
            title.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(title, Dock.Left);
            bar.Children.Add(title);
            bar.Children.Add(shown); // fills what is left, from the left
        }

        return new Border { BorderBrush = RuleBrush, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 4 * scale), Child = bar };
    }

    /// <summary>
    /// The title, then either the list or one guide's detail, built and measured in place between the panel's top and
    /// the room it has: the box the player gave it, otherwise down to the gold (it may have been moved).
    /// </summary>
    private void Relayout()
    {
        if (!IsVisible || Suspended || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            _staleWhileSuspended |= Suspended;
            _panel.Visibility = Visibility.Collapsed;
            _popup.Hide();
            return;
        }

        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;
        var scale = TavernLayout.Scale(height);
        var rect = TavernLayout.TargetPanel(width, height);
        var placed = _mover.Place(_panel, PanelId, rect, interactive: true,
            new PanelResize(PanelFit.TargetMinWidth * scale, PanelFit.TargetMinHeight * scale, () => _run(Relayout)));
        var resized = _mover.IsResized(PanelId);
        _panel.Width = placed.Width;
        _panel.MinHeight = resized ? Math.Min(rect.Height, placed.Height) : rect.Height;
        var top = Canvas.GetTop(_panel);
        var room = resized ? placed.Height : Math.Max(rect.Height, PanelFit.BottomLimit * height - top);
        // The frame at its thickest, 3 px in move mode (PanelMover), which a switch of mode does not redraw: computed with
        // the 2 px of the normal frame, the content was 2 px wider than the room in move mode and cut on the right (found by
        // the simulation's self-test). Six ovals still fit: the last one's 4 px gap on the right is what gives way.
        var inner = Math.Max(0, placed.Width - 2 * (PanelFit.Padding * scale + Math.Max(PanelFit.Border, 3)));
        var frame = 2 * (VerticalPadding * scale + Math.Max(PanelFit.Border, 3));
        var lines = new StackPanel { Margin = new Thickness(PanelFit.Padding * scale, VerticalPadding * scale, PanelFit.Padding * scale, VerticalPadding * scale), Width = inner };
        _panel.Child = lines;
        _panel.Visibility = Visibility.Visible;

        var guide = _view.DetailId is { } id ? _board.All.Select(p => p.Guide).FirstOrDefault(g => g.Id == id) : null;
        double needed;
        _shownLines = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
        if (guide != null && DetailEnabled)
        {
            needed = LayoutDetail(lines, guide, scale, height, inner, room - frame) + frame;
        }
        else
        {
            _view.Back(); // the guide left HDT's list, or the detail was switched off
            _loggedDetail = null;
            needed = LayoutList(lines, scale, height, inner, room - frame) + frame;
        }

        // Never less than what is shown: a list always shows one line, even in a box too small for it.
        _panel.MaxHeight = Math.Max(room, needed);

        // Last, the panel being complete: the popup of a hovered line follows the redraw (no line in a detail: it hides).
        _popup.Listed(_shownLines);
    }

    /// <summary>The list, as much of it as fits in <paramref name="room"/>; returns the height used.</summary>
    private double LayoutList(StackPanel lines, double scale, double height, double inner, double room)
    {
        var chrome = new List<FrameworkElement> { TitleBar(scale, detail: false, out var shownCount) };
        shownCount.Text = "99 of 99 shown"; // the widest it will be, while measuring
        if (!string.IsNullOrEmpty(_status))
        {
            var status = Text(_status!, PanelTypography.Small, scale, Brushes.Gold);
            status.Margin = new Thickness(0, 4 * scale, 0, 0);
            chrome.Add(status);
        }

        TextBlock? note = null;
        if (!string.IsNullOrEmpty(_note))
        {
            note = Text(_note!, PanelTypography.Small, scale, MutedBrush);
            note.Margin = new Thickness(0, 4 * scale, 0, 0);
            chrome.Add(note);
        }

        var pieces = new List<(FrameworkElement Element, Action? FillOvals, CompGuideItemKind Kind, int Group, bool Target, string? GuideId)>();
        var tiers = CompTargets.Tiers(_board, _targets);
        for (var g = 0; g < tiers.Count; g++)
        {
            pieces.Add((TierHeader(tiers[g], scale), null, CompGuideItemKind.TierHeader, g, false, null));
            foreach (var progress in tiers[g].Rows)
            {
                var (element, fill) = Row(progress, scale, height);
                pieces.Add((element, fill, CompGuideItemKind.Row, g, CompTargets.Find(_targets, progress.Guide) != null, progress.Guide.Id));
            }
        }

        // The board's power: chrome, as the warband line was, so that guide lines give way before it does.
        var footer = _footer != null ? BoardPowerView.Build(_footer, scale) : null;

        // Measured in place, so that the canvas's inherited font applies: the heights are the ones drawn.
        foreach (var element in chrome.Concat(pieces.Select(p => p.Element)).Concat(footer != null ? new FrameworkElement[] { footer } : Array.Empty<FrameworkElement>()))
        {
            lines.Children.Add(element);
        }

        lines.Measure(new Size(inner, double.PositiveInfinity));
        var used = chrome.Sum(e => e.DesiredSize.Height) + (footer?.DesiredSize.Height ?? 0);
        var items = pieces.Select(p => new CompGuideFitItem(p.Kind, p.Group, p.Element.DesiredSize.Height, p.Target)).ToList();
        if (note != null && !CompGuideLayout.KeepsOptionalLine(items, room - used + note.DesiredSize.Height, note.DesiredSize.Height))
        {
            chrome.Remove(note); // the note gives way to a target's line, never the reverse
            used -= note.DesiredSize.Height;
        }

        var fit = CompGuideLayout.Fit(items, room - used, moreLineHeight: 0, atLeastOne: true);

        lines.Children.Clear();
        foreach (var element in chrome)
        {
            lines.Children.Add(element);
        }

        var shownHeight = 0.0;
        foreach (var index in fit.Shown)
        {
            pieces[index].FillOvals?.Invoke();
            lines.Children.Add(pieces[index].Element);
            shownHeight += pieces[index].Element.DesiredSize.Height;
            if (pieces[index].GuideId is { } shownId)
            {
                _shownLines[shownId] = pieces[index].Element;
            }
        }

        shownCount.Text = fit.ShowsMoreLine
            ? $"{fit.RowsShown.ToString(CultureInfo.InvariantCulture)} of {fit.RowsTotal.ToString(CultureInfo.InvariantCulture)} shown"
            : string.Empty;
        if (footer != null)
        {
            lines.Children.Add(footer);
        }

        return used + shownHeight;
    }

    /// <summary>
    /// One guide's detail, in place of the list: "← All comp guides" in the title bar, its tick box, name and badges, its
    /// line of Firestone context when it has one, then its sections in HDT's order, as many as fit, a section that does not
    /// fit left out whole (CompGuideLayout.Sections). Returns the height used.
    /// </summary>
    private double LayoutDetail(StackPanel lines, CompGuide guide, double scale, double height, double inner, double room)
    {
        var target = CompTargets.Find(_targets, guide);
        var chrome = new List<FrameworkElement> { TitleBar(scale, detail: true) };
        if (!string.IsNullOrEmpty(_status))
        {
            chrome.Add(Text(_status!, PanelTypography.Small, scale, Brushes.Gold));
        }

        var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 6 * scale, 0, 0) };
        if (SelectionEnabled)
        {
            var box = TickBox(guide.Id, target?.Ticked == true, scale);
            box.Margin = new Thickness(0, 0, 6 * scale, 0);
            DockPanel.SetDock(box, Dock.Left);
            header.Children.Add(box);
        }

        if (target != null)
        {
            var rank = RankBadge(target, scale);
            ((FrameworkElement)rank).Margin = new Thickness(0, 0, 6 * scale, 0);
            ((FrameworkElement)rank).VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(rank, Dock.Left);
            header.Children.Add(rank);
        }

        var badges = GuideView.Badges(guide, scale);
        DockPanel.SetDock(badges, Dock.Right);
        header.Children.Add(badges);
        var name = Text(guide.Name, PanelTypography.CompositionName, scale, target != null ? HexBrush.Of(target.Colour) : Brushes.White, bold: true);
        name.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(name);
        chrome.Add(header);
        if (GuideView.Context(_contextFor(guide), scale) is { } context)
        {
            chrome.Add(context);
        }

        // Hover on an oval of the detail shows the card (beside the panel); a click on it does nothing.
        var sections = GuideView.Sections(guide, _held, _pivotsFor(guide), scale, card => Oval(card, scale, height, onClick: null));
        var more = GuideView.MoreSections(scale);
        foreach (var element in chrome.Concat(sections).Append(more))
        {
            lines.Children.Add(element);
        }

        lines.Measure(new Size(inner, double.PositiveInfinity));
        var used = chrome.Sum(e => e.DesiredSize.Height);
        var fit = CompGuideLayout.Sections(sections.Select(s => s.DesiredSize.Height).ToList(), room - used, more.DesiredSize.Height);

        lines.Children.Clear();
        foreach (var element in chrome)
        {
            lines.Children.Add(element);
        }

        var shownHeight = 0.0;
        foreach (var index in fit.Shown)
        {
            lines.Children.Add(sections[index]);
            shownHeight += sections[index].DesiredSize.Height;
        }

        if (fit.ShowsMoreLine)
        {
            more.Text = GuideView.SectionsShown(fit);
            lines.Children.Add(more);
            shownHeight += more.DesiredSize.Height;
        }

        if (_loggedDetail != guide.Id)
        {
            _loggedDetail = guide.Id;
            _detailShown(guide, fit);
        }

        return used + shownHeight;
    }
}
