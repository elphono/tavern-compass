using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>How strong the board is against its hero's average at the same turn (<see cref="BoardPowerLevels"/>).</summary>
public enum BoardPower
{
    /// <summary>No colour: no curve, no average for the turn, or an average the data cannot back (<see cref="WarbandComparison.Note"/>).</summary>
    None,

    /// <summary>Red: more than half a turn of growth behind.</summary>
    Behind,

    /// <summary>Yellow: within half a turn of the average.</summary>
    Even,

    /// <summary>Green: half a turn to a turn ahead.</summary>
    Ahead,

    /// <summary>Shiny: a whole turn ahead or more, the average board of the next turn.</summary>
    Shiny,
}

/// <summary>
/// The levels of the board's power, in turns of growth of the average curve: Firestone's warband averages grow by about
/// <see cref="TurnGrowth"/> from one turn to the next mid-game, so a board half a turn behind is at 1/√1.8 of the average
/// (−25 %), half a turn ahead at √1.8 (+34 %), a turn ahead at 1.8 (+80 %). Measured on 2026-10-06 on the three cached
/// brackets (116 heroes each, turns 4 to 11): median growth 1.79 (mmr-100), 1.82 (mmr-50), 1.84 (mmr-25), quartiles 1.68 to
/// 2.06. No colour where the average cannot be trusted: under <see cref="MinAverageStats"/> (turns 1 and 2: one minion is the
/// whole difference), from the turn the curve stops growing (fewer and fewer games reach late turns: on that day the curves
/// first fell at turn 15 to 18 for most heroes, as early as turn 5 in a thin bracket, and Ali's boards read +248 % to
/// +4885 % past turn 14), and for a hero with fewer than <see cref="MinHeroGames"/> games in the bracket (Firestone gives no
/// count per turn; 78 is the fewest measured, in mmr-25 — a judgement, not a measure).
/// </summary>
public static class BoardPowerLevels
{
    public const double TurnGrowth = 1.8;
    public static readonly double BehindBelow = 1 / Math.Sqrt(TurnGrowth);
    public static readonly double AheadFrom = Math.Sqrt(TurnGrowth);
    public const double ShinyFrom = TurnGrowth;

    public const int MinHeroGames = 100;
    public const double MinAverageStats = 12;

    /// <summary>The level of a board <paramref name="ratio"/> times its hero's average.</summary>
    public static BoardPower Of(double ratio) =>
        ratio < BehindBelow ? BoardPower.Behind
        : ratio < AheadFrom ? BoardPower.Even
        : ratio < ShinyFrom ? BoardPower.Ahead
        : BoardPower.Shiny;

    /// <summary>
    /// "#RRGGBB": red, yellow, green, and a pale gold for shiny (with its star and the fourth place of the gauge, it is told
    /// from the yellow); a grey for none. Black text reads on each.
    /// </summary>
    public static string Colour(BoardPower power) => power switch
    {
        BoardPower.Behind => "#FF6B6B",
        BoardPower.Even => "#FFD43B",
        BoardPower.Ahead => "#3DDC84",
        BoardPower.Shiny => "#FFF4C2",
        _ => "#8A8F99",
    };

    /// <summary>The level's sign, so that it never rests on the colour alone.</summary>
    public static string Symbol(BoardPower power) => power switch
    {
        BoardPower.Behind => "▼",
        BoardPower.Even => "≈",
        BoardPower.Ahead => "▲",
        BoardPower.Shiny => "★",
        _ => "–",
    };

    /// <summary>"behind", "even", "ahead", "shiny", "none": for the log line.</summary>
    public static string Name(BoardPower power) => power.ToString().ToLowerInvariant();

    /// <summary>The four coloured levels, in gauge order.</summary>
    public static readonly IReadOnlyList<BoardPower> Gauge = new[] { BoardPower.Behind, BoardPower.Even, BoardPower.Ahead, BoardPower.Shiny };
}

/// <summary>The player's board against the average board of the same hero at the same turn.</summary>
public sealed class WarbandComparison
{
    public WarbandComparison(int turn, int boardStats, double? average, string line, BoardPower power = BoardPower.None, string? details = null,
        string? percent = null, string? note = null)
    {
        Turn = turn;
        BoardStats = boardStats;
        Average = average;
        Line = line;
        Power = power;
        Details = details ?? line;
        Percent = percent;
        Note = note;
    }

    public int Turn { get; }
    public int BoardStats { get; }

    /// <summary>The hero's average at this turn; null when no source has one.</summary>
    public double? Average { get; }

    /// <summary>The whole line, for HDT's log: "Board 142 · hero avg 120 at turn 8 · +18%".</summary>
    public string Line { get; }

    /// <summary>The level; <see cref="BoardPower.None"/> when the data cannot say (<see cref="Note"/> says why, when there is an average).</summary>
    public BoardPower Power { get; }

    /// <summary>What the indicator writes beside its badge: the line without the percentage, "Board 142 · hero avg 120 at turn 8".</summary>
    public string Details { get; }

    /// <summary>"+18%", "−33%"; null without an average above 0.</summary>
    public string? Percent { get; }

    /// <summary>Why there is no colour although there is an average: "too early", "few games (78)", "curve falls after turn 16".</summary>
    public string? Note { get; }

    /// <summary>"power=ahead", "power=none (too early)": appended to the log line.</summary>
    public string PowerText => "power=" + BoardPowerLevels.Name(Power) + (Note != null ? $" ({Note})" : string.Empty);
}

/// <summary>
/// Firestone's warbandStats give, per hero and turn, the average sum of attack and health of the player's
/// minions at that turn's combat. Set against the player's own board, they tell whether to push stats or
/// level: a board below its hero's curve loses more than it should. Nothing is extrapolated: a turn the
/// curve does not reach is said so. The level (<see cref="BoardPowerLevels"/>) colours the panel's indicator.
/// </summary>
public static class WarbandCurve
{
    /// <summary>Attack plus health of every minion on the board, Firestone's measure.</summary>
    public static int BoardStats(IEnumerable<(int Attack, int Health)> minions) => minions.Sum(m => Math.Max(0, m.Attack) + Math.Max(0, m.Health));

    /// <param name="sources">Stats files, most trusted first; the first one with a curve for the hero is used.</param>
    public static WarbandComparison Compare(int turn, int boardStats, string heroCardId, IReadOnlyList<HeroStatsFile> sources)
    {
        var hero = sources.Select(s => s.Find(heroCardId)).FirstOrDefault(h => h is { WarbandCurve.Count: > 0 });
        var board = $"Board {boardStats}";
        if (hero == null)
        {
            return new WarbandComparison(turn, boardStats, null, $"{board} · no curve for this hero");
        }

        var curve = hero.WarbandCurve;
        var point = curve.FirstOrDefault(p => p.Turn == turn);
        if (point == null)
        {
            return new WarbandComparison(turn, boardStats, null, $"{board} · no average for turn {turn}");
        }

        var inv = CultureInfo.InvariantCulture;
        var details = $"{board} · hero avg {point.AverageStats.ToString("0", inv)} at turn {turn}";
        if (point.AverageStats <= 0)
        {
            return new WarbandComparison(turn, boardStats, point.AverageStats, details, note: "too early");
        }

        var ratio = boardStats / point.AverageStats;
        var delta = (ratio - 1) * 100;
        var percent = $"{(delta >= 0 ? "+" : "−")}{Math.Abs(delta).ToString("0", inv)}%";
        var line = $"{details} · {percent}";
        var note = point.AverageStats < BoardPowerLevels.MinAverageStats ? "too early"
            : FallsFrom(curve) is { } fall && turn >= fall ? $"curve falls after turn {fall - 1}"
            : hero.DataPoints < BoardPowerLevels.MinHeroGames ? $"few games ({hero.DataPoints.ToString(inv)})"
            : null;
        var power = note == null ? BoardPowerLevels.Of(ratio) : BoardPower.None;
        return new WarbandComparison(turn, boardStats, point.AverageStats, line, power, details, percent, note);
    }

    /// <summary>The first turn whose average is below the turn before's (in turn order); null when the curve never falls.</summary>
    private static int? FallsFrom(IReadOnlyList<WarbandPoint> curve)
    {
        var ordered = curve.OrderBy(p => p.Turn).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].AverageStats < ordered[i - 1].AverageStats)
            {
                return ordered[i].Turn;
            }
        }

        return null;
    }
}
