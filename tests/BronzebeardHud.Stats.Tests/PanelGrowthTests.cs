namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// The "Compositions" panel sized to its content (+ / −, 2026-10-06), at 1920 × 1080: the boards end at 678.24, the gold
/// bound is at 1020.6 (PanelFit.BottomLimit), the hero runs from x = 744 to 1176 below the boards, and the panel's default
/// column is x = 1181 to 1669 (over the boards' columns, right of the hero).
/// </summary>
public class PanelGrowthTests
{
    private const double W = 1920;
    private const double H = 1080;
    private const double BoardsBottom = 678.24;
    private const double Gold = 0.945 * H;

    private static LayoutRect Box(double left, double top, double width, double height) => new(left + width / 2, top + height / 2, width, height);

    /// <summary>The default column, below the boards, with room down to the gold: 691 to 991.</summary>
    private static readonly LayoutRect Default = Box(1181, 691, 488, 300);

    private static IReadOnlyList<LayoutRect> Zones => PanelGrowth.Obstacles(W, H);

    [Theory]
    [InlineData(250)] // more than... less than its box: shrinks
    [InlineData(300)]
    [InlineData(329)] // down to 1020: just above the gold
    public void WithRoomBelow_ThePanelKeepsItsTop_AndGrowsOrShrinksDownwards(double wanted)
    {
        var span = PanelGrowth.Place(Default, wanted, minimum: 100, H, Zones);

        Assert.Equal((691, wanted, PanelAnchor.Top), (span.Top, span.Height, span.Anchor));
    }

    [Fact]
    public void OutOfRoomBelow_ItGrowsUpwards_ItsBottomOnTheGoldBound_NeverOntoTheBoards()
    {
        var some = PanelGrowth.Place(Default, 340, minimum: 100, H, Zones);
        var tooMuch = PanelGrowth.Place(Default, 500, minimum: 100, H, Zones);

        Assert.Equal(PanelAnchor.Bottom, some.Anchor);
        Assert.Equal(Gold, some.Bottom, precision: 6);
        Assert.Equal(Gold - 340, some.Top, precision: 6);       // 680.6: above its box, still below the boards (678.24)
        Assert.Equal(BoardsBottom, tooMuch.Top, precision: 6);  // all the room between the boards and the gold, no more
        Assert.Equal(Gold - BoardsBottom, tooMuch.Height, precision: 6);
        Assert.Equal(PanelAnchor.Bottom, tooMuch.Anchor);
    }

    [Fact]
    public void ABoxAgainstTheBottomOfTheScreen_KeepsItsBottom_GrowingAndShrinkingUpwards()
    {
        // Dragged down to the very bottom (its box ends at 1080, below the gold bound: Ali put it there).
        var bottom = Box(1181, 880, 488, 200);

        var small = PanelGrowth.Place(bottom, 120, minimum: 100, H, Zones);
        var bigger = PanelGrowth.Place(bottom, 300, minimum: 100, H, Zones);
        var all = PanelGrowth.Place(bottom, 900, minimum: 100, H, Zones);

        Assert.Equal((960.0, 120.0, PanelAnchor.Bottom), (small.Top, small.Height, small.Anchor));
        Assert.Equal((780.0, 300.0, PanelAnchor.Bottom), (bigger.Top, bigger.Height, bigger.Anchor));
        Assert.Equal(BoardsBottom, all.Top, precision: 6);      // up to the boards, not onto them
        Assert.Equal(H, all.Bottom, precision: 6);              // never off the screen
    }

    /// <summary>
    /// A box the handle pulled down to the gold line (691 to 1021: the place the default panel may reach) is not "at the bottom
    /// of the screen": − shrinks it from its bottom, its top stays under the boards. Seen in the simulation on 2026-10-06: it
    /// kept its bottom, and the panel jumped 78 px down.
    /// </summary>
    [Fact]
    public void ABoxEndingOnTheGoldLine_KeepsItsTop_ItIsNotAgainstTheBottom()
    {
        var box = Box(1181, 691, 488, 330);

        var smaller = PanelGrowth.Place(box, 252, minimum: 100, H, Zones);

        Assert.Equal((691.0, PanelAnchor.Top), (smaller.Top, smaller.Anchor));
    }

    [Fact]
    public void ABoxAboveTheBoards_GrowsDownwardsUpToThem_ThenUpwards_NeverOntoThem()
    {
        var top = Box(1181, 40, 488, 200); // over the boards' columns, above the tavern row (it starts at 320.76)

        var some = PanelGrowth.Place(top, 250, minimum: 100, H, Zones);
        var more = PanelGrowth.Place(top, 400, minimum: 100, H, Zones);

        Assert.Equal((40.0, 290.0, PanelAnchor.Top), (some.Top, some.Bottom, some.Anchor));
        Assert.Equal(320.76, more.Bottom, precision: 6);    // down to the tavern row, not onto it
        Assert.Equal((0.0, PanelAnchor.Bottom), (more.Top, more.Anchor)); // then up to the top of the screen
    }

    [Fact]
    public void ABoxClearOfEveryZone_MayTakeTheWholeHeightAboveTheGold_NeverOffTheScreen()
    {
        var right = Box(1700, 100, 200, 200); // right of the 4:3 frame: no zone of the game in its columns

        var span = PanelGrowth.Place(right, 2000, minimum: 100, H, Zones);

        Assert.Equal((0.0, Gold), (span.Top, span.Bottom));
    }

    [Fact]
    public void AnotherPanelAboveIt_TheSkipCombatButton_StopsItsGrowthToo()
    {
        var skip = Box(1540, 690 - 39, 130, 39); // just above the panel, as if moved there
        var bottom = Box(1181, 880, 488, 200);

        var span = PanelGrowth.Place(bottom, 900, minimum: 100, H, PanelGrowth.Obstacles(W, H, new[] { skip }));

        Assert.Equal(690, span.Top, precision: 6); // below the button (690), not up to the boards (678.24)
    }

    [Fact]
    public void AZoneTheBoxAlreadyCovers_IsWhereAliPutIt_AndDoesNotStopIt()
    {
        var onTheBoards = Box(1181, 600, 488, 150); // covers the bottom of the boards, by choice

        var span = PanelGrowth.Place(onTheBoards, 300, minimum: 100, H, Zones);

        Assert.Equal((600.0, 300.0, PanelAnchor.Top), (span.Top, span.Height, span.Anchor));
    }

    [Fact]
    public void NeverUnderTheMinimum_EvenWhenTheContentAsksForLess()
    {
        var span = PanelGrowth.Place(Default, 50, minimum: 162, H, Zones);

        Assert.Equal((691.0, 162.0), (span.Top, span.Height));
    }

    [Fact]
    public void AMinimumLargerThanTheRoom_IsKept_InsideTheScreen()
    {
        var span = PanelGrowth.Place(Default, 50, minimum: 500, H, Zones);

        Assert.Equal(500, span.Height, precision: 6);
        Assert.True(span.Top >= 0 && span.Bottom <= H, $"{span.Top}..{span.Bottom}");
    }

    /// <summary>
    /// + and − pressed again and again (1 → 4 → 1 → 4 → 1 → 4): the box is the layout's, never the place the panel was last
    /// given, so each height lands where it landed the first time — no creeping up a pixel a press.
    /// </summary>
    [Fact]
    public void PressedAgainAndAgain_EachHeightLandsWhereItLandedTheFirstTime()
    {
        var heights = new[] { 160.0, 400, 160, 400, 160, 400 };

        var spans = heights.Select(h => PanelGrowth.Place(Default, h, minimum: 100, H, Zones)).ToList();

        Assert.Equal(spans[0], spans[2]);
        Assert.Equal(spans[2], spans[4]);
        Assert.Equal(spans[1], spans[3]);
        Assert.Equal(spans[3], spans[5]);
        Assert.NotEqual(spans[0].Top, spans[1].Top); // 1 line keeps the top, 4 lines push it up
    }

    [Fact]
    public void ResizeLine_HasTheExactFormat()
    {
        var line = PanelGrowth.ResizeLine(4, Box(1181.4, 557.2, 488, 463.4), PanelAnchor.Bottom, linesShown: 4, linesWanted: 4, linesTotal: 15);

        Assert.Equal("Bronzebeard HUD: targets n=4 panel resized to (1181,557 488x463) anchor=bottom lines=4/4 shown, of 15", line);
    }
}
