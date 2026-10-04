using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>One card of a composition's detail block.</summary>
public sealed class CompDetailCard
{
    public CompDetailCard(string cardId, int? techLevel, int finalBoards)
    {
        CardId = cardId;
        TechLevel = techLevel;
        FinalBoards = finalBoards;
    }

    /// <summary>Base card id (golden copies mapped to their card).</summary>
    public string CardId { get; }

    /// <summary>Tavern tier of the card, 1 to 7; null when unknown.</summary>
    public int? TechLevel { get; }

    /// <summary>How many of the composition's final boards hold the card (a board holding two copies counts once).</summary>
    public int FinalBoards { get; }
}

/// <summary>
/// The detail block of a composition, opened from the target panel. Neither Firestone's comp stats (schema 4:
/// core and add-on cards, five final boards with their turn, a reference board, per-hero figures) nor the
/// hand-written HSReplay format carry any early-game, enabler or "when to commit" data. Every section here is
/// therefore DERIVED from those lists, and <see cref="SourceNote"/> says so on screen:
/// - early enablers: add-on and final-board cards of tier ≤ <see cref="MaxEarlyTier"/>, most frequent on the
///   final boards first, then in order of first appearance (add-ons first, then boards left to right);
/// - when to commit: the key pieces (core cards), the cards that define the composition, with their tier;
/// - typical final turn: the median turn of the final boards that give one.
/// Computed on the fly from a <see cref="Composition"/>: nothing is added to the cache format.
/// </summary>
public sealed class CompDetail
{
    /// <summary>Highest tavern tier still counted as early game.</summary>
    public const int MaxEarlyTier = 3;

    public const int DefaultMaxEnablers = 6;

    private CompDetail(Composition composition, string header, IReadOnlyList<CompDetailCard> earlyEnablers, IReadOnlyList<CompDetailCard> commitCards,
        double? typicalFinalTurn, string sourceNote)
    {
        Composition = composition;
        Header = header;
        EarlyEnablers = earlyEnablers;
        CommitCards = commitCards;
        TypicalFinalTurn = typicalFinalTurn;
        SourceNote = sourceNote;
    }

    public Composition Composition { get; }

    /// <summary>"Undead Butcher · Undead, Beast · avg place 3,8 · 1 234 games · tier A", absent parts left out.</summary>
    public string Header { get; }

    public IReadOnlyList<CompDetailCard> EarlyEnablers { get; }

    /// <summary>The key pieces, in the composition's order.</summary>
    public IReadOnlyList<CompDetailCard> CommitCards { get; }

    /// <summary>Median turn of the final boards that give a turn; null when none does.</summary>
    public double? TypicalFinalTurn { get; }

    /// <summary>"Typical final turn: 11,5", decimal comma as Ali reads it; null without a turn.</summary>
    public string? TypicalFinalTurnText =>
        TypicalFinalTurn is { } turn ? "Typical final turn: " + turn.ToString("0.#", CultureInfo.GetCultureInfo("fr-FR")) : null;

    /// <summary>Where the sections come from, behind the ⓘ of the detail view, so that nothing passes for an expert guide.</summary>
    public string SourceNote { get; }

    /// <summary>
    /// The detail view's one meta line, under the name and placement it does not repeat:
    /// "Undead, Beast · 1 234 games · tier A · final turn ≈ 11,5", absent parts left out.
    /// </summary>
    public string Meta
    {
        get
        {
            var parts = new List<string>();
            if (Composition.Tribes.Count > 0)
            {
                parts.Add(string.Join(", ", Composition.Tribes.Select(t => t.Substring(0, 1) + t.Substring(1).ToLowerInvariant())));
            }

            if (Composition.DataPoints is { } games)
            {
                parts.Add(games.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", " ") + " games");
            }

            if (!string.IsNullOrEmpty(Composition.Tier))
            {
                parts.Add("tier " + Composition.Tier);
            }

            if (TypicalFinalTurn is { } turn)
            {
                parts.Add(FinalTurnText(turn));
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>"final turn ≈ 11,5", decimal comma as Ali reads it.</summary>
    public static string FinalTurnText(double turn) => "final turn ≈ " + turn.ToString("0.#", CultureInfo.GetCultureInfo("fr-FR"));

    /// <summary>Median turn of the composition's final boards that give a turn; null when none does.</summary>
    public static double? MedianFinalTurn(Composition composition)
    {
        var turns = composition.FinalBoards.Where(b => b.Turn.HasValue).Select(b => b.Turn!.Value).OrderBy(t => t).ToList();
        return turns.Count == 0
            ? null
            : turns.Count % 2 == 1
                ? turns[turns.Count / 2]
                : (turns[turns.Count / 2 - 1] + turns[turns.Count / 2]) / 2.0;
    }

    /// <param name="techLevel">Tavern tier of a card id; null (or 0) when unknown. In the plugin it comes from HearthDb.</param>
    public static CompDetail For(Composition composition, Func<string, int?> techLevel, int maxEnablers = DefaultMaxEnablers)
    {
        int? Tier(string cardId) => techLevel(cardId) is { } tier && tier > 0 ? tier : null;
        var boards = composition.FinalBoards
            .Select(b => new HashSet<string>(b.Cards.Select(CardIds.Normalize), StringComparer.Ordinal))
            .ToList();
        int Frequency(string cardId) => boards.Count(b => b.Contains(cardId));

        var enablers = composition.AddonCards
            .Concat(composition.FinalBoards.SelectMany(b => b.Cards))
            .Select(CardIds.Normalize)
            .Distinct(StringComparer.Ordinal)
            .Select(id => new CompDetailCard(id, Tier(id), Frequency(id)))
            .Where(c => c.TechLevel is { } tier && tier <= MaxEarlyTier)
            .OrderByDescending(c => c.FinalBoards) // stable: ties keep their order of first appearance
            .Take(maxEnablers)
            .ToList();

        var commit = composition.CoreCards
            .Select(CardIds.Normalize)
            .Distinct(StringComparer.Ordinal)
            .Select(id => new CompDetailCard(id, Tier(id), Frequency(id)))
            .ToList();

        var median = MedianFinalTurn(composition);

        var note = composition.FinalBoards.Count > 0
            ? $"Derived from {composition.FinalBoards.Count} top final boards (Firestone) and the comp's card lists; the source has no early-game guide."
            : "Derived from the comp's card lists; the source has no final boards and no early-game guide.";
        return new CompDetail(composition, HeaderOf(composition), enablers, commit, median, note);
    }

    private static string HeaderOf(Composition composition)
    {
        var parts = new List<string> { composition.Name };
        if (composition.Tribes.Count > 0)
        {
            parts.Add(string.Join(", ", composition.Tribes.Select(t => t.Substring(0, 1) + t.Substring(1).ToLowerInvariant())));
        }

        if (composition.AveragePlacement is { } placement)
        {
            parts.Add("avg place " + placement.ToString("0.0", CultureInfo.GetCultureInfo("fr-FR")));
        }

        if (composition.DataPoints is { } games)
        {
            // Thousands grouped with a plain space, whatever the culture's own separator.
            parts.Add(games.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", " ") + " games");
        }

        if (!string.IsNullOrEmpty(composition.Tier))
        {
            parts.Add("tier " + composition.Tier);
        }

        return string.Join(" · ", parts);
    }
}
