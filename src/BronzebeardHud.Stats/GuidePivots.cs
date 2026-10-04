using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>A comp guide one can turn to from another, and the cards the two have in common.</summary>
public sealed class GuidePivot
{
    public GuidePivot(CompGuide to, IReadOnlyList<string> shared, int heldCount)
    {
        To = to;
        Shared = shared;
        HeldCount = heldCount;
    }

    public CompGuide To { get; }

    /// <summary>
    /// Cards that are a core card or an add-on of both guides: the ones held first, then the others, each part in the
    /// source guide's order (its core cards, then its add-ons).
    /// </summary>
    public IReadOnlyList<string> Shared { get; }

    /// <summary>How many of <see cref="Shared"/> the player holds: they are its first <see cref="HeldCount"/> cards.</summary>
    public int HeldCount { get; }
}

/// <summary>
/// Where a guide can turn to without starting over: the guides that share the most cards with it, the equivalent for
/// HDT's guides of <see cref="CompTransitions"/> (Firestone compositions). A heuristic, not a statistic — neither HSReplay
/// nor Firestone publishes transitions — hence the "≈" on screen. A card counts when it is a core card or an add-on of
/// both (enablers are left out: they are mostly the two lists' cards again, and a pivot is about the board one keeps);
/// a single common card is no pivot, so at least <see cref="MinimumShared"/>.
/// </summary>
public static class GuidePivots
{
    public const int MinimumShared = 2;
    public const int MaxPivots = 2;

    private static IReadOnlyList<string> CardsOf(CompGuide guide) =>
        guide.CoreCards.Concat(guide.AddonCards).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>
    /// Most cards in common first, then HDT's order (<see cref="CompGuideSet.All"/>: tier, tier rank, name); at most
    /// <see cref="MaxPivots"/>, never <paramref name="from"/> itself.
    /// </summary>
    /// <param name="held">Base card ids the player holds (board and hand).</param>
    public static IReadOnlyList<GuidePivot> For(CompGuide from, CompGuideSet all, IReadOnlyCollection<string> held)
    {
        var mine = CardsOf(from);
        return all.All
            .Select((guide, order) => (Guide: guide, Order: order))
            .Where(x => x.Guide.Id != from.Id)
            .Select(x =>
            {
                var theirs = new HashSet<string>(CardsOf(x.Guide), StringComparer.Ordinal);
                var shared = mine.Where(theirs.Contains).ToList();
                var heldShared = shared.Where(held.Contains).ToList();
                return (x.Order, Pivot: new GuidePivot(x.Guide, heldShared.Concat(shared.Where(c => !held.Contains(c))).ToList(), heldShared.Count));
            })
            .Where(x => x.Pivot.Shared.Count >= MinimumShared)
            .OrderByDescending(x => x.Pivot.Shared.Count)
            .ThenBy(x => x.Order)
            .Take(MaxPivots)
            .Select(x => x.Pivot)
            .ToList();
    }

    /// <summary>"≈ pivot → Undead Attack (3 shared) · Naga Spells (2 shared)"; null when there is none.</summary>
    public static string? Text(IReadOnlyList<GuidePivot> pivots) =>
        pivots.Count == 0
            ? null
            : "≈ pivot → " + string.Join(" · ", pivots.Select(p => $"{p.To.Name} ({p.Shared.Count} shared)"));
}
