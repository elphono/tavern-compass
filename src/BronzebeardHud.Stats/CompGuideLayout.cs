using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>A piece of the comp guides panel, for <see cref="CompGuideLayout.Fit"/>.</summary>
public enum CompGuideItemKind
{
    TierHeader,
    Row,
}

/// <summary>One piece of the comp guides panel, in display order, with its measured height (overlay pixels).</summary>
public readonly struct CompGuideFitItem
{
    public CompGuideFitItem(CompGuideItemKind kind, int group, double height, bool highlighted = false)
    {
        Kind = kind;
        Group = group;
        Height = height;
        Highlighted = highlighted;
    }

    public CompGuideItemKind Kind { get; }

    /// <summary>The tier the piece belongs to: a header and its rows share it.</summary>
    public int Group { get; }

    public double Height { get; }
    public bool Highlighted { get; }
}

/// <summary>Which pieces of the panel are shown (indexes in display order), and whether a "k of n shown" line goes under them.</summary>
public sealed class CompGuideFit
{
    public CompGuideFit(IReadOnlyList<int> shown, int rowsShown, int rowsTotal)
    {
        Shown = shown;
        RowsShown = rowsShown;
        RowsTotal = rowsTotal;
    }

    public IReadOnlyList<int> Shown { get; }
    public int RowsShown { get; }
    public int RowsTotal { get; }
    public bool ShowsMoreLine => RowsShown < RowsTotal;
}

/// <summary>Which sections of a guide's detail are shown (indexes, in order), and whether "k of n sections" goes under them.</summary>
public sealed class SectionFit
{
    public SectionFit(IReadOnlyList<int> shown, int total)
    {
        Shown = shown;
        Total = total;
    }

    public IReadOnlyList<int> Shown { get; }
    public int Total { get; }
    public bool ShowsMoreLine => Shown.Count < Total;
}

/// <summary>
/// Where the comp guides panel goes by default, and how much of it fits. Nothing is shrunk: what does not fit is
/// left out, and the panel says how many guides it shows.
/// </summary>
public static class CompGuideLayout
{
    // No panel id any more: the comp guides are shown in the target panel ("target-compositions") since 2026-10-04, and
    // PanelLayout ignores a "comp-guides" entry. DefaultPanel and the minimum stay for whoever lays a guide list out alone.

    /// <summary>Space kept between the panel and its neighbours, × H.</summary>
    public const double Gap = 0.01;

    /// <summary>
    /// Smallest box of the panel when resized (design pixels, PanelResize): wide enough for its title and the
    /// "Tier 7" tag on one line; tall enough for the title, one tier's bar and one guide line. A smaller box would show
    /// nothing; in a box this small, Fit shows what fits and says how many guides are left out.
    /// </summary>
    public const double MinWidth = 200;
    public const double MinHeight = 2 * PanelFit.Border + 2 * PanelFit.Padding + 28 + 30 + 20;

    /// <summary>
    /// The default place: the lower left part of Hearthstone's 4:3 frame, mirror of the target composition panel
    /// (TavernLayout.TargetPanel) on the other side of the hero. Right of the leaderboard and of the opponents' MMR
    /// labels (LeaderboardLayout.MmrLabel), left of the hero and hero power (within W/2 ± 0.2 × H), below the player's
    /// board row, down to PanelFit.BottomLimit. Clear of Bob's cards and their buttons, the target composition panel
    /// and the Skip combat button, which all sit right of the hero or above the board's bottom.
    /// About 0.26 × H wide at any window ratio. HDT's own session widget defaults to the window's left edge at 15 %
    /// of its height (Config.SessionRecapLeft = 0, SessionRecapTop = 15), which is why the panel does not use the
    /// margin left of the frame.
    /// </summary>
    public static LayoutRect DefaultPanel(double width, double height)
    {
        var left = Enumerable.Range(1, 8).Max(place => LeaderboardLayout.MmrLabel(width, height, place).Right) + Gap * height;
        var right = width / 2 - 0.2 * height - Gap * height;
        var top = TavernLayout.PlayerRowBottom(height) + 0.012 * height;
        var bottom = PanelFit.BottomLimit * height;
        return new LayoutRect((left + right) / 2, (top + bottom) / 2, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    /// <summary>
    /// Which pieces fit in <paramref name="room"/> (overlay pixels). The highlighted guides first, in display order, so
    /// that the most probable ones never give way to the others; then the other guides in display order, until one
    /// does not fit. A tier's header shows when at least one of its guides does. When some guides are left out, a line
    /// <paramref name="moreLineHeight"/> tall is kept for "k of n shown". The result keeps the display order.
    /// </summary>
    /// <param name="atLeastOne">
    /// When no row fits at all, the first highlighted row (else the first row) is shown anyway, with its tier's header:
    /// a panel that always shows one line grows to hold it rather than show none.
    /// </param>
    public static CompGuideFit Fit(IReadOnlyList<CompGuideFitItem> items, double room, double moreLineHeight, bool atLeastOne = false)
    {
        var rows = items.Count(i => i.Kind == CompGuideItemKind.Row);
        var all = Try(items, room);
        if (all.Count(i => items[i].Kind == CompGuideItemKind.Row) == rows)
        {
            return new CompGuideFit(all, rows, rows);
        }

        var some = Try(items, room - moreLineHeight);
        var shown = some.Count(i => items[i].Kind == CompGuideItemKind.Row);
        if (shown == 0 && atLeastOne && rows > 0)
        {
            var first = Enumerable.Range(0, items.Count).Where(i => items[i].Kind == CompGuideItemKind.Row)
                .OrderBy(i => items[i].Highlighted ? 0 : 1)
                .ThenBy(i => i)
                .First();
            var header = Enumerable.Range(0, first).Where(i => items[i].Kind == CompGuideItemKind.TierHeader && items[i].Group == items[first].Group).ToList();
            some = header.Take(1).Append(first).ToList();
            shown = 1;
        }

        return new CompGuideFit(some, shown, rows);
    }

    /// <summary>
    /// Which sections of a guide's detail fit in <paramref name="room"/> (overlay pixels), each measured in place: all of
    /// them when they fit; otherwise, with a line <paramref name="moreLineHeight"/> tall kept for "k of n sections", each
    /// section in order when it fits in what is left, a section that does not being left out whole (never cut). The first
    /// sections are served first; a later, shorter one may still take the room a longer one left.
    /// </summary>
    public static SectionFit Sections(IReadOnlyList<double> heights, double room, double moreLineHeight)
    {
        if (heights.Sum() <= room + Tolerance)
        {
            return new SectionFit(Enumerable.Range(0, heights.Count).ToList(), heights.Count);
        }

        var shown = new List<int>();
        var used = 0.0;
        for (var i = 0; i < heights.Count; i++)
        {
            if (used + heights[i] <= room - moreLineHeight + Tolerance)
            {
                used += heights[i];
                shown.Add(i);
            }
        }

        return new SectionFit(shown, heights.Count);
    }

    /// <summary>Overlay pixels of slack: a box dragged to exactly the height of its content holds it.</summary>
    private const double Tolerance = 1e-6;

    private static IReadOnlyList<int> Try(IReadOnlyList<CompGuideFitItem> items, double room)
    {
        var headers = new Dictionary<int, int>();
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Kind == CompGuideItemKind.TierHeader && !headers.ContainsKey(items[i].Group))
            {
                headers[items[i].Group] = i;
            }
        }

        var accepted = new HashSet<int>();
        var used = 0.0;
        bool Take(int i)
        {
            var group = items[i].Group;
            var header = headers.TryGetValue(group, out var h) && !accepted.Contains(h) ? h : -1;
            var need = items[i].Height + (header >= 0 ? items[header].Height : 0);
            if (used + need > room)
            {
                return false;
            }

            used += need;
            accepted.Add(i);
            if (header >= 0)
            {
                accepted.Add(header);
            }

            return true;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Kind == CompGuideItemKind.Row && items[i].Highlighted)
            {
                Take(i);
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Kind == CompGuideItemKind.Row && !items[i].Highlighted && !Take(i))
            {
                break;
            }
        }

        return accepted.OrderBy(i => i).ToList();
    }
}
