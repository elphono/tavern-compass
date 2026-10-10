using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>A guide the lobby cannot play, and why ("no BEAST", "key cards X, Y: no QUILBOAR").</summary>
public sealed class LeftOutGuide
{
    public LeftOutGuide(CompGuide guide, string reason)
    {
        Guide = guide;
        Reason = reason;
    }

    public CompGuide Guide { get; }
    public string Reason { get; }
}

/// <summary>
/// The comp guides a Battlegrounds lobby can play: what the panel lists, what the targets are chosen from, what the frames
/// on Bob's cards, the labels of choices and the pivots speak of. A game has about five of the tribes; HDT's free list of
/// guides is not filtered on them (only its Tier 7 list is: docs/journal/2026-10-04-comp-guides-hdt.md), and on 2026-10-06
/// 15 of Ali's 69 shop rounds had a target of a tribe absent from the lobby, because a neutral key card held (Titus
/// Rivendare is a key card of several guides) made those guides look probable (docs/journal/2026-10-06-tribus-cases-puissance.md).
///
/// A guide is left out when its main tribe (<see cref="CompGuide.PrimaryTribe"/>) is a Battlegrounds tribe the lobby does not
/// have, whatever its key cards: the guide is written for that tribe (its board, its add-ons, its how-to-play). It is also left
/// out when at least half of its key cards cannot show up in the lobby (<see cref="MissingKeyShare"/>): a card shows up when it
/// has no tribe, is an amalgam ("ALL"), or has one of its tribes (dual types: either) in the lobby; a card whose tribes are
/// unknown is never held against a guide. A lobby not known yet (empty) leaves nothing out.
/// </summary>
public sealed class LobbyGuides
{
    /// <summary>A guide is left out when this share of its key cards, or more, cannot show up in the lobby.</summary>
    public const double MissingKeyShare = 0.5;

    /// <summary>With a key card missing, the share of add-ons missing that leaves the guide out.</summary>
    public const double MissingAddonShare = 0.5;

    private readonly Dictionary<string, string> _reasons;
    private readonly Func<string, IReadOnlyCollection<string>?>? _cardTribes;

    private LobbyGuides(CompGuideSet all, CompGuideSet playable, IReadOnlyList<LeftOutGuide> leftOut, IReadOnlyList<string> tribes,
        Func<string, IReadOnlyCollection<string>?>? cardTribes = null)
    {
        _cardTribes = cardTribes;
        All = all;
        Playable = playable;
        LeftOut = leftOut;
        Tribes = tribes;
        _reasons = leftOut.GroupBy(l => l.Guide.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Reason, StringComparer.Ordinal);
    }

    /// <summary>Every guide HDT lists, as given.</summary>
    public CompGuideSet All { get; }

    /// <summary>The guides this lobby can play, in HDT's tiers and order; a tier left empty is dropped.</summary>
    public CompGuideSet Playable { get; }

    /// <summary>The guides left out, in HDT's order, with why.</summary>
    public IReadOnlyList<LeftOutGuide> LeftOut { get; }

    /// <summary>The lobby's tribes (<see cref="Stats.Tribes.All"/> names), sorted; empty when not known yet.</summary>
    public IReadOnlyList<string> Tribes { get; }

    public bool Known => Tribes.Count > 0;

    /// <summary>Every guide playable: the lobby is not known (yet).</summary>
    public static LobbyGuides Unknown(CompGuideSet guides) => new(guides, guides, Array.Empty<LeftOutGuide>(), Array.Empty<string>());

    /// <param name="lobbyTribes">The lobby's tribes as <see cref="Stats.Tribes.All"/> names (GuideTribes.NameOrEnum); empty when unknown.</param>
    /// <param name="cardTribes">A card's tribes (Battlegrounds names, "ALL" for an amalgam, empty for none); null when the card is unknown.</param>
    public static LobbyGuides Of(CompGuideSet guides, IReadOnlyCollection<string> lobbyTribes, Func<string, IReadOnlyCollection<string>?> cardTribes)
    {
        var tribes = lobbyTribes.Where(Stats.Tribes.All.Contains).Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).ToList();
        if (tribes.Count == 0)
        {
            return Unknown(guides);
        }

        var leftOut = new List<LeftOutGuide>();
        foreach (var guide in guides.All)
        {
            if (Unplayable(guide, tribes, cardTribes) is { } reason)
            {
                leftOut.Add(new LeftOutGuide(guide, reason));
            }
        }

        var out_ = new HashSet<string>(leftOut.Select(l => l.Guide.Id), StringComparer.Ordinal);
        var playable = new CompGuideSet(guides.Source, guides.Tiers
            .Select(t => new CompGuideTier(t.Tier, t.Guides.Where(g => !out_.Contains(g.Id)).ToList()))
            .Where(t => t.Guides.Count > 0)
            .ToList(), guides.UnknownCards);
        return new LobbyGuides(guides, playable, leftOut, tribes, cardTribes);
    }

    /// <summary>Why the lobby cannot play a guide; null when it can (or when the lobby is not known).</summary>
    public static string? Unplayable(CompGuide guide, IReadOnlyCollection<string> lobbyTribes, Func<string, IReadOnlyCollection<string>?> cardTribes)
    {
        if (lobbyTribes.Count == 0)
        {
            return null;
        }

        if (GuideTribes.NameOf(guide.PrimaryTribe) is { } main && !lobbyTribes.Contains(main))
        {
            return "no " + main;
        }

        var missing = new List<(string Card, IReadOnlyList<string> Tribes)>();
        foreach (var card in guide.CoreCards)
        {
            if (AbsentTribes(card, lobbyTribes, cardTribes) is { } cardAbsent)
            {
                missing.Add((card, cardAbsent));
            }
        }

        if (missing.Count == 0)
        {
            return null;
        }

        if (missing.Count >= MissingKeyShare * guide.CoreCards.Count)
        {
            return $"key cards {string.Join(", ", missing.Select(m => m.Card))}: no {Absent(missing)}";
        }

        // A key card short is still a guide; a key card short and half of its support with it is not (2026-10-10).
        var addons = new List<(string Card, IReadOnlyList<string> Tribes)>();
        foreach (var card in guide.AddonCards)
        {
            if (AbsentTribes(card, lobbyTribes, cardTribes) is { } cardAbsent)
            {
                addons.Add((card, cardAbsent));
            }
        }

        if (addons.Count == 0 || addons.Count < MissingAddonShare * guide.AddonCards.Count)
        {
            return null;
        }

        return $"key cards {string.Join(", ", missing.Select(m => m.Card))} and add-ons {string.Join(", ", addons.Select(m => m.Card))}: " +
            $"no {Absent(missing.Concat(addons))}";
    }

    /// <summary>
    /// True when this card cannot show up in the lobby: every one of its Battlegrounds tribes is absent. Never for a neutral
    /// card, an amalgam, a card the database does not know, or while the lobby is not known. The panel leaves such a key card
    /// out of a guide's line, and shows it greyed and struck in the detail and the popup (Ali, 2026-10-08).
    /// </summary>
    private static string Absent(IEnumerable<(string Card, IReadOnlyList<string> Tribes)> cards) =>
        string.Join(", ", cards.SelectMany(m => m.Tribes).Distinct(StringComparer.Ordinal));

    public bool CannotShowUp(string cardId) => Known && _cardTribes != null && AbsentTribes(cardId, Tribes, _cardTribes) != null;

    /// <summary>The card's Battlegrounds tribes when none is in the lobby; null when it can show up (or is unknown).</summary>
    private static IReadOnlyList<string>? AbsentTribes(string card, IReadOnlyCollection<string> lobbyTribes, Func<string, IReadOnlyCollection<string>?> cardTribes)
    {
        var known = cardTribes(CardIds.Normalize(card));
        if (known == null || known.Contains(Stats.Tribes.Any))
        {
            return null; // unknown: never held against the guide; an amalgam is every tribe
        }

        var battlegrounds = known.Where(Stats.Tribes.All.Contains).ToList();
        return battlegrounds.Count > 0 && !battlegrounds.Any(lobbyTribes.Contains) ? battlegrounds : null;
    }

    /// <summary>Why the lobby cannot play this guide; null when it can.</summary>
    public string? Reason(string guideId) => _reasons.TryGetValue(guideId, out var reason) ? reason : null;

    /// <summary>
    /// The line for HDT's log, whenever the lobby or the list of guides changes: "Bronzebeard HUD: lobby tribes=[DEMON,…]
    /// (shop turn 1): 14/27 guides playable; left out: Beasts - Lobster (no BEAST), …", or "lobby tribes unknown (…)".
    /// </summary>
    /// <param name="when">When it was read, for measuring when HDT knows the lobby ("hero selection", "shop turn 3").</param>
    public string Line(string when)
    {
        if (!Known)
        {
            return $"Bronzebeard HUD: lobby tribes unknown ({when}): {All.Count} guides, none left out";
        }

        var left = LeftOut.Count == 0 ? "none" : string.Join(", ", LeftOut.Select(l => $"{l.Guide.Name} ({l.Reason})"));
        return $"Bronzebeard HUD: lobby tribes=[{string.Join(",", Tribes)}] ({when}): {Playable.Count}/{All.Count} guides playable; left out: {left}";
    }
}
