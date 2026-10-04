using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>The Firestone composition a comp guide of HDT was matched to, and the cards the match rests on.</summary>
public sealed class GuideEvidence
{
    public GuideEvidence(CompGuide guide, Composition composition, IReadOnlyList<string> sharedCards, int sharedKeyCount)
    {
        Guide = guide;
        Composition = composition;
        SharedCards = sharedCards;
        SharedKeyCount = sharedKeyCount;
    }

    public CompGuide Guide { get; }

    public Composition Composition { get; }

    /// <summary>The guide's cards (core cards, then add-ons, in its order) found anywhere in the composition.</summary>
    public IReadOnlyList<string> SharedCards { get; }

    /// <summary>How many of the guide's core cards are among <see cref="SharedCards"/>.</summary>
    public int SharedKeyCount { get; }

    /// <summary>The composition's games in Firestone's data; 0 when the source does not say (a hand-written comp).</summary>
    public int Games => Composition.DataPoints ?? 0;
}

/// <summary>
/// HDT's comp guides (HSReplay) and Firestone's compositions are two nomenclatures with no key in common: this bridges them by
/// the cards they share, so that Firestone's statistics (final boards, per-hero figures) can speak for a guide.
///
/// The guide's cards are its core cards and add-ons (not its enablers: they are mostly early cards every comp plays). The
/// composition's cards are everything Firestone gives for it: core cards, add-ons, reference board and final boards, golden
/// copies as their base card. A composition is the guide's only when the match is honest:
/// at least <see cref="MinimumShared"/> cards in common AND at least half of the guide's core cards found in it (two of four,
/// two of three). A guide without core cards is never bridged: nothing would say which comp it is. Among the honest
/// candidates, the most core cards in common, then the most cards in common, then the most games, then the smaller id (so
/// that the cache's order never decides). Below the bar: no bridge, rather than a doubtful one.
///
/// One composition may serve two guides: HSReplay splits into variants what Firestone counts as one archetype, and both
/// variants then read the same statistics. Pure and stateless.
/// </summary>
public static class GuideBridge
{
    public const int MinimumShared = 2;

    /// <summary>The composition that is the guide's (see the class); null when none is honest.</summary>
    public static GuideEvidence? Match(CompGuide guide, IReadOnlyList<Composition> comps)
    {
        var keys = guide.CoreCards.Select(CardIds.Normalize).Distinct(StringComparer.Ordinal).ToList();
        if (keys.Count == 0)
        {
            return null;
        }

        var cards = keys.Concat(guide.AddonCards.Select(CardIds.Normalize)).Distinct(StringComparer.Ordinal).ToList();
        return comps
            .Select(comp =>
            {
                var theirs = CardsOf(comp);
                var shared = cards.Where(theirs.Contains).ToList();
                return new GuideEvidence(guide, comp, shared, keys.Count(theirs.Contains));
            })
            .Where(e => e.SharedCards.Count >= MinimumShared && 2 * e.SharedKeyCount >= keys.Count)
            .OrderByDescending(e => e.SharedKeyCount)
            .ThenByDescending(e => e.SharedCards.Count)
            .ThenByDescending(e => e.Games)
            .ThenBy(e => e.Composition.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>Guide id (<see cref="CompGuide.Id"/>) → its composition, for every guide that has one.</summary>
    public static IReadOnlyDictionary<string, GuideEvidence> For(CompGuideSet guides, IReadOnlyList<Composition> comps)
    {
        var bridge = new Dictionary<string, GuideEvidence>(StringComparer.Ordinal);
        foreach (var guide in guides.All)
        {
            if (!bridge.ContainsKey(guide.Id) && Match(guide, comps) is { } evidence)
            {
                bridge[guide.Id] = evidence;
            }
        }

        return bridge;
    }

    /// <summary>
    /// The line for HDT's log, every guide in HDT's order, so that the match can be judged:
    /// "Bronzebeard HUD: bridge: Undead Butcher → undead_butcher (3/4 keys, 5 cards, 6461 games); Beast Lobster → no match".
    /// "? games" for a composition whose source gives no count.
    /// </summary>
    public static string Line(CompGuideSet guides, IReadOnlyDictionary<string, GuideEvidence> bridge)
    {
        var inv = CultureInfo.InvariantCulture;
        var all = guides.All;
        if (all.Count == 0)
        {
            return "Bronzebeard HUD: bridge: no guides";
        }

        return "Bronzebeard HUD: bridge: " + string.Join("; ", all.Select(g => bridge.TryGetValue(g.Id, out var e)
            ? $"{g.Name} → {e.Composition.Id} ({e.SharedKeyCount.ToString(inv)}/{g.CoreCards.Count.ToString(inv)} keys, " +
              $"{e.SharedCards.Count.ToString(inv)} cards, {(e.Composition.DataPoints is { } games ? games.ToString(inv) : "?")} games)"
            : $"{g.Name} → no match"));
    }

    private static HashSet<string> CardsOf(Composition comp) =>
        new(comp.CoreCards
                .Concat(comp.AddonCards)
                .Concat(comp.ReferenceBoard ?? Array.Empty<string>())
                .Concat(comp.FinalBoards.SelectMany(b => b.Cards))
                .Select(CardIds.Normalize),
            StringComparer.Ordinal);
}

/// <summary>What the final boards of a bridged composition say of one card: on how many of them it stands, and where.</summary>
public sealed class CardEvidence
{
    /// <summary>Boards a card must stand on, at least, to be worth a word when no guide of the target lists it ("≥ 2 of 5").</summary>
    public const int MinimumBoards = 2;

    private CardEvidence(string cardId, GuideEvidence evidence, int boardsWith, int boardCount, int? usualPosition)
    {
        CardId = cardId;
        Evidence = evidence;
        BoardsWith = boardsWith;
        BoardCount = boardCount;
        UsualPosition = usualPosition;
    }

    /// <summary>Base card id (a golden copy is its card).</summary>
    public string CardId { get; }

    public GuideEvidence Evidence { get; }

    /// <summary>Final boards of the composition that hold the card (a board holding two copies counts once).</summary>
    public int BoardsWith { get; }

    /// <summary>Final boards the composition has (Firestone keeps five); 0 for a composition without boards.</summary>
    public int BoardCount { get; }

    /// <summary>
    /// The position (1 to 7, left to right) the card holds most often on those boards, the leftmost on a tie: the
    /// <see cref="MinionLineups.UsualPosition"/> of this one composition; null when on no board.
    /// </summary>
    public int? UsualPosition { get; }

    public bool HasBoards => BoardCount > 0;

    /// <summary>On at least <see cref="MinimumBoards"/> of the boards.</summary>
    public bool IsTop => BoardsWith >= MinimumBoards;

    /// <summary>"4/5"; null without boards.</summary>
    public string? Count => HasBoards ? $"{BoardsWith.ToString(CultureInfo.InvariantCulture)}/{BoardCount.ToString(CultureInfo.InvariantCulture)}" : null;

    /// <summary>"4/5 boards", "0/5 boards"; null without boards.</summary>
    public string? Text => Count is { } count ? count + " boards" : null;

    /// <summary>Counted by <see cref="MinionLineups"/> on the bridged composition alone, so that both say the same.</summary>
    public static CardEvidence For(string cardId, GuideEvidence evidence)
    {
        var lineups = MinionLineups.For(cardId, new[] { evidence.Composition }, maxCompositions: 1);
        var boardsWith = lineups.Compositions.Count > 0 ? lineups.Compositions[0].Boards.Count : 0;
        return new CardEvidence(lineups.CardId, evidence, boardsWith, evidence.Composition.FinalBoards.Count, lineups.UsualPosition);
    }
}

/// <summary>What the bridged composition of one target says of a card (<see cref="CardEvidence"/>).</summary>
public sealed class BoardEvidence
{
    public BoardEvidence(CompTarget target, CardEvidence card)
    {
        Target = target;
        Card = card;
    }

    public CompTarget Target { get; }

    public CardEvidence Card { get; }

    /// <summary>
    /// For each target in its order whose guide is bridged to a composition with final boards, what those boards say of
    /// the card; empty without a bridge.
    /// </summary>
    public static IReadOnlyList<BoardEvidence> For(string cardId, IReadOnlyList<CompTarget> targets, IReadOnlyDictionary<string, GuideEvidence>? bridge)
    {
        if (bridge == null || bridge.Count == 0)
        {
            return Array.Empty<BoardEvidence>();
        }

        return targets
            .Where(t => bridge.TryGetValue(t.Guide.Id, out var e) && e.Composition.FinalBoards.Count > 0)
            .Select(t => new BoardEvidence(t, CardEvidence.For(cardId, bridge[t.Guide.Id])))
            .ToList();
    }

    /// <summary>"Undead Butcher → undead_butcher 4/5 pos 2", for the log lines ("pos" left out when on no board).</summary>
    public string Diagnostic =>
        $"{Target.Guide.Name} → {Card.Evidence.Composition.Id} {Card.Count}" +
        (Card.UsualPosition is { } position ? " pos " + position.ToString(CultureInfo.InvariantCulture) : string.Empty);
}
