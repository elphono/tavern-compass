using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Firestone's hero tier rule (libs/battlegrounds/data-access/src/lib/meta-heroes/bgs-meta-hero-stats.ts,
/// buildTiers): with μ and σ the mean and population standard deviation of the heroes' average
/// placements, S &lt; μ−3σ ≤ A &lt; μ−1.5σ ≤ B &lt; μ ≤ C &lt; μ+σ ≤ D &lt; μ+2σ ≤ E.
/// Firestone's last bucket stops just below 8; here E includes 8, so no valid hero goes untiered.
/// </summary>
public static class HeroTiers
{
    public static string TierFor(double averagePlacement, double mean, double standardDeviation)
    {
        if (averagePlacement < mean - 3 * standardDeviation) return "S";
        if (averagePlacement < mean - 1.5 * standardDeviation) return "A";
        if (averagePlacement < mean) return "B";
        if (averagePlacement < mean + standardDeviation) return "C";
        if (averagePlacement < mean + 2 * standardDeviation) return "D";
        return "E";
    }

    /// <summary>Mean and population standard deviation, as Firestone's getStandardDeviation.</summary>
    public static (double Mean, double StandardDeviation) Distribution(IReadOnlyCollection<double> placements)
    {
        if (placements.Count == 0)
        {
            return (0, 0);
        }

        var mean = placements.Average();
        var variance = placements.Sum(p => (p - mean) * (p - mean)) / placements.Count;
        return (mean, Math.Sqrt(variance));
    }

    /// <summary>
    /// Below this many heroes, a file is not a hero pool (typically a hand-typed excerpt of the
    /// four offered heroes) and a tier computed against it would be meaningless.
    /// </summary>
    public const int MinimumHeroesForComputedTier = 20;

    /// <summary>
    /// Tier of every hero of a file. A tier given by the source wins; the others are computed
    /// against the whole file, the way Firestone ranks a hero against the full hero pool, or
    /// left null when the file is too small to be a pool.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> Compute(HeroStatsFile file)
    {
        var placements = file.Heroes.Select(h => h.AveragePlacement).ToList();
        var (mean, standardDeviation) = Distribution(placements);
        var canCompute = file.Heroes.Count >= MinimumHeroesForComputedTier;
        return file.Heroes.ToDictionary(
            h => h.HeroCardId,
            h => h.Tier ?? (canCompute ? TierFor(h.AveragePlacement, mean, standardDeviation) : null),
            StringComparer.Ordinal);
    }
}
