using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Picks the Firestone stats bracket that matches the player: the most exclusive percentile whose
/// minimum MMR the player reaches (e.g. 6840 with thresholds 50 % ≥ 6043, 25 % ≥ 6575, 10 % ≥ 7177
/// gives the top 25 %). Brackets Firestone does not publish are ignored; with no rating or no table,
/// the answer is 100, i.e. every player.
/// </summary>
public static class MmrBracket
{
    public const int EveryPlayer = 100;

    public static int Select(int? rating, IReadOnlyList<MmrThreshold> thresholds)
    {
        if (rating is not { } mmr)
        {
            return EveryPlayer;
        }

        return thresholds
            .Where(t => FirestoneEndpoints.MmrPercentiles.Contains(t.Percentile) && mmr >= t.Mmr)
            .Select(t => t.Percentile)
            .DefaultIfEmpty(EveryPlayer)
            .Min();
    }
}
