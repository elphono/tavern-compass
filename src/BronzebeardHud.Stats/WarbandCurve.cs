using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>The player's board against the average board of the same hero at the same turn.</summary>
public sealed class WarbandComparison
{
    public WarbandComparison(int turn, int boardStats, double? average, string line)
    {
        Turn = turn;
        BoardStats = boardStats;
        Average = average;
        Line = line;
    }

    public int Turn { get; }
    public int BoardStats { get; }

    /// <summary>The hero's average at this turn; null when no source has one.</summary>
    public double? Average { get; }

    /// <summary>The line shown under the target compositions, e.g. "Board 142 · hero avg 120 at turn 8 · +18%".</summary>
    public string Line { get; }
}

/// <summary>
/// Firestone's warbandStats give, per hero and turn, the average sum of attack and health of the player's
/// minions at that turn's combat. Set against the player's own board, they tell whether to push stats or
/// level: a board below its hero's curve loses more than it should. Nothing is extrapolated: a turn the
/// curve does not reach is said so.
/// </summary>
public static class WarbandCurve
{
    /// <summary>Attack plus health of every minion on the board, Firestone's measure.</summary>
    public static int BoardStats(IEnumerable<(int Attack, int Health)> minions) => minions.Sum(m => Math.Max(0, m.Attack) + Math.Max(0, m.Health));

    /// <param name="sources">Stats files, most trusted first; the first one with a curve for the hero is used.</param>
    public static WarbandComparison Compare(int turn, int boardStats, string heroCardId, IReadOnlyList<HeroStatsFile> sources)
    {
        var curve = sources.Select(s => s.Find(heroCardId)?.WarbandCurve).FirstOrDefault(c => c is { Count: > 0 });
        var board = $"Board {boardStats}";
        if (curve == null)
        {
            return new WarbandComparison(turn, boardStats, null, $"{board} · no curve for this hero");
        }

        var point = curve.FirstOrDefault(p => p.Turn == turn);
        if (point == null)
        {
            return new WarbandComparison(turn, boardStats, null, $"{board} · no average for turn {turn}");
        }

        var inv = CultureInfo.InvariantCulture;
        var line = $"{board} · hero avg {point.AverageStats.ToString("0", inv)} at turn {turn}";
        if (point.AverageStats > 0)
        {
            var delta = (boardStats - point.AverageStats) / point.AverageStats * 100;
            line += $" · {(delta >= 0 ? "+" : "−")}{Math.Abs(delta).ToString("0", inv)}%";
        }

        return new WarbandComparison(turn, boardStats, point.AverageStats, line);
    }
}
