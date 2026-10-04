namespace BronzebeardHud.Stats.Tests;

public class TavernLayoutTests
{
    [Theory]
    [InlineData(3, 2000, 1220)]
    [InlineData(4, 2000, 1220)]
    [InlineData(5, 2000, 1220)]
    [InlineData(6, 2000, 1220)]
    [InlineData(7, 2000, 1220)]
    [InlineData(7, 1440, 1080)] // 4:3, the narrowest window HDT's layout supports
    public void Markers_AreCentredUnderEachMinion_EquallySpaced_WithoutOverlap(int minions, double width, double height)
    {
        var markers = TavernLayout.Markers(width, height, minions);
        var s = height / 1080;
        var pitch = 138 * s; // HDT's shop card slot

        Assert.Equal(minions, markers.Count);
        for (var i = 0; i < minions; i++)
        {
            Assert.Equal(width, markers[i].CenterX + markers[minions - 1 - i].CenterX, precision: 6);
            Assert.Equal(height / 2 - 145 * s + 95 * s, markers[i].CenterY, precision: 6); // bottom edge of the card
            Assert.InRange(markers[i].Left, 0, width);
            Assert.InRange(markers[i].Right, 0, width);
        }

        for (var i = 0; i + 1 < minions; i++)
        {
            Assert.Equal(pitch, markers[i + 1].CenterX - markers[i].CenterX, precision: 6);
            Assert.True(markers[i].Right < markers[i + 1].Left, $"markers {i} and {i + 1} overlap");
        }
    }

    [Fact]
    public void Markers_FollowTheWindowWhenItIsResized()
    {
        var small = TavernLayout.Markers(1600, 900, 5);
        var large = TavernLayout.Markers(3200, 1800, 5);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(2 * (small[i].CenterX - 800), large[i].CenterX - 1600, precision: 6);
            Assert.Equal(2 * small[i].CenterY, large[i].CenterY, precision: 6);
        }
    }

    [Theory]
    [InlineData(1920, 1080)] // 16:9
    [InlineData(2291, 1360)] // Ali's window
    [InlineData(2560, 1080)] // 21:9
    [InlineData(1600, 1200)] // 4:3
    public void TargetPanel_Default_AvoidsBoardsLeaderboardAndHero_InsideTheWindow(double width, double height)
    {
        var panel = TavernLayout.TargetPanel(width, height);

        foreach (var (name, zone) in NoGoZones.For(width, height))
        {
            Assert.False(NoGoZones.Overlaps(panel, zone), $"target panel covers the {name}");
        }

        Assert.True(panel.Left >= 0 && panel.Top >= 0 && panel.Right <= width && panel.Top + panel.Height <= height, "outside the window");
        var s = height / 1080;
        Assert.True(panel.Width - (2 * PanelFit.Border + 2 * PanelFit.Padding) * s >= (PanelFit.BoxColumn + PanelFit.NameColumn + PanelFit.CoreOvalsPerRow * (PanelFit.OvalWidth + PanelFit.OvalGap)) * s - 1e-9,
            "a tick box, a name column and six ovals do not fit on one line");
        Assert.True(panel.Top + panel.Height <= PanelFit.BottomLimit * height, "the default panel reaches the gold");
    }

    [Theory]
    [InlineData(1600, 900)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    public void LineupButtons_NeverTouchAPinButton_NorACard_OnAllSevenSlots(double width, double height)
    {
        var pins = TavernLayout.PinButtons(width, height, 7);
        var lineups = TavernLayout.LineupButtons(width, height, 7);
        var slots = TavernLayout.CardSlots(width, height, 7);

        Assert.Equal(7, lineups.Count);
        for (var i = 0; i < 7; i++)
        {
            for (var j = 0; j < 7; j++)
            {
                Assert.False(NoGoZones.Overlaps(lineups[i], pins[j]), $"{height}: lineup button {i} touches pin button {j}");
                Assert.False(NoGoZones.Overlaps(lineups[i], slots[j]), $"{height}: lineup button {i} covers card {j}");
            }
        }
    }

    /// <summary>
    /// The widest white margins measured around the art of a card portrait (art.hearthstonejson.com/v1/256x/{id}.jpg,
    /// 25 portraits, 2026-10-04): 36 columns on one side (BG36_352, BGS_004), 5 rows at the top or the bottom.
    /// </summary>
    private const double WidestWhiteSide = 36;

    private const double WidestWhiteTopOrBottom = 5;

    [Fact]
    public void PortraitCut_LeavesOutTheWhiteMarginsAroundTheArt_AndFillsTheOvalUndistorted()
    {
        var cut = TavernLayout.PortraitCut;
        var size = TavernLayout.PortraitSize;
        var bottom = cut.Top + cut.Height;

        Assert.True(cut.Left >= 0 && cut.Top >= 0 && cut.Right <= size && bottom <= size, "the cut reaches out of the portrait");
        Assert.True(cut.Left >= WidestWhiteSide, $"only {cut.Left} columns left out on the left: a white crescent shows");
        Assert.True(size - cut.Right >= WidestWhiteSide, $"only {size - cut.Right} columns left out on the right: a white crescent shows");
        Assert.True(cut.Top >= WidestWhiteTopOrBottom, $"only {cut.Top} rows left out at the top");
        Assert.True(size - bottom >= WidestWhiteTopOrBottom, $"only {size - bottom} rows left out at the bottom");
        Assert.Equal(TavernLayout.OvalAspect, cut.Height / cut.Width, precision: 9);
    }

    /// <summary>
    /// An oval must fit the fixed lines the target panel draws it in, or its ring and tick are clipped: a composition
    /// line is PanelFit.RowHeight tall, a pivot line PivotLine − 4, and a detail section counts DetailSection for its
    /// title (3 + 16 + 2) and one line of ovals. HDT's own oval (240 / 174) is 55 px tall at this width: it does not.
    /// </summary>
    [Fact]
    public void Ovals_FitTheLinesOfTheTargetPanel()
    {
        Assert.True(PanelFit.OvalHeight <= PanelFit.RowHeight, $"oval {PanelFit.OvalHeight} px in a {PanelFit.RowHeight} px composition line");
        Assert.True(PanelFit.OvalHeight <= PanelFit.PivotLine - 4, $"oval {PanelFit.OvalHeight} px in a {PanelFit.PivotLine - 4} px pivot line");
        Assert.True(PanelFit.OvalHeight <= PanelFit.DetailSection - (3 + 16 + 2), $"oval {PanelFit.OvalHeight} px in a detail section counted {PanelFit.DetailSection}");
    }

    [Fact]
    public void NothingToLayOut_IsEmpty()
    {
        Assert.Empty(TavernLayout.Markers(2000, 1220, 0));
        Assert.Empty(TavernLayout.Markers(0, 1220, 5));
    }

    [Fact]
    public void Markers_GrowWithTheirLines_AndCardSlotsFollowHdtsShop()
    {
        const double width = 2291, height = 1360;
        var s = height / 1080;
        var one = TavernLayout.Markers(width, height, 4, lines: 1)[0];
        var two = TavernLayout.Markers(width, height, 4, lines: 2)[0];
        Assert.Equal((13 * 1.3 + 8) * s, one.Height, precision: 6);
        Assert.Equal((2 * 13 * 1.3 + 8) * s, two.Height, precision: 6);

        var slots = TavernLayout.CardSlots(width, height, 3);
        Assert.Equal(width / 2 - 138 * s, slots[0].CenterX, precision: 6);
        Assert.Equal(width / 2, slots[1].CenterX, precision: 6);
        Assert.Equal(width / 2 + 138 * s, slots[2].CenterX, precision: 6);
        Assert.All(slots, r =>
        {
            Assert.Equal(height / 2 - 145 * s, r.CenterY, precision: 6);
            Assert.Equal(138 * s, r.Width, precision: 6);
            Assert.Equal(190 * s, r.Height, precision: 6);
        });
    }
}
