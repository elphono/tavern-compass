namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// The power inset under the panel (2026-10-06): its lamps and their glow, what − and + may do, how many lines the panel is
/// sized for, and its log line.
/// </summary>
public class PowerInsetTests
{
    [Fact]
    public void Lamps_OneLitPerLevel_AtItsPlaceFromRedToGold_NoneWithoutALevel()
    {
        Assert.Equal(new[] { 0, 1, 2, 3, -1 },
            new[] { BoardPower.Behind, BoardPower.Even, BoardPower.Ahead, BoardPower.Shiny, BoardPower.None }.Select(BoardPowerLevels.LitLamp));
    }

    [Fact]
    public void Halo_TheLevelsOwnColour_ADeepGoldForShiny_NothingWithoutALevel()
    {
        Assert.Equal("#FF6B6B", BoardPowerLevels.Halo(BoardPower.Behind));
        Assert.Equal("#FFD43B", BoardPowerLevels.Halo(BoardPower.Even));
        Assert.Equal("#3DDC84", BoardPowerLevels.Halo(BoardPower.Ahead));
        Assert.Equal("#FFC400", BoardPowerLevels.Halo(BoardPower.Shiny)); // the pale gold lamp (#FFF4C2) glows in deep gold
        Assert.Null(BoardPowerLevels.Halo(BoardPower.None));
        Assert.Equal(4, BoardPowerLevels.Gauge.Select(BoardPowerLevels.Halo).Distinct().Count());
    }

    private static CompTarget Target(int rank, TargetKind kind) =>
        new(new CompGuideProgress(GuideTestData.Guide($"G{rank}", tier: 1, tierRank: rank, core: new[] { $"C{rank}" }), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), 3),
            rank, CompTargetTracker.Palette[rank - 1], kind);

    /// <summary>
    /// The panel is sized for the N lines − n + set, and for every ticked guide when more are ticked (ticked while fewer were
    /// wanted). With as many ticks as wanted it lists them alone (CompTargets.Listed); + shows the others again.
    /// </summary>
    [Theory]
    [InlineData(new TargetKind[0], 3, 3)]                                                      // nothing ticked: the number wanted
    [InlineData(new[] { TargetKind.Probable }, 4, 4)]                                         // one guess, four wanted: four lines
    [InlineData(new[] { TargetKind.Probable, TargetKind.Probable }, 1, 1)]
    [InlineData(new[] { TargetKind.Chosen, TargetKind.InProgress }, 4, 4)]                    // ticked: still the N lines
    [InlineData(new[] { TargetKind.Chosen }, 3, 3)]
    [InlineData(new[] { TargetKind.Chosen, TargetKind.Chosen, TargetKind.Chosen }, 2, 3)]       // more ticked than N: all of them
    [InlineData(new[] { TargetKind.Chosen, TargetKind.InProgress, TargetKind.InProgress }, 1, 1)] // in progress never widens it
    [InlineData(new TargetKind[0], 9, 4)]                                                      // out of range: brought inside
    [InlineData(new TargetKind[0], 0, 1)]
    public void FitRows_TheNumberWanted_OrEveryTickedGuide(TargetKind[] kinds, int count, int expected)
    {
        var targets = kinds.Select((k, i) => Target(i + 1, k)).ToList();

        Assert.Equal(expected, CompTargets.FitRows(targets, count));
    }

    private static readonly LayoutRect Inset = new(1181 + 244, 994 + 21, 488, 42);

    [Fact]
    public void Line_SaysWhereAndWhatEachRowShows()
    {
        var sources = new[] { new HeroStatsFile(StatsSources.Firestone, new[] { new HeroStat("H", 4, 1000, warbandCurve: new[] { new WarbandPoint(2, 6), new WarbandPoint(8, 120) }) }) };
        var ahead = WarbandCurve.Compare(8, 190, "H", sources);
        var early = WarbandCurve.Compare(2, 3, "H", sources);

        Assert.Equal("Bronzebeard HUD: power inset at (1181,994 488x42): you ▲ +58% (ahead) · opp – (none: too early)", PowerInset.Line(Inset, ahead, early));
        Assert.Equal("Bronzebeard HUD: power inset at (1181,994 488x42): you – (none) · opp off", PowerInset.Line(Inset, WarbandCurve.Compare(8, 1, "X", sources), null));
    }
}
