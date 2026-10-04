using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>How far the player's board and hand go towards one of HDT's comp guides.</summary>
public sealed class CompGuideProgress
{
    public CompGuideProgress(CompGuide guide, IReadOnlyList<string> keyHeld, IReadOnlyList<string> enablersHeld, IReadOnlyList<string> addonsHeld,
        double score, int? highlight = null)
    {
        Guide = guide;
        KeyHeld = keyHeld;
        EnablersHeld = enablersHeld;
        AddonsHeld = addonsHeld;
        Score = score;
        Highlight = highlight;
    }

    public CompGuide Guide { get; }

    /// <summary>Key pieces held, in the guide's order.</summary>
    public IReadOnlyList<string> KeyHeld { get; }

    /// <summary>Common enablers held that are not key pieces.</summary>
    public IReadOnlyList<string> EnablersHeld { get; }

    /// <summary>Add-ons held that are neither key pieces nor enablers.</summary>
    public IReadOnlyList<string> AddonsHeld { get; }

    /// <summary>Key pieces still missing, in the guide's order.</summary>
    public IReadOnlyList<string> KeyMissing => Guide.CoreCards.Where(c => !KeyHeld.Contains(c)).ToList();

    // No "every card held" list on purpose: HDT never adds core cards and add-ons together, and a "✓ 6 cards" line that
    // did (key pieces, enablers and add-ons in one list) read as a composition of more than seven cards (2026-10-04).
    // KeyHeld, EnablersHeld and AddonsHeld are shown apart, or counted apart (HeldText).

    /// <summary><see cref="CompGuideMatch"/>'s weighted count of the cards held; 0 when nothing is held.</summary>
    public double Score { get; }

    /// <summary>Share of the key pieces held, 0 to 1 (0 for a guide without key pieces).</summary>
    public double KeyShare => Guide.CoreCards.Count == 0 ? 0 : KeyHeld.Count / (double)Guide.CoreCards.Count;

    /// <summary>1 for the most probable guide, then 2, 3; null when the guide is not highlighted.</summary>
    public int? Highlight { get; }

    public bool IsHighlighted => Highlight != null;

    /// <summary>"★2/4 +1": key pieces held of the guide's key pieces, then the other cards held; empty when nothing is held.</summary>
    public string HeldText
    {
        get
        {
            if (Score <= 0)
            {
                return string.Empty;
            }

            var others = EnablersHeld.Count + AddonsHeld.Count;
            var key = Guide.CoreCards.Count > 0 ? $"★{KeyHeld.Count}/{Guide.CoreCards.Count}" : string.Empty;
            var more = others > 0 ? $"+{others}" : string.Empty;
            return string.Join(" ", new[] { key, more }.Where(s => s.Length > 0));
        }
    }

    internal CompGuideProgress WithHighlight(int rank) => new(Guide, KeyHeld, EnablersHeld, AddonsHeld, Score, rank);
}

/// <summary>One tier of the panel: its guides, the highlighted ones first, then the others in HDT's order.</summary>
public sealed class CompGuideBoardTier
{
    public CompGuideBoardTier(int tier, IReadOnlyList<CompGuideProgress> rows)
    {
        Tier = tier;
        Rows = rows;
    }

    public int Tier { get; }
    public string Letter => CompGuideTiers.Letter(Tier);
    public IReadOnlyList<CompGuideProgress> Rows { get; }
}

/// <summary>What the comp guides panel shows.</summary>
public sealed class CompGuideBoard
{
    /// <param name="all">Every guide, in HDT's order; null: the rows of <paramref name="tiers"/>, in their order.</param>
    /// <param name="ranked">Every guide that scores, most probable first; null: <paramref name="highlighted"/>.</param>
    public CompGuideBoard(IReadOnlyList<CompGuideBoardTier> tiers, IReadOnlyList<CompGuideProgress> highlighted,
        IReadOnlyList<CompGuideProgress>? all = null, IReadOnlyList<CompGuideProgress>? ranked = null)
    {
        Tiers = tiers;
        Highlighted = highlighted;
        All = all ?? tiers.SelectMany(t => t.Rows).ToList();
        Ranked = ranked ?? highlighted;
    }

    public static CompGuideBoard Empty { get; } = new(Array.Empty<CompGuideBoardTier>(), Array.Empty<CompGuideProgress>());

    /// <summary>HDT's tiers, in HDT's order.</summary>
    public IReadOnlyList<CompGuideBoardTier> Tiers { get; }

    /// <summary>The highlighted guides, most probable first.</summary>
    public IReadOnlyList<CompGuideProgress> Highlighted { get; }

    /// <summary>Every guide, in HDT's display order (tier, then tier rank, then name), whatever its score.</summary>
    public IReadOnlyList<CompGuideProgress> All { get; }

    /// <summary>
    /// Every guide with a score above 0, most probable first, in <see cref="CompGuideMatch"/>'s order (score, then share of
    /// key pieces held, then HDT's order). <see cref="Highlighted"/> is its head; CompTargetTracker reads further down
    /// when ticked guides take some of the places.
    /// </summary>
    public IReadOnlyList<CompGuideProgress> Ranked { get; }

    public int Count => Tiers.Sum(t => t.Rows.Count);

    /// <summary>"[1. Name 6 ★2/4; 2. …]", for the log line.</summary>
    public string HighlightSummary
    {
        get
        {
            var inv = CultureInfo.InvariantCulture;
            return "[" + string.Join("; ", Highlighted.Select(p => $"{p.Highlight}. {p.Guide.Name} {p.Score.ToString("0.#", inv)} {p.HeldText}")) + "]";
        }
    }
}

/// <summary>
/// Which of HDT's comp guides the player's cards make most probable. The board and the hand count alike (a key piece
/// held in hand orients the game as much as one already played), golden copies as their base card.
///
/// Score of a guide = <see cref="KeyWeight"/> × key pieces held + <see cref="EnablerWeight"/> × common enablers held
/// + <see cref="AddonWeight"/> × add-ons held, each distinct card counted once, in its strongest role (an enabler that
/// is also a key piece counts as a key piece). The <c>count</c> best non-zero scores are highlighted (<see cref="MaxHighlighted"/>
/// by default); ties go to the larger share of key pieces held, then to HDT's order. The tiers keep HDT's order; in each,
/// the highlighted guides come first, the others keep HDT's order. Stateless: only the current cards count.
/// </summary>
public static class CompGuideMatch
{
    public const double KeyWeight = 3;
    public const double EnablerWeight = 2;
    public const double AddonWeight = 1;
    public const int MaxHighlighted = 3;

    /// <param name="count">How many guides to highlight, at most (fewer when fewer score); 0 or less highlights none.</param>
    public static CompGuideBoard Rank(CompGuideSet guides, PlayerCards cards, int count = MaxHighlighted)
    {
        var held = new HashSet<string>(cards.All.Select(c => c.CardId), StringComparer.Ordinal);
        var order = new Dictionary<CompGuide, int>();
        foreach (var guide in guides.All)
        {
            order[guide] = order.Count;
        }

        var progress = guides.All.ToDictionary(g => g, g => Progress(g, held));
        var ranked = progress.Values
            .Where(p => p.Score > 0)
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.KeyShare)
            .ThenBy(p => order[p.Guide])
            .Select((p, i) => i < count ? p.WithHighlight(i + 1) : p)
            .ToList();
        var highlighted = ranked.Where(p => p.IsHighlighted).ToList();
        foreach (var p in highlighted)
        {
            progress[p.Guide] = p;
        }

        var tiers = guides.Tiers
            .Select(t => new CompGuideBoardTier(t.Tier, t.Guides
                .Select(g => progress[g])
                .OrderBy(p => p.Highlight ?? int.MaxValue)
                .ThenBy(p => order[p.Guide])
                .ToList()))
            .ToList();
        return new CompGuideBoard(tiers, highlighted, guides.All.Select(g => progress[g]).ToList(), ranked);
    }

    /// <summary>Where the player stands on one guide; <paramref name="held"/> holds base card ids.</summary>
    public static CompGuideProgress Progress(CompGuide guide, IReadOnlyCollection<string> held)
    {
        var key = guide.CoreCards.Where(held.Contains).ToList();
        var enablers = guide.Enablers.Where(c => held.Contains(c) && !key.Contains(c)).ToList();
        var addons = guide.AddonCards.Where(c => held.Contains(c) && !key.Contains(c) && !enablers.Contains(c)).ToList();
        var score = KeyWeight * key.Count + EnablerWeight * enablers.Count + AddonWeight * addons.Count;
        return new CompGuideProgress(guide, key, enablers, addons, score);
    }
}
