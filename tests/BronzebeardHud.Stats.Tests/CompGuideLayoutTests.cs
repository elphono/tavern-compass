namespace BronzebeardHud.Stats.Tests;

public class CompGuideLayoutTests
{
    private static CompGuideFitItem Header(int group, double height = 20) => new(CompGuideItemKind.TierHeader, group, height);

    private static CompGuideFitItem Row(int group, double height = 18, bool highlighted = false, int rank = 0) => new(CompGuideItemKind.Row, group, height, highlighted, rank);

    // S: three rows; A: three rows, the last one highlighted (54 tall); B: two rows.
    private static readonly CompGuideFitItem[] Items =
    {
        Header(0), Row(0), Row(0), Row(0),
        Header(1), Row(1), Row(1), Row(1, 54, highlighted: true),
        Header(2), Row(2), Row(2),
    };

    [Fact]
    public void Fit_EverythingFits_EverythingShown_NoMoreLine()
    {
        var fit = CompGuideLayout.Fit(Items, room: 1000, moreLineHeight: 16);

        Assert.Equal(Enumerable.Range(0, Items.Length), fit.Shown);
        Assert.Equal((8, 8, false), (fit.RowsShown, fit.RowsTotal, fit.ShowsMoreLine));
    }

    [Fact]
    public void Fit_ShortOfRoom_TheHighlightedGuideStays_OthersFollowInOrder_HeadersOnlyForShownTiers()
    {
        // 150 - 16 = 134: A's header and its highlighted row (20 + 54), then S's header and two rows (20 + 18 + 18) = 130;
        // S's third row would need 18 more. B shows nothing, not even its header.
        var fit = CompGuideLayout.Fit(Items, room: 150, moreLineHeight: 16);

        Assert.Equal(new[] { 0, 1, 2, 4, 7 }, fit.Shown);
        Assert.Equal((3, 8, true), (fit.RowsShown, fit.RowsTotal, fit.ShowsMoreLine));
    }

    [Fact]
    public void Fit_TheMoreLineIsKeptOnlyWhenSomethingIsLeftOut()
    {
        var total = Items.Sum(i => i.Height);

        var exact = CompGuideLayout.Fit(Items, room: total, moreLineHeight: 16);
        var oneShort = CompGuideLayout.Fit(Items, room: total - 1, moreLineHeight: 16);

        Assert.Equal(240, total);
        Assert.False(exact.ShowsMoreLine);
        Assert.Equal(8, exact.RowsShown);
        Assert.True(oneShort.ShowsMoreLine);
        Assert.Equal(Enumerable.Range(0, 10), oneShort.Shown); // 239 - 16 = 223: B's last row (it would end at 240) goes
        Assert.Equal(7, oneShort.RowsShown);
    }

    /// <summary>
    /// A box exactly as tall as a tier bar and n guide lines shows the n lines, not n − 1. The heights are not whole numbers
    /// (a line one 54-wide oval tall, PanelFit.RowHeight, × the scale of every window height from 600 to 2200), and the
    /// room is worked out apart from the running sum Fit makes (bar + n × line), as a box dragged to its content is.
    /// </summary>
    [Fact]
    public void Fit_ARoomExactlyTheHeightOfItsLines_ShowsThemAll_AtEveryWindowHeight()
    {
        var shortOfALine = new List<string>();
        var oneTooMany = new List<string>();
        for (var height = 600; height <= 2200; height++)
        {
            var s = TavernLayout.Scale(height);
            var bar = 21.3 * s;
            var line = PanelFit.RowHeight * s;
            var items = new[] { Header(0, bar) }.Concat(Enumerable.Range(0, 6).Select(_ => Row(0, line))).ToArray();
            foreach (var n in new[] { 1, 2, 3, 4, 5 })
            {
                var fit = CompGuideLayout.Fit(items, room: bar + n * line, moreLineHeight: 0, atLeastOne: true);
                if (fit.RowsShown != n)
                {
                    shortOfALine.Add($"{height}:{n}->{fit.RowsShown}");
                }

                // A hundredth of a pixel short, the last line does not fit: the slack is not room.
                var tight = CompGuideLayout.Fit(items, room: bar + n * line - 0.01, moreLineHeight: 0, atLeastOne: true);
                if (tight.RowsShown != Math.Max(1, n - 1))
                {
                    oneTooMany.Add($"{height}:{n}->{tight.RowsShown}");
                }
            }
        }

        Assert.True(shortOfALine.Count == 0, $"{shortOfALine.Count} of {1601 * 5} boxes show a line too few: {string.Join(" ", shortOfALine.Take(12))}");
        Assert.True(oneTooMany.Count == 0, $"{oneTooMany.Count} boxes a hundredth of a pixel short show a line too many: {string.Join(" ", oneTooMany.Take(12))}");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 20 + 54)]                 // A's header and its highlighted line, first whatever its place
    [InlineData(2, 20 + 54 + 20 + 18)]       // then S's header and first line
    [InlineData(3, 20 + 54 + 20 + 18 + 18)]  // S's second line: its header is already counted
    [InlineData(5, 20 + 54 + 20 + 18 * 3 + 18)]  // S complete, then A's first line (A's header already counted)
    [InlineData(8, 240)]                     // every piece
    [InlineData(20, 240)]                    // more lines asked than there are: every piece, once
    public void HeightFor_TheHighlightedLinesFirst_ThenTheOthersInOrder_EachHeaderOnce(int rows, double expected) =>
        Assert.Equal(expected, CompGuideLayout.HeightFor(Items, rows), precision: 9);

    /// <summary>
    /// The panel sized for N lines (+ / −) shows those N lines, the highlighted one among them, and nothing more: a room of
    /// HeightFor(n) makes Fit show n lines, for every n, at every window height (non-integer heights: the same slack as Fit).
    /// </summary>
    [Fact]
    public void HeightFor_IsTheRoomInWhichFitShowsExactlyThoseLines_AtEveryWindowHeight()
    {
        var wrong = new List<string>();
        for (var height = 600; height <= 2200; height += 7)
        {
            var s = TavernLayout.Scale(height);
            var bar = 21.3 * s;
            var line = PanelFit.RowHeight * s;
            var items = new[]
            {
                Header(0, bar), Row(0, line), Row(0, line * 1.31),
                Header(1, bar), Row(1, line, highlighted: true), Row(1, line),
                Header(2, bar), Row(2, line, highlighted: true), Row(2, line), Row(2, line),
            };
            for (var n = 1; n <= 7; n++)
            {
                var fit = CompGuideLayout.Fit(items, CompGuideLayout.HeightFor(items, n), moreLineHeight: 0, atLeastOne: true);
                var highlightedShown = fit.Shown.Count(i => items[i].Highlighted);
                if (fit.RowsShown != n || highlightedShown != Math.Min(n, 2))
                {
                    wrong.Add($"{height}:{n}->{fit.RowsShown} ({highlightedShown} highlighted)");
                }
            }
        }

        Assert.True(wrong.Count == 0, $"{wrong.Count} boxes show another number of lines: {string.Join(" ", wrong.Take(12))}");
    }

    /// <summary>
    /// A highlighted guide (a target) left out is never replaced by one that is not: its line needed its tier's bar too, and a
    /// guide of a tier already shown fitted in the room left. Seen in the simulation on 2026-10-06, the panel sized for three
    /// lines a hair too short: two targets of S, then a guide of S that is no target, and the third target (of C), whose frames
    /// were on Bob's cards, nowhere in the list.
    /// </summary>
    [Fact]
    public void Fit_ATargetLeftOut_IsNeverReplacedByAGuideThatIsNotOne()
    {
        // S: two targets and a guide; C: a target. 20 + 18 + 18 + 20 + 18 = 94 for the three targets; 93 leaves C's out,
        // and S's third guide (18, no bar to add) would fit in what is left.
        var items = new[] { Header(0), Row(0, highlighted: true), Row(0, highlighted: true), Row(0), Header(1), Row(1, highlighted: true) };

        var all = CompGuideLayout.Fit(items, room: 94, moreLineHeight: 0, atLeastOne: true);
        var short1 = CompGuideLayout.Fit(items, room: 93, moreLineHeight: 0, atLeastOne: true);

        Assert.Equal(new[] { 0, 1, 2, 4, 5 }, all.Shown);
        Assert.Equal(new[] { 0, 1, 2 }, short1.Shown); // the two targets of S, and nothing in the third target's place
        Assert.Equal((2, 4), (short1.RowsShown, short1.RowsTotal));
    }

    /// <summary>
    /// Targets that do not all fit are taken by rank, not by place in the list: the best stay. Seen in the simulation on
    /// 2026-10-06: + from 3 to 4 targets, room for three, and the third target (tier C) gave way to the fourth (tier B, higher
    /// in the list) — a press on + made a better composition disappear.
    /// </summary>
    [Fact]
    public void Fit_TargetsThatDoNotAllFit_AreTakenByRank_TheBestStay_ShownInListOrder()
    {
        // S: the 1st target; B: the 3rd; C: the 2nd. Room for two of them with their bars: 20 + 18 + 20 + 18.
        var items = new[]
        {
            Header(0), Row(0, highlighted: true, rank: 1),
            Header(1), Row(1, highlighted: true, rank: 3),
            Header(2), Row(2, highlighted: true, rank: 2),
        };

        var fit = CompGuideLayout.Fit(items, room: 76, moreLineHeight: 0, atLeastOne: true);

        Assert.Equal(new[] { 0, 1, 4, 5 }, fit.Shown); // ranks 1 and 2, drawn in the list's order
        Assert.Equal(76, CompGuideLayout.HeightFor(items, 2), precision: 9);
        Assert.Equal(new[] { 0, 1, 4, 5 }, CompGuideLayout.Fit(items, CompGuideLayout.HeightFor(items, 2), 0, atLeastOne: true).Shown);
    }

    [Fact]
    public void Fit_NoRoomAtAll_NothingShown_AndTheLineSaysSo()
    {
        var fit = CompGuideLayout.Fit(Items, room: 10, moreLineHeight: 16);

        Assert.Empty(fit.Shown);
        Assert.Equal((0, 8, true), (fit.RowsShown, fit.RowsTotal, fit.ShowsMoreLine));
    }
}
