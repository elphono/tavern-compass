using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>A composition one can pivot to, and the cards the two have in common.</summary>
public sealed class CompTransition
{
    public CompTransition(Composition to, IReadOnlyList<string> shared)
    {
        To = to;
        Shared = shared;
    }

    public Composition To { get; }

    /// <summary>Cards of both compositions (key pieces, add-ons, final board), in the source composition's order.</summary>
    public IReadOnlyList<string> Shared { get; }
}

/// <summary>
/// Where a composition can turn to without starting over: the playable compositions that share the most
/// cards with it. A heuristic, not a statistic — Firestone publishes no transitions between compositions —
/// hence the "≈" on screen. A card counts when it is a key piece, an add-on or on the reference final board
/// of both; a single common card is no pivot (on last-patch, 2026-09-26, 158 of the 552 pairs of
/// compositions share exactly one card, usually a neutral minion), so at least <see cref="MinimumShared"/>.
/// </summary>
public static class CompTransitions
{
    public const int MinimumShared = 2;
    public const int MaxTransitions = 2;

    private static IEnumerable<string> CardsOf(Composition c) =>
        c.CoreCards.Concat(c.AddonCards).Concat(c.ReferenceBoard ?? Array.Empty<string>()).Select(CardIds.Normalize).Distinct(StringComparer.Ordinal);

    /// <summary>Most cards in common first, then better placement; at most <see cref="MaxTransitions"/>.</summary>
    public static IReadOnlyList<CompTransition> For(Composition from, IReadOnlyList<Composition> candidates)
    {
        var mine = CardsOf(from).ToList();
        return candidates
            .Where(c => c.Id != from.Id)
            .Select(c =>
            {
                var theirs = new HashSet<string>(CardsOf(c), StringComparer.Ordinal);
                return new CompTransition(c, mine.Where(theirs.Contains).ToList());
            })
            .Where(t => t.Shared.Count >= MinimumShared)
            .OrderByDescending(t => t.Shared.Count)
            .ThenBy(t => t.To.AveragePlacement ?? double.MaxValue)
            .ThenBy(t => t.To.Id, StringComparer.Ordinal)
            .Take(MaxTransitions)
            .ToList();
    }

    /// <summary>"≈ pivot → Undead Deathrattle (3 shared) · Naga Spells (2 shared)"; null when there is none.</summary>
    public static string? Text(IReadOnlyList<CompTransition> transitions) =>
        transitions.Count == 0
            ? null
            : "≈ pivot → " + string.Join(" · ", transitions.Select(t => $"{t.To.Name} ({t.Shared.Count} shared)"));
}
