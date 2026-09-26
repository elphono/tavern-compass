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

/// <summary>How much having one tribe in the lobby moves a hero's average placement (Firestone tribeStats).</summary>
public sealed class TribeImpact
{
    public TribeImpact(string tribe, double impact, int dataPoints, int dataPointsOnMissingTribe)
    {
        Tribe = tribe;
        Impact = impact;
        DataPoints = dataPoints;
        DataPointsOnMissingTribe = dataPointsOnMissingTribe;
    }

    /// <summary>Race name, e.g. UNDEAD.</summary>
    public string Tribe { get; }

    /// <summary>Firestone's impactAveragePosition: negative = the tribe helps (lower placement).</summary>
    public double Impact { get; }

    public int DataPoints { get; }
    public int DataPointsOnMissingTribe { get; }
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
        IReadOnlyList<double>? placementDistribution = null,
        IReadOnlyList<TribeImpact>? tribeImpacts = null)
    {
        TribeImpacts = tribeImpacts ?? Array.Empty<TribeImpact>();
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

    /// <summary>Per-tribe effect on the average placement; empty when the source does not say.</summary>
    public IReadOnlyList<TribeImpact> TribeImpacts { get; }
}

/// <summary>Minimum MMR to belong to a percentile bracket (Firestone's mmrPercentiles table).</summary>
public sealed class MmrThreshold
{
    public MmrThreshold(int percentile, int mmr)
    {
        Percentile = percentile;
        Mmr = mmr;
    }

    /// <summary>100 = every player, 1 = the top 1 %.</summary>
    public int Percentile { get; }

    public int Mmr { get; }
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
        string? timePeriod = null,
        IReadOnlyList<MmrThreshold>? mmrThresholds = null)
    {
        MmrThresholds = mmrThresholds ?? Array.Empty<MmrThreshold>();
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

    /// <summary>The source's MMR brackets, when it publishes them; empty otherwise.</summary>
    public IReadOnlyList<MmrThreshold> MmrThresholds { get; }

    public HeroStat? Find(string baseHeroCardId) =>
        Heroes.FirstOrDefault(h => string.Equals(h.HeroCardId, baseHeroCardId, StringComparison.Ordinal));
}
