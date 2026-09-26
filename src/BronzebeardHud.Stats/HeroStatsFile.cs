using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>Known values of the <c>source</c> field of a local stats file.</summary>
public static class StatsSources
{
    /// <summary>Downloaded from Firestone's public JSON endpoints.</summary>
    public const string Firestone = "firestone";

    /// <summary>Copied by hand from hsreplay.net.</summary>
    public const string HsReplayManual = "hsreplay-manual";

    public static readonly IReadOnlyList<string> All = new[] { Firestone, HsReplayManual };
}

/// <summary>Aggregated figures for one base hero, as one source reports them.</summary>
public sealed class HeroStat
{
    public HeroStat(
        string heroCardId,
        double averagePlacement,
        int dataPoints,
        double? pickRate = null,
        string? tier = null,
        IReadOnlyList<double>? placementDistribution = null)
    {
        HeroCardId = heroCardId;
        AveragePlacement = averagePlacement;
        DataPoints = dataPoints;
        PickRate = pickRate;
        Tier = tier;
        PlacementDistribution = placementDistribution;
    }

    /// <summary>Base hero card id: skins are always mapped to their parent.</summary>
    public string HeroCardId { get; }

    /// <summary>Average final placement, 1 (first) to 8 (last). Lower is better.</summary>
    public double AveragePlacement { get; }

    public int DataPoints { get; }

    /// <summary>Picked / offered, in [0, 1]. Null when the source does not say.</summary>
    public double? PickRate { get; }

    /// <summary>Tier letter given by the source itself, or null to compute it.</summary>
    public string? Tier { get; }

    /// <summary>Percentage of games ending at each placement, index 0 = first place.</summary>
    public IReadOnlyList<double>? PlacementDistribution { get; }
}

/// <summary>One local stats file: one source, one time period, one MMR bracket.</summary>
public sealed class HeroStatsFile
{
    public const int CurrentSchema = 1;

    public HeroStatsFile(
        string source,
        IReadOnlyList<HeroStat> heroes,
        string? sourceUrl = null,
        DateTimeOffset? generatedAt = null,
        DateTimeOffset? fetchedAt = null,
        int? mmrPercentile = null,
        string? timePeriod = null)
    {
        Source = source;
        Heroes = heroes;
        SourceUrl = sourceUrl;
        GeneratedAt = generatedAt;
        FetchedAt = fetchedAt;
        MmrPercentile = mmrPercentile;
        TimePeriod = timePeriod;
    }

    public string Source { get; }
    public IReadOnlyList<HeroStat> Heroes { get; }
    public string? SourceUrl { get; }

    /// <summary>When the source computed the aggregates.</summary>
    public DateTimeOffset? GeneratedAt { get; }

    /// <summary>When we downloaded (or typed) them. Drives the cache refresh.</summary>
    public DateTimeOffset? FetchedAt { get; }

    public int? MmrPercentile { get; }
    public string? TimePeriod { get; }

    public HeroStat? Find(string baseHeroCardId) =>
        Heroes.FirstOrDefault(h => string.Equals(h.HeroCardId, baseHeroCardId, StringComparison.Ordinal));
}
