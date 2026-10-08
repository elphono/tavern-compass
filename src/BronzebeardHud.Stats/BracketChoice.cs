using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// The MMR bracket picked in the overlay (Ali, 2026-10-08: "on devrait pouvoir hot-swap dans l'overlay"): a click on the
/// panel's bracket button goes to the next bracket Firestone publishes, from every player down to the top 1 %, then back.
/// Forgotten at the next game, like − n +: the player's own bracket comes back.
/// </summary>
public static class BracketChoice
{
    public static int Next(int current)
    {
        var brackets = FirestoneEndpoints.MmrPercentiles;
        var index = brackets.ToList().IndexOf(current);
        return index < 0 ? MmrBracket.EveryPlayer : brackets[(index + 1) % brackets.Count];
    }

    /// <summary>"all MMR" for every player, "top 25%" otherwise.</summary>
    public static string Label(int percentile) =>
        percentile >= MmrBracket.EveryPlayer ? "all MMR" : $"top {percentile.ToString(CultureInfo.InvariantCulture)}%";
}
