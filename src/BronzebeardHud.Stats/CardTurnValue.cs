using System;
using System.Globalization;

namespace BronzebeardHud.Stats;

public enum CardTurnVerdict
{
    /// <summary>Games where the card was played at this turn end better placed than those of the average card played then.</summary>
    Better,

    Worse,
}

/// <summary>What card-stats says of one card at one turn, when it says something above the noise.</summary>
public sealed class CardTurnNote
{
    public CardTurnNote(int turn, double placement, double turnAverage, int played, CardTurnVerdict verdict)
    {
        Turn = turn;
        Placement = placement;
        TurnAverage = turnAverage;
        Played = played;
        Verdict = verdict;
    }

    public int Turn { get; }

    /// <summary>Average final placement of the games where the card was played at this turn.</summary>
    public double Placement { get; }

    /// <summary>The same, for every card played at this turn (weighted by times played).</summary>
    public double TurnAverage { get; }

    public int Played { get; }

    public CardTurnVerdict Verdict { get; }
}

/// <summary>
/// One card at the current turn, against every card played at that turn (docs/plans/2026-10-08-stats-multi-sources.html,
/// components 1 and 2). Not against the games where it was not played: measured on 2026-10-08, those are worse for every
/// card (playing anything goes with surviving), which tells no card from another. A correlation, never a cause: the label
/// gives the figure and its comparison, never "buy". The thresholds are starting points (decision 8), to be recalibrated on
/// measurements.
/// </summary>
public static class CardTurnValue
{
    /// <summary>Under this many games at the turn, nothing is said.</summary>
    public const int MinimumPlayed = 200;

    /// <summary>A gap smaller than this is not worth a label, however many games back it.</summary>
    public const double MinimumGap = 0.1;

    /// <summary>One game's placement varies by about 2.3 places (the same figure as HeroCompAffinity's).</summary>
    public const double PlacementSpread = 2.3;

    /// <summary>The note for a card at a turn; null when there is no file, no such card or turn, too few games, or a gap under the noise.</summary>
    public static CardTurnNote? For(CardStatsFile? file, string cardId, int turn)
    {
        if (file?.Find(cardId)?.At(turn) is not { } stat || stat.Played < MinimumPlayed || file.TurnAverage(turn) is not { } average)
        {
            return null;
        }

        var gap = stat.AveragePlacement - average;
        var noise = Math.Max(MinimumGap, 2 * PlacementSpread / Math.Sqrt(stat.Played));
        if (Math.Abs(gap) < noise)
        {
            return null;
        }

        return new CardTurnNote(turn, stat.AveragePlacement, average, stat.Played, gap < 0 ? CardTurnVerdict.Better : CardTurnVerdict.Worse);
    }

    /// <summary>"t6 ▲ 3.6 vs 3.9" (a lower placement is better: ▲), else "t6 ▲ 3.6", else null when neither fits <paramref name="maxChars"/>.</summary>
    public static string? Label(CardTurnNote? note, int maxChars)
    {
        if (note == null)
        {
            return null;
        }

        var inv = CultureInfo.InvariantCulture;
        var mark = note.Verdict == CardTurnVerdict.Better ? "▲" : "▼";
        var shortForm = $"t{note.Turn.ToString(inv)} {mark} {note.Placement.ToString("0.0", inv)}";
        var longForm = $"{shortForm} vs {note.TurnAverage.ToString("0.0", inv)}";
        return MarkerText.DisplayLength(longForm) <= maxChars ? longForm
            : MarkerText.DisplayLength(shortForm) <= maxChars ? shortForm
            : null;
    }
}
