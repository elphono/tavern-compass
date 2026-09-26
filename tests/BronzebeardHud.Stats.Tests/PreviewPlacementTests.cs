namespace BronzebeardHud.Stats.Tests;

public class PreviewPlacementTests
{
    private static bool Overlap(LayoutRect a, LayoutRect b) =>
        a.Left < b.Right && b.Left < a.Right && a.Top < b.Top + b.Height && b.Top < a.Top + a.Height;

    /// <summary>A vignette of the panel's second line, fourth position, as the panel lays them out.</summary>
    private static LayoutRect Vignette(LayoutRect panel, double height)
    {
        var size = TavernLayout.VignetteSize * height;
        return new LayoutRect(panel.Left + 0.01 * height + 3.5 * size * 1.08, panel.Top + panel.Height * 0.55, size, size);
    }

    public static IEnumerable<object[]> Panels() =>
        from size in new[] { (1920.0, 1080.0), (2290.0, 1359.0) }
        from place in new[] { "right", "left", "bottom" }
        select new object[] { size.Item1, size.Item2, place };

    private static LayoutRect PanelAt(string where, double width, double height)
    {
        var standard = TavernLayout.TargetPanel(width, height); // the default place, lower right
        return where switch
        {
            "right" => standard,
            "left" => new LayoutRect(0.02 * width + standard.Width / 2, standard.CenterY, standard.Width, standard.Height),
            // Moved to the bottom middle and widened across the window: no room beside it, the preview goes above.
            _ => new LayoutRect(width / 2, height - standard.Height / 2 - 0.01 * height, width * 0.9, standard.Height),
        };
    }

    [Theory]
    [MemberData(nameof(Panels))]
    public void ThePreviewNeverCoversThePanel_StaysInTheWindow_AndHdtPutsItThere(double width, double height, string where)
    {
        var panel = PanelAt(where, width, height);
        var target = Vignette(panel, height);

        var (side, preview) = TavernLayout.PreviewRect(panel, target, width, height);

        Assert.Equal(where switch { "right" => PreviewSide.Left, "left" => PreviewSide.Right, _ => PreviewSide.Above }, side);
        Assert.False(Overlap(preview, panel), $"{where}: the preview covers the panel");
        Assert.True(preview.Left >= 0 && preview.Right <= width && preview.Top >= 0 && preview.Top + preview.Height <= height, $"{where}: off the window");
        Assert.Equal(TavernLayout.PreviewHeight * height, preview.Height, precision: 6);
        Assert.Equal(TavernLayout.PreviewHeight * height * 256 / 388, preview.Width, precision: 6);

        // HDT's own formula with our offsets (Windows/OverlayWindow.Tooltips.cs:147-150) lands exactly there.
        var (dx, dy) = TavernLayout.HdtTooltipOffsets(side, preview, target);
        var (left, top) = side switch
        {
            PreviewSide.Left => (target.Left - preview.Width - dx, target.Top + target.Height / 2 - preview.Height / 2 + dy),
            PreviewSide.Right => (target.Left + target.Width + dx, target.Top + target.Height / 2 - preview.Height / 2 + dy),
            PreviewSide.Above => (target.Left + target.Width / 2 - preview.Width / 2 + dx, target.Top - preview.Height - dy),
            _ => (target.Left + target.Width / 2 - preview.Width / 2 + dx, target.Top + target.Height + dy),
        };
        Assert.Equal(preview.Left, left, precision: 6);
        Assert.Equal(preview.Top, top, precision: 6);

        // And HDT's side check (121-137) would not flip it: the room it tests is there.
        Assert.True(side switch
        {
            PreviewSide.Left => target.Left - preview.Width >= 0,
            PreviewSide.Right => target.Right + preview.Width <= width,
            PreviewSide.Above => target.Top - preview.Height >= 0,
            _ => target.Top + target.Height + preview.Height <= height,
        });
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(2290, 1359)]
    public void AVignetteAtTheVeryBottomOrTop_ThePreviewIsPushedBackInsideTheWindow(double width, double height)
    {
        var standard = TavernLayout.TargetPanel(width, height);
        var low = new LayoutRect(standard.CenterX, height - standard.Height / 2, standard.Width, standard.Height); // moved to the bottom edge
        var lastRow = new LayoutRect(low.CenterX, height - 0.03 * height, 0.05 * height, 0.05 * height);
        var (_, below) = TavernLayout.PreviewRect(low, lastRow, width, height);
        Assert.Equal(height, below.Top + below.Height, precision: 6);

        var high = new LayoutRect(standard.CenterX, standard.Height / 2, standard.Width, standard.Height); // moved to the top edge
        var firstRow = new LayoutRect(high.CenterX, 0.03 * height, 0.05 * height, 0.05 * height);
        Assert.Equal(0, TavernLayout.PreviewRect(high, firstRow, width, height).Rect.Top, precision: 6);
    }

    [Fact]
    public void OnTheDefaultPanel_ThePreviewIsCentredOnTheHoveredVignette()
    {
        const double width = 2290, height = 1359;
        var panel = TavernLayout.TargetPanel(width, height);
        var near = new LayoutRect(panel.CenterX, panel.Top + 0.1 * height, 65, 65);
        var (_, preview) = TavernLayout.PreviewRect(panel, near, width, height);
        Assert.Equal(near.CenterY, preview.CenterY, precision: 6);
        Assert.Equal(panel.Left - TavernLayout.PreviewGap * height, preview.Right, precision: 6);
    }
}
