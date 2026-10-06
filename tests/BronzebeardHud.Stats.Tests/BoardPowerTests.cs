using System.Globalization;

namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// The board's power against the hero's average at the same turn, as a level (BoardPower): red, yellow, green, shiny, or no
/// colour when the data cannot say. Synthetic curves only (no Firestone data in the repository).
/// </summary>
public class BoardPowerTests
{
    private const string Hero = "TST_HERO";

    /// <summary>A curve that grows ×1.8 a turn from 6 at turn 2, as Firestone's averages do mid-game; turn 8 is 120 (rounded).</summary>
    private static HeroStatsFile File(int games = 1000, params (int Turn, double Average)[] curve)
    {
        var points = curve.Length > 0
            ? curve.Select(p => new WarbandPoint(p.Turn, p.Average)).ToList()
            : new List<WarbandPoint> { new(1, 4), new(2, 6), new(3, 11), new(4, 19), new(5, 35), new(6, 37), new(7, 67), new(8, 120), new(9, 216), new(10, 389) };
        return new HeroStatsFile(StatsSources.Firestone, new[] { new HeroStat(Hero, 4.1, games, warbandCurve: points) });
    }

    [Theory]
    [InlineData(0.20, BoardPower.Behind)]
    [InlineData(0.745, BoardPower.Behind)]   // just under half a turn behind
    [InlineData(0.746, BoardPower.Even)]
    [InlineData(1.0, BoardPower.Even)]
    [InlineData(1.341, BoardPower.Even)]
    [InlineData(1.342, BoardPower.Ahead)]    // half a turn ahead
    [InlineData(1.799, BoardPower.Ahead)]
    [InlineData(1.80, BoardPower.Shiny)]     // a whole turn ahead: the average board of the next turn
    [InlineData(9.0, BoardPower.Shiny)]
    public void Levels_AreHalfATurnAndATurnOfTheCurvesGrowth(double ratio, BoardPower expected) =>
        Assert.Equal(expected, BoardPowerLevels.Of(ratio));

    [Fact]
    public void TheThresholds_WrittenOut_ComeFromAGrowthOf1Point8ATurn()
    {
        // 1.8: the median turn-over-turn growth of Firestone's warband averages, turns 4 to 11, 116 heroes, measured on
        // 2026-10-06 in Ali's cache: 1.79 (mmr-100), 1.82 (mmr-50), 1.84 (mmr-25); quartiles 1.68 to 2.06.
        Assert.Equal(1.8, BoardPowerLevels.TurnGrowth);
        Assert.Equal("-25% +34% +80%", string.Join(" ", new[] { BoardPowerLevels.BehindBelow, BoardPowerLevels.AheadFrom, BoardPowerLevels.ShinyFrom }
            .Select(r => ((r - 1) * 100).ToString("+0;-0", CultureInfo.InvariantCulture) + "%")));
        Assert.Equal(100, BoardPowerLevels.MinHeroGames);
        Assert.Equal(12, BoardPowerLevels.MinAverageStats);
    }

    [Fact]
    public void FourBoards_OneCurve_FourLevels_TheNumbersStayReadable()
    {
        var sources = new[] { File() };

        var levels = new[] { 80, 130, 190, 260 }.Select(board => WarbandCurve.Compare(8, board, Hero, sources)).ToList();

        Assert.Equal(new[] { BoardPower.Behind, BoardPower.Even, BoardPower.Ahead, BoardPower.Shiny }, levels.Select(l => l.Power));
        Assert.Equal(new[] { "−33%", "+8%", "+58%", "+117%" }, levels.Select(l => l.Percent));
        Assert.Equal("Board 190 · hero avg 120 at turn 8", levels[2].Details);
        Assert.Equal("Board 190 · hero avg 120 at turn 8 · +58%", levels[2].Line); // the line of before, unchanged
        Assert.All(levels, l => Assert.Null(l.Note));
        Assert.Equal("power=ahead", levels[2].PowerText);
    }

    [Fact]
    public void WithoutData_NoColourIsInvented_AndTheReasonIsSaid()
    {
        var sources = new[] { File() };
        var thin = new[] { File(games: 78) };
        var falling = new[] { File(1000, (13, 3400), (14, 4500), (15, 6300), (16, 7400), (17, 6200), (18, 2000)) };

        var noCurve = WarbandCurve.Compare(8, 140, "OTHER_HERO", sources);
        var noTurn = WarbandCurve.Compare(11, 140, Hero, sources);
        var early = WarbandCurve.Compare(2, 3, Hero, sources);
        var few = WarbandCurve.Compare(8, 260, Hero, thin);
        var fallen = WarbandCurve.Compare(17, 9000, Hero, falling);
        var after = WarbandCurve.Compare(18, 9000, Hero, falling);
        var before = WarbandCurve.Compare(16, 9000, Hero, falling);

        Assert.All(new[] { noCurve, noTurn, early, few, fallen, after }, c => Assert.Equal(BoardPower.None, c.Power));
        Assert.Equal((null, "Board 140 · no curve for this hero"), (noCurve.Percent, noCurve.Details));
        Assert.Equal((null, "Board 140 · no average for turn 11"), (noTurn.Percent, noTurn.Details));
        Assert.Equal("too early", early.Note);                 // 6 stats at turn 2: one minion is the whole difference
        Assert.Equal("few games (78)", few.Note);
        Assert.Equal("curve falls after turn 16", fallen.Note);
        Assert.Equal("curve falls after turn 16", after.Note);
        Assert.Equal(BoardPower.Even, before.Power);           // up to turn 16 the curve still grows: a colour (9000 / 7400)
        Assert.Equal("power=none (too early)", early.PowerText);
        Assert.Equal("power=none", noCurve.PowerText);
    }

    [Fact]
    public void Colours_FourDistinct_BlackTextReadsOnEach_AndEachHasItsSymbol()
    {
        static (double R, double G, double B) Rgb(string hex) => (
            int.Parse(hex.Substring(1, 2), NumberStyles.HexNumber), int.Parse(hex.Substring(3, 2), NumberStyles.HexNumber), int.Parse(hex.Substring(5, 2), NumberStyles.HexNumber));
        static double Luminance(string hex)
        {
            static double Channel(double c) => (c /= 255) <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            var (r, g, b) = Rgb(hex);
            return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
        }
        static double Distance(string a, string b)
        {
            var (r1, g1, b1) = Rgb(a);
            var (r2, g2, b2) = Rgb(b);
            return Math.Sqrt((r1 - r2) * (r1 - r2) + (g1 - g2) * (g1 - g2) + (b1 - b2) * (b1 - b2));
        }

        var levels = new[] { BoardPower.Behind, BoardPower.Even, BoardPower.Ahead, BoardPower.Shiny };
        var colours = levels.Select(BoardPowerLevels.Colour).ToList();
        Assert.Equal(4, colours.Distinct().Count());
        foreach (var colour in colours)
        {
            Assert.True((Luminance(colour) + 0.05) / 0.05 >= 7, $"{colour}: black text contrast {(Luminance(colour) + 0.05) / 0.05:0.0}");
        }

        for (var i = 0; i < colours.Count; i++)
        {
            for (var j = i + 1; j < colours.Count; j++)
            {
                Assert.True(Distance(colours[i], colours[j]) > 100, $"{colours[i]} and {colours[j]} too close");
            }
        }

        Assert.Equal(new[] { "▼", "≈", "▲", "★", "–" }, levels.Append(BoardPower.None).Select(BoardPowerLevels.Symbol));
        Assert.Equal(new[] { "behind", "even", "ahead", "shiny", "none" }, levels.Append(BoardPower.None).Select(BoardPowerLevels.Name));
    }
}
