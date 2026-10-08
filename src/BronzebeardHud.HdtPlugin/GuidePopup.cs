using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace BronzebeardHud.HdtPlugin;

/// <summary>What the popup needs to draw one guide, asked of the panel when the delay elapses.</summary>
internal sealed class GuidePopupContent
{
    public GuidePopupContent(CompGuide guide, CompTarget? target, ICollection<string> held, IReadOnlyList<GuidePivot>? pivots, string? context,
        LayoutRect panel, IReadOnlyList<LayoutRect> avoid)
        : this(panel, avoid)
    {
        Guide = guide;
        Target = target;
        Held = held;
        Pivots = pivots;
        Context = context;
    }

    /// <summary>The early cards (EarlyCards) in place of a guide: the section at the panel's top, hovered.</summary>
    public GuidePopupContent(IReadOnlyList<EarlyCardsRow> early, int turn, LayoutRect panel, IReadOnlyList<LayoutRect> avoid)
        : this(panel, avoid)
    {
        Early = early;
        Turn = turn;
    }

    private GuidePopupContent(LayoutRect panel, IReadOnlyList<LayoutRect> avoid)
    {
        Held = new HashSet<string>();
        Panel = panel;
        Avoid = avoid;
    }

    /// <summary>The guide to draw; null for the early cards.</summary>
    public CompGuide? Guide { get; }

    public IReadOnlyList<EarlyCardsRow>? Early { get; }

    public int Turn { get; }

    /// <summary>A key card the lobby cannot offer: greyed and struck (CardImages.Unavailable).</summary>
    public Func<string, bool> CannotShowUp { get; set; } = _ => false;
    public CompTarget? Target { get; }
    public ICollection<string> Held { get; }
    public IReadOnlyList<GuidePivot>? Pivots { get; }

    /// <summary>The guide's line of Firestone context (TargetContext), under its name; null: none.</summary>
    public string? Context { get; }

    /// <summary>The panel's rectangle on the canvas: the popup goes above it, or below.</summary>
    public LayoutRect Panel { get; }

    /// <summary>The other panels that show (the Skip combat button): the popup never covers them.</summary>
    public IReadOnlyList<LayoutRect> Avoid { get; }
}

/// <summary>
/// The whole guide of a hovered line of the "Compositions" panel, in a box of its own beside the panel (Ali, 2026-10-04:
/// "the full guide, as in HDT, in a popup on hover"): the name (in the target's colour, else white), the tier and
/// difficulty badges, the line of Firestone context when the guide is bridged to a Firestone comp (GuideView.Context), then
/// the six sections in HDT's order (GuideView.Sections), free of the panel's box. Where it goes:
/// GuidePopupLayout.Place (above the panel, right of the boards; below it when there is no room above; nothing, and one
/// log line per game, when there is room on neither side). What does not fit is left out whole, the last sections first,
/// with "k of n sections".
///
/// Not HDT's overlay tooltip: HDT keeps one tooltip slot for the whole overlay (OverlayWindow.SetTooltip returns while
/// its grid has a child), so a tooltip on a line would have kept the ovals of that line from showing their card. The
/// popup is an element of the canvas (OverlayLayer, under HDT's tooltips), shown on the line's MouseEnter after a delay
/// and hidden on its MouseLeave (GuideHover); the card previews of the ovals keep HDT's tooltip and can show at the same
/// time, never on the popup (GuidePopupLayout keeps their place clear). Nothing in it can be hovered or clicked.
/// </summary>
internal sealed class GuidePopup
{
    /// <summary>Between the cursor entering a line and its popup: running down the list does not flash a popup per line.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(250);

    private const double VerticalPadding = 6;

    private readonly Canvas _canvas;
    private readonly Border _popup;
    private readonly DispatcherTimer _timer;
    private readonly GuideHover _hover = new();
    private readonly Func<string, GuidePopupContent?> _content;
    private readonly Func<bool> _blocked;
    private readonly Func<FrameworkElement, bool> _cursorOver;
    private readonly Action<Action> _run;
    private Dictionary<string, FrameworkElement> _lines = new(StringComparer.Ordinal);
    private bool _noRoomLogged;

    /// <param name="content">What to draw for a guide id; null when the guide is no longer listed.</param>
    /// <param name="blocked">True while no popup may show: move mode, a guide's detail, the panel hidden.</param>
    /// <param name="cursorOver">Whether the cursor is within an element (<see cref="IsCursorOver"/>; the simulation's self-test swaps it).</param>
    /// <param name="run">Runs what an event triggers, under the panel's feature guard (a WPF handler is under none).</param>
    public GuidePopup(Canvas canvas, Func<string, GuidePopupContent?> content, Func<bool> blocked, Func<FrameworkElement, bool> cursorOver, Action<Action> run)
    {
        _canvas = canvas;
        _content = content;
        _blocked = blocked;
        _cursorOver = cursorOver;
        _run = run;
        _popup = new Border
        {
            Background = CompsPanel.PanelBrush,
            BorderBrush = CompsPanel.FrameBrush,
            BorderThickness = new Thickness(PanelFit.Border),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        OverlayLayer.Add(_canvas, _popup);
        _timer = new DispatcherTimer(DispatcherPriority.Normal, canvas.Dispatcher) { Interval = Delay };
        _timer.Tick += (_, _) => _run(OnDelayElapsed);
    }

    /// <summary>The popup on the canvas (the simulation's self-test reads it).</summary>
    public Border Element => _popup;

    public bool IsVisible => _popup.Visibility == Visibility.Visible;

    /// <summary>The guide whose popup shows; null when none does.</summary>
    public string? ShownGuide => IsVisible ? _hover.Shown : null;

    /// <summary>How many times a popup was shown (one per "shown" log line); the self-test counts them.</summary>
    public int Shows { get; private set; }

    /// <summary>
    /// Makes a guide line hoverable: HDT's probe then raises MouseEnter and MouseLeave on it (a purely geometric test of
    /// the line's rectangle, Windows/OverlayWindow.cs UpdateHoverable), and WPF its own over the line's clickable parts.
    /// Both are handled the same way (GuideHover): a second enter changes nothing, a leave with the cursor still on the
    /// line is ignored.
    /// </summary>
    public void Track(FrameworkElement line, string guideId)
    {
        OverlayExtensions.SetIsOverlayHoverVisible(line, true);
        line.MouseEnter += (_, _) => _run(() => Apply(_hover.Enter(guideId, _blocked())));
        line.MouseLeave += (_, _) => _run(() => Apply(_hover.Leave(guideId, _lines.TryGetValue(guideId, out var shown) && _cursorOver(shown))));
    }

    /// <summary>
    /// The panel was redrawn and shows these lines (none in a guide's detail): a hovered guide that left them is hidden;
    /// one still there is drawn again, with what changed (cards held, colours, the panel's place).
    /// </summary>
    public void Listed(IReadOnlyDictionary<string, FrameworkElement> lines)
    {
        _lines = lines.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        Apply(_hover.Listed(lines.Keys));
        if (IsVisible && _hover.Shown is { } shown)
        {
            Draw(shown, log: false);
        }
    }

    /// <summary>Hides the popup and forgets the line (detail opened, move mode, the panel hidden, a phase change).</summary>
    public void Hide()
    {
        _hover.Reset();
        Apply(GuideHoverAction.Hide);
    }

    /// <summary>Out of the game: the next game logs "no room" again, once.</summary>
    public void NewGame() => _noRoomLogged = false;

    public void Detach()
    {
        _timer.Stop();
        _canvas.Children.Remove(_popup);
    }

    private void Apply(GuideHoverAction action)
    {
        switch (action)
        {
            case GuideHoverAction.StartDelay:
                _timer.Stop();
                _popup.Visibility = Visibility.Collapsed;
                _timer.Start();
                break;
            case GuideHoverAction.Hide:
                _timer.Stop();
                _popup.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void OnDelayElapsed()
    {
        _timer.Stop();
        if (_hover.Elapsed(_blocked()) is { } guideId)
        {
            Draw(guideId, log: true);
        }
    }

    /// <summary>
    /// Builds the popup for a guide and measures it in place (the canvas's inherited font applies), asks
    /// GuidePopupLayout for a place, leaves out the last sections that do not fit, and shows it there.
    /// </summary>
    private void Draw(string guideId, bool log)
    {
        // The panel and the Skip combat button at their current size: one may have just been redrawn.
        _canvas.UpdateLayout();
        var content = _content(guideId);
        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;
        if (content == null || width <= 0 || height <= 0)
        {
            _popup.Visibility = Visibility.Collapsed;
            return;
        }

        var scale = TavernLayout.Scale(height);
        var popupWidth = GuidePopupLayout.Width * scale;
        var inner = Math.Max(0, popupWidth - 2 * (PanelFit.Padding * scale + PanelFit.Border));
        var frame = 2 * (VerticalPadding * scale + PanelFit.Border);
        var lines = new StackPanel { Margin = new Thickness(PanelFit.Padding * scale, VerticalPadding * scale, PanelFit.Padding * scale, VerticalPadding * scale), Width = inner };

        var guide = content.Guide;
        // The name and badges, then the line of Firestone context when the guide has one: always shown, never left out.
        var head = new List<FrameworkElement>();
        List<FrameworkElement> sections;
        if (guide != null)
        {
            head.Add(Header(guide, content.Target, scale));
            if (GuideView.Context(content.Context, scale) is { } context)
            {
                head.Add(context);
            }

            sections = GuideView.Sections(guide, content.Held, content.Pivots, scale,
                card =>
                {
                    var oval = CardImages.Vignette(card, content.Held.Contains(card), PanelFit.OvalWidth * scale, scale, 0, placePreview: null, onClick: null, CompsPanel.TierOf(card));
                    return content.CannotShowUp(card) ? CardImages.Unavailable(oval, scale) : oval;
                }).ToList();
        }
        else
        {
            head.Add(GuideView.Text($"Early cards · turn {content.Turn.ToString(CultureInfo.InvariantCulture)}", PanelTypography.CompositionName, scale, Brushes.White, bold: true));
            head.Add(GuideView.Text("placement when played this turn, vs every card played this turn", PanelTypography.Small, scale, GuideView.MutedBrush));
            sections = (content.Early ?? Array.Empty<EarlyCardsRow>()).Select(row => EarlySection(row, scale)).ToList();
        }

        var more = GuideView.MoreSections(scale);
        foreach (var element in head.Concat(sections).Append(more))
        {
            lines.Children.Add(element);
        }

        _popup.Width = popupWidth;
        _popup.BorderBrush = content.Target != null ? HexBrush.Of(content.Target.Colour) : guide == null ? CompsPanel.EarlyBrush : CompsPanel.FrameBrush;
        _popup.Child = lines;
        _popup.Visibility = Visibility.Visible;
        lines.Measure(new Size(inner, double.PositiveInfinity));

        var chrome = head.Sum(e => e.DesiredSize.Height);
        var heights = sections.Select(s => s.DesiredSize.Height).ToList();
        var whole = frame + chrome + heights.Sum();
        var least = frame + chrome + more.DesiredSize.Height + (heights.Count > 0 ? heights.Min() : 0);
        if (GuidePopupLayout.Place(content.Panel, width, height, popupWidth, whole, least, content.Avoid) is not { } room)
        {
            _popup.Visibility = Visibility.Collapsed;
            if (!_noRoomLogged)
            {
                _noRoomLogged = true;
                Log.Info($"Bronzebeard HUD: guide popup: no room (panel at ({content.Panel.Left:0},{content.Panel.Top:0} {content.Panel.Width:0}×{content.Panel.Height:0}), "
                         + $"popup {popupWidth:0}×{whole:0}, at least {least:0} tall)");
            }

            return;
        }

        var fit = CompGuideLayout.Sections(heights, room.Height - frame - chrome, more.DesiredSize.Height);
        lines.Children.Clear();
        foreach (var element in head)
        {
            lines.Children.Add(element);
        }

        var used = frame + chrome;
        foreach (var index in fit.Shown)
        {
            lines.Children.Add(sections[index]);
            used += heights[index];
        }

        if (fit.ShowsMoreLine)
        {
            more.Text = GuideView.SectionsShown(fit);
            lines.Children.Add(more);
            used += more.DesiredSize.Height;
        }

        // Placed again at the height it takes: against the panel, as close to it as the room allows.
        var place = GuidePopupLayout.Place(content.Panel, width, height, popupWidth, used, used, content.Avoid) ?? room;
        Canvas.SetLeft(_popup, place.Left);
        Canvas.SetTop(_popup, place.Top);
        if (log)
        {
            Shows++;
            Log.Info($"Bronzebeard HUD: guide popup {guide?.Name ?? "early cards"} shown at ({place.Left:0},{place.Top:0} {place.Width:0}×{place.Height:0}) "
                     + $"sections={fit.Shown.Count.ToString(CultureInfo.InvariantCulture)}/{fit.Total.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    /// <summary>One tier of the early cards: "TIER 3 · NEXT", then one line per card, its oval, its name and its figures.</summary>
    private static FrameworkElement EarlySection(EarlyCardsRow row, double scale)
    {
        var inv = CultureInfo.InvariantCulture;
        var section = new StackPanel { Margin = new Thickness(0, 6 * scale, 0, 0) };
        section.Children.Add(GuideView.Text($"TIER {row.Tier.ToString(inv)}{(row.IsNext ? " · NEXT" : string.Empty)}", PanelTypography.Small, scale, CompsPanel.EarlyBrush, bold: true));
        foreach (var card in row.Cards)
        {
            var line = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2 * scale, 0, 0) };
            var oval = CardImages.Vignette(card.CardId, false, PanelFit.OvalWidth * scale, scale, 0, placePreview: null, onClick: null, CompsPanel.TierOf(card.CardId));
            DockPanel.SetDock(oval, Dock.Left);
            line.Children.Add(oval);
            var figures = GuideView.Text($"{card.Note.Placement.ToString("0.0", inv)} vs {card.Note.TurnAverage.ToString("0.0", inv)} ({card.Note.Played.ToString(inv)})",
                PanelTypography.Small, scale, GuideView.MutedBrush);
            figures.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(figures, Dock.Right);
            line.Children.Add(figures);
            var name = GuideView.Text(Database.GetCardFromId(card.CardId)?.LocalizedName ?? card.CardId, PanelTypography.Body, scale, Brushes.White, bold: true);
            name.VerticalAlignment = VerticalAlignment.Center;
            name.Margin = new Thickness(6 * scale, 0, 6 * scale, 0);
            line.Children.Add(name);
            section.Children.Add(line);
        }

        return section;
    }

    /// <summary>The guide's name, in its target's colour (else white), then its tier and difficulty badges on the right.</summary>
    private static FrameworkElement Header(CompGuide guide, CompTarget? target, double scale)
    {
        var header = new DockPanel { LastChildFill = true };
        var badges = GuideView.Badges(guide, scale);
        DockPanel.SetDock(badges, Dock.Right);
        header.Children.Add(badges);
        var name = GuideView.Text(guide.Name, PanelTypography.CompositionName, scale, target != null ? HexBrush.Of(target.Colour) : Brushes.White, bold: true);
        name.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(name);
        return header;
    }

    /// <summary>
    /// Whether the cursor is within <paramref name="element"/>'s rectangle, as HDT's probe tests it (screen position,
    /// GetCursorPos, brought into the element): false when the element is not on screen.
    /// </summary>
    public static bool IsCursorOver(FrameworkElement element)
    {
        try
        {
            if (!element.IsVisible || PresentationSource.FromVisual(element) == null || !GetCursorPos(out var cursor))
            {
                return false;
            }

            var local = element.PointFromScreen(new Point(cursor.X, cursor.Y));
            return local.X >= 0 && local.Y >= 0 && local.X < element.ActualWidth && local.Y < element.ActualHeight;
        }
        catch (InvalidOperationException)
        {
            return false; // no longer connected to a window
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
}
