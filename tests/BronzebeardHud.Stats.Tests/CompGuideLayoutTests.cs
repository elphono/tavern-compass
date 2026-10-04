namespace BronzebeardHud.Stats.Tests;

public class CompGuideLayoutTests
{
    private static CompGuideFitItem Header(int group, double height = 20) => new(CompGuideItemKind.TierHeader, group, height);

    private static CompGuideFitItem Row(int group, double height = 18, bool highlighted = false) => new(CompGuideItemKind.Row, group, height, highlighted);

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

    [Fact]
    public void Fit_NoRoomAtAll_NothingShown_AndTheLineSaysSo()
    {
        var fit = CompGuideLayout.Fit(Items, room: 10, moreLineHeight: 16);

        Assert.Empty(fit.Shown);
        Assert.Equal((0, 8, true), (fit.RowsShown, fit.RowsTotal, fit.ShowsMoreLine));
    }
}
