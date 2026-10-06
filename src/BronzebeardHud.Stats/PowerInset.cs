using System.Globalization;

namespace BronzebeardHud.Stats;

/// <summary>
/// The power inset under the "Compositions" panel (Ali, 2026-10-06: "take the composition strength indicator out into a small
/// inset under the main frame, framed by the + and − of the other feature"): the player's board against their hero's average
/// (<see cref="WarbandCurve"/>), and under it the opponent's against theirs (<see cref="OpponentPower"/>), each a row of four
/// lamps (red, yellow, green, gold), the lit one glowing in its level's colour (<see cref="BoardPowerLevels.Halo"/>), a badge
/// and the figures; − at its left and + at its right set how many compositions the panel shows.
/// </summary>
public static class PowerInset
{
    /// <summary>
    /// The line written in HDT's log when what the inset shows changes (a level, a figure, its place):
    /// "Bronzebeard HUD: power inset at (1181,994 488x42): you ▲ +58% (ahead) · opp ≈ +12% (even)"; a row without data says
    /// "– (none: too early)", a row switched off by its guard "off".
    /// </summary>
    public static string Line(LayoutRect inset, WarbandComparison? own, WarbandComparison? opponent)
    {
        var inv = CultureInfo.InvariantCulture;
        static string Row(WarbandComparison? row) => row == null
            ? "off"
            : row.Power == BoardPower.None || row.Percent == null
                ? $"{BoardPowerLevels.Symbol(BoardPower.None)} (none{(row.Note != null ? ": " + row.Note : string.Empty)})"
                : $"{BoardPowerLevels.Symbol(row.Power)} {row.Percent} ({BoardPowerLevels.Name(row.Power)})";
        return $"Bronzebeard HUD: power inset at ({inset.Left.ToString("0", inv)},{inset.Top.ToString("0", inv)} {inset.Width.ToString("0", inv)}x{inset.Height.ToString("0", inv)}): "
               + $"you {Row(own)} · opp {Row(opponent)}";
    }
}
