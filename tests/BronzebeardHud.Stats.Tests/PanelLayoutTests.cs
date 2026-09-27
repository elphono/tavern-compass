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
}
