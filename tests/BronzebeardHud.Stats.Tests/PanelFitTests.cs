namespace BronzebeardHud.Stats.Tests;

public class PanelFitTests
{
    [Fact]
    public void Floor_EveryFontOfThePanels_IsAtLeastTwelvePixels_At1080p()
    {
        var smallest = PanelTypography.All.Min();

        Assert.True(smallest * TavernLayout.Scale(1080) >= 12, $"smallest font {smallest} px at 1080p");
        Assert.Equal(PanelTypography.Floor, smallest);
        Assert.Equal(12, PanelTypography.Px(PanelTypography.Floor, 1080), precision: 9);
    }

    [Fact]
    public void Floor_TheHeroBadgesTheMmrLabelsAndTheChoiceMarkers_UseTheSizesOfTheFloor()
    {
        var sizes = new[] { PanelTypography.HeroTier, PanelTypography.HeroNoData, PanelTypography.Body, PanelTypography.Small, PanelTypography.Marker };

        Assert.All(sizes, size => Assert.Contains(size, PanelTypography.All));
        Assert.All(sizes, size => Assert.True(PanelTypography.Px(size, 1080) >= 12, $"{size} px at 1080p"));
    }

    /// <summary>
    /// The plugin never shrinks text to make it fit (no Viewbox) and takes every font size from PanelTypography
    /// (no FontSize written as a number): the floor holds only if nothing gets around it. Reads the plugin's sources,
    /// which the test project does not compile (the plugin targets net48 and HDT).
    /// </summary>
    [Fact]
    public void Floor_PluginSources_ShrinkNoText_AndWriteNoFontSizeAsANumber()
    {
        var sources = Directory.GetFiles(Path.Combine(RepositoryRoot(), "src", "BronzebeardHud.HdtPlugin"), "*.cs");
        var literalSize = new System.Text.RegularExpressions.Regex(@"FontSize\s*=\s*[0-9]");

        Assert.True(sources.Length >= 10, $"only {sources.Length} plugin sources found");
        foreach (var file in sources)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var where = $"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}";
                Assert.False(lines[i].Contains("Viewbox"), where);
                Assert.False(literalSize.IsMatch(lines[i]), where);
            }
        }
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BronzebeardHud.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException($"BronzebeardHud.sln not found above {AppContext.BaseDirectory}");
    }

    /// <summary>
    /// The list height that sizes the default box (TavernLayout.TargetPanel) and the resize minimum, written out in literal
    /// numbers on purpose, the oval's aspect aside (TavernLayout.OvalAspect, which may change on its own): 52 = 2 × 2 border
    /// + 2 × 8 padding + 32 title bar; a footer 20, a status line 16; a line one 54-wide oval tall, 6 apart.
    /// </summary>
    [Fact]
    public void ListHeight_WrittenOutInLiteralNumbers()
    {
        var oval = 54 * TavernLayout.OvalAspect;

        Assert.Equal(52 + oval, PanelFit.ListHeight(1, footer: false, status: false), precision: 9);
        Assert.Equal(52 + 20 + 3 * oval + 2 * 6, PanelFit.ListHeight(3, footer: true, status: false), precision: 9);
        Assert.Equal(52 + 16 + 2 * oval + 6, PanelFit.ListHeight(2, footer: false, status: true), precision: 9);
        Assert.Equal(52 + 20, PanelFit.ListHeight(0, footer: true, status: false), precision: 9); // no line, no gap
    }

    [Fact]
    public void AGuideLine_ATickBoxANameAndSixOvals_InTheWidthThePanelHadWithSevenSmallerOnes()
    {
        Assert.Equal(54, PanelFit.OvalWidth);
        Assert.Equal(6, PanelFit.CoreOvalsPerRow);
        Assert.Equal(PanelFit.OvalHeight, PanelFit.RowHeight); // a line is one oval tall
        Assert.Equal(54 * TavernLayout.OvalAspect, PanelFit.OvalHeight, precision: 9);
        Assert.Equal(2 * 2 + 2 * 8 + PanelFit.BoxColumn + PanelFit.NameColumn + 6 * (54 + 4), PanelFit.PanelWidth);
        Assert.True(PanelFit.PanelWidth <= 1.1 * 488, $"{PanelFit.PanelWidth}: more than 10 % wider than the panel of seven ovals");
        Assert.True(PanelFit.NameColumn >= 90, $"{PanelFit.NameColumn}: no room left for a guide's name");
    }

    [Fact]
    public void TargetMinimum_HoldsTheTitleAndOneLine_AndNothingMore()
    {
        Assert.Equal(PanelFit.ListHeight(1, footer: false, status: false), PanelFit.TargetMinHeight, precision: 9);
        Assert.True(PanelFit.TargetMinHeight < PanelFit.ListHeight(2, false, false), "the minimum box has room for a second line");
        Assert.Equal(PanelFit.PanelWidth, PanelFit.TargetMinWidth); // narrower than its six ovals it cannot go
    }
}
