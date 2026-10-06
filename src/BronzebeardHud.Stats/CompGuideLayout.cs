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
    public CompGuideFitItem(CompGuideItemKind kind, int group, double height, bool highlighted = false, int rank = 0)
    {
        Kind = kind;
        Group = group;
        Height = height;
        Highlighted = highlighted;
        Rank = rank;
    }

    public CompGuideItemKind Kind { get; }

    /// <summary>The tier the piece belongs to: a header and its rows share it.</summary>
    public int Group { get; }

    public double Height { get; }
    public bool Highlighted { get; }

    /// <summary>A highlighted line's rank (a target's: 1 the most probable); highlighted lines are taken in rank order. 0: none.</summary>
    public int Rank { get; }
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
/// How much of the guide list, or of a guide's detail, fits in the "Compositions" panel. Nothing is shrunk: what does
/// not fit is left out, and the panel says how many guides (or sections) it shows. The panel's place and size are
/// the target panel's (TavernLayout.TargetPanel, PanelFit); since 2026-10-04 there is no separate comp guides panel,
/// and PanelLayout ignores a "comp-guides" entry.
/// </summary>
public static class CompGuideLayout
{
    /// <summary>
    /// Which pieces fit in <paramref name="room"/> (overlay pixels). The highlighted guides first, by rank (then in display
    /// order), so that the most probable ones never give way to the others; then, if every highlighted guide fitted, the other
    /// guides in display order, until one does not fit. A tier's header shows when at least one of its guides does. When some
    /// guides are left out, a line <paramref name="moreLineHeight"/> tall is kept for "k of n shown". The result keeps the
    /// display order.
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
    /// The height of the pieces <see cref="Fit"/> shows for <paramref name="rows"/> guide lines: the highlighted lines first, in
    /// display order, then the others in display order, up to <paramref name="rows"/> lines, each tier's header once, with the
    /// first of its lines taken. A room of exactly this height makes Fit show those very lines (the panel sized for N lines,
    /// Ali, 2026-10-06: "+ or − resizes the window to show the N best compositions"). 0 for no line; every piece when
    /// <paramref name="rows"/> is more than there are lines.
    /// </summary>
    public static double HeightFor(IReadOnlyList<CompGuideFitItem> items, int rows)
    {
        var headers = Headers(items);
        var counted = new HashSet<int>();
        var used = 0.0;
        var taken = 0;

        // The order Try takes them in, and its very sums (a line with its header added first), so that Fit, given this
        // room, finds the same total to the last bit.
        var order = HighlightedByRank(items)
            .Concat(Enumerable.Range(0, items.Count).Where(i => items[i].Kind == CompGuideItemKind.Row && !items[i].Highlighted));
        foreach (var i in order)
        {
            if (taken >= rows)
            {
                break;
            }

            var header = headers.TryGetValue(items[i].Group, out var h) && counted.Add(h) ? h : -1;
            used += items[i].Height + (header >= 0 ? items[header].Height : 0);
            taken++;
        }

        return used;
    }

    /// <summary>
    /// The highlighted lines, by rank (a target's: the best first), then in the list's order: when they do not all fit, the
    /// best stay, whatever their tier (a + that left out the third target for the fourth would make a better one disappear).
    /// </summary>
    private static IEnumerable<int> HighlightedByRank(IReadOnlyList<CompGuideFitItem> items) =>
        Enumerable.Range(0, items.Count)
            .Where(i => items[i].Kind == CompGuideItemKind.Row && items[i].Highlighted)
            .OrderBy(i => items[i].Rank)
            .ThenBy(i => i);

    /// <summary>Each tier's header, by group: the first header piece of the group.</summary>
    private static Dictionary<int, int> Headers(IReadOnlyList<CompGuideFitItem> items)
    {
        var headers = new Dictionary<int, int>();
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Kind == CompGuideItemKind.TierHeader && !headers.ContainsKey(items[i].Group))
            {
                headers[items[i].Group] = i;
            }
        }

        return headers;
    }

    /// <summary>
    /// Whether an optional line <paramref name="lineHeight"/> tall (the note "Lobby tribes unknown: every guide listed") is
    /// drawn above the list: only when it costs no target its line. Seen in the simulation on 2026-10-06: at the default
    /// size, the note pushed the third target out of the list; a target is what the player acts on, the note only explains.
    /// </summary>
    public static bool KeepsOptionalLine(IReadOnlyList<CompGuideFitItem> items, double room, double lineHeight, double moreLineHeight = 0)
    {
        int Targets(CompGuideFit fit) => fit.Shown.Count(i => items[i].Kind == CompGuideItemKind.Row && items[i].Highlighted);
        return Targets(Fit(items, room - lineHeight, moreLineHeight, atLeastOne: true)) >= Targets(Fit(items, room, moreLineHeight, atLeastOne: true));
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

    /// <summary>
    /// Overlay pixels of slack in Fit and Sections: a box dragged to exactly the height of its content holds it. Without it,
    /// a room worked out apart from the running sum of non-integer heights came back a hair short (measured: 1044 of 8005
    /// boxes of 1 to 5 guide lines, window heights 600 to 2200, showed a line too few).
    /// </summary>
    private const double Tolerance = 1e-6;

    private static IReadOnlyList<int> Try(IReadOnlyList<CompGuideFitItem> items, double room)
    {
        var headers = Headers(items);
        var accepted = new HashSet<int>();
        var used = 0.0;
        bool Take(int i)
        {
            var group = items[i].Group;
            var header = headers.TryGetValue(group, out var h) && !accepted.Contains(h) ? h : -1;
            var need = items[i].Height + (header >= 0 ? items[header].Height : 0);
            if (used + need > room + Tolerance)
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

        var targetLeftOut = false;
        foreach (var i in HighlightedByRank(items))
        {
            targetLeftOut |= !Take(i);
        }

        // A highlighted guide left out is never replaced by one that is not (it may have needed its tier's bar, which a guide
        // of a tier already shown does not).
        for (var i = 0; i < items.Count && !targetLeftOut; i++)
        {
            if (items[i].Kind == CompGuideItemKind.Row && !items[i].Highlighted && !Take(i))
            {
                break;
            }
        }

        return accepted.OrderBy(i => i).ToList();
    }
}
