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

    [Fact]
    public void Fit_NoRoomAtAll_NothingShown_AndTheLineSaysSo()
    {
        var fit = CompGuideLayout.Fit(Items, room: 10, moreLineHeight: 16);

        Assert.Empty(fit.Shown);
        Assert.Equal((0, 8, true), (fit.RowsShown, fit.RowsTotal, fit.ShowsMoreLine));
    }
}
