using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Adjusts hero stats to the tribes actually in the lobby, with Firestone's rule
/// (libs/battlegrounds/data-access/src/lib/meta-heroes/bgs-meta-hero-stats.ts, buildHeroStats):
/// drop tribe figures backed by too few games (dataPoints ≤ hero dataPoints / 20, or games without the
/// tribe ≤ tribe dataPoints / 20); when the lobby has fewer tribes than the whole pool, add up the
/// impacts of the lobby's tribes to the average placement, and drop a hero that has none of them.
/// Tiers are then computed on the adjusted placements, as Firestone does.
/// </summary>
public static class LobbyTribes
{
    /// <summary>Adjusted average placement, or null when the hero has no usable figure for this lobby.</summary>
    public static double? AdjustedPlacement(HeroStat hero, IReadOnlyCollection<string> lobbyTribes)
    {
        var usable = hero.TribeImpacts
            .Where(t => t.DataPoints > hero.DataPoints / 20.0)
            .Where(t => t.DataPointsOnMissingTribe > t.DataPoints / 20.0)
            .ToList();
        var restricted = lobbyTribes.Count > 0 && lobbyTribes.Count < Tribes.All.Count;
        if (!restricted)
        {
            return hero.AveragePlacement;
        }

        var inLobby = usable.Where(t => lobbyTribes.Contains(t.Tribe)).ToList();
        if (inLobby.Count == 0)
        {
            return null;
        }

        return hero.AveragePlacement + inLobby.Sum(t => t.Impact);
    }

    /// <summary>
    /// The same file with every hero's placement adjusted to the lobby; heroes without figures for it are
    /// left out. A file with no tribe figures at all (hand-typed HSReplay data) is returned unchanged.
    /// </summary>
    public static HeroStatsFile Apply(HeroStatsFile file, IReadOnlyCollection<string> lobbyTribes)
    {
        if (file.Heroes.All(h => h.TribeImpacts.Count == 0))
        {
            return file;
        }

        var heroes = file.Heroes
            .Select(h => (Hero: h, Adjusted: AdjustedPlacement(h, lobbyTribes)))
            .Where(x => x.Adjusted is >= 1 and <= 8)
            .Select(x => new HeroStat(x.Hero.HeroCardId, x.Adjusted!.Value, x.Hero.DataPoints, x.Hero.PickRate, x.Hero.Tier,
                x.Hero.PlacementDistribution, x.Hero.TribeImpacts))
            .ToList();
        return new HeroStatsFile(file.Source, heroes, file.SourceUrl, file.GeneratedAt, file.FetchedAt, file.MmrPercentile,
            file.TimePeriod, file.MmrThresholds);
    }
}
