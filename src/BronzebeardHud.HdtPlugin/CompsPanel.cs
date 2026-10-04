using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Extensions;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// The single "Compositions" panel (Ali, 2026-10-04: the two panels of before said the same thing twice, and HDT's way of
/// showing a guide reads better): HDT's own comp guides, grouped by tier as HDT groups them, the targets first in their tier
/// (CompTargets.Tiers), each guide on one line — its tick box, its name, its core cards as ovals (held: green ring and
/// tick). A target carries its colour (CompTargetTracker.Palette): a 3 px bar on the left, a tint, its rank in a round
/// badge, its name in bold in that colour; the frames on Bob's cards and the labels of choices use the same colour. A click
/// on a name or an oval replaces the list by that guide's detail, as HDT does: "← All comp guides", the name with its tier
/// and difficulty badges, then HOW TO PLAY, CORE CARDS, ADDON CARDS, WHEN TO COMMIT, COMMON ENABLERS and PIVOTS.
///
/// Movable and resizable ("target-compositions", the id the panel always had: a place Ali saved stays valid), in the shop
/// and in combat. Nothing is shrunk: every piece is built and measured in place, then what fits is shown (CompGuideLayout:
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

    private static readonly Brush PanelBrush = new SolidColorBrush(Color.FromArgb(0xEB, 0x14, 0x14, 0x1E));
    private static readonly Brush FrameBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x48, 0x0F));
    private static readonly Brush ButtonBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x44));
    private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xCD, 0xD8));
    private static readonly Brush RuleBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
    private static readonly Brush PillBrush = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));

    // One colour per kind of card, the same in every section title: core (solid frame in the tavern), add-on, enabler, pivot.
    private static readonly Brush CoreBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x3F));
    private static readonly Brush AddonBrush = new SolidColorBrush(Color.FromRgb(0x7C, 0xE3, 0x8B));
    private static readonly Brush EnablerBrush = new SolidColorBrush(Color.FromRgb(0x5C, 0xE1, 0xFF));
    private static readonly Brush PivotBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x43));

    private readonly Canvas _canvas;
    private readonly PanelMover _mover;
    private readonly Border _panel;
    private readonly Action<string> _toggle;
    private readonly Func<int> _count;
    private readonly Action<int> _changeCount;
    private readonly Action _openMeta;
    private readonly Func<CompGuide, IReadOnlyList<GuidePivot>?> _pivotsFor;
    private readonly Action<CompGuide, SectionFit> _detailShown;
    private readonly Action<Action> _run;
    private readonly TargetPanelView _view = new();

    private CompGuideBoard _board = CompGuideBoard.Empty;
    private IReadOnlyList<CompTarget> _targets = Array.Empty<CompTarget>();
    private HashSet<string> _held = new(StringComparer.Ordinal);
    private string? _source;
    private string? _status;
    private string? _footer;
    private string? _loggedDetail;

    /// <param name="toggle">Called with a guide id (CompGuide.Id) when its tick box is clicked.</param>
    /// <param name="count">How many targets are wanted (settings.json), shown between − and +.</param>
    /// <param name="changeCount">Called with −1 or +1 when − or + is clicked.</param>
    /// <param name="pivotsFor">A guide's pivots (GuidePivots), under their own guard; null when that feature failed.</param>
    /// <param name="detailShown">Called once each time a guide's detail is opened, with what of it fits (the log line).</param>
    /// <param name="run">Runs what a click or a resize triggers, under the panel's feature guard (a WPF handler is under none).</param>
    public CompsPanel(Canvas canvas, PanelMover mover, Action<string> toggle, Func<int> count, Action<int> changeCount, Action openMeta,
        Func<CompGuide, IReadOnlyList<GuidePivot>?> pivotsFor, Action<CompGuide, SectionFit> detailShown, Action<Action> run)
    {
        _canvas = canvas;
        _mover = mover;
        _toggle = toggle;
        _count = count;
        _changeCount = changeCount;
        _openMeta = openMeta;
        _pivotsFor = pivotsFor;
        _detailShown = detailShown;
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
    }

    public bool IsVisible { get; private set; }

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

    /// <param name="board">HDT's guides ranked against the player's board and hand (CompGuideMatch.Rank).</param>
    /// <param name="targets">The targets and their colours (CompTargetTracker.Next).</param>
    /// <param name="held">Base card ids of the player's board and hand: the green rings.</param>
    /// <param name="source">CompGuideSources.HdtFree or HdtTier7; null when HDT shows no guides.</param>
    /// <param name="status">Why there are no guides (HDT's state), or null.</param>
    public void Show(CompGuideBoard board, IReadOnlyList<CompTarget> targets, IEnumerable<string> held, string? source, string? status)
    {
        _board = board;
        _targets = targets;
        _held = new HashSet<string>(held, StringComparer.Ordinal);
        _source = source;
        _status = status;
        IsVisible = true;
        Relayout();
    }

    /// <summary>A last line under the list (the warband against its curve); null for none.</summary>
    public void SetFooter(string? footer)
    {
        if (footer == _footer)
        {
            return;
        }

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
    }

    public void Detach()
    {
        _canvas.SizeChanged -= OnCanvasSizeChanged;
        _canvas.Children.Remove(_panel);
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

    private static TextBlock Text(string text, double size, double scale, Brush brush, bool bold = false) => new()
    {
        Text = text,
        FontSize = size * scale,
        Foreground = brush,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>One line of a guide's text, card names in bold as HDT draws them (CompGuideText).</summary>
    private static TextBlock Runs(IReadOnlyList<CompGuideTextRun> runs, double size, double scale, Brush brush)
    {
        var text = new TextBlock { FontSize = size * scale, Foreground = brush, TextWrapping = TextWrapping.Wrap };
        foreach (var run in runs)
        {
            text.Inlines.Add(new Run(run.Text) { FontWeight = run.IsCard ? FontWeights.Bold : FontWeights.Normal });
        }

        return text;
    }

    /// <summary>A card's tavern tier for its badge; null when HDT knows none.</summary>
    private static int? TierOf(string cardId) => Database.GetCardFromId(cardId)?.TechLevel is > 0 and var tier ? tier : null;

    /// <summary>HDT's tier colours: BattlegroundsCompGuideViewModel.TierColor, a left-to-right gradient.</summary>
    private static Brush TierBrush(int tier)
    {
        var (from, to) = tier switch
        {
            1 => (Color.FromRgb(64, 138, 191), Color.FromRgb(56, 95, 122)),
            2 => (Color.FromRgb(107, 160, 54), Color.FromRgb(88, 121, 55)),
            3 => (Color.FromRgb(146, 160, 54), Color.FromRgb(104, 121, 55)),
            4 => (Color.FromRgb(160, 124, 54), Color.FromRgb(121, 95, 55)),
            5 => (Color.FromRgb(160, 72, 54), Color.FromRgb(121, 66, 55)),
            _ => (Color.FromRgb(112, 112, 112), Color.FromRgb(64, 64, 64)),
        };
        return new LinearGradientBrush(from, to, new Point(0, 0.5), new Point(1, 0.5));
    }

    /// <summary>A tier's bar, as in HDT's Tier 7 list: the letter on the tier's gradient.</summary>
    private static FrameworkElement TierHeader(CompGuideBoardTier tier, double scale) => new Border
    {
        Background = TierBrush(tier.Tier),
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
        var nameCell = new Border { Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(0, 0, 6 * scale, 0), Child = name };
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

        // Ticked guides are the targets, alone: the number of automatic targets no longer applies, so − and + are dim and
        // the title counts what was chosen.
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
        if (!IsVisible || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            _panel.Visibility = Visibility.Collapsed;
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

        var pieces = new List<(FrameworkElement Element, Action? FillOvals, CompGuideItemKind Kind, int Group, bool Target)>();
        var tiers = CompTargets.Tiers(_board, _targets);
        for (var g = 0; g < tiers.Count; g++)
        {
            pieces.Add((TierHeader(tiers[g], scale), null, CompGuideItemKind.TierHeader, g, false));
            foreach (var progress in tiers[g].Rows)
            {
                var (element, fill) = Row(progress, scale, height);
                pieces.Add((element, fill, CompGuideItemKind.Row, g, CompTargets.Find(_targets, progress.Guide) != null));
            }
        }

        TextBlock? footer = null;
        if (!string.IsNullOrEmpty(_footer))
        {
            footer = Text(_footer!, PanelTypography.Small, scale, Brushes.White, bold: true);
            footer.Margin = new Thickness(0, 4 * scale, 0, 0);
        }

        // Measured in place, so that the canvas's inherited font applies: the heights are the ones drawn.
        foreach (var element in chrome.Concat(pieces.Select(p => p.Element)).Concat(footer != null ? new FrameworkElement[] { footer } : Array.Empty<FrameworkElement>()))
        {
            lines.Children.Add(element);
        }

        lines.Measure(new Size(inner, double.PositiveInfinity));
        var used = chrome.Sum(e => e.DesiredSize.Height) + (footer?.DesiredSize.Height ?? 0);
        var fit = CompGuideLayout.Fit(
            pieces.Select(p => new CompGuideFitItem(p.Kind, p.Group, p.Element.DesiredSize.Height, p.Target)).ToList(),
            room - used, moreLineHeight: 0, atLeastOne: true);

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

    /// <summary>A section's title in its colour, then a short hint in grey, on one line that wraps.</summary>
    private static TextBlock SectionTitle(string title, string? hint, Brush colour, double scale)
    {
        var text = new TextBlock { FontSize = PanelTypography.Small * scale, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6 * scale, 0, 0) };
        text.Inlines.Add(new Run(title.ToUpperInvariant()) { Foreground = colour, FontWeight = FontWeights.Bold });
        if (!string.IsNullOrEmpty(hint))
        {
            text.Inlines.Add(new Run("  " + hint) { Foreground = MutedBrush });
        }

        return text;
    }

    private static FrameworkElement Section(string title, string? hint, Brush colour, double scale, FrameworkElement content)
    {
        var section = new StackPanel();
        section.Children.Add(SectionTitle(title, hint, colour, scale));
        section.Children.Add(content);
        return section;
    }

    /// <summary>Cards as ovals, <see cref="PanelFit.CoreOvalsPerRow"/> to a line, as many lines as needed; hover shows the card.</summary>
    private FrameworkElement OvalLines(IReadOnlyList<string> cards, double scale, double height)
    {
        var block = new StackPanel();
        for (var i = 0; i < cards.Count; i += PanelFit.CoreOvalsPerRow)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4 * scale, 0, 0) };
            foreach (var card in cards.Skip(i).Take(PanelFit.CoreOvalsPerRow))
            {
                line.Children.Add(Oval(card, scale, height, onClick: null));
            }

            block.Children.Add(line);
        }

        return block;
    }

    /// <summary>A guide's tier (its letter on HDT's gradient) and difficulty (HDT's colours), right of its name.</summary>
    private static FrameworkElement Badges(CompGuide guide, double scale)
    {
        var badges = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        badges.Children.Add(new Border
        {
            Background = TierBrush(guide.Tier),
            CornerRadius = new CornerRadius(3 * scale),
            Padding = new Thickness(6 * scale, 0, 6 * scale, 1 * scale),
            Margin = new Thickness(6 * scale, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Text(guide.TierLetter, PanelTypography.PanelTitle, scale, Brushes.White, bold: true),
        });
        if (guide.Difficulty is >= 1 and <= 3)
        {
            var label = Text(CompGuideDifficulty.Text(guide.Difficulty), PanelTypography.Small, scale, Brushes.White, bold: true);
            label.TextWrapping = TextWrapping.NoWrap;
            badges.Children.Add(new Border
            {
                Background = HexBrush.Of(CompGuideDifficulty.Colour(guide.Difficulty)),
                CornerRadius = new CornerRadius(3 * scale),
                Padding = new Thickness(6 * scale, 2 * scale, 6 * scale, 2 * scale),
                Margin = new Thickness(4 * scale, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = label,
            });
        }

        return badges;
    }

    /// <summary>
    /// One guide's detail, in place of the list: "← All comp guides" in the title bar, its tick box, name and badges, then its sections in
    /// HDT's order, as many as fit, a section that does not fit left out whole (CompGuideLayout.Sections). Returns the
    /// height used.
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

        var badges = Badges(guide, scale);
        DockPanel.SetDock(badges, Dock.Right);
        header.Children.Add(badges);
        var name = Text(guide.Name, PanelTypography.CompositionName, scale, target != null ? HexBrush.Of(target.Colour) : Brushes.White, bold: true);
        name.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(name);
        chrome.Add(header);

        var sections = new List<FrameworkElement>();
        if (guide.HowToPlayFirstLine.Count > 0)
        {
            sections.Add(Section("How to play", null, Brushes.White, scale, Runs(guide.HowToPlayFirstLine, PanelTypography.Body, scale, Brushes.White)));
        }

        var coreHeld = guide.CoreCards.Count(_held.Contains);
        sections.Add(Section("Core cards", $"{coreHeld}/{guide.CoreCards.Count} held · solid frame in the tavern", CoreBrush, scale, OvalLines(guide.CoreCards, scale, height)));
        if (guide.AddonCards.Count > 0)
        {
            sections.Add(Section("Addon cards", "dotted frame", AddonBrush, scale, OvalLines(guide.AddonCards, scale, height)));
        }

        if (guide.WhenToCommitLines.Count > 0)
        {
            var pills = new StackPanel();
            foreach (var line in guide.WhenToCommitLines)
            {
                pills.Children.Add(new Border
                {
                    Background = PillBrush,
                    BorderBrush = CoreBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(9 * scale),
                    Padding = new Thickness(8 * scale, 2 * scale, 8 * scale, 3 * scale),
                    Margin = new Thickness(0, 3 * scale, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = Runs(line, PanelTypography.Small, scale, Brushes.White),
                });
            }

            sections.Add(Section("When to commit", null, CoreBrush, scale, pills));
        }

        if (guide.Enablers.Count > 0)
        {
            sections.Add(Section("Common enablers", "dotted frame", EnablerBrush, scale, OvalLines(guide.Enablers, scale, height)));
        }

        if (_pivotsFor(guide) is { Count: > 0 } pivots)
        {
            var block = new StackPanel();
            foreach (var pivot in pivots)
            {
                var label = new TextBlock { FontSize = PanelTypography.Small * scale, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2 * scale, 0, 0) };
                label.Inlines.Add(new Run("→ ") { Foreground = PivotBrush, FontWeight = FontWeights.Bold });
                label.Inlines.Add(new Run(pivot.To.Name) { Foreground = Brushes.White, FontWeight = FontWeights.Bold });
                label.Inlines.Add(new Run($" · {pivot.Shared.Count.ToString(CultureInfo.InvariantCulture)} shared") { Foreground = MutedBrush });
                block.Children.Add(label);
                block.Children.Add(OvalLines(pivot.Shared, scale, height));
            }

            sections.Add(Section("Pivots", "≈ guides sharing core or add-on cards", PivotBrush, scale, block));
        }

        var more = Text("9 of 9 sections", PanelTypography.Small, scale, MutedBrush);
        more.Margin = new Thickness(0, 6 * scale, 0, 0);
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
            more.Text = $"{fit.Shown.Count.ToString(CultureInfo.InvariantCulture)} of {fit.Total.ToString(CultureInfo.InvariantCulture)} sections";
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
