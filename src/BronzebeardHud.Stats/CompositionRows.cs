using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>One minion of a composition's board, as a vignette.</summary>
public sealed class CompositionVignette
{
    public CompositionVignette(int position, string cardId, bool owned)
    {
        Position = position;
        CardId = cardId;
        Owned = owned;
    }

    /// <summary>1 to 7, left to right on the final board (or list order when the board order is unknown).</summary>
    public int Position { get; }

    public string CardId { get; }
    public bool Owned { get; }
}

/// <summary>One line of the target composition panel.</summary>
public sealed class CompositionRow
{
    public CompositionRow(Composition composition, double score, bool isTarget, int keyOwned, IReadOnlyList<CompositionVignette> vignettes, bool orderKnown)
    {
        Composition = composition;
        Score = score;
        IsTarget = isTarget;
        KeyOwned = keyOwned;
        Vignettes = vignettes;
        OrderKnown = orderKnown;
    }

    public Composition Composition { get; }
    public double Score { get; }

    /// <summary>False for a suggestion (best placement in the lobby) shown before anything is targeted.</summary>
    public bool IsTarget { get; }

    public int KeyOwned { get; }
    public int KeyTotal => Composition.CoreCards.Count;
    public IReadOnlyList<CompositionVignette> Vignettes { get; }

    /// <summary>False when the source gives no board order: vignettes are then key pieces, then add-ons.</summary>
    public bool OrderKnown { get; }

    /// <summary>Average placement as Ali reads it, decimal comma: "3,8"; "–" when unknown.</summary>
    public string PlacementText =>
        Composition.AveragePlacement is { } placement ? placement.ToString("0.0", CultureInfo.GetCultureInfo("fr-FR")) : "–";
}

public static class CompositionRows
{
    public const int MaxRows = 3;

    /// <summary>
    /// Target compositions first, in ranking order; then, while there is room, the lobby's best-placed
    /// playable compositions as suggestions (so the panel is not empty on turn 1). Each row shows the
    /// composition's reference board left to right, a position being owned when the player holds that card
    /// (board or hand, golden copies count), copies matched left to right: holding one copy of a card the
    /// board has twice marks only the first. Without a reference board, key pieces then add-ons, order unknown.
    /// </summary>
    public static IReadOnlyList<CompositionRow> Build(
        IReadOnlyList<CompProgress> targets, IReadOnlyList<Composition> playable, IEnumerable<OwnedCard> owned, int maxRows = MaxRows)
    {
        var ownedCounts = owned
            .GroupBy(c => c.CardId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var rows = targets.Take(maxRows)
            .Select(t => Row(t.Composition, t.Score, isTarget: true, ownedCounts))
            .ToList();
        var shown = new HashSet<string>(rows.Select(r => r.Composition.Id), StringComparer.Ordinal);
        rows.AddRange(playable
            .Where(c => !shown.Contains(c.Id))
            .OrderBy(c => c.AveragePlacement ?? double.MaxValue)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .Take(maxRows - rows.Count)
            .Select(c => Row(c, 0, isTarget: false, ownedCounts)));
        return rows;
    }

    private static CompositionRow Row(Composition composition, double score, bool isTarget, IReadOnlyDictionary<string, int> ownedCounts)
    {
        var orderKnown = composition.ReferenceBoard is { Count: > 0 };
        var cards = orderKnown
            ? composition.ReferenceBoard!.Select(CardIds.Normalize).ToList()
            : composition.CoreCards.Concat(composition.AddonCards).Take(7).ToList();
        var remaining = ownedCounts.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        var vignettes = cards.Select((cardId, index) =>
        {
            var owned = remaining.TryGetValue(cardId, out var left) && left > 0;
            if (owned)
            {
                remaining[cardId] = left - 1;
            }

            return new CompositionVignette(index + 1, cardId, owned);
        }).ToList();
        var keyOwned = composition.CoreCards.Count(ownedCounts.ContainsKey);
        return new CompositionRow(composition, score, isTarget, keyOwned, vignettes, orderKnown);
    }
}

/// <summary>Where the game is, as far as the tavern overlay cares.</summary>
public enum OverlayPhase
{
    OutOfGame,
    HeroSelection,
    Shop,
    Combat,
}

/// <summary>
/// The target composition panel stays up in the shop and in combat (Ali, 2026-09-26: "ne fais pas
/// disparaître l'onglet des compos pendant le combat"); the markers on Bob's cards only in the shop,
/// since the tavern is gone in combat. In combat the panel keeps the last shop's rows.
/// </summary>
public sealed class CompositionPanelState
{
    public static (bool Panel, bool Markers) Visibility(OverlayPhase phase) => phase switch
    {
        OverlayPhase.Shop => (true, true),
        OverlayPhase.Combat => (true, false),
        _ => (false, false),
    };

    public IReadOnlyList<CompositionRow> Rows { get; private set; } = Array.Empty<CompositionRow>();
    public bool PanelVisible { get; private set; }
    public bool MarkersVisible { get; private set; }

    /// <param name="computeRows">Called only in the shop, where the cards are known.</param>
    public void Update(OverlayPhase phase, Func<IReadOnlyList<CompositionRow>> computeRows)
    {
        (PanelVisible, MarkersVisible) = Visibility(phase);
        if (phase == OverlayPhase.Shop)
        {
            Rows = computeRows();
        }
        else if (phase != OverlayPhase.Combat)
        {
            Rows = Array.Empty<CompositionRow>();
        }
    }
}
