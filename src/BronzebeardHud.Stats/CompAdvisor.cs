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

/// <summary>What the tavern overlay shows: the target compositions and one marker entry per tavern card.</summary>
public sealed class TavernAdvice
{
    public TavernAdvice(IReadOnlyList<CompProgress> targets, IReadOnlyList<ShopAdvice> cards, int playableCompositions)
    {
        Targets = targets;
        Cards = cards;
        PlayableCompositions = playableCompositions;
    }

    public IReadOnlyList<CompProgress> Targets { get; }

    /// <summary>One entry per tavern card, left to right; an entry with advances gets a marker.</summary>
    public IReadOnlyList<ShopAdvice> Cards { get; }

    /// <summary>Compositions whose tribes are all in the lobby (or tribeless): the pool markers come from.</summary>
    public int PlayableCompositions { get; }

    public int MarkerCount => Cards.Count(c => c.Advances.Count > 0);
}

public static class TavernAdvisor
{
    /// <summary>
    /// Marks the tavern for the player, from turn 1 on. Replay of Ali's game of 2026-09-26 showed why the
    /// first rule (only pieces of the three target compositions) left the tavern unmarked: with an empty
    /// board and hand there is no target, and later the targets' pieces rarely show up. Rule now:
    /// - a <b>key piece</b> of any composition playable in this lobby is marked, target or not (like
    ///   Tier7's comp key pieces), even when the player already holds a copy (a triple is on the way);
    /// - an <b>add-on</b> of a target composition is marked when the player does not hold it yet.
    /// A composition is playable when all its tribes are in the lobby, or it has none; an unknown lobby
    /// (empty list) filters nothing. Compositions on a card are listed targets first, in ranking order,
    /// then by average placement.
    /// </summary>
    public static TavernAdvice Advise(
        IReadOnlyList<string> tavernCardIds,
        IReadOnlyList<OwnedCard> owned,
        IReadOnlyList<Composition> compositions,
        IReadOnlyCollection<string> lobbyTribes)
    {
        var playable = compositions
            .Where(c => lobbyTribes.Count == 0 || c.Tribes.All(lobbyTribes.Contains))
            .ToList();
        var targets = CompAdvisor.Rank(owned, playable);
        var targetOrder = targets.Select((t, i) => (t.Composition.Id, i)).ToDictionary(x => x.Id, x => x.i, StringComparer.Ordinal);
        var ownedIds = new HashSet<string>(owned.Select(c => c.CardId), StringComparer.Ordinal);

        int Order(Composition c) => targetOrder.TryGetValue(c.Id, out var rank) ? rank : int.MaxValue;

        var cards = tavernCardIds.Select((rawId, position) =>
        {
            var cardId = CardIds.Normalize(rawId);
            var advances = playable
                .Where(c => c.CoreCards.Contains(cardId))
                .Select(c => (Composition: c, IsKeyPiece: true))
                .Concat(targets
                    .Select(t => t.Composition)
                    .Where(c => !ownedIds.Contains(cardId) && !c.CoreCards.Contains(cardId) && c.AddonCards.Contains(cardId))
                    .Select(c => (Composition: c, IsKeyPiece: false)))
                .OrderBy(a => Order(a.Composition))
                .ThenBy(a => a.Composition.AveragePlacement ?? double.MaxValue)
                .ThenBy(a => a.Composition.Id, StringComparer.Ordinal)
                .ToList();
            return new ShopAdvice(position, cardId, advances);
        }).ToList();
        return new TavernAdvice(targets, cards, playable.Count);
    }

    /// <summary>
    /// One log line per shop round, e.g.
    /// <c>Bronzebeard HUD: tavern round=3 comps=24 (ok) playable=22 targets=[Murloc Scam 3.5; Murloc Handbuff 0.5] tavern=4 markers=1 first=#0 x=1086 y=497 w=176 h=57 canvas=2291x1360</c>.
    /// </summary>
    public static string DiagnosticLine(int round, int compositionCount, string compositionState, TavernAdvice advice, LayoutRect? firstMarker, double canvasWidth, double canvasHeight)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string N(double v) => Math.Round(v).ToString("0", inv);
        var targets = advice.Targets.Count == 0
            ? "none"
            : "[" + string.Join("; ", advice.Targets.Select(t => $"{t.Composition.Name} {t.Score.ToString("0.#", inv)}")) + "]";
        var firstIndex = advice.Cards.FirstOrDefault(c => c.Advances.Count > 0)?.Position;
        var first = firstIndex is { } index && firstMarker is { } rect
            ? $"#{index} x={N(rect.Left)} y={N(rect.Top)} w={N(rect.Width)} h={N(rect.Height)}"
            : "none";
        return $"Bronzebeard HUD: tavern round={round} comps={compositionCount} ({compositionState}) playable={advice.PlayableCompositions} " +
               $"targets={targets} tavern={advice.Cards.Count} markers={advice.MarkerCount} first={first} canvas={N(canvasWidth)}x{N(canvasHeight)}";
    }
}
