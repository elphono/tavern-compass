namespace BronzebeardHud.Stats.Tests;

public class HeroPickLayoutTests
{
    // Ali's hero-picking screenshot (2026-09-26): grey plate centres, and the Hearthstone
    // window it was taken from, per Hearthstone's options.txt (graphicswidth/graphicsheight).
    private static readonly double[] ScreenshotPlateCentres = { 450, 870, 1290, 1700 };
    private const double WindowWidth = 2291, WindowHeight = 1360;

    [Theory]
    [InlineData(2, 2000, 1220)]
    [InlineData(3, 2000, 1220)]
    [InlineData(4, 2000, 1220)]
    [InlineData(2, 2291, 1360)]
    [InlineData(3, 2291, 1360)]
    [InlineData(4, 2291, 1360)]
    public void Compute_IsSymmetric_EquallySpaced_AndBadgesNeverOverlap(int heroes, double width, double height)
    {
        var rects = HeroPickLayout.Compute(width, height, heroes);
        var scale = height / 1080;
        var axis = width / 2 + 7 * scale;

        Assert.Equal(heroes, rects.Count);
        for (var i = 0; i < heroes; i++)
        {
            // Mirror images around the grid axis.
            Assert.Equal(2 * axis, rects[i].CenterX + rects[heroes - 1 - i].CenterX, precision: 6);
            Assert.InRange(rects[i].Left, 0, width);
            Assert.InRange(rects[i].Right, 0, width);
        }

        for (var i = 0; i + 1 < heroes; i++)
        {
            Assert.Equal(340 * scale, rects[i + 1].CenterX - rects[i].CenterX, precision: 6);
            Assert.True(rects[i].Right < rects[i + 1].Left, $"badges {i} and {i + 1} overlap");
        }
    }

    [Fact]
    public void Compute_FourHeroes_AgreesWithHdtGameGeometry()
    {
        // Independent check against HDT's game-region constants (RegionDrawer.cs:42-43, 374-378):
        // heroes 0.1725 wide every 0.236, centred in a 4:3 frame (Helper.GetScaledXPos).
        var rects = HeroPickLayout.Compute(WindowWidth, WindowHeight, 4);
        var frameWidth = WindowHeight * 4 / 3;
        var frameLeft = (WindowWidth - frameWidth) / 2;
        var leftEdge = 0.5 - (4 * 0.1725 + 3 * 0.0635) / 2;
        for (var i = 0; i < 4; i++)
        {
            var hdtCentre = frameLeft + frameWidth * (leftEdge + i * 0.236 + 0.1725 / 2);
            Assert.InRange(rects[i].CenterX - hdtCentre, -0.01 * WindowHeight, 0.01 * WindowHeight);
        }
    }

    [Fact]
    public void Compute_FourHeroes_FallsOnThePlatesOfAlisScreenshot()
    {
        // The screenshot is a free-form crop of the 2291 x 1360 window, not the window itself: its plates
        // are symmetric around x ≈ 1075, not around its own middle (1000). Positions are therefore compared
        // relative to the row's axis, with the requested tolerance of 3 % of the screenshot width (2000 px).
        var rects = HeroPickLayout.Compute(WindowWidth, WindowHeight, 4);
        var layoutAxis = rects.Average(r => r.CenterX);
        var screenshotAxis = ScreenshotPlateCentres.Average();
        for (var i = 0; i < 4; i++)
        {
            var expected = ScreenshotPlateCentres[i] - screenshotAxis;
            Assert.InRange(rects[i].CenterX - layoutAxis, expected - 0.03 * 2000, expected + 0.03 * 2000);
        }
    }

    [Fact]
    public void Compute_BadgeSitsOnTheMeasuredPlate()
    {
        foreach (var rect in HeroPickLayout.Compute(WindowWidth, WindowHeight, 4))
        {
            // Plate centre measured between 0.663 and 0.670 of the height; plate ≈ 0.175 × 0.062 of the height.
            Assert.InRange(rect.CenterY / WindowHeight, 0.663, 0.670);
            Assert.True(rect.Width <= 0.175 * WindowHeight);
            Assert.True(rect.Height <= 0.062 * WindowHeight);
        }
    }

    [Fact]
    public void Compute_FollowsTheWindowWhenItIsResized()
    {
        var small = HeroPickLayout.Compute(1600, 900, 3);
        var large = HeroPickLayout.Compute(3200, 1800, 3);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(2 * (small[i].CenterX - 800), large[i].CenterX - 1600, precision: 6);
            Assert.Equal(2 * small[i].CenterY, large[i].CenterY, precision: 6);
            Assert.Equal(2 * small[i].Width, large[i].Width, precision: 6);
        }
    }

    [Theory]
    [InlineData(0, 1080, 4)]
    [InlineData(1920, 0, 4)]
    [InlineData(1920, 1080, 0)]
    public void Compute_NothingToLayOut_IsEmpty(double width, double height, int heroes)
    {
        Assert.Empty(HeroPickLayout.Compute(width, height, heroes));
    }
}
