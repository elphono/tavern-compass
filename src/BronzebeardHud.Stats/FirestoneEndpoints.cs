using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Firestone's public stats endpoints, as its own code builds them
/// (libs/battlegrounds/data-access/src/lib/meta-heroes/bgs-meta-hero-stats-access.service.ts).
/// The author agreed on 2026-09-26 that we fetch them ourselves; see the spec for the refresh policy.
/// </summary>
public static class FirestoneEndpoints
{
    private const string Base = "https://static.zerotoheroes.com/api/bgs";

    /// <summary>MMR percentiles Firestone publishes: 100 = every player, 1 = the top 1%.</summary>
    public static readonly IReadOnlyList<int> MmrPercentiles = new[] { 100, 50, 25, 10, 1 };

    /// <summary>From bgs-active-time-filter.type.ts.</summary>
    public static readonly IReadOnlyList<string> TimePeriods = new[] { "last-patch", "past-three", "past-seven", "all-time" };

    public static string HeroStats(int mmrPercentile, string timePeriod)
    {
        Check(mmrPercentile, timePeriod);
        return $"{Base}/hero-stats/mmr-{mmrPercentile}/{timePeriod}/overview-from-hourly.gz.json";
    }

    /// <summary>Composition stats (libs/battlegrounds/services/src/lib/services/bgs-comps.service.ts, BGS_CARDS_URL).</summary>
    public static string CompStats(string timePeriod)
    {
        Check(100, timePeriod);
        return $"{Base}/comp-stats/{timePeriod}/overview-from-hourly.gz.json";
    }

    /// <summary>Trinket stats (libs/battlegrounds/services/src/lib/services/bgs-trinkets.service.ts, BGS_TRINKETS_URL).</summary>
    public static string TrinketStats(string timePeriod)
    {
        Check(100, timePeriod);
        return $"{Base}/trinket-stats/{timePeriod}/overview-from-hourly.gz.json";
    }

    /// <summary>
    /// Card stats per MMR bracket: how each card did when played at each turn. Covered by the maintainer's agreement of
    /// 2026-09-27 (every public file); found and measured on 2026-10-08 (mmr-100: 806 cards, 1.46 MB uncompressed).
    /// </summary>
    public static string CardStats(int mmrPercentile, string timePeriod)
    {
        Check(mmrPercentile, timePeriod);
        return $"{Base}/card-stats/mmr-{mmrPercentile}/{timePeriod}/overview-from-hourly.gz.json";
    }

    public static string MmrPercentileTable(string timePeriod)
    {
        Check(100, timePeriod);
        return $"{Base}/hero-stats/{timePeriod}/mmr-percentiles.gz.json";
    }

    private static void Check(int mmrPercentile, string timePeriod)
    {
        if (!MmrPercentiles.Contains(mmrPercentile))
        {
            throw new ArgumentOutOfRangeException(nameof(mmrPercentile), mmrPercentile,
                $"expected one of {string.Join(", ", MmrPercentiles)}");
        }

        if (!TimePeriods.Contains(timePeriod))
        {
            throw new ArgumentOutOfRangeException(nameof(timePeriod), timePeriod,
                $"expected one of {string.Join(", ", TimePeriods)}");
        }
    }
}
