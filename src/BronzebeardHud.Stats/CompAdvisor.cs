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
/// <summary>
/// The player's cards that can steer a composition: minions on the board and every card in hand, since a
/// key piece held in hand orients the game as much as one already played.
/// </summary>
public sealed class PlayerCards
{
    public PlayerCards(IReadOnlyList<OwnedCard> board, IReadOnlyList<OwnedCard> hand)
    {
        Board = board;
        Hand = hand;
    }

    public static PlayerCards None { get; } = new(Array.Empty<OwnedCard>(), Array.Empty<OwnedCard>());

    public IReadOnlyList<OwnedCard> Board { get; }

    public IReadOnlyList<OwnedCard> Hand { get; }

    /// <summary>What the advisor scores: board and hand alike.</summary>
    public IReadOnlyList<OwnedCard> All => Board.Concat(Hand).ToList();
}

public sealed class CompProgress
{
    public CompProgress(Composition composition, IReadOnlyList<string> coreOwned, IReadOnlyList<string> addonOwned, int tribeMatches, double score, double heroBonus = 0)
    {
        HeroBonus = heroBonus;
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

    /// <summary>What the player holds (key pieces, add-ons, tribe), plus <see cref="HeroBonus"/>.</summary>
    public double Score { get; }

    /// <summary>Points given or taken by the hero being played on this composition (<see cref="CompAdvisor.HeroPlacementWeight"/>).</summary>
    public double HeroBonus { get; }
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

    /// <summary>
    /// Points per place the hero being played gains (or loses) on a composition, from
    /// <see cref="HeroCompAffinity"/>. Measured on last-patch (2026-09-26): 90 % of the hero effects lie
    /// within ±0.32 place and all within ±0.76, so at 2 points a place the hero moves a composition by
    /// ±0.6 point usually and 1.5 at most: it decides between compositions equally advanced and may pass a
    /// single add-on (1), never a key piece (3). The hero alone never makes a composition a target.
    /// </summary>
    public const double HeroPlacementWeight = 2;

    /// <param name="heroEffects">The hero being played on each composition (HeroCompAffinity.Effects); none before the pick.</param>
    public static IReadOnlyList<CompProgress> Rank(IEnumerable<OwnedCard> owned, IEnumerable<Composition> compositions, int maxTargets = MaxTargets,
        IReadOnlyDictionary<string, HeroCompPick>? heroEffects = null)
    {
        var cards = owned.ToList();
        return compositions
            .Select(comp => Progress(cards, comp))
            .Where(p => p.Score > 0)
            .Select(p => heroEffects != null && heroEffects.TryGetValue(p.Composition.Id, out var effect)
                ? new CompProgress(p.Composition, p.CoreOwned, p.AddonOwned, p.TribeMatches, p.Score + HeroPlacementWeight * effect.Gain, HeroPlacementWeight * effect.Gain)
                : p)
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.Composition.AveragePlacement ?? double.MaxValue)
            .ThenBy(p => p.Composition.Id, StringComparer.Ordinal)
            .Take(maxTargets)
            .ToList();
    }

    /// <summary>
    /// The suggested compositions: every reachable one (something held counts for it: a key piece, an
    /// add-on or a minion of its tribe; <see cref="Rank"/>), best average placement first, the estimate with
    /// the hero being played when there is one (<see cref="HeroCompAffinity"/>), the composition's own
    /// average otherwise; how far the player is (the score) only breaks ties. At most <paramref name="count"/>.
    /// </summary>
    public static IReadOnlyList<CompProgress> Suggest(IReadOnlyList<OwnedCard> owned, IReadOnlyList<Composition> playable, int count,
        IReadOnlyCollection<string>? exclude = null, IReadOnlyDictionary<string, HeroCompPick>? heroEffects = null) =>
        Rank(owned, playable, int.MaxValue, heroEffects)
            .Where(p => exclude == null || !exclude.Contains(p.Composition.Id))
            .OrderBy(p => PlacementFor(p.Composition, heroEffects))
            .ThenByDescending(p => p.Score)
            .ThenBy(p => p.Composition.Id, StringComparer.Ordinal)
            .Take(Math.Max(0, count))
            .ToList();

    /// <summary>The placement suggestions are sorted by: with the hero being played when known, else the composition's own.</summary>
    public static double PlacementFor(Composition composition, IReadOnlyDictionary<string, HeroCompPick>? heroEffects) =>
        heroEffects != null && heroEffects.TryGetValue(composition.Id, out var effect)
            ? effect.Estimate
            : composition.AveragePlacement ?? double.MaxValue;

    /// <summary>Where the player stands on one composition, whatever its score (a ticked one may be at 0).</summary>
    public static CompProgress Progress(IReadOnlyList<OwnedCard> cards, Composition comp)
    {
        var ids = new HashSet<string>(cards.Select(c => c.CardId), StringComparer.Ordinal);
        var core = comp.CoreCards.Where(ids.Contains).ToList();
        var addon = comp.AddonCards.Where(ids.Contains).ToList();
        var tribeMatches = comp.Tribes.Count == 0
            ? 0
            : cards.Count(c => c.Tribe == Tribes.Any || (c.Tribe != null && comp.Tribes.Contains(c.Tribe)));
        var score = KeyPieceWeight * core.Count + AddonWeight * addon.Count + TribeWeight * tribeMatches;
        return new CompProgress(comp, core, addon, tribeMatches, score);
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
    public TavernAdvice(IReadOnlyList<CompProgress> targets, IReadOnlyList<ShopAdvice> cards, IReadOnlyList<Composition> playable)
    {
        Targets = targets;
        Cards = cards;
        Playable = playable;
    }

    /// <summary>Compositions whose tribes are all in the lobby (or tribeless).</summary>
    public IReadOnlyList<Composition> Playable { get; }

    public IReadOnlyList<CompProgress> Targets { get; }

    /// <summary>One entry per tavern card, left to right; an entry with advances gets a marker.</summary>
    public IReadOnlyList<ShopAdvice> Cards { get; }

    /// <summary>Compositions whose tribes are all in the lobby (or tribeless): the pool markers come from.</summary>
    public int PlayableCompositions => Playable.Count;

    public int MarkerCount => Cards.Count(c => c.Advances.Count > 0);
}

/// <summary>What the target panel shows, and what the markers aim at (<see cref="TavernAdvisor.Aim"/>).</summary>
public sealed class CompositionFocus
{
    public CompositionFocus(IReadOnlyList<CompProgress> shown, IReadOnlyList<CompProgress> aimed, IReadOnlyList<Composition> pool)
    {
        Shown = shown;
        Aimed = aimed;
        Pool = pool;
    }

    public IReadOnlyList<CompProgress> Shown { get; }
    public IReadOnlyList<CompProgress> Aimed { get; }

    /// <summary>The compositions whose key pieces are marked.</summary>
    public IReadOnlyList<Composition> Pool { get; }
}

public static class TavernAdvisor
{
    /// <summary>
    /// Marks the tavern for the player. The compositions shown and aimed at come from <see cref="Aim"/>; a
    /// card is marked when it is a <b>key piece</b> of an aimed composition (even when a copy is held: a
    /// triple is on the way) or an <b>add-on</b> of one not held yet. Before anything is reachable (empty
    /// board and hand, nothing ticked), the key pieces of every composition playable in the lobby are marked,
    /// so that turn 1 is not left blank (replay of Ali's game of 2026-09-26: no marker at all otherwise).
    /// </summary>
    public static TavernAdvice Advise(
        IReadOnlyList<string> tavernCardIds,
        IReadOnlyList<OwnedCard> owned,
        IReadOnlyList<Composition> compositions,
        IReadOnlyCollection<string> lobbyTribes,
        IReadOnlyList<string>? chosen = null,
        IReadOnlyDictionary<string, HeroCompPick>? heroEffects = null,
        int suggested = HudSettings.DefaultSuggested)
    {
        var playable = Playable(compositions, lobbyTribes);
        var focus = Aim(compositions, playable, owned, chosen, suggested, heroEffects);
        var ownedIds = new HashSet<string>(owned.Select(c => c.CardId), StringComparer.Ordinal);
        var cards = tavernCardIds.Select((rawId, position) =>
        {
            var advances = CardEffect.On(rawId, focus.Aimed, focus.Pool, ownedIds)
                .Select(e => (e.Composition, e.IsKeyPiece))
                .ToList();
            return new ShopAdvice(position, CardIds.Normalize(rawId), advances);
        }).ToList();
        return new TavernAdvice(focus.Shown, cards, playable);
    }

    /// <summary>
    /// What the target panel shows, and what the markers aim at (Ali, 2026-09-26: a chosen number of
    /// suggestions, best average result first).
    /// - Shown: the ticked compositions first, in the order they were ticked, then <paramref name="suggested"/>
    ///   reachable compositions (<see cref="CompAdvisor.Suggest"/>), best placement first.
    /// - Aimed: the ticked compositions alone when something is ticked (only their minions are marked);
    ///   otherwise the shown suggestions; before anything is reachable, none, with the key pieces of every
    ///   playable composition as the pool.
    /// </summary>
    public static CompositionFocus Aim(
        IReadOnlyList<Composition> compositions, IReadOnlyList<Composition> playable, IReadOnlyList<OwnedCard> owned,
        IReadOnlyList<string>? chosen, int suggested, IReadOnlyDictionary<string, HeroCompPick>? heroEffects)
    {
        var byId = compositions.GroupBy(c => c.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var ticked = (chosen ?? Array.Empty<string>()).Where(byId.ContainsKey).Select(id => CompAdvisor.Progress(owned, byId[id])).ToList();
        var suggestions = CompAdvisor.Suggest(owned, playable, suggested, ticked.Select(t => t.Composition.Id).ToList(), heroEffects);
        var shown = ticked.Concat(suggestions).ToList();
        if (ticked.Count > 0)
        {
            return new CompositionFocus(shown, ticked, ticked.Select(t => t.Composition).ToList());
        }

        return suggestions.Count > 0
            ? new CompositionFocus(shown, suggestions, suggestions.Select(t => t.Composition).ToList())
            : new CompositionFocus(shown, Array.Empty<CompProgress>(), playable);
    }

    /// <summary>The compositions whose tribes are all in the lobby; all of them when the lobby is unknown.</summary>
    public static IReadOnlyList<Composition> Playable(IReadOnlyList<Composition> compositions, IReadOnlyCollection<string> lobbyTribes) =>
        compositions.Where(c => lobbyTribes.Count == 0 || c.Tribes.All(lobbyTribes.Contains)).ToList();

    /// <summary>
    /// One log line per shop round, e.g.
    /// <c>Bronzebeard HUD: tavern round=3 comps=24 (ok) playable=22 targets=[Murloc Scam 3.5; Murloc Handbuff 0.5] tavern=4 markers=1 first=#0 x=1086 y=497 w=176 h=57 canvas=2291x1360</c>.
    /// </summary>
    /// <summary>
    /// One line per shop round in HDT's log, written when the round ends. compsInLobby counts the
    /// compositions whose tribes are all in the lobby (the only ones the advisor may target); board and hand
    /// count the cards read from HDT; tavern counts Bob's row (minions and the tavern spell), minions the
    /// minions in it; changes and refreshes come from <see cref="TavernRowTracker"/>.
    /// </summary>
    public static string DiagnosticLine(int round, int compositionCount, string compositionState, TavernAdvice advice, PlayerCards cards,
        int minions, int changes, int refreshes, LayoutRect? firstMarker, double canvasWidth, double canvasHeight)
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
        return $"Bronzebeard HUD: tavern round={round} comps={compositionCount} ({compositionState}) compsInLobby={advice.PlayableCompositions} " +
               $"shown={advice.Targets.Count} sort=placement " +
               $"board={cards.Board.Count} hand={cards.Hand.Count} " +
               $"targets={targets} tavern={advice.Cards.Count} minions={minions} markers={advice.MarkerCount} first={first} " +
               $"changes={changes} refreshes={refreshes} canvas={N(canvasWidth)}x{N(canvasHeight)}";
    }
}
