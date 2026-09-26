namespace BronzebeardHud.Stats.Tests;

public class PanelLayoutTests
{
    private static readonly LayoutRect TargetsDefault = new(600, 950, 300, 160);
    private static readonly LayoutRect CombatsDefault = new(1900, 300, 520, 280);

    [Fact]
    public void StoreThenReload_KeepsEachPanelWhereItWasDropped()
    {
        var layout = PanelLayout.Empty;
        layout.Store("target-compositions", 229.1, 870.4, 2291, 1360);
        layout.Store("combats", 1604, 68, 2291, 1360);
        layout.Store("trinkets", 10, 20, 2291, 1360);

        var (reloaded, error) = PanelLayout.Parse(layout.Serialize());

        Assert.Null(error);
        Assert.Equal(new[] { "combats", "target-compositions", "trinkets" }, reloaded.MovedPanels.OrderBy(p => p));
        var targets = reloaded.Resolve("target-compositions", TargetsDefault, 2291, 1360);
        Assert.Equal((229.1, 870.4), (Math.Round(targets.Left, 1), Math.Round(targets.Top, 1)));
        var combats = reloaded.Resolve("combats", CombatsDefault, 2291, 1360);
        Assert.Equal((1604.0, 68.0), (Math.Round(combats.Left, 1), Math.Round(combats.Top, 1)));
    }

    [Fact]
    public void ANeverMovedPanel_KeepsItsDefaultPlace()
    {
        var rect = PanelLayout.Empty.Resolve("combats", CombatsDefault, 2291, 1360);
        Assert.Equal((CombatsDefault.Left, CombatsDefault.Top, CombatsDefault.Width), (rect.Left, rect.Top, rect.Width));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{\"schema\": 2, \"panels\": {}}")]
    [InlineData("{\"schema\": 1, \"panels\": {\"combats\": {\"left\": \"far\"}}}")]
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
        layout.Store("combats", 2291 - 520, 1360 - 280, 2291, 1360); // bottom right corner at 2291 × 1360

        var small = layout.Resolve("combats", CombatsDefault, 1600, 900);   // same fractions, same size: would overflow
        Assert.Equal(1600 - 520, small.Left, precision: 6);
        Assert.Equal(900 - 280, small.Top, precision: 6);

        layout.Store("trinkets", -300, -50, 2291, 1360);                     // dropped past the top left edge
        var trinkets = layout.Resolve("trinkets", TargetsDefault, 2291, 1360);
        Assert.Equal((0.0, 0.0), (trinkets.Left, trinkets.Top));
    }

    [Fact]
    public void APanelLargerThanTheOverlay_SticksToTheTopLeft()
    {
        var huge = new LayoutRect(800, 500, 1800, 1100);
        var rect = PanelLayout.Empty.Resolve("combats", huge, 1600, 900);
        Assert.Equal((0.0, 0.0), (rect.Left, rect.Top));
    }
}
