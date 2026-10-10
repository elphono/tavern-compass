using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.Hearthstone;

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
/// Under the frame, the power inset (Ali, 2026-10-06: "the composition strength indicator out into a small inset under the
/// main frame, framed by the + and − of the other feature"): the player's board against their hero's average, the opponent's
/// against theirs (BoardPowerView, one row each, each under its own feature guard), between − on the left and + on the right.
///
/// Movable and resizable ("target-compositions", the id the panel always had: a place Ali saved stays valid), frame and inset
/// together, from the hero selection on, in the shop and in combat, and on the screen while a choice is open (Ali, 2026-10-08: always visible). Its height
/// (Ali, 2026-10-06: "with Move panels we set the window's default size and place; a press on + or − resizes the window to
/// show the N best compositions"): a panel never resized by its handle is always sized to its content, N guide lines (− n +,
/// or more ticked guides: CompTargets.FitRows) and the inset; a panel resized by its handle keeps that box — its
/// default size — until + or − is pressed in a game, or as many guides are ticked as are wanted (Ali, 2026-10-10: the panel
/// then lists them alone, CompTargets.Listed), then is sized to its content for the rest of the game. Sized to its
/// content, it keeps the top of its box, or its bottom when the box is against the bottom, and grows up when there is no room
/// below (PanelGrowth), never onto the game's zones. In move mode it shows its box, the one the handle edits.
/// Nothing is shrunk: every piece is built and measured in place, then what fits is shown (CompGuideLayout:
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

    /// <summary>The early cards' colour: teal, apart from the four target colours and the gold of the status line.</summary>
    internal static readonly Brush EarlyBrush = new SolidColorBrush(Color.FromRgb(0x22, 0xD3, 0xB6));

    /// <summary>The early cards' section, tracked for the popup like a guide line (no guide id can be this).</summary>
    internal const string EarlyKey = "@early";
    private static readonly Brush ButtonBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x44));
    private static readonly Brush MutedBrush = GuideView.MutedBrush;
    private static readonly Brush RuleBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));

    private readonly Canvas _canvas;
    private readonly PanelMover _mover;

    // The panel PanelMover moves: the frame with the list, and the power inset under it. Its own box has no background and no
    // border; the frame wears the panel's background and its orange (cyan in move mode) border.
    private readonly Border _panel;
    private readonly Border _frame;
    private readonly Border _inset;
    private readonly Action<string> _toggle;
    private readonly Func<int> _count;
    private readonly Action<int> _changeCount;
    private readonly Action _openMeta;
    private readonly Func<CompGuide, IReadOnlyList<GuidePivot>?> _pivotsFor;
    private readonly Action<CompGuide, SectionFit> _detailShown;
    private readonly Func<CompGuide, string?> _contextFor;
    private readonly Action<Action> _run;
    private readonly Action<Action> _powerRun;
    private readonly Action<Action> _opponentRun;
    private readonly Action<string> _log;
    private readonly TargetPanelView _view = new();
    private readonly GuidePopup _popup;
    private Dictionary<string, FrameworkElement> _shownLines = new(StringComparer.Ordinal);

    private CompGuideBoard _board = CompGuideBoard.Empty;
    private IReadOnlyList<CompTarget> _targets = Array.Empty<CompTarget>();
    private HashSet<string> _held = new(StringComparer.Ordinal);
    private string? _source;
    private string? _status;
    private string? _note;
    private WarbandComparison? _power;
    private string? _powerKey;
    private WarbandComparison? _opponent;
    private string? _opponentKey;
    private string? _loggedDetail;
    private string? _loggedInset;

    // + or − was pressed in this game (outside move mode): a panel resized by its handle is sized to its content from then on,
    // until the next game or a switch of move mode. A panel never resized always is — "Reset panel positions" makes it one.
    private bool _fitted;
    private bool _onlyTicked; // the last Show listed the ticked guides alone (CompTargets.OnlyTicked)

    // A press on + or − asks for one log line once the panel is resized: at the next redraw that comes with the targets the
    // new number gives (Show), or at once when the number did not change (already 1 or 4).
    private ResizeLog _resizeLog;

    private enum ResizeLog
    {
        None,
        AfterTargets,
        Now,
    }

    // What the last redraw gave, for the log line of a resize: where the panel went, which edge it kept, the lines shown.
    private PanelSpan? _span;
    private CompGuideFit? _listFit;
    private int _fitRows;

    private bool _inRelayout;
    private bool _relayoutAgain;

    /// <param name="toggle">Called with a guide id (CompGuide.Id) when its tick box is clicked.</param>
    /// <param name="count">How many targets are wanted (settings.json), shown in the title, set by − and + in the inset.</param>
    /// <param name="changeCount">Called with −1 or +1 when − or + is clicked.</param>
    /// <param name="pivotsFor">A guide's pivots (GuidePivots), under their own guard; null when that feature failed.</param>
    /// <param name="detailShown">Called once each time a guide's detail is opened, with what of it fits (the log line).</param>
    /// <param name="contextFor">
    /// A guide's line of Firestone context (TargetContext.For, on the bridge and the hero being played), under its header in
    /// the detail and the popup; null: no line.
    /// </param>
    /// <param name="run">Runs what a click or a resize triggers, under the panel's feature guard (a WPF handler is under none).</param>
    /// <param name="powerRun">Draws the player's row of the inset, under its own guard: a row that throws is left out alone.</param>
    /// <param name="opponentRun">Draws the opponent's row of the inset, under its own guard.</param>
    /// <param name="log">Writes a line in HDT's log: a resize by + / −, a change of what the inset shows.</param>
    /// <param name="cursorOver">
    /// Whether the cursor is within an element, for the guide popup's MouseLeave (GuidePopup.IsCursorOver when null; the
    /// simulation's self-test swaps it).
    /// </param>
    public CompsPanel(Canvas canvas, PanelMover mover, Action<string> toggle, Func<int> count, Action<int> changeCount, Action openMeta,
        Func<CompGuide, IReadOnlyList<GuidePivot>?> pivotsFor, Action<CompGuide, SectionFit> detailShown, Func<CompGuide, string?> contextFor,
        Action<Action> run, Action<Action> powerRun, Action<Action> opponentRun, Action<string> log, Func<FrameworkElement, bool>? cursorOver = null)
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
        _powerRun = powerRun;
        _opponentRun = opponentRun;
        _log = log;
        _frame = new Border
        {
            Background = PanelBrush,
            BorderBrush = FrameBrush,
            BorderThickness = new Thickness(PanelFit.Border),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
        };
        _inset = new Border
        {
            Background = InsetBrush,
            BorderBrush = FrameBrush,
            BorderThickness = new Thickness(PanelFit.InsetBorder),
            CornerRadius = new CornerRadius(6),
        };
        var stack = new StackPanel();
        stack.Children.Add(_frame);
        stack.Children.Add(_inset);
        _panel = new Border
        {
            Background = Brushes.Transparent, // the gap between the frame and the inset catches a drag in move mode
            Child = stack,
            Visibility = Visibility.Collapsed,
        };
        OverlayLayer.Add(_canvas, _panel);
        _canvas.SizeChanged += OnCanvasSizeChanged;
        _popup = new GuidePopup(canvas, PopupContent, () => _mover.MoveMode || _view.ShowsDetail || !IsVisible, cursorOver ?? GuidePopup.IsCursorOver, run);
        _mover.MoveModeChanged += OnMoveModeChanged;
    }

    /// <summary>The inset's background: the panel's, a shade lighter at the top, so that it reads as a piece of its own.</summary>
    private static readonly Brush InsetBrush = new LinearGradientBrush(Color.FromArgb(0xF2, 0x24, 0x24, 0x32), Color.FromArgb(0xF2, 0x10, 0x10, 0x18), 90);

    /// <summary>Shown by the plugin (hero selection, shop, combat).</summary>
    public bool IsVisible { get; private set; }

    /// <summary>The popup of a hovered guide line (the simulation's self-test drives it).</summary>
    public GuidePopup Popup => _popup;

    /// <summary>The guide lines the list shows, by guide id: what hover is tracked on (none in a guide's detail).</summary>
    public IReadOnlyDictionary<string, FrameworkElement> ShownLines => _shownLines;

    /// <summary>Hides the guide popup: the shop phase ended or began (Plugin.UpdateComps).</summary>
    public void HideGuidePopup() => _popup.Hide();

    /// <summary>The panel on the canvas (the simulation's self-test reads its texts).</summary>
    public Border Element => _panel;

    /// <summary>
    /// What the frame draws now (its title and list, or a detail): a new element at every redraw, so that the self-test can
    /// tell a panel put back as it was from one rebuilt. The panel's own child never changes (the frame and the inset).
    /// </summary>
    public UIElement Content => _frame.Child;

    /// <summary>True while a guide's detail stands in place of the list.</summary>
    public bool ShowsDetail => _view.ShowsDetail;

    /// <summary>The bracket button of the title bar ("top 25%"): the bracket shown, and what a click does; no button while null.</summary>
    public (string Label, Action Next)? Bracket { get; set; }

    /// <summary>
    /// Whether a key card cannot show up in this game's lobby (LobbyGuides.CannotShowUp): left out of a guide's line, greyed
    /// and struck in its detail and popup (Ali, 2026-10-08, a quilboar among Menagerie's key cards in a lobby without them).
    /// </summary>
    public Func<string, bool> CannotShowUp { get; set; } = _ => false;

    private IReadOnlyList<EarlyCardsRow> _early = Array.Empty<EarlyCardsRow>();
    private int _earlyTurn;
    private string _earlyKey = string.Empty;

    /// <summary>
    /// The best cards of the player's tavern tier and of the next one at this turn (EarlyCards), in a section under the title
    /// bar, early in the game; none: no section. Hovered, the section shows them with their figures in the popup.
    /// </summary>
    public void SetEarly(IReadOnlyList<EarlyCardsRow> rows, int turn)
    {
        var key = EarlyCards.LogLine(turn, 0, rows);
        if (key == _earlyKey)
        {
            return;
        }

        _earlyKey = key;
        _early = rows;
        _earlyTurn = turn;
        RelayoutIfShown();
    }

    /// <summary>"EARLY · turn 3", then one line per tier: "T2", "T3 next", and its cards' ovals (hover: the card).</summary>
    private FrameworkElement EarlySection(double scale, double height)
    {
        var section = new StackPanel { Margin = new Thickness(0, 4 * scale, 0, 0), Background = Brushes.Transparent };
        section.Children.Add(Text($"EARLY · turn {_earlyTurn.ToString(CultureInfo.InvariantCulture)}", PanelTypography.Small, scale, EarlyBrush, bold: true));
        foreach (var row in _early)
        {
            var line = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2 * scale, 0, 0) };
            var label = Text($"T{row.Tier.ToString(CultureInfo.InvariantCulture)}{(row.IsNext ? " next" : string.Empty)}", PanelTypography.Body, scale, EarlyBrush, bold: true);
            label.Width = 64 * scale;
            label.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(label, Dock.Left);
            line.Children.Add(label);
            var ovals = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (var card in row.Cards)
            {
                ovals.Children.Add(Oval(card.CardId, scale, height, onClick: null));
            }

            line.Children.Add(ovals);
            section.Children.Add(line);
        }

        _popup.Track(section, EarlyKey);
        return section;
    }

    private FrameworkElement KeyOval(string cardId, double scale, double height) =>
        CannotShowUp(cardId) ? CardImages.Unavailable(Oval(cardId, scale, height, onClick: null), scale) : Oval(cardId, scale, height, onClick: null);

    /// <summary>When false, no "Meta ↗" button (the meta-snapshot feature was switched off by its guard).</summary>
    public bool MetaEnabled { get; set; } = true;

    /// <summary>When false, a click on a guide opens nothing (the comp-detail feature was switched off by its guard).</summary>
    public bool DetailEnabled { get; set; } = true;

    /// <summary>When false, no tick boxes (the comp-selection feature was switched off by its guard).</summary>
    public bool SelectionEnabled { get; set; } = true;

    /// <param name="board">
    /// The lobby's guides ranked against the player's board and hand (CompTargets.Round: LobbyGuides.Playable); listed whole,
    /// or the ticked guides alone when as many are ticked as are wanted (CompTargets.Listed).
    /// </param>
    /// <param name="targets">The targets and their colours (CompTargetTracker.Next).</param>
    /// <param name="held">Base card ids of the player's board and hand: the green rings.</param>
    /// <param name="source">CompGuideSources.HdtFree or HdtTier7; null when HDT shows no guides.</param>
    /// <param name="status">Why there are no guides (HDT's state), or null.</param>
    /// <param name="note">A muted line under the title ("Lobby tribes unknown: every guide listed"), or null.</param>
    public void Show(CompGuideBoard board, IReadOnlyList<CompTarget> targets, IEnumerable<string> held, string? source, string? status, string? note = null)
    {
        _board = CompTargets.Listed(board, targets, _count());
        _targets = targets;
        var onlyTicked = CompTargets.OnlyTicked(targets, _count());
        if (onlyTicked && !_onlyTicked && !_mover.MoveMode)
        {
            _fitted = true; // as on + or −: the panel shrinks to the ticked guides, whatever box the handle gave it
            _log($"Bronzebeard HUD: ticked guides fill the {_count()} compositions wanted: only they are listed");
        }

        _onlyTicked = onlyTicked;
        _held = new HashSet<string>(held, StringComparer.Ordinal);
        _source = source;
        _status = status;
        _note = note;
        IsVisible = true;
        if (_resizeLog == ResizeLog.AfterTargets)
        {
            _resizeLog = ResizeLog.Now; // the targets of the new number: this redraw is the resize + or − asked for
        }

        Relayout();
    }

    /// <summary>The player's row of the inset: the board against their hero's average (WarbandCurve.Compare); null for none.</summary>
    public void SetPower(WarbandComparison? power)
    {
        var key = power == null ? null : power.Line + "|" + power.PowerText;
        if (key == _powerKey)
        {
            return;
        }

        _powerKey = key;
        _power = power;
        RelayoutIfShown();
    }

    /// <summary>The opponent's row of the inset: their board against their hero's average (OpponentPower.Compare); null for none.</summary>
    public void SetOpponentPower(WarbandComparison? opponent)
    {
        var key = opponent == null ? null : opponent.Line + "|" + opponent.PowerText;
        if (key == _opponentKey)
        {
            return;
        }

        _opponentKey = key;
        _opponent = opponent;
        RelayoutIfShown();
    }

    /// <summary>
    /// Redraws when shown; asked during a redraw (a row's guard switching it off from inside it), once that redraw is over:
    /// never a redraw within a redraw.
    /// </summary>
    private void RelayoutIfShown()
    {
        if (_inRelayout)
        {
            _relayoutAgain = true;
        }
        else if (IsVisible)
        {
            Relayout();
        }
    }

    public void Hide()
    {
        IsVisible = false;
        _view.Back(); // out of the game: the next one starts on the list
        _loggedDetail = null;
        _fitted = false; // the next game starts in the box Move panels gave
        _resizeLog = ResizeLog.None;
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

    /// <summary>
    /// Move mode shows the box Move panels edits: a panel sized by + / − goes back to it, and stays there once move mode is
    /// off, until + or − is pressed again.
    /// </summary>
    private void OnMoveModeChanged() => _run(() =>
    {
        _popup.Hide();
        _fitted = false;
        RelayoutIfShown();
    });

    /// <summary>
    /// − or + in the inset: one target less or more (settings.json), and, outside move mode, the panel sized to its content
    /// from now on in this game: the N lines of the new number (PanelGrowth). One log line once it is resized.
    /// </summary>
    private void OnCountClicked(int step)
    {
        var before = _count();
        var fit = !_mover.MoveMode;
        _fitted |= fit;
        _resizeLog = fit ? ResizeLog.AfterTargets : ResizeLog.None;
        _changeCount(step); // the simulation redraws right away (Show, which logs the resize), the plugin at its next update
        if (_resizeLog == ResizeLog.AfterTargets && _count() == before)
        {
            _resizeLog = ResizeLog.Now; // already 1 or 4: no new targets will come, the panel is sized at once
        }

        RelayoutIfShown();
    }

    /// <summary>What the popup draws for a guide of the list, and where the panel and the other panels are; null when it left the list.</summary>
    private GuidePopupContent? PopupContent(string guideId)
    {
        var guide = _board.All.Select(p => p.Guide).FirstOrDefault(g => g.Id == guideId);
        var left = Canvas.GetLeft(_panel);
        var top = Canvas.GetTop(_panel);
        var early = guideId == EarlyKey && _early.Count > 0;
        if ((guide == null && !early) || !IsVisible || double.IsNaN(left) || double.IsNaN(top) || _panel.ActualWidth <= 0)
        {
            return null;
        }

        var panel = new LayoutRect(left + _panel.ActualWidth / 2, top + _panel.ActualHeight / 2, _panel.ActualWidth, _panel.ActualHeight);
        if (guide == null)
        {
            return new GuidePopupContent(_early, _earlyTurn, panel, _mover.VisiblePanels(except: _panel));
        }

        return new GuidePopupContent(guide, CompTargets.Find(_targets, guide), _held, _pivotsFor(guide), _contextFor(guide), panel, _mover.VisiblePanels(except: _panel))
        {
            CannotShowUp = CannotShowUp,
        };
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
        OverlayClickable.Declare(button);
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
        OverlayClickable.Declare(box);
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
            OverlayClickable.Declare(nameCell);
        }

        Grid.SetColumn(nameCell, 1);
        grid.Children.Add(nameCell);

        var ovals = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(ovals, 2);
        grid.Children.Add(ovals);
        void FillOvals()
        {
            var possible = guide.CoreCards.Where(card => !CannotShowUp(card)).ToList();
            var (shown, more) = PanelFit.ListOvals(possible.Count);
            foreach (var card in possible.Take(shown))
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
    /// then the source, the MMR bracket (a click: the next one), Meta ↗ and "n targets" (− and + are in the inset).
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

        if (Bracket is { } bracket)
        {
            var button = PanelButton(bracket.Label, scale, bracket.Next);
            button.Margin = new Thickness(0, 0, 8 * scale, 0);
            right.Children.Add(button);
        }

        if (MetaEnabled)
        {
            var meta = PanelButton("Meta ↗", scale, _openMeta);
            meta.Margin = new Thickness(0, 0, 8 * scale, 0);
            right.Children.Add(meta);
        }

        // How many compositions: − and + set it, in the inset under the frame, ticked ones included (CompTargets.Choose). With
        // a guide ticked the title says how many of them are chosen: "2/3 chosen"; as many as wanted, the list holds them alone.
        var chosen = _targets.Count(t => t.Ticked);
        var n = _count();
        var count = Text(chosen > 0
                ? $"{chosen.ToString(CultureInfo.InvariantCulture)}/{Math.Max(n, chosen).ToString(CultureInfo.InvariantCulture)} chosen"
                : $"{n.ToString(CultureInfo.InvariantCulture)} target{(n == 1 ? string.Empty : "s")}",
            PanelTypography.Small, scale, MutedBrush);
        count.TextWrapping = TextWrapping.NoWrap;
        count.VerticalAlignment = VerticalAlignment.Center;
        right.Children.Add(count);
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
    /// The power inset: the player's row and the opponent's (BoardPowerView), each drawn under its own guard, between − on the
    /// left and + on the right; − dim and without effect once the number wanted is the ticked guides' (CompTargets.CanDecrease). Returns which
    /// rows were drawn (a row switched off by its guard is not).
    /// </summary>
    private (bool Own, bool Opponent) BuildInset(double scale)
    {
        var rows = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        bool AddRow(Action<Action> run, WarbandComparison? comparison, string tags)
        {
            var drawn = false;
            if (comparison != null)
            {
                run(() =>
                {
                    var row = BoardPowerView.Build(comparison, scale, tags);
                    row.Margin = new Thickness(0, rows.Children.Count > 0 ? PanelFit.InsetRowGap * scale : 0, 0, 0);
                    rows.Children.Add(row);
                    drawn = true;
                });
            }

            return drawn;
        }

        var own = AddRow(_powerRun, _power, BoardPowerView.Own);
        var opponent = AddRow(_opponentRun, _opponent, BoardPowerView.Opponent);

        var minus = PanelButton("−", scale, () => OnCountClicked(-1), enabled: CompTargets.CanDecrease(_targets, _count()));
        var plus = PanelButton("+", scale, () => OnCountClicked(+1), enabled: true);
        minus.Tag = MinusTag;
        plus.Tag = PlusTag;
        minus.Margin = new Thickness(0, 0, 6 * scale, 0);
        plus.Margin = new Thickness(6 * scale, 0, 0, 0);
        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(minus, Dock.Left);
        DockPanel.SetDock(plus, Dock.Right);
        dock.Children.Add(minus);
        dock.Children.Add(plus);
        dock.Children.Add(rows);
        _inset.Padding = new Thickness(4 * scale, PanelFit.InsetPadding * scale, 4 * scale, PanelFit.InsetPadding * scale);
        _inset.Margin = new Thickness(0, PanelFit.InsetGap * scale, 0, 0);
        _inset.Child = dock;
        return (own, opponent);
    }

    /// <summary>Tags of the inset's − and + (the simulation's self-test finds them by them).</summary>
    public const string MinusTag = "count-minus";
    public const string PlusTag = "count-plus";

    /// <summary>The power inset under the frame (the simulation's self-test reads it).</summary>
    public Border Inset => _inset;

    /// <summary>The frame with the title and the list or a detail, above the inset (the simulation's self-test reads it).</summary>
    public Border Frame => _frame;

    /// <summary>True while the panel is sized to its content (a panel never resized, or + / − pressed in this game).</summary>
    public bool FitsContent => !_mover.MoveMode && (!_mover.IsResized(PanelId) || _fitted);

    /// <summary>The span the panel was given by the last redraw sized to its content; null otherwise.</summary>
    public PanelSpan? Span => _span;

    /// <summary>
    /// The frame with the title and either the list or one guide's detail, then the inset, built and measured in place. Its
    /// height: sized to its content (<see cref="FitsContent"/>: N guide lines, or the whole detail) between the zones of the
    /// game and the gold (PanelGrowth); otherwise the box the player gave it, or the default one down to the gold.
    /// </summary>
    private void Relayout()
    {
        if (_inRelayout)
        {
            _relayoutAgain = true;
            return;
        }

        if (!IsVisible || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            _panel.Visibility = Visibility.Collapsed;
            _popup.Hide();
            return;
        }

        _inRelayout = true;
        try
        {
            var passes = 0;
            do
            {
                _relayoutAgain = false;
                LayoutOnce();
            }
            while (_relayoutAgain && ++passes < 3);
        }
        finally
        {
            _inRelayout = false;
        }
    }

    private void LayoutOnce()
    {
        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;
        var scale = TavernLayout.Scale(height);
        var rect = TavernLayout.TargetPanel(width, height);
        var placed = _mover.Place(_panel, PanelId, rect, interactive: true,
            new PanelResize(PanelFit.TargetMinWidth * scale, PanelFit.TargetMinHeight * scale, () => _run(Relayout)), frame: _frame);
        var resized = _mover.IsResized(PanelId);
        var boxLeft = Canvas.GetLeft(_panel);
        var boxTop = Canvas.GetTop(_panel);
        _panel.Width = placed.Width;
        _panel.Visibility = Visibility.Visible;

        // The inset first: the frame has what it leaves.
        var (ownDrawn, opponentDrawn) = BuildInset(scale);
        _inset.Measure(new Size(placed.Width, double.PositiveInfinity));
        var insetBlock = _inset.DesiredSize.Height; // its gap above it included

        // Across, the frame at its thickest, 3 px in move mode (PanelMover): computed with the 2 px of the normal frame, the
        // content was 2 px wider than the room in move mode and cut on the right (found by the simulation's self-test). Six
        // ovals still fit: the last one's 4 px gap on the right is what gives way. Upright, the frame as drawn now: a switch
        // of move mode redraws the panel (OnMoveModeChanged), and in the room between the boards and the gold, three targets
        // in two tiers need the 2 px the thicker frame would take.
        var inner = Math.Max(0, placed.Width - 2 * (PanelFit.Padding * scale + Math.Max(PanelFit.Border, 3)));
        var chrome = 2 * (VerticalPadding * scale + (_mover.MoveMode ? 3 : PanelFit.Border));
        var lines = new StackPanel { Margin = new Thickness(PanelFit.Padding * scale, VerticalPadding * scale, PanelFit.Padding * scale, VerticalPadding * scale), Width = inner };
        _frame.Child = lines;

        var fit = FitsContent;
        var boxHeight = resized ? placed.Height : rect.Height;
        var fixedRoom = resized ? placed.Height : Math.Max(rect.Height, PanelFit.BottomLimit * height - boxTop);
        _span = null;

        // The room the frame's content gets once it says what it would like (N lines, or the whole detail): sized to it, the
        // panel is placed by PanelGrowth from the box Move panels gives, never from where the last redraw put it.
        double RoomFor(double content)
        {
            if (!fit)
            {
                return fixedRoom - insetBlock - chrome;
            }

            var box = new LayoutRect(boxLeft + placed.Width / 2, boxTop + boxHeight / 2, placed.Width, boxHeight);
            var span = PanelGrowth.Place(box, content + chrome + insetBlock, PanelFit.TargetMinHeight * scale, height,
                PanelGrowth.Obstacles(width, height, _mover.VisiblePanels(except: _panel)));
            _span = span;
            Canvas.SetTop(_panel, span.Top);
            return span.Height - insetBlock - chrome;
        }

        var guide = _view.DetailId is { } id ? _board.All.Select(p => p.Guide).FirstOrDefault(g => g.Id == id) : null;
        double needed;
        _shownLines = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
        _fitRows = CompTargets.FitRows(_targets, _count());
        _listFit = null;
        if (guide != null && DetailEnabled)
        {
            needed = LayoutDetail(lines, guide, scale, height, inner, RoomFor) + chrome;
        }
        else
        {
            _view.Back(); // the guide left HDT's list, or the detail was switched off
            _loggedDetail = null;
            needed = LayoutList(lines, scale, height, inner, RoomFor, fit ? _fitRows : null) + chrome;
        }

        // Never less than what is shown: a list always shows one line, even in a box too small for it. Sized to its content,
        // the frame is what it shows: less than its span when a line did not fit (a target left out takes no other guide's
        // place), and a panel that kept its bottom keeps it then too. Otherwise never smaller than the default box.
        var frameRoom = (_span is { } given ? given.Height : fixedRoom) - insetBlock;
        _frame.MinHeight = fit ? 0 : Math.Max(0, (resized ? Math.Min(rect.Height, placed.Height) : rect.Height) - insetBlock);
        _frame.MaxHeight = Math.Max(frameRoom, needed);

        var frameHeight = Math.Max(_frame.MinHeight, Math.Min(_frame.MaxHeight, needed));
        if (_span is { } span && frameHeight + insetBlock < span.Height)
        {
            var top0 = span.Anchor == PanelAnchor.Bottom ? span.Bottom - (frameHeight + insetBlock) : span.Top;
            Canvas.SetTop(_panel, top0);
            _span = new PanelSpan(top0, frameHeight + insetBlock, span.Anchor);
        }

        var top = Canvas.GetTop(_panel);
        var insetHeight = insetBlock - PanelFit.InsetGap * scale;
        var inset = new LayoutRect(boxLeft + placed.Width / 2, top + frameHeight + PanelFit.InsetGap * scale + insetHeight / 2, placed.Width, insetHeight);
        var insetLine = PowerInset.Line(inset, ownDrawn ? _power : null, opponentDrawn ? _opponent : null);
        if (insetLine != _loggedInset)
        {
            _loggedInset = insetLine;
            _log(insetLine);
        }

        if (_resizeLog == ResizeLog.Now)
        {
            _resizeLog = ResizeLog.None;
            var panel = new LayoutRect(boxLeft + placed.Width / 2, top + (frameHeight + insetBlock) / 2, placed.Width, frameHeight + insetBlock);
            _log(PanelGrowth.ResizeLine(_count(), panel, _span?.Anchor ?? PanelAnchor.Top, _listFit?.RowsShown ?? 0, _fitRows, _listFit?.RowsTotal ?? 0));
        }

        // Last, the panel being complete: the popup of a hovered line follows the redraw (no line in a detail: it hides).
        _popup.Listed(_shownLines);
    }

    /// <summary>
    /// The list, as much of it as fits in the room <paramref name="roomFor"/> gives once it knows what the list would like:
    /// <paramref name="fitRows"/> guide lines (the panel sized to them), or as many as fit in a fixed box (null). Returns
    /// the height used.
    /// </summary>
    private double LayoutList(StackPanel lines, double scale, double height, double inner, Func<double, double> roomFor, int? fitRows)
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

        if (_early.Count > 0)
        {
            var early = EarlySection(scale, height);
            chrome.Add(early);
            _shownLines[EarlyKey] = early;
        }

        var pieces = new List<(FrameworkElement Element, Action? FillOvals, CompGuideItemKind Kind, int Group, int Rank, string? GuideId)>();
        var tiers = CompTargets.Tiers(_board, _targets);
        for (var g = 0; g < tiers.Count; g++)
        {
            pieces.Add((TierHeader(tiers[g], scale), null, CompGuideItemKind.TierHeader, g, 0, null));
            foreach (var progress in tiers[g].Rows)
            {
                var (element, fill) = Row(progress, scale, height);
                pieces.Add((element, fill, CompGuideItemKind.Row, g, CompTargets.Find(_targets, progress.Guide)?.Rank ?? 0, progress.Guide.Id));
            }
        }

        // Measured in place, so that the canvas's inherited font applies: the heights are the ones drawn.
        foreach (var element in chrome.Concat(pieces.Select(p => p.Element)))
        {
            lines.Children.Add(element);
        }

        lines.Measure(new Size(inner, double.PositiveInfinity));
        var used = chrome.Sum(e => e.DesiredSize.Height);
        // A target is highlighted with its rank: when the targets do not all fit, the best stay (CompGuideLayout.Fit).
        var items = pieces.Select(p => new CompGuideFitItem(p.Kind, p.Group, p.Element.DesiredSize.Height, p.Rank > 0, p.Rank)).ToList();

        // Sized for N lines: the chrome and exactly the pieces Fit shows for N lines (CompGuideLayout.HeightFor).
        var room = roomFor(fitRows is { } rows ? used + CompGuideLayout.HeightFor(items, rows) : double.PositiveInfinity);
        if (note != null && !CompGuideLayout.KeepsOptionalLine(items, room - used + note.DesiredSize.Height, note.DesiredSize.Height))
        {
            chrome.Remove(note); // the note gives way to a target's line, never the reverse
            used -= note.DesiredSize.Height;
        }

        var fit = CompGuideLayout.Fit(items, room - used, moreLineHeight: 0, atLeastOne: true);
        _listFit = fit;

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

        return used + shownHeight;
    }

    /// <summary>
    /// One guide's detail, in place of the list: "← All comp guides" in the title bar, its tick box, name and badges, its
    /// line of Firestone context when it has one, then its sections in HDT's order, as many as fit in the room
    /// <paramref name="roomFor"/> gives for the whole detail, a section that does not fit left out whole
    /// (CompGuideLayout.Sections). Returns the height used.
    /// </summary>
    private double LayoutDetail(StackPanel lines, CompGuide guide, double scale, double height, double inner, Func<double, double> roomFor)
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
        var sections = GuideView.Sections(guide, _held, _pivotsFor(guide), scale, card => KeyOval(card, scale, height));
        var more = GuideView.MoreSections(scale);
        foreach (var element in chrome.Concat(sections).Append(more))
        {
            lines.Children.Add(element);
        }

        lines.Measure(new Size(inner, double.PositiveInfinity));
        var used = chrome.Sum(e => e.DesiredSize.Height);
        var heights = sections.Select(s => s.DesiredSize.Height).ToList();
        var room = roomFor(used + heights.Sum()); // sized to its content, the panel takes what the whole detail asks for
        var fit = CompGuideLayout.Sections(heights, room - used, more.DesiredSize.Height);

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
