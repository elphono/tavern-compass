using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>Where a trinket's placement stands among the trinkets of the player's bracket.</summary>
public enum PlacementBand
{
    /// <summary>Not graded: no reference, or the trinket's games unknown.</summary>
    None,

    Good,

    Average,

    Bad,
}

/// <summary>
/// The trinkets of the player's bracket, to grade one against them: their mean placement, weighed by games, and each one's
/// games. A trinket is picked by a player who places as players of that bracket place, so the mean moves with the bracket
/// (measured on Firestone's trinket-stats of 2026-10-11, 238 trinkets: 3.86 for every player, 4.04 for the top 25%, 3.47
/// for the top 1%): a fixed bound would call nearly every trinket good in the top 1% and bad in the top 25%.
/// </summary>
public sealed class TrinketReference
{
    private readonly IReadOnlyDictionary<string, int> _games;

    public TrinketReference(double mean, IReadOnlyDictionary<string, int> games)
    {
        Mean = mean;
        _games = games;
    }

    /// <summary>The trinkets' mean placement in the bracket, weighed by their games.</summary>
    public double Mean { get; }

    /// <summary>A trinket's games (every bracket: Firestone gives no count per bracket); 0 when unknown.</summary>
    public int GamesOf(string trinketCardId) => _games.TryGetValue(trinketCardId, out var games) ? games : 0;

    /// <summary>
    /// From the trinket stats loaded, at <paramref name="bracket"/> (<see cref="TrinketStat.PlacementFor"/>, as ChoiceAdvisor
    /// reads them); null without stats, or without a trinket that has games.
    /// </summary>
    public static TrinketReference? Of(IEnumerable<TrinketStat>? trinkets, int bracket)
    {
        var counted = trinkets?.Where(t => t.DataPoints > 0).ToList();
        if (counted == null || counted.Count == 0)
        {
            return null;
        }

        var games = counted.Sum(t => (double)t.DataPoints);
        var mean = counted.Sum(t => t.PlacementFor(bracket) * t.DataPoints) / games;
        var byId = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var trinket in counted)
        {
            byId[trinket.TrinketCardId] = trinket.DataPoints;
        }

        return new TrinketReference(mean, byId);
    }
}

/// <summary>
/// The colour scale of a trinket's placement (Ali, 2026-10-11: "les couleurs, les notes"): good, average or bad against the
/// mean of the trinkets of the player's bracket (<see cref="TrinketReference"/>). Measured on Firestone's trinket-stats of
/// 2026-10-11 (238 trinkets), ± <see cref="Band"/> around that mean puts about one trinket in five on each side, in every
/// bracket from every player to the top 10% (top 25%: 47 good, 141 average, 50 bad; top 1%, fewer games and a wider spread:
/// 73, 91, 74); ± 0.15 painted half of them, ± 0.25 a quarter. Nothing under the noise, as for the cards
/// (<see cref="CardTurnValue"/>): the band widens to 2 × 2.3 / √games when that is larger (under 529 games; 4 trinkets of 238
/// that day), and the games are those of every bracket, Firestone giving no count per bracket, so the noise is a floor. The
/// gap is taken at the hundredth, as the figure is shown.
/// </summary>
public static class PlacementGrade
{
    /// <summary>How far from the bracket's mean, in places, a trinket is good (below) or bad (above), noise allowing.</summary>
    public const double Band = 0.2;

    public const string GoodColour = "#3DDC84";
    public const string AverageColour = "#FFFFFF";
    public const string BadColour = "#FF5C5C";
    public const string UngradedColour = "#C8CDD8";

    /// <summary>The trinket's band; <see cref="PlacementBand.None"/> when its games are unknown.</summary>
    public static PlacementBand Of(double placement, double reference, int games)
    {
        if (games <= 0)
        {
            return PlacementBand.None;
        }

        var threshold = Math.Max(Band, 2 * CardTurnValue.PlacementSpread / Math.Sqrt(games));
        var gap = Math.Round(placement - reference, 2);
        return gap <= -threshold ? PlacementBand.Good
            : gap >= threshold ? PlacementBand.Bad
            : PlacementBand.Average;
    }

    /// <summary>Green, white (the game's armour), red; light grey when not graded.</summary>
    public static string Colour(PlacementBand band) => band switch
    {
        PlacementBand.Good => GoodColour,
        PlacementBand.Average => AverageColour,
        PlacementBand.Bad => BadColour,
        _ => UngradedColour,
    };
}
