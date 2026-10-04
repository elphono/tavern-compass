namespace BronzebeardHud.Stats.Tests;

public class PanelLayoutTests
{
    private static readonly LayoutRect TargetsDefault = new(600, 950, 300, 160);
    private static readonly LayoutRect LineupsDefault = new(1900, 300, 520, 280);

    /// <summary>The panel removed on 2026-09-27, still present in layout.json files written before.</summary>
    private const string RemovedPanel = "combats";

    [Fact]
    public void StoreThenReload_KeepsEachPanelWhereItWasDropped()
    {
        var layout = PanelLayout.Empty;
        layout.Store("target-compositions", 229.1, 870.4, 2291, 1360);
        layout.Store("lineups", 1604, 68, 2291, 1360);
        layout.Store("skip-combat", 10, 20, 2291, 1360);

        var (reloaded, error) = PanelLayout.Parse(layout.Serialize());

        Assert.Null(error);
        Assert.Equal(new[] { "lineups", "skip-combat", "target-compositions" }, reloaded.MovedPanels.OrderBy(p => p));
        var targets = reloaded.Resolve("target-compositions", TargetsDefault, 2291, 1360);
        Assert.Equal((229.1, 870.4), (Math.Round(targets.Left, 1), Math.Round(targets.Top, 1)));
        var lineups = reloaded.Resolve("lineups", LineupsDefault, 2291, 1360);
        Assert.Equal((1604.0, 68.0), (Math.Round(lineups.Left, 1), Math.Round(lineups.Top, 1)));
    }

    [Fact]
    public void AnOldFileWithTheRemovedCombatsPanel_IsReadWithoutError_TheOthersKeepTheirPlace()
    {
        var json = "{\"schema\": 1, \"panels\": {\"" + RemovedPanel + "\": {\"left\": 0.70, \"top\": 0.05}, " +
                   "\"target-compositions\": {\"left\": 0.1, \"top\": 0.64}, \"lineups\": {\"left\": 0.75, \"top\": 0.3}}}";

        var (layout, error) = PanelLayout.Parse(json);

        Assert.Null(error); // nothing to report: the entry is ignored silently
        Assert.Equal(new[] { "lineups", "target-compositions" }, layout.MovedPanels.OrderBy(p => p));
        var targets = layout.Resolve("target-compositions", TargetsDefault, 2000, 1000);
        Assert.Equal((200.0, 640.0), (Math.Round(targets.Left, 1), Math.Round(targets.Top, 1)));
        Assert.DoesNotContain(RemovedPanel, layout.Serialize()); // gone at the next save
    }

    [Fact]
    public void ANeverMovedPanel_KeepsItsDefaultPlace()
    {
        var rect = PanelLayout.Empty.Resolve("lineups", LineupsDefault, 2291, 1360);
        Assert.Equal((LineupsDefault.Left, LineupsDefault.Top, LineupsDefault.Width), (rect.Left, rect.Top, rect.Width));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{\"schema\": 2, \"panels\": {}}")]
    [InlineData("{\"schema\": 1, \"panels\": {\"lineups\": {\"left\": \"far\"}}}")]
    [InlineData("[1, 2]")]
    public void ACorruptFile_GivesTheDefaultLayout_AndSaysSo(string json)
    {
        var (layout, error) = PanelLayout.Parse(json);

        Assert.Empty(layout.MovedPanels);
        Assert.StartsWith("layout.json:", error);
        var rect = layout.Resolve("target-compositions", TargetsDefault, 2291, 1360);
        Assert.Equal((TargetsDefault.Left, TargetsDefault.Top), (rect.Left, rect.Top));
    }

    [Fact]
    public void AfterASmallerResolution_APanelIsPulledBackOnScreen()
    {
        var layout = PanelLayout.Empty;
        layout.Store("lineups", 2291 - 520, 1360 - 280, 2291, 1360); // bottom right corner at 2291 × 1360

        var small = layout.Resolve("lineups", LineupsDefault, 1600, 900);   // same fractions, same size: would overflow
        Assert.Equal(1600 - 520, small.Left, precision: 6);
        Assert.Equal(900 - 280, small.Top, precision: 6);

        layout.Store("skip-combat", -300, -50, 2291, 1360);                 // dropped past the top left edge
        var skip = layout.Resolve("skip-combat", TargetsDefault, 2291, 1360);
        Assert.Equal((0.0, 0.0), (skip.Left, skip.Top));
    }

    [Fact]
    public void APanelLargerThanTheOverlay_SticksToTheTopLeft()
    {
        var huge = new LayoutRect(800, 500, 1800, 1100);
        var rect = PanelLayout.Empty.Resolve("lineups", huge, 1600, 900);
        Assert.Equal((0.0, 0.0), (rect.Left, rect.Top));
    }

    // --- resizing: the size a panel was given, kept next to its place -------------------------------------------

    private static readonly (double Width, double Height) Minimum = (300, 120);

    private static LayoutRect At(double left, double top, double width, double height) =>
        new(left + width / 2, top + height / 2, width, height);

    private static (double Left, double Top, double Width, double Height) Round(LayoutRect r) =>
        (Math.Round(r.Left, 1), Math.Round(r.Top, 1), Math.Round(r.Width, 1), Math.Round(r.Height, 1));

    [Fact]
    public void ResizeThenReload_KeepsTheChosenSize_AsFractionsOfTheOverlay()
    {
        var layout = PanelLayout.Empty;
        layout.StoreRect("lineups", 1500, 68, 700, 400, 2291, 1360);

        var (reloaded, error) = PanelLayout.Parse(layout.Serialize());

        Assert.Null(error);
        Assert.True(reloaded.IsResized("lineups"));
        Assert.Equal((1500.0, 68.0, 700.0, 400.0), Round(reloaded.Resolve("lineups", LineupsDefault, 2291, 1360, Minimum)));
        // Another overlay size: the same fractions, so half the pixels at half the size.
        Assert.Equal((750.0, 34.0, 350.0, 200.0), Round(reloaded.Resolve("lineups", LineupsDefault, 1145.5, 680, Minimum)));
    }

    [Fact]
    public void AnInvalidSize_IsNeverWritten_SoTheFileStaysReadable()
    {
        var layout = PanelLayout.Empty;
        layout.StoreRect("lineups", 100, 50, 0, 400, 2000, 1000);
        layout.StoreRect("target-compositions", 100, 50, 700, -3, 2000, 1000);

        var (reloaded, error) = PanelLayout.Parse(layout.Serialize());

        Assert.Null(error);
        Assert.Equal(new[] { "lineups", "target-compositions" }, reloaded.MovedPanels.OrderBy(p => p));
        Assert.False(reloaded.IsResized("lineups"));
        Assert.False(reloaded.IsResized("target-compositions"));
    }

    [Fact]
    public void MovingAResizedPanel_KeepsItsSize()
    {
        var layout = PanelLayout.Empty;
        layout.StoreRect("lineups", 100, 50, 700, 400, 2000, 1000);

        layout.Store("lineups", 300, 80, 2000, 1000); // dragged somewhere else afterwards

        Assert.Equal((300.0, 80.0, 700.0, 400.0), Round(layout.Resolve("lineups", LineupsDefault, 2000, 1000, Minimum)));
    }

    [Fact]
    public void AFileWithoutSizes_ReadsAsBefore_AndKeepsTheDefaultSize()
    {
        var json = "{\"schema\": 1, \"panels\": {\"lineups\": {\"left\": 0.75, \"top\": 0.3}}}";

        var (layout, error) = PanelLayout.Parse(json);

        Assert.Null(error);
        Assert.False(layout.IsResized("lineups"));
        Assert.Equal((1800.0, 300.0, LineupsDefault.Width, LineupsDefault.Height), Round(layout.Resolve("lineups", LineupsDefault, 2400, 1000, Minimum)));
        Assert.DoesNotContain("width", layout.Serialize()); // a panel never resized writes no size: the file stays as it was
    }

    [Fact]
    public void ASizeBelowTheMinimum_IsRaisedToIt_AndOneAboveTheOverlay_IsCutToTheOverlay()
    {
        var layout = PanelLayout.Empty;
        layout.StoreRect("lineups", 100, 50, 100, 40, 2000, 1000);
        layout.StoreRect("target-compositions", 100, 50, 5000, 4000, 2000, 1000);

        var small = layout.Resolve("lineups", LineupsDefault, 2000, 1000, Minimum);
        var big = layout.Resolve("target-compositions", TargetsDefault, 2000, 1000, Minimum);

        Assert.Equal((100.0, 50.0, 300.0, 120.0), Round(small));
        Assert.Equal((0.0, 0.0, 2000.0, 1000.0), Round(big)); // the whole overlay, pulled back to its corner
    }

    [Fact]
    public void APanelThatCannotBeResized_IgnoresAStoredSize()
    {
        var layout = PanelLayout.Empty;
        layout.StoreRect("skip-combat", 100, 50, 700, 400, 2000, 1000);

        var rect = layout.Resolve("skip-combat", TargetsDefault, 2000, 1000); // no minimum: not resizable

        Assert.Equal((100.0, 50.0, TargetsDefault.Width, TargetsDefault.Height), Round(rect));
    }

    [Fact]
    public void Forget_ForgetsTheSizeToo()
    {
        var layout = PanelLayout.Empty;
        layout.StoreRect("lineups", 100, 50, 700, 400, 2400, 1000);

        layout.Forget("lineups");

        Assert.False(layout.IsResized("lineups"));
        Assert.Equal((LineupsDefault.Left, LineupsDefault.Top, LineupsDefault.Width, LineupsDefault.Height), Round(layout.Resolve("lineups", LineupsDefault, 2400, 1000, Minimum)));
    }

    [Theory]
    [InlineData("\"width\": 0.3")]                      // a width without a height
    [InlineData("\"width\": \"wide\", \"height\": 0.3")]
    [InlineData("\"width\": 0, \"height\": 0.3")]
    [InlineData("\"width\": 0.3, \"height\": -0.2")]
    public void ACorruptSize_GivesTheDefaultLayout_AndSaysSo(string size)
    {
        var json = "{\"schema\": 1, \"panels\": {\"lineups\": {\"left\": 0.1, \"top\": 0.1, " + size + "}}}";

        var (layout, error) = PanelLayout.Parse(json);

        Assert.Empty(layout.MovedPanels);
        Assert.StartsWith("layout.json:", error);
    }

    // Resize takes the panel's place from the layout itself, never from a rectangle the caller kept: a rectangle cached
    // at the last redraw is stale as soon as the panel is moved (a move ends without a redraw), and resizing from it
    // sent the panel back to where it was before the move.

    [Fact]
    public void Resize_TheCornerFollowsThePointer_TheTopLeftStays()
    {
        var layout = PanelLayout.Empty;
        layout.Store("lineups", 200, 100, 2000, 1000); // the panel sits at (200, 100)

        layout.Resize("lineups", LineupsDefault, cornerX: 1000, cornerY: 700, Minimum, 2000, 1000);

        Assert.Equal((200.0, 100.0, 800.0, 600.0), Round(layout.Resolve("lineups", LineupsDefault, 2000, 1000, Minimum)));
    }

    [Fact]
    public void Resize_OfAPanelNeverMoved_KeepsItsDefaultPlace()
    {
        var layout = PanelLayout.Empty;

        layout.Resize("lineups", LineupsDefault, cornerX: 2300, cornerY: 700, Minimum, 2400, 1000);

        // Default place: left 1640, top 160 on a 2400 × 1000 overlay.
        Assert.Equal((1640.0, 160.0, 660.0, 540.0), Round(layout.Resolve("lineups", LineupsDefault, 2400, 1000, Minimum)));
    }

    [Fact]
    public void Resize_AfterAMove_KeepsTheMovedPlace_NotTheDefaultOne()
    {
        var layout = PanelLayout.Empty;
        layout.Store("lineups", 200, 500, 2400, 1000); // moved away from its default place (1640, 160)

        layout.Resize("lineups", LineupsDefault, cornerX: 1000, cornerY: 900, Minimum, 2400, 1000);

        Assert.Equal((200.0, 500.0, 800.0, 400.0), Round(layout.Resolve("lineups", LineupsDefault, 2400, 1000, Minimum)));
    }

    [Fact]
    public void Resize_OfAPanelPulledBackOnScreen_StartsFromWhereItIsShown()
    {
        // A stored place that no longer fits (a smaller window, a hand-edited file): the panel is shown pulled back
        // inside, with the size it has, and the resize must start from that place, not from the stored one.
        var layout = PanelLayout.Empty;
        layout.StoreRect("lineups", 2300, 100, 700, 300, 2400, 1000); // 2300 + 700 does not fit in 2400: shown at 1700

        layout.Resize("lineups", LineupsDefault, cornerX: 2350, cornerY: 500, Minimum, 2400, 1000);

        Assert.Equal((1700.0, 100.0, 650.0, 400.0), Round(layout.Resolve("lineups", LineupsDefault, 2400, 1000, Minimum)));
    }

    [Fact]
    public void MoveAndResize_RepeatedThreeTimes_AlwaysStartFromWhereThePanelIs()
    {
        // Two rounds validate the transition; the third shows that nothing keeps an old place alive.
        var layout = PanelLayout.Empty;

        layout.Store("lineups", 200, 500, 2400, 1000);
        layout.Resize("lineups", LineupsDefault, cornerX: 1000, cornerY: 900, Minimum, 2400, 1000);
        Assert.Equal((200.0, 500.0, 800.0, 400.0), Round(layout.Resolve("lineups", LineupsDefault, 2400, 1000, Minimum)));

        layout.Store("lineups", 700, 50, 2400, 1000);
        layout.Resize("lineups", LineupsDefault, cornerX: 1500, cornerY: 400, Minimum, 2400, 1000);
        Assert.Equal((700.0, 50.0, 800.0, 350.0), Round(layout.Resolve("lineups", LineupsDefault, 2400, 1000, Minimum)));

        layout.Resize("lineups", LineupsDefault, cornerX: 1300, cornerY: 300, Minimum, 2400, 1000); // resized again, not moved
        Assert.Equal((700.0, 50.0, 600.0, 250.0), Round(layout.Resolve("lineups", LineupsDefault, 2400, 1000, Minimum)));

        layout.Store("lineups", 900, 600, 2400, 1000);                                              // moved again: the size stays
        Assert.Equal((900.0, 600.0, 600.0, 250.0), Round(layout.Resolve("lineups", LineupsDefault, 2400, 1000, Minimum)));
    }

    [Fact]
    public void Resize_StopsAtTheMinimum_AndAtTheOverlayEdge_WithoutMovingThePanel()
    {
        var layout = PanelLayout.Empty;
        layout.Store("lineups", 200, 100, 2000, 1000);

        layout.Resize("lineups", LineupsDefault, cornerX: 210, cornerY: 105, Minimum, 2000, 1000); // dragged past the top left
        Assert.Equal((200.0, 100.0, 300.0, 120.0), Round(layout.Resolve("lineups", LineupsDefault, 2000, 1000, Minimum)));

        layout.Resize("lineups", LineupsDefault, cornerX: 2600, cornerY: 1400, Minimum, 2000, 1000); // dragged out of the window
        Assert.Equal((200.0, 100.0, 1800.0, 900.0), Round(layout.Resolve("lineups", LineupsDefault, 2000, 1000, Minimum)));
    }

    [Fact]
    public void Resize_StoresTheClampedSize_NotOnlyShowsIt()
    {
        // Resolve raises any stored size to the minimum it is given, which would hide a Resize that stored less:
        // read the file back with a tiny minimum to see what was really kept.
        var layout = PanelLayout.Empty;
        layout.Store("lineups", 200, 100, 2000, 1000);
        var tiny = (Width: 1.0, Height: 1.0);

        layout.Resize("lineups", LineupsDefault, cornerX: 210, cornerY: 105, Minimum, 2000, 1000);
        Assert.Equal((200.0, 100.0, 300.0, 120.0), Round(layout.Resolve("lineups", LineupsDefault, 2000, 1000, tiny)));

        layout.Resize("lineups", LineupsDefault, cornerX: 2600, cornerY: 1400, Minimum, 2000, 1000);
        Assert.Equal((200.0, 100.0, 1800.0, 900.0), Round(layout.Resolve("lineups", LineupsDefault, 2000, 1000, tiny)));
    }

    [Fact]
    public void Resize_ACornerDraggedBeyondThePanelsOwnTopLeft_GivesTheMinimum_NotTheDefaultSize()
    {
        var layout = PanelLayout.Empty;
        layout.Store("lineups", 200, 100, 2000, 1000);

        layout.Resize("lineups", LineupsDefault, cornerX: 50, cornerY: 20, Minimum, 2000, 1000); // left of and above its top left corner

        Assert.True(layout.IsResized("lineups"), "the size was dropped: the panel would jump back to its default size");
        Assert.Equal((200.0, 100.0, 300.0, 120.0), Round(layout.Resolve("lineups", LineupsDefault, 2000, 1000, Minimum)));
    }
}
