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
