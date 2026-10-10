using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>What one source says about one offered hero.</summary>
public sealed class HeroFigures
{
    public HeroFigures(string source, string? tier, double averagePlacement, double? pickRate, int dataPoints, int? mmrPercentile = null,
        IReadOnlyList<double>? placementDistribution = null)
    {
        (Top4Rate, FirstRate) = HeroPickAdvisor.PlacementOdds(placementDistribution);
        MmrPercentile = mmrPercentile;
        Source = source;
        Tier = tier;
        AveragePlacement = averagePlacement;
        PickRate = pickRate;
        DataPoints = dataPoints;
    }

    public string Source { get; }

    /// <summary>Null when the source gives none and the file is too small to compute one.</summary>
    public string? Tier { get; }

    public double AveragePlacement { get; }
    public double? PickRate { get; }
    public int DataPoints { get; }

    /// <summary>Bracket the figures come from (100 = every player), when the source says.</summary>
    public int? MmrPercentile { get; }

    /// <summary>Share of games ending in the top 4, where MMR is won, in percent; null without a distribution.</summary>
    public double? Top4Rate { get; }

    /// <summary>Share of games won, in percent; null without a distribution.</summary>
    public double? FirstRate { get; }

    /// <summary>"top 4 54% · 1st 10%", or null without a distribution.</summary>
    public string? OddsText => Top4Rate is { } top4 && FirstRate is { } first
        ? $"top 4 {top4.ToString("0", System.Globalization.CultureInfo.InvariantCulture)}% · 1st {first.ToString("0", System.Globalization.CultureInfo.InvariantCulture)}%"
        : null;
}

/// <summary>One column of the hero-pick panel.</summary>
public sealed class HeroPickRow
{
    public HeroPickRow(OfferedHero hero, IReadOnlyList<HeroFigures> figures, ConsolidatedStat? stat = null)
    {
        Hero = hero;
        Figures = figures;
        Stat = stat;
        Consensus = HeroConsensus.For(stat);
    }

    /// <summary>The hero's consolidated figure, when a view was given; its "why" goes to HDT's log.</summary>
    public ConsolidatedStat? Stat { get; }

    /// <summary>The badge's consolidated line (component 3); null: the badge keeps its per-source lines.</summary>
    public HeroConsensusLine? Consensus { get; }

    public OfferedHero Hero { get; }

    /// <summary>One entry per source that knows the hero, in the sources' priority order. Empty = no data.</summary>
    public IReadOnlyList<HeroFigures> Figures { get; }

    public bool HasData => Figures.Count > 0;
}

public static class HeroPickAdvisor
{
    /// <summary>
    /// Top-4 and first-place shares, in percent, from the eight placement percentages (index 0 = first).
    /// Normalised by their sum, so rounding in the source does not bias them; null when there is nothing to divide.
    /// </summary>
    public static (double? Top4, double? First) PlacementOdds(IReadOnlyList<double>? distribution)
    {
        if (distribution is not { Count: 8 })
        {
            return (null, null);
        }

        var total = distribution.Sum();
        if (total <= 0)
        {
            return (null, null);
        }

        return (distribution.Take(4).Sum() / total * 100, distribution[0] / total * 100);
    }

    /// <param name="offered">Offered heroes, in on-screen order.</param>
    /// <param name="sources">Loaded stats files, most trusted first (the plugin puts hand-typed HSReplay data first).</param>
    public static IReadOnlyList<HeroPickRow> BuildRows(IReadOnlyList<OfferedHero> offered, IReadOnlyList<HeroStatsFile> sources, ConsolidatedView? view = null)
    {
        var tiersBySource = sources.Select(HeroTiers.Compute).ToList();
        return offered
            .Select(hero =>
            {
                var figures = new List<HeroFigures>();
                ConsolidatedStat? consolidated = null;
                for (var i = 0; i < sources.Count; i++)
                {
                    var stat = sources[i].Find(hero.BaseCardId);
                    if (stat == null)
                    {
                        continue;
                    }

                    consolidated ??= view?.Find("hero", stat.HeroCardId, "placement", "games");
                    figures.Add(new HeroFigures(
                        sources[i].Source,
                        tiersBySource[i][stat.HeroCardId],
                        stat.AveragePlacement,
                        stat.PickRate,
                        stat.DataPoints,
                        sources[i].MmrPercentile,
                        stat.PlacementDistribution));
                }

                return new HeroPickRow(hero, figures, consolidated);
            })
            .ToList();
    }
}
