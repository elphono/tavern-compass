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

    // Written out in literal numbers on purpose, the oval's aspect aside (TavernLayout.OvalAspect, which may change on its
    // own): 72 = 2 × 2 border + 2 × 8 padding + 32 title bar + 20 footer; a guide line is one 54-wide oval tall, 6 apart.
    private const double ListChrome = 72;
    private static double LinePitch => 54 * TavernLayout.OvalAspect + 6;

    /// <summary>Lines of the list that fit in <paramref name="room"/> design pixels, footer included, wanted 8.</summary>
    private static int LinesIn(double room) => Math.Max(1, Math.Min(8, (int)Math.Floor((room - ListChrome + 6) / LinePitch)));

    [Theory]
    [InlineData(1600, 900)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    public void Rows_AtTheDefaultPlace_AsManyAsFitAboveTheGold_TheSameAtEveryHeight(double width, double height)
    {
        var s = TavernLayout.Scale(height);
        var top = TavernLayout.TargetPanel(width, height).Top;

        var rows = PanelFit.Rows(height, top, wanted: 8, footer: true, status: false);

        Assert.Equal(LinesIn((PanelFit.BottomLimit * height - top) / s), rows); // 8 wanted, fewer fit: fewer lines, never smaller ones
        Assert.True(rows >= 3, $"{rows}: the default place is sized for three guides");
        Assert.True(top + PanelFit.ListHeight(rows, true, false) * s <= PanelFit.BottomLimit * height + 1e-6, "the list reaches the gold");
        Assert.True(top + PanelFit.ListHeight(rows + 1, true, false) * s > PanelFit.BottomLimit * height, "one more line would still fit");
    }

    [Fact]
    public void Rows_FewerWhenThePanelSitsLower_NeverMoreThanWanted_NoneWhenNothingToShow()
    {
        const double height = 1080;
        var tops = new[] { 0.30, 0.50, 0.64, 0.75 };
        var rows = tops.Select(t => PanelFit.Rows(height, t * height, 8, true, false)).ToList();

        Assert.Equal(tops.Select(t => LinesIn((PanelFit.BottomLimit - t) * height)), rows);
        Assert.Equal(rows.OrderByDescending(r => r), rows);
        Assert.True(rows[0] > rows[3], "the lowest place shows as many lines as the highest");
        Assert.Equal(2, PanelFit.Rows(height, 0.30 * height, wanted: 2, footer: true, status: false));
        Assert.Equal(0, PanelFit.Rows(height, 0.30 * height, wanted: 0, footer: true, status: false));
        Assert.Equal(1, PanelFit.Rows(height, 0.94 * height, wanted: 3, footer: true, status: false)); // no room: one line all the same
    }

    [Fact]
    public void DetailPivots_AsManyAsFitUnderTheTwoSections()
    {
        const double width = 1920, height = 1080;
        var top = TavernLayout.TargetPanel(width, height).Top;
        var atDefault = (int)Math.Floor((PanelFit.BottomLimit * height - top - PanelFit.DetailMinHeight) / PanelFit.PivotLine);

        Assert.Equal(Math.Max(0, Math.Min(2, atDefault)), PanelFit.DetailPivots(height, top, pivots: 2)); // default place: what fits under the sections
        Assert.Equal(2, PanelFit.DetailPivots(height, 0.30 * height, pivots: 2)); // moved up: both
        Assert.Equal(0, PanelFit.DetailPivots(height, 0.75 * height, pivots: 2)); // moved down: none
        Assert.Equal(0, PanelFit.DetailPivots(height, 0.30 * height, pivots: 0));
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

    // --- resizing: the room a box gives is the room the content gets ------------------------------------------------

    [Theory]
    [InlineData(900)]
    [InlineData(1080)]
    [InlineData(1440)]
    public void Rows_InAChosenBox_ShowsExactlyTheLinesThatFit_WhateverTheHeight(double height)
    {
        var s = TavernLayout.Scale(height);
        var top = 0.30 * height;
        int RowsIn(double boxHeight) => PanelFit.Rows(height, top, wanted: 8, footer: true, status: false, bottom: top + boxHeight);

        var byLines = Enumerable.Range(1, 5).Select(n => RowsIn(PanelFit.ListHeight(n, true, false) * s)).ToList();

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, byLines);
        Assert.Equal(2, RowsIn(PanelFit.ListHeight(3, true, false) * s - 1)); // a pixel short of three lines: two
        Assert.Equal(1, RowsIn(10));                                          // no room at all: one line all the same
        Assert.Equal(2, PanelFit.Rows(height, top, wanted: 2, footer: true, status: false, bottom: top + 5000)); // never more than wanted
        Assert.Equal(0, PanelFit.Rows(height, top, wanted: 0, footer: true, status: false, bottom: top + 5000));
    }

    [Fact]
    public void Boxes_ExactlyTheSizeOfTheirContent_HoldIt_AtEveryWindowHeight()
    {
        // A box dragged to exactly the height of n lines must show n lines, not n - 1 because of a rounding error
        // in a division by the scale: the sweep catches the heights where the scale is not a binary fraction.
        var shortOfARow = new List<string>();
        var shortOfAPivot = new List<string>();
        var shortOfAnOval = new List<string>();
        for (var height = 600; height <= 2200; height++)
        {
            var s = TavernLayout.Scale(height);
            var top = 0.30 * height;
            foreach (var n in new[] { 1, 2, 3, 4, 5 })
            {
                var rows = PanelFit.Rows(height, top, wanted: 8, footer: true, status: false, bottom: top + PanelFit.ListHeight(n, true, false) * s);
                if (rows != n)
                {
                    shortOfARow.Add($"{height}:{n}->{rows}");
                }
            }

            foreach (var n in new[] { 1, 2, 3 })
            {
                var pivots = PanelFit.DetailPivots(height, top, pivots: 5, bottom: top + (PanelFit.DetailMinHeight + n * PanelFit.PivotLine) * s);
                if (pivots != n)
                {
                    shortOfAPivot.Add($"{height}:{n}->{pivots}");
                }
            }

            foreach (var n in new[] { 1, PanelFit.CoreOvalsPerRow, 7 })
            {
                var ovals = PanelFit.OvalsPerLine((2 * PanelFit.Border + 2 * PanelFit.Padding + n * (PanelFit.OvalWidth + PanelFit.OvalGap)) * s, height);
                if (ovals != n)
                {
                    shortOfAnOval.Add($"{height}:{n}->{ovals}");
                }
            }
        }

        Assert.Empty(shortOfARow);
        Assert.Empty(shortOfAPivot);
        Assert.Empty(shortOfAnOval);
    }

    [Fact]
    public void Rows_WithNoChosenBox_StillStopAboveTheGold()
    {
        const double height = 1080;
        var top = 0.30 * height;

        Assert.Equal(PanelFit.Rows(height, top, 8, true, false), PanelFit.Rows(height, top, 8, true, false, bottom: null));
        Assert.Equal(PanelFit.Rows(height, top, 8, true, false), PanelFit.Rows(height, top, 8, true, false, bottom: PanelFit.BottomLimit * height));
    }

    [Theory]
    [InlineData(900)]
    [InlineData(1440)]
    public void DetailPivots_InAChosenBox_ShowsThePivotsThatFitUnderTheFixedPart(double height)
    {
        var s = TavernLayout.Scale(height);
        var top = 0.30 * height;
        int PivotsIn(double boxHeight) => PanelFit.DetailPivots(height, top, pivots: 3, bottom: top + boxHeight);

        Assert.Equal(0, PivotsIn(PanelFit.DetailMinHeight * s));
        Assert.Equal(1, PivotsIn((PanelFit.DetailMinHeight + PanelFit.PivotLine) * s));
        Assert.Equal(1, PivotsIn((PanelFit.DetailMinHeight + 2 * PanelFit.PivotLine) * s - 1));
        Assert.Equal(2, PivotsIn((PanelFit.DetailMinHeight + 2 * PanelFit.PivotLine) * s));
        Assert.Equal(0, PivotsIn(10));
    }

    [Fact]
    public void DetailMinHeight_IsWhatTheDetailAlwaysShows_WrittenOutInLiteralNumbers()
    {
        // 52 = 2 × 2 border + 2 × 8 padding + 32 title bar; 36 "← back" and name; 34 meta line; 2 sections, each a 21 px
        // title and a line of 54-wide ovals; a pivot is 4 px and such a line. Written as numbers on purpose (the oval's
        // aspect aside, which may change on its own): the other tests build their boxes from the constants themselves.
        var oval = 54 * TavernLayout.OvalAspect;
        var fixedPart = 52 + 36 + 34 + 2 * (21 + oval);
        Assert.Equal(fixedPart, PanelFit.DetailMinHeight, precision: 9);
        Assert.Equal(4 + oval, PanelFit.PivotLine, precision: 9);

        foreach (var height in new[] { 1080.0, 1440.0 })
        {
            foreach (var topFraction in new[] { 0.20, 0.35, 0.50, 0.60, 0.70, 0.80 })
            {
                var top = topFraction * height;
                var room = (PanelFit.BottomLimit * height - top) / TavernLayout.Scale(height);
                var expected = Math.Max(0, Math.Min(5, (int)Math.Floor((room - fixedPart) / (4 + oval))));
                Assert.Equal(expected, PanelFit.DetailPivots(height, top, pivots: 5));
            }
        }
    }

    [Theory]
    [InlineData(900)]
    [InlineData(1440)]
    public void TargetMinimum_HoldsTheTitleAndOneLine_AndNothingMore(double height)
    {
        var s = TavernLayout.Scale(height);
        var top = 0.30 * height;

        var rows = PanelFit.Rows(height, top, wanted: 8, footer: false, status: false, bottom: top + PanelFit.TargetMinHeight * s);

        Assert.Equal(1, rows);
        Assert.True(PanelFit.ListHeight(rows, false, false) <= PanelFit.TargetMinHeight + 1e-9, "the one line does not fit the minimum box");
        Assert.True(PanelFit.TargetMinHeight < PanelFit.ListHeight(2, false, false), "the minimum box has room for a second line");
        Assert.Equal(PanelFit.PanelWidth, PanelFit.TargetMinWidth); // narrower than its six ovals it cannot go
        Assert.Equal(PanelFit.CoreOvalsPerRow, PanelFit.OvalsPerLine(PanelFit.TargetMinWidth * s - PanelFit.BoxColumn * s - PanelFit.NameColumn * s, height));
    }
}
