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

    [Theory]
    [InlineData(1600, 900)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    public void Rows_AtTheDefaultPlace_AsManyAsFitAboveTheGold_TheSameAtEveryHeight(double width, double height)
    {
        var s = TavernLayout.Scale(height);
        var top = TavernLayout.TargetPanel(width, height).Top;

        var rows = PanelFit.Rows(height, top, wanted: 8, footer: true, status: false);

        Assert.Equal(4, rows); // 8 wanted, 4 fit: the panel shows fewer lines, never smaller ones
        Assert.True(top + PanelFit.ListHeight(rows, true, false) * s <= PanelFit.BottomLimit * height + 1e-6, "the list reaches the gold");
        Assert.True(top + PanelFit.ListHeight(rows + 1, true, false) * s > PanelFit.BottomLimit * height, "one more line would still fit");
    }

    [Fact]
    public void Rows_FewerWhenThePanelSitsLower_NeverMoreThanWanted_NoneWhenNothingToShow()
    {
        const double height = 1080;
        var rows = new[] { 0.30, 0.50, 0.64, 0.75 }.Select(t => PanelFit.Rows(height, t * height, 8, true, false)).ToList();

        Assert.Equal(new[] { 8, 7, 4, 2 }, rows);
        Assert.Equal(2, PanelFit.Rows(height, 0.30 * height, wanted: 2, footer: true, status: false));
        Assert.Equal(0, PanelFit.Rows(height, 0.30 * height, wanted: 0, footer: true, status: false));
        Assert.Equal(1, PanelFit.Rows(height, 0.94 * height, wanted: 3, footer: true, status: false)); // no room: one line all the same
    }

    [Fact]
    public void DetailPivots_AsManyAsFitUnderTheTwoSections()
    {
        const double width = 1920, height = 1080;
        var top = TavernLayout.TargetPanel(width, height).Top;

        Assert.Equal(1, PanelFit.DetailPivots(height, top, pivots: 2));        // default place: one of two
        Assert.Equal(2, PanelFit.DetailPivots(height, 0.30 * height, pivots: 2)); // moved up: both
        Assert.Equal(0, PanelFit.DetailPivots(height, 0.75 * height, pivots: 2)); // moved down: none
        Assert.Equal(0, PanelFit.DetailPivots(height, 0.30 * height, pivots: 0));
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
        var shortOfABoard = new List<string>();
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

            var minimum = new LayoutRect(0, 0, PanelFit.LineupsMinWidth * s, PanelFit.LineupsMinHeight * s);
            if (PanelFit.OvalsPerLine(minimum.Width, height) != 7 || PanelFit.LineupCompositions(minimum, height, new[] { 7 }) != 1)
            {
                shortOfABoard.Add(height.ToString());
            }
        }

        Assert.Empty(shortOfARow);
        Assert.Empty(shortOfABoard);
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
        // 52 = 2 × 2 border + 2 × 8 padding + 32 title bar; 36 "← back" and name; 34 meta line; 2 × 69 sections.
        // Written as numbers on purpose: the other tests build their boxes from the constant itself.
        Assert.Equal(52 + 36 + 34 + 2 * 69, PanelFit.DetailMinHeight);

        foreach (var height in new[] { 1080.0, 1440.0 })
        {
            foreach (var topFraction in new[] { 0.20, 0.35, 0.50, 0.60, 0.70, 0.80 })
            {
                var top = topFraction * height;
                var room = (PanelFit.BottomLimit * height - top) / TavernLayout.Scale(height);
                var expected = Math.Max(0, Math.Min(5, (int)Math.Floor((room - 52 - 36 - 34 - 2 * 69) / 54)));
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
        Assert.Equal(PanelFit.PanelWidth, PanelFit.TargetMinWidth); // narrower than its seven ovals it cannot go
    }

    [Theory]
    [InlineData(900)]
    [InlineData(1080)]
    [InlineData(1440)]
    [InlineData(1600)]
    public void LineupsMinimum_ShowsAtLeastOneBoardOfSeven_AndNothingMore(double height)
    {
        var s = TavernLayout.Scale(height);
        var sevens = new[] { 7, 7, 7 };
        var minimum = new LayoutRect(0, 0, PanelFit.LineupsMinWidth * s, PanelFit.LineupsMinHeight * s);

        Assert.True(PanelFit.OvalsPerLine(minimum.Width, height) >= 7, "seven ovals do not fit a line of the minimum box");
        Assert.Equal(1, PanelFit.LineupCompositions(minimum, height, sevens));
        var oneLess = new LayoutRect(0, 0, minimum.Width, minimum.Height - 1);
        Assert.Equal(0, PanelFit.LineupCompositions(oneLess, height, sevens)); // the minimum is tight: no padding in it
        Assert.Equal(6, PanelFit.OvalsPerLine(minimum.Width - 1, height));
    }

    [Theory]
    [InlineData(1600, 900)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(1440, 1080)] // 4:3
    [InlineData(2560, 1080)] // 21:9
    public void LineupsPanel_ClearOfTheOtherPanels_AndOfTheGame(double width, double height)
    {
        var lineups = PanelFit.LineupsPanel(width, height);

        Assert.False(NoGoZones.Overlaps(lineups, TavernLayout.TargetPanel(width, height)), "covers the target composition panel");
        Assert.False(NoGoZones.Overlaps(lineups, SkipCombatLayout.Button(width, height)), "covers the Skip combat button");
        foreach (var (name, zone) in NoGoZones.For(width, height))
        {
            Assert.False(NoGoZones.Overlaps(lineups, zone), $"covers the {name}");
        }

        foreach (var count in new[] { 3, 5, 7 })
        {
            foreach (var button in TavernLayout.PinButtons(width, height, count).Concat(TavernLayout.LineupButtons(width, height, count)))
            {
                Assert.False(NoGoZones.Overlaps(lineups, button), $"{count} cards: covers a ◇ or ? button");
            }
        }

        Assert.True(lineups.Top >= 0.05 * height, "under HDT's top bar");
        Assert.True(lineups.Left >= 0 && lineups.Right <= width && lineups.Top + lineups.Height <= height, "outside the window");
        Assert.True(PanelFit.OvalsPerLine(lineups.Width, height) >= 4, "too narrow for four ovals a line");
        Assert.True(lineups.Height >= 0.4 * height, "too short");
    }

    [Fact]
    public void LineupCompositions_AsManyBoardsAsFit_WideWindowsFitMore()
    {
        var sevens = new[] { 7, 7, 7 };

        Assert.Equal(3, PanelFit.LineupCompositions(PanelFit.LineupsPanel(1920, 1080), 1080, sevens)); // one line of 7 ovals each
        Assert.Equal(2, PanelFit.LineupCompositions(PanelFit.LineupsPanel(1440, 1080), 1080, sevens)); // 4:3: two lines each
        Assert.Equal(9, PanelFit.OvalsPerLine(PanelFit.LineupsPanel(1920, 1080).Width, 1080));
        Assert.Equal(4, PanelFit.OvalsPerLine(PanelFit.LineupsPanel(1440, 1080).Width, 1080));
    }
}
