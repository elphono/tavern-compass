using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Which slice of Blizzard's public Battlegrounds leaderboard the opponent-MMR lookup (phase 2)
/// downloads. Expressed in MMR rather than in page numbers: pages drift during a season as more
/// players pass the leaderboard floor, while an MMR bound keeps its meaning; the lookup resolves
/// the bounds to pages when it runs. See docs/plans/2026-09-26-parite-tier7-plan.md, phase 2,
/// for the measurement behind the default.
/// </summary>
public sealed class LeaderboardRange
{
    /// <summary>Regions the leaderboard API accepts.</summary>
    public static readonly IReadOnlyList<string> Regions = new[] { "EU", "US", "AP" };

    /// <summary>
    /// Default, measured on 2026-09-26 for a player at 6840 MMR on EU: the leaderboard stops at
    /// 8000, so no page lies around that MMR and the default is the bottom of the board, the part
    /// closest to it: 8000 to 8050, i.e. pages 90 to 121 of 121 that day.
    /// </summary>
    public static LeaderboardRange Default { get; } = new("EU", minRating: 8000, maxRating: 8050);

    public LeaderboardRange(string region, int minRating, int maxRating)
    {
        if (!Regions.Contains(region))
        {
            throw new ArgumentOutOfRangeException(nameof(region), region, $"expected one of {string.Join(", ", Regions)}");
        }

        if (minRating > maxRating)
        {
            throw new ArgumentException($"minRating {minRating} is above maxRating {maxRating}", nameof(minRating));
        }

        Region = region;
        MinRating = minRating;
        MaxRating = maxRating;
    }

    public string Region { get; }

    /// <summary>Lowest MMR to download, inclusive.</summary>
    public int MinRating { get; }

    /// <summary>Highest MMR to download, inclusive.</summary>
    public int MaxRating { get; }

    /// <summary>The default with any provided value replacing its counterpart.</summary>
    public static LeaderboardRange WithOverrides(string? region = null, int? minRating = null, int? maxRating = null) =>
        new(region ?? Default.Region, minRating ?? Default.MinRating, maxRating ?? Default.MaxRating);
}
