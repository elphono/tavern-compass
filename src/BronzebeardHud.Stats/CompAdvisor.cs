using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>A card the player holds, on the board or in hand.</summary>
public sealed class OwnedCard
{
    public OwnedCard(string cardId, string? tribe = null)
    {
        CardId = CardIds.Normalize(cardId);
        Tribe = tribe;
    }

    public string CardId { get; }

    /// <summary>Race name (<see cref="Tribes"/>), <see cref="Tribes.Any"/> for amalgams, null for none or a spell.</summary>
    public string? Tribe { get; }
}

/// <summary>How far the player's cards go towards one composition.</summary>
public sealed class CompProgress
{
    public CompProgress(Composition composition, IReadOnlyList<string> coreOwned, IReadOnlyList<string> addonOwned, int tribeMatches, double score)
    {
        Composition = composition;
        CoreOwned = coreOwned;
        AddonOwned = addonOwned;
        TribeMatches = tribeMatches;
        Score = score;
    }

    public Composition Composition { get; }
    public IReadOnlyList<string> CoreOwned { get; }
    public IReadOnlyList<string> AddonOwned { get; }
    public int TribeMatches { get; }
    public double Score { get; }
}

/// <summary>What one card offered by Bob would bring.</summary>
public sealed class ShopAdvice
{
    public ShopAdvice(int position, string cardId, IReadOnlyList<(Composition Composition, bool IsKeyPiece)> advances)
    {
        Position = position;
        CardId = cardId;
        Advances = advances;
    }

    /// <summary>Index of the card in the tavern, left to right.</summary>
    public int Position { get; }

    public string CardId { get; }

    /// <summary>Target compositions this card would add a new piece to. Empty = not relevant.</summary>
    public IReadOnlyList<(Composition Composition, bool IsKeyPiece)> Advances { get; }
}

/// <summary>
/// Picks the compositions the player's board and hand are heading towards, and flags the tavern
/// cards that would move them closer. Stateless: every call looks only at the current cards, so a
/// composition dropped on one turn comes back as soon as the cards point to it again.
///
/// Score of a composition = <see cref="KeyPieceWeight"/> × distinct key pieces held
/// + <see cref="AddonWeight"/> × distinct add-ons held
/// + <see cref="TribeWeight"/> × held minions of one of its tribes (an amalgam matches every tribe).
/// Only compositions scoring above zero are targets; ties go to the better average placement.
/// </summary>
public static class CompAdvisor
{
    public const double KeyPieceWeight = 3;
    public const double AddonWeight = 1;
    public const double TribeWeight = 0.5;
    public const int MaxTargets = 3;

    public static IReadOnlyList<CompProgress> Rank(IEnumerable<OwnedCard> owned, IEnumerable<Composition> compositions, int maxTargets = MaxTargets)
    {
        var cards = owned.ToList();
        var ids = new HashSet<string>(cards.Select(c => c.CardId), StringComparer.Ordinal);
        return compositions
            .Select(comp =>
            {
                var core = comp.CoreCards.Where(ids.Contains).ToList();
                var addon = comp.AddonCards.Where(ids.Contains).ToList();
                var tribeMatches = comp.Tribes.Count == 0
                    ? 0
                    : cards.Count(c => c.Tribe == Tribes.Any || (c.Tribe != null && comp.Tribes.Contains(c.Tribe)));
                var score = KeyPieceWeight * core.Count + AddonWeight * addon.Count + TribeWeight * tribeMatches;
                return new CompProgress(comp, core, addon, tribeMatches, score);
            })
            .Where(p => p.Score > 0)
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.Composition.AveragePlacement ?? double.MaxValue)
            .ThenBy(p => p.Composition.Id, StringComparer.Ordinal)
            .Take(maxTargets)
            .ToList();
    }

    /// <summary>For each tavern card, the target compositions it would add a piece to that the player lacks.</summary>
    public static IReadOnlyList<ShopAdvice> AdviseShop(IReadOnlyList<string> tavernCardIds, IReadOnlyList<CompProgress> targets, IEnumerable<OwnedCard> owned)
    {
        var ids = new HashSet<string>(owned.Select(c => c.CardId), StringComparer.Ordinal);
        return tavernCardIds
            .Select((rawId, position) =>
            {
                var cardId = CardIds.Normalize(rawId);
                var advances = ids.Contains(cardId)
                    ? new List<(Composition, bool)>()
                    : targets
                        .Select(t => t.Composition)
                        .Where(c => c.CoreCards.Contains(cardId) || c.AddonCards.Contains(cardId))
                        .Select(c => (c, c.CoreCards.Contains(cardId)))
                        .ToList();
                return new ShopAdvice(position, cardId, advances);
            })
            .ToList();
    }
}
