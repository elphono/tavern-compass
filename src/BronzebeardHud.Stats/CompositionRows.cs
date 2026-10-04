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
    public CompositionRow(Composition composition, double score, int keyOwned, IReadOnlyList<CompositionVignette> vignettes, bool orderKnown,
        bool isChecked = false, HeroCompPick? heroEffect = null)
    {
        IsChecked = isChecked;
        HeroEffect = heroEffect;
        Composition = composition;
        Score = score;
        KeyOwned = keyOwned;
        Vignettes = vignettes;
        OrderKnown = orderKnown;
    }

    public Composition Composition { get; }
    public double Score { get; }

    /// <summary>Ticked by the player (the ids passed to <see cref="CompositionRows.Build"/>): shown first, whatever its rank.</summary>
    public bool IsChecked { get; }

    /// <summary>
    /// Not ticked: one of the automatic suggestions (reachable, best placement first). A ticked row is a target
    /// the player chose; every other row is a suggestion, and the panel must tell them apart.
    /// </summary>
    public bool IsSuggestion => !IsChecked;

    /// <summary>The hero being played on this composition, when the data qualifies (shown as "≈ 3,5 with your hero (23)").</summary>
    public HeroCompPick? HeroEffect { get; }

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
    /// <summary>
    /// One row per composition shown (<see cref="TavernAdvisor.Aim"/>: ticked ones first, then the
    /// suggestions, best placement first), in that order. Each row shows the composition's reference board
    /// left to right, a position being owned when the player holds that card (board or hand, golden copies
    /// count), copies matched left to right: holding one copy of a card the board has twice marks only the
    /// first. Without a reference board, key pieces then add-ons, order unknown. Nothing is shown before
    /// something is reachable: a list of the lobby's best compositions would be the same every game.
    /// </summary>
    public static IReadOnlyList<CompositionRow> Build(
        IReadOnlyList<CompProgress> shown, IEnumerable<OwnedCard> owned,
        IReadOnlyList<string>? chosen = null, IReadOnlyDictionary<string, HeroCompPick>? heroEffects = null)
    {
        var ownedCounts = owned
            .GroupBy(c => c.CardId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var ticked = new HashSet<string>(chosen ?? Array.Empty<string>(), StringComparer.Ordinal);
        return shown
            .Select(p => Row(p.Composition, p.Score, ownedCounts, ticked.Contains(p.Composition.Id),
                heroEffects != null && heroEffects.TryGetValue(p.Composition.Id, out var effect) ? effect : null))
            .ToList();
    }

    private static CompositionRow Row(Composition composition, double score, IReadOnlyDictionary<string, int> ownedCounts, bool isChecked,
        HeroCompPick? heroEffect)
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
        return new CompositionRow(composition, score, keyOwned, vignettes, orderKnown, isChecked, heroEffect);
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

    /// <summary>New rows while the panel is up, whatever the phase: a composition ticked during combat.</summary>
    public void Replace(IReadOnlyList<CompositionRow> rows)
    {
        if (PanelVisible)
        {
            Rows = rows;
        }
    }

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
