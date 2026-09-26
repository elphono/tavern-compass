using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>One composition a hero does well with, and how sure that is.</summary>
public sealed class HeroCompPick
{
    public HeroCompPick(Composition composition, double estimate, int games)
    {
        Composition = composition;
        Estimate = estimate;
        Games = games;
    }

    public Composition Composition { get; }

    /// <summary>Expected placement of the hero with this composition, pulled towards the composition's own average.</summary>
    public double Estimate { get; }

    /// <summary>Games of this hero with this composition in Firestone's data.</summary>
    public int Games { get; }

    /// <summary>"comp ≈ Mech Volumizer 3,0 (23)": estimated placement with a decimal comma, and the number of games.</summary>
    public string Label => $"comp ≈ {Composition.Name} {Estimate.ToString("0.0", CultureInfo.GetCultureInfo("fr-FR"))} ({Games})";
}

/// <summary>
/// The best composition for an offered hero, from Firestone's per-hero figures of each composition. Those
/// samples are small (median 17 games per hero and composition on last-patch, measured 2026-09-26), so a raw
/// average would mostly rank noise: each one is pulled towards the composition's overall placement as if it
/// had <see cref="PriorGames"/> more games at that placement (empirical Bayes), pairs under
/// <see cref="MinimumGames"/> games are left out, and the number of games is shown with the estimate.
/// </summary>
public static class HeroCompAffinity
{
    /// <summary>
    /// Weight of the composition's own average, in games. A judgement: one game's placement varies by about
    /// 2.3 places, the difference between heroes on one composition by about 0.4, and 2.3² / 0.4² ≈ 30.
    /// </summary>
    public const double PriorGames = 30;

    public const int MinimumGames = 10;

    /// <summary>The composition with the best estimate for this hero among the playable ones; null when none qualifies.</summary>
    public static HeroCompPick? Best(string baseHeroCardId, IReadOnlyList<Composition> playable)
    {
        return playable
            .Where(c => c.AveragePlacement.HasValue)
            .Select(c => (Composition: c, Stat: c.HeroStats.FirstOrDefault(h => h.HeroCardId == baseHeroCardId)))
            .Where(x => x.Stat != null && x.Stat.DataPoints >= MinimumGames)
            .Select(x => new HeroCompPick(
                x.Composition,
                (x.Stat!.DataPoints * x.Stat.AveragePlacement + PriorGames * x.Composition.AveragePlacement!.Value) / (x.Stat.DataPoints + PriorGames),
                x.Stat.DataPoints))
            .OrderBy(p => p.Estimate)
            .ThenBy(p => p.Composition.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
