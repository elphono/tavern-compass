using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Where the popup of a guide goes when its line of the "Compositions" panel is hovered (Ali, 2026-10-04: "the whole
/// guide, as in HDT, in a popup on hover"). Above the panel first, in the free column right of the boards at the
/// default place (at 1080p: x ≥ 1443, from the top margin down to the panel), its right edge on the panel's when nothing
/// is in the way and pushed aside just enough when something is; below the panel when there is no room above (the panel
/// was moved to the top); nothing when there is room on neither side. It never covers a zone of the game (the boards,
/// the hero, the leaderboard: <see cref="GameZones"/>), the panel, what it is told to avoid (the Skip combat button), nor
/// the place a card preview of the panel can take (<see cref="PreviewArea"/>): both show at once when an oval of the
/// hovered line is hovered too.
/// </summary>
public static class GuidePopupLayout
{
    /// <summary>The popup's width, in design pixels (× TavernLayout.Scale): six ovals of the detail in a line, and the padding.</summary>
    public const double Width = 400;

    /// <summary>Space kept free along the overlay's edges, × H.</summary>
    public const double Margin = 0.01;

    /// <summary>Space between the popup and the panel, or anything it keeps clear of, × H.</summary>
    public const double Gap = 0.008;

    /// <summary>
    /// The parts of the game the popup never covers, from HDT's constants (HearthSim/Hearthstone-Deck-Tracker, master 509bb0b),
    /// the same as the tests' NoGoZones (a test holds them equal): the boards, from the opponent (tavern) row top,
    /// H/2 − 0.158 H − 0.045 H (Windows/OverlayWindow.Update.cs:534-535, MouseOverDetection.cs:38), to the player row
    /// bottom, over seven shop card slots of 138 units; the native leaderboard column, tiles of 0.69 H / 8 from 0.15 H at
    /// the left of the 4:3 frame (MouseOverDetection.cs:48, 114) and the MMR labels right of them; the player's hero and
    /// hero power, W/2 ± 0.2 H below the board.
    /// </summary>
    public static IReadOnlyList<LayoutRect> GameZones(double width, double height)
    {
        var s = TavernLayout.Scale(height);
        var boardHalfWidth = 3.5 * TavernLayout.ShopCardWidth * s;
        var boardTop = height / 2 - TavernLayout.BoardRowHeight * height - 0.045 * height;
        var boardBottom = TavernLayout.PlayerRowBottom(height);
        var frameLeft = (width - height * 4 / 3) / 2;
        var tile = LeaderboardLayout.TileSize * height;
        var tilesTop = LeaderboardLayout.Top * height;
        return new[]
        {
            Edges(width / 2 - boardHalfWidth, boardTop, width / 2 + boardHalfWidth, boardBottom),
            Edges(frameLeft, tilesTop, frameLeft + tile + 0.1 * height, tilesTop + 8 * tile),
            Edges(width / 2 - 0.2 * height, boardBottom, width / 2 + 0.2 * height, height),
        };
    }

    /// <summary>
    /// Every place a card preview of the panel can take (TavernLayout.PreviewRect, for any vignette inside the panel). The
    /// side it goes depends on the panel alone, and along that side it follows the vignette, kept inside the window: the
    /// previews of vignettes at the panel's two opposite corners bound all the others.
    /// </summary>
    public static LayoutRect PreviewArea(LayoutRect panel, double width, double height)
    {
        var first = TavernLayout.PreviewRect(panel, new LayoutRect(panel.Left, panel.Top, 0, 0), width, height).Rect;
        var last = TavernLayout.PreviewRect(panel, new LayoutRect(panel.Right, panel.Top + panel.Height, 0, 0), width, height).Rect;
        return Edges(Math.Min(first.Left, last.Left), Math.Min(first.Top, last.Top),
            Math.Max(first.Right, last.Right), Math.Max(first.Top + first.Height, last.Top + last.Height));
    }

    /// <summary>
    /// The popup's rectangle, <paramref name="popupWidth"/> wide and as tall as <paramref name="popupHeight"/> or as the
    /// room it gets, never under <paramref name="minHeight"/> (the least it can show: its header, one section and "k of n
    /// sections"); null when no place gives that much. Above the panel first, then below. On a side, the place that gives
    /// the most height wins; then the one nearest the panel; then the one whose right edge is nearest the panel's.
    /// </summary>
    /// <param name="avoid">Other elements the popup must not cover: the Skip combat button, when it shows.</param>
    public static LayoutRect? Place(LayoutRect panel, double width, double height, double popupWidth, double popupHeight, double minHeight = 0,
        IReadOnlyList<LayoutRect>? avoid = null)
    {
        var margin = Margin * height;
        var gap = Gap * height;
        if (width <= 0 || height <= 0 || popupWidth <= 0 || popupHeight <= 0 || popupWidth > width - 2 * margin)
        {
            return null;
        }

        var least = Math.Max(0, Math.Min(minHeight, popupHeight));
        var obstacles = GameZones(width, height)
            .Concat(avoid ?? Array.Empty<LayoutRect>())
            .Append(PreviewArea(panel, width, height))
            .Where(o => o.Width > 0 && o.Height > 0)
            .Select(o => (Left: o.Left - gap, Top: o.Top - gap, Right: o.Right + gap, Bottom: o.Top + o.Height + gap))
            .ToList();

        // The right edge on the panel's, or against an obstacle's side: the only places where what is in the way changes.
        var aligned = panel.Right - popupWidth;
        var lowest = margin;
        var highest = width - margin - popupWidth;
        var xs = new[] { aligned, lowest, highest }
            .Concat(obstacles.Select(o => o.Right))
            .Concat(obstacles.Select(o => o.Left - popupWidth))
            .Select(x => Math.Max(lowest, Math.Min(highest, x)))
            .Distinct()
            .ToList();

        foreach (var above in new[] { true, false })
        {
            var spanTop = above ? margin : panel.Top + panel.Height + gap;
            var spanBottom = above ? panel.Top - gap : height - margin;
            (double Height, double Distance, double Shift, LayoutRect Rect)? best = null;
            foreach (var x in xs)
            {
                var blocked = obstacles.Where(o => o.Left < x + popupWidth && x < o.Right).Select(o => (o.Top, o.Bottom));
                foreach (var (top, bottom) in Free(spanTop, spanBottom, blocked))
                {
                    var h = Math.Min(popupHeight, bottom - top);
                    if (h < least - Tolerance)
                    {
                        continue;
                    }

                    // Against the panel's side of the free interval: as close to the panel as the interval allows.
                    var rectTop = above ? bottom - h : top;
                    var distance = above ? panel.Top - bottom : top - (panel.Top + panel.Height);
                    var candidate = (h, distance, Math.Abs(x - aligned), new LayoutRect(x + popupWidth / 2, rectTop + h / 2, popupWidth, h));
                    if (best is not { } b || Better(candidate, b))
                    {
                        best = candidate;
                    }
                }
            }

            if (best is { } found)
            {
                return found.Rect;
            }
        }

        return null;
    }

    private const double Tolerance = 1e-6;

    private static bool Better((double Height, double Distance, double Shift, LayoutRect Rect) a, (double Height, double Distance, double Shift, LayoutRect Rect) b)
    {
        if (Math.Abs(a.Height - b.Height) > Tolerance)
        {
            return a.Height > b.Height;
        }

        if (Math.Abs(a.Distance - b.Distance) > Tolerance)
        {
            return a.Distance < b.Distance;
        }

        return a.Shift < b.Shift - Tolerance;
    }

    /// <summary>The parts of [<paramref name="top"/>, <paramref name="bottom"/>] that no blocked range covers, top to bottom.</summary>
    private static IEnumerable<(double Top, double Bottom)> Free(double top, double bottom, IEnumerable<(double Top, double Bottom)> blocked)
    {
        var cursor = top;
        foreach (var (from, to) in blocked.OrderBy(b => b.Top))
        {
            if (to <= cursor)
            {
                continue;
            }

            if (from >= bottom)
            {
                break;
            }

            if (from > cursor)
            {
                yield return (cursor, from);
            }

            cursor = to;
            if (cursor >= bottom)
            {
                yield break;
            }
        }

        if (cursor < bottom)
        {
            yield return (cursor, bottom);
        }
    }

    private static LayoutRect Edges(double left, double top, double right, double bottom) =>
        new((left + right) / 2, (top + bottom) / 2, right - left, bottom - top);
}

/// <summary>What the panel does after a hover event: nothing, (re)start the delay before the popup, or hide the popup.</summary>
public enum GuideHoverAction
{
    None,

    /// <summary>Hide the popup if one shows, and start the delay again: when it elapses, <see cref="GuideHover.Elapsed"/> says which guide to show.</summary>
    StartDelay,

    /// <summary>Stop the delay and hide the popup.</summary>
    Hide,
}

/// <summary>
/// When the popup of a hovered guide line is asked, shown and hidden. HDT's probe (≈ 60 Hz, purely geometric) raises
/// MouseEnter and MouseLeave on a line declared hoverable; WPF raises its own as well while the cursor is over a clickable
/// element of the overlay, and a MouseLeave of its own when HDT makes its window click-through again, the cursor still on
/// the line. Both go through the same calls: a second "enter" on the line asked or shown changes nothing, and a "leave"
/// while the cursor is still on the line is ignored. A delay (the panel's timer) runs between "enter" and the popup, so
/// that running the cursor down the list does not flash a popup per line.
/// </summary>
public sealed class GuideHover
{
    private string? _asked;

    /// <summary>The guide whose popup shows; null when none does.</summary>
    public string? Shown { get; private set; }

    /// <param name="blocked">True in move mode, in a guide's detail, or with the panel hidden: no popup then.</param>
    public GuideHoverAction Enter(string guideId, bool blocked)
    {
        if (blocked || _asked == guideId)
        {
            return GuideHoverAction.None;
        }

        _asked = guideId;
        Shown = null;
        return GuideHoverAction.StartDelay;
    }

    /// <param name="cursorStillOver">Whether the cursor is still within the line (WPF's leave as HDT's window turns click-through).</param>
    public GuideHoverAction Leave(string guideId, bool cursorStillOver)
    {
        if (_asked != guideId || cursorStillOver)
        {
            return GuideHoverAction.None;
        }

        Reset();
        return GuideHoverAction.Hide;
    }

    /// <summary>The delay elapsed: the guide to show now, or null (nothing asked, already shown, or blocked meanwhile).</summary>
    public string? Elapsed(bool blocked)
    {
        if (_asked == null || Shown != null || blocked)
        {
            return null;
        }

        Shown = _asked;
        return Shown;
    }

    /// <summary>The panel was redrawn with these lines: a hovered guide no longer among them is forgotten and hidden.</summary>
    public GuideHoverAction Listed(IEnumerable<string> guideIds)
    {
        if (_asked == null || guideIds.Contains(_asked))
        {
            return GuideHoverAction.None;
        }

        Reset();
        return GuideHoverAction.Hide;
    }

    /// <summary>Forgets the line (detail opened, move mode, panel hidden): the next "enter" asks again.</summary>
    public void Reset()
    {
        _asked = null;
        Shown = null;
    }
}
