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

    /// <summary>Places gained with this hero against the composition's own average: positive = better with this hero.</summary>
    public double Gain => (Composition.AveragePlacement ?? Estimate) - Estimate;

    /// <summary>"comp ≈ Mech Volumizer 3,0 (23)": estimated placement with a decimal comma, and the number of games.</summary>
    /// <summary>On the composition's line in the shop: "≈ 3,5 with your hero (23)", next to its own average.</summary>
    public string ShopText => $"≈ {Estimate.ToString("0.0", CultureInfo.GetCultureInfo("fr-FR"))} with your hero ({Games})";
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
    /// <summary>The hero's estimate on one composition; null when the composition has no average or too few games with it.</summary>
    public static HeroCompPick? Effect(string baseHeroCardId, Composition composition)
    {
        var stat = composition.HeroStats.FirstOrDefault(h => h.HeroCardId == baseHeroCardId);
        if (composition.AveragePlacement is not { } average || stat == null || stat.DataPoints < MinimumGames)
        {
            return null;
        }

        return new HeroCompPick(composition, (stat.DataPoints * stat.AveragePlacement + PriorGames * average) / (stat.DataPoints + PriorGames), stat.DataPoints);
    }

    /// <summary>Composition id → the hero's estimate, for every composition where it qualifies.</summary>
    public static IReadOnlyDictionary<string, HeroCompPick> Effects(string? baseHeroCardId, IReadOnlyList<Composition> compositions)
    {
        var effects = new Dictionary<string, HeroCompPick>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(baseHeroCardId))
        {
            return effects;
        }

        foreach (var composition in compositions)
        {
            if (!effects.ContainsKey(composition.Id) && Effect(baseHeroCardId!, composition) is { } pick)
            {
                effects[composition.Id] = pick;
            }
        }

        return effects;
    }
}
