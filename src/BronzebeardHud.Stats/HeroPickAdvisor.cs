using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>What one source says about one offered hero.</summary>
public sealed class HeroFigures
{
    public HeroFigures(string source, string? tier, double averagePlacement, double? pickRate, int dataPoints)
    {
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
}

/// <summary>One column of the hero-pick panel.</summary>
public sealed class HeroPickRow
{
    public HeroPickRow(OfferedHero hero, IReadOnlyList<HeroFigures> figures)
    {
        Hero = hero;
        Figures = figures;
    }

    public OfferedHero Hero { get; }

    /// <summary>One entry per source that knows the hero, in the sources' priority order. Empty = no data.</summary>
    public IReadOnlyList<HeroFigures> Figures { get; }

    public bool HasData => Figures.Count > 0;
}

public static class HeroPickAdvisor
{
    /// <param name="offered">Offered heroes, in on-screen order.</param>
    /// <param name="sources">Loaded stats files, most trusted first (the plugin puts hand-typed HSReplay data first).</param>
    public static IReadOnlyList<HeroPickRow> BuildRows(IReadOnlyList<OfferedHero> offered, IReadOnlyList<HeroStatsFile> sources)
    {
        var tiersBySource = sources.Select(HeroTiers.Compute).ToList();
        return offered
            .Select(hero =>
            {
                var figures = new List<HeroFigures>();
                for (var i = 0; i < sources.Count; i++)
                {
                    var stat = sources[i].Find(hero.BaseCardId);
                    if (stat == null)
                    {
                        continue;
                    }

                    figures.Add(new HeroFigures(
                        sources[i].Source,
                        tiersBySource[i][stat.HeroCardId],
                        stat.AveragePlacement,
                        stat.PickRate,
                        stat.DataPoints));
                }

                return new HeroPickRow(hero, figures);
            })
            .ToList();
    }
}
