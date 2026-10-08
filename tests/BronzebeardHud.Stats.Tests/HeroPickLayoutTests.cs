namespace BronzebeardHud.Stats.Tests;

public class HeroPickLayoutTests
{
    private static readonly double[] ScreenshotPlateCentres = { 450, 870, 1290, 1700 };
    private const double WindowWidth = 2291, WindowHeight = 1360;

    public static TheoryData<int, double, double> HeroesAndWindows()
    {
        var data = new TheoryData<int, double, double>();
        foreach (var heroes in new[] { 2, 3, 4 })
        {
            data.Add(heroes, 1600, 900);
            data.Add(heroes, 1920, 1080);
            data.Add(heroes, 2560, 1440);
        }

        return data;
    }

    [Theory]
    [InlineData(2, 2000, 1220)]
    [InlineData(3, 2000, 1220)]
    [InlineData(4, 2000, 1220)]
    [InlineData(2, 2291, 1360)]
    [InlineData(3, 2291, 1360)]
    [InlineData(4, 2291, 1360)]
    public void HeroCenters_AreSymmetric_AndEquallySpaced(int heroes, double width, double height)
    {
        var centres = HeroPickLayout.HeroCenters(width, height, heroes);
        var scale = height / 1080;
        var axis = width / 2 + 7 * scale;

        Assert.Equal(heroes, centres.Count);
        for (var i = 0; i < heroes; i++)
        {
            Assert.Equal(2 * axis, centres[i] + centres[heroes - 1 - i], precision: 6);
        }

        for (var i = 0; i + 1 < heroes; i++)
        {
            Assert.Equal(340 * scale, centres[i + 1] - centres[i], precision: 6);
        }
    }

    [Fact]
    public void HeroCenters_FourHeroes_AgreeWithHdtGameGeometry()
    {
        var centres = HeroPickLayout.HeroCenters(WindowWidth, WindowHeight, 4);
        var frameWidth = WindowHeight * 4 / 3;
        var frameLeft = (WindowWidth - frameWidth) / 2;
        var leftEdge = 0.5 - (4 * 0.1725 + 3 * 0.0635) / 2;
        for (var i = 0; i < 4; i++)
        {
            var hdtCentre = frameLeft + frameWidth * (leftEdge + i * 0.236 + 0.1725 / 2);
            Assert.InRange(centres[i] - hdtCentre, -0.01 * WindowHeight, 0.01 * WindowHeight);
        }
    }

    [Fact]
    public void HeroCenters_FourHeroes_FallOnThePlatesOfAlisScreenshot()
    {
        var centres = HeroPickLayout.HeroCenters(WindowWidth, WindowHeight, 4);
        var layoutAxis = centres.Average();
        var screenshotAxis = ScreenshotPlateCentres.Average();
        for (var i = 0; i < 4; i++)
        {
            var expected = ScreenshotPlateCentres[i] - screenshotAxis;
            Assert.InRange(centres[i] - layoutAxis, expected - 0.03 * 2000, expected + 0.03 * 2000);
        }
    }

    /// <summary>
    /// The badges and the status line leave visible, for 2, 3 and 4 heroes at 900, 1080 and 1440 pixels high,
    /// zones written here independently of <see cref="HeroPickLayout"/>, in fractions of the window height H:
    /// - the reroll button ("Réinitialiser") under each hero: 0.632 to 0.718 H, from 0.125 H left to 0.087 H
    ///   right of the hero's centre;
    /// - each hero's name plate: 0.527 to 0.638 H, over the same span;
    /// - the OK button, 0.751 to 0.825 H, from 0.065 H left to 0.075 H right of the window's middle, with 0.01 H
    ///   to spare all round;
    /// all three measured on Hearthstone's own capture of Ali's hero selection (2026-09-26 18:29:44, 2291 × 1360);
    /// - HDT's hero-picking stats: everything above 0.29 H. A hypothesis, to be confirmed in game: HDT puts the
    ///   heroes' top at heroY = 0.29 (HearthSim/Hearthstone-Deck-Tracker 509bb0b, Utility/RegionDrawer/RegionDrawer.cs:379)
    ///   and its commit e51a316a53 lifts its stats above them with a negative margin.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroesAndWindows))]
    public void Compute_LeavesTheGameButtonsTheNamePlatesAndHdtStatsVisible(int heroes, double width, double height)
    {
        var badges = HeroPickLayout.Compute(width, height, heroes);
        var status = HeroPickLayout.Status(width, height, badges);
        var zones = VisibleZones(width, height, heroes).ToList();

        Assert.Equal(heroes, badges.Count);
        foreach (var (rect, what) in badges.Select((b, i) => (b, $"badge {i + 1}/{heroes}")).Append((status, "status line")))
        {
            foreach (var (name, zone) in zones)
            {
                Assert.False(NoGoZones.Overlaps(rect, zone), $"{what} covers {name} at {width}×{height}: {rect.Top / height:0.000}–{(rect.Top + rect.Height) / height:0.000} H");
            }

            Assert.True(rect.Left >= 0 && rect.Right <= width && rect.Top >= 0 && rect.Top + rect.Height <= height, $"{what} leaves the window");
        }
    }

    [Theory]
    [MemberData(nameof(HeroesAndWindows))]
    public void Compute_EachBadgeStaysInItsHerosColumn_WithoutTouchingTheOthers(int heroes, double width, double height)
    {
        var badges = HeroPickLayout.Compute(width, height, heroes);
        var centres = HeroPickLayout.HeroCenters(width, height, heroes);
        var status = HeroPickLayout.Status(width, height, badges);

        for (var i = 0; i < heroes; i++)
        {
            Assert.InRange(badges[i].CenterX - centres[i], -0.03 * height, 0.03 * height);
            Assert.InRange(badges[i].Width / height, 0.17 - 1e-9, 0.17 + 1e-9);
            for (var j = i + 1; j < heroes; j++)
            {
                Assert.False(NoGoZones.Overlaps(badges[i], badges[j]), $"badges {i + 1} and {j + 1} overlap");
            }

            Assert.False(NoGoZones.Overlaps(badges[i], status), $"badge {i + 1} and the status line overlap");
        }
    }

    [Fact]
    public void Compute_ThreeHeroes_PutsTheMiddleBadgeUnderTheOkButton_AndKeepsTheOthersUnderTheirReroll()
    {
        var badges = HeroPickLayout.Compute(1920, 1080, 3);

        Assert.True(badges[1].Top >= (0.825 + 0.01) * 1080 - 1e-6, $"middle badge top {badges[1].Top / 1080:0.000} H");
        Assert.InRange(badges[0].Top / 1080, 0.718, 0.73);
        Assert.InRange(badges[2].Top / 1080, 0.718, 0.73);
    }

    [Theory]
    [InlineData(2, 1600, 900)]
    [InlineData(2, 2560, 1440)]
    [InlineData(4, 1920, 1080)]
    [InlineData(4, 2560, 1440)]
    public void Compute_TwoOrFourHeroes_KeepsEveryBadgeRightUnderItsReroll_MovingAsideFromTheOkButton(int heroes, double width, double height)
    {
        var badges = HeroPickLayout.Compute(width, height, heroes);
        var centres = HeroPickLayout.HeroCenters(width, height, heroes);

        foreach (var (badge, centre) in badges.Zip(centres))
        {
            Assert.InRange(badge.Top / height, 0.718, 0.73);
            var sideways = badge.CenterX - centre;
            var towardsTheMiddle = centre < width / 2 ? sideways > 1e-9 : sideways < -1e-9;
            Assert.False(towardsTheMiddle, $"badge moved {sideways / height:0.0000} H towards the OK button");
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
        Assert.Empty(HeroPickLayout.HeroCenters(width, height, heroes));
    }

    [Fact]
    public void Badge_HoldsTwoSourcesAndTheOdds_AtTheirOwnSize()
    {
        var items = new[] { HeroPickLayout.SourceLine, HeroPickLayout.BodyLine, HeroPickLayout.SourceLine };

        Assert.Equal(new[] { 0, 1, 2 }, HeroPickLayout.ItemsThatFit(items, HeroPickLayout.ContentHeight));
    }

    [Fact]
    public void Badge_EveryLineIsTallerThanItsFont()
    {
        Assert.True(HeroPickLayout.SourceLine >= 1.25 * PanelTypography.HeroTier);
        Assert.True(HeroPickLayout.BodyLine >= 1.25 * PanelTypography.Body);
        Assert.True(HeroPickLayout.NoDataLine >= 1.25 * PanelTypography.HeroNoData);
    }

    [Fact]
    public void ItemsThatFit_LeavesOutWhatNoLongerFits_AndKeepsALaterItemThatStillDoes()
    {
        // Two sources and the odds: the second source line fits, a fourth line after it no longer does.
        var twoSources = new[] { 28.0, 17, 28, 16 };
        Assert.Equal(new[] { 0, 1, 2 }, HeroPickLayout.ItemsThatFit(twoSources, 78.4));

        // An item too tall is left out whole, never cut; a smaller item after it still shows.
        Assert.Equal(new[] { 0, 1, 3 }, HeroPickLayout.ItemsThatFit(new[] { 28.0, 17, 48, 18 }, 78.4));
        Assert.Empty(HeroPickLayout.ItemsThatFit(new[] { 80.0 }, 78.4));
    }

    private static IEnumerable<(string Name, LayoutRect Zone)> VisibleZones(double width, double height, int heroes)
    {
        var s = height / 1080;
        for (var i = 0; i < heroes; i++)
        {
            // The hero grid, written out: one hero every 340 units, centred 7 units right of the middle.
            var centre = width / 2 + 7 * s + (i + 0.5 - heroes / 2.0) * 340 * s;
            yield return ($"reroll button {i + 1}", Edges(centre - 0.125 * height, 0.632 * height, centre + 0.087 * height, 0.718 * height));
            yield return ($"name plate {i + 1}", Edges(centre - 0.125 * height, 0.527 * height, centre + 0.087 * height, 0.638 * height));
        }

        // A badge may touch the 0.01 H margin, not enter it: a millionth of a pixel absorbs rounding.
        const double touch = 1e-6;
        yield return ("OK button (0.01 H around it)", Edges(width / 2 - 0.075 * height + touch, 0.741 * height + touch, width / 2 + 0.085 * height - touch, 0.835 * height - touch));
        yield return ("HDT hero-picking stats", Edges(0, 0, width, 0.29 * height));
    }

    private static LayoutRect Edges(double left, double top, double right, double bottom) =>
        new((left + right) / 2, (top + bottom) / 2, right - left, bottom - top);
}
