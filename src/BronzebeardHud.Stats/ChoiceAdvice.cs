using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace BronzebeardHud.Stats;

/// <summary>How the game lays the options out, hence which of HDT's geometries applies.</summary>
public enum ChoiceKind
{
    /// <summary>No choice pending, or it is already made.</summary>
    None,

    /// <summary>Minions or spells: triple reward, tavern spells, "Discover" effects.</summary>
    Discover,

    /// <summary>A discover whose options carry a Dark Gift: larger cards, another layout.</summary>
    DarkGift,

    Trinket,

    /// <summary>Heroes, hero powers, quests, anomalies or a mix: no layout known, so no marker.</summary>
    Unsupported,
}

/// <summary>One option of the player's pending choice, as the plugin reads it from HDT.</summary>
public sealed class OfferedOption
{
    public OfferedOption(int entityId, string cardId, string cardType, string? tribe = null, bool hasDarkGift = false, int zone = ChoiceClassifier.SetAsideZone, string? text = null)
    {
        EntityId = entityId;
        CardId = CardIds.Normalize(cardId);
        CardType = cardType;
        Tribe = tribe;
        HasDarkGift = hasDarkGift;
        Zone = zone;
        Text = text;
    }

    public int EntityId { get; }
    public string CardId { get; }

    /// <summary>HearthDb CardType name: MINION, SPELL, BATTLEGROUND_SPELL, BATTLEGROUND_TRINKET, HERO_POWER…</summary>
    public string CardType { get; }

    public string? Tribe { get; }

    /// <summary>DARK_GIFT_ENTITY &gt; 0, HDT's own test (Windows/OverlayWindow.xaml.cs:1714).</summary>
    public bool HasDarkGift { get; }

    public int Zone { get; }

    /// <summary>English card text, read only for trinkets (see <see cref="TrinketAffinity"/>).</summary>
    public string? Text { get; }
}

public static class ChoiceClassifier
{
    /// <summary>Zone.SETASIDE: where the game keeps the options while the choice is open.</summary>
    public const int SetAsideZone = 6;

    private static readonly HashSet<string> CardLike = new(StringComparer.Ordinal) { "MINION", "SPELL", "BATTLEGROUND_SPELL" };

    /// <summary>
    /// HDT's own sorting of Battlegrounds choices (Windows/OverlayWindow.xaml.cs:1707-1717): all trinkets →
    /// trinket layout; all minions or spells → discover layout, the Dark Gift one when an option carries a
    /// gift. Anything else has no known layout. The choice is over as soon as one option leaves the
    /// set-aside zone (picked into the hand or into play), even before HDT clears its list.
    /// </summary>
    public static ChoiceKind Kind(IReadOnlyList<OfferedOption> options)
    {
        if (options.Count < 2 || options.Any(o => o.Zone != SetAsideZone || string.IsNullOrEmpty(o.CardId)))
        {
            return ChoiceKind.None;
        }

        if (options.All(o => o.CardType == "BATTLEGROUND_TRINKET"))
        {
            return ChoiceKind.Trinket;
        }

        if (options.All(o => CardLike.Contains(o.CardType)))
        {
            return options.Any(o => o.HasDarkGift) ? ChoiceKind.DarkGift : ChoiceKind.Discover;
        }

        return ChoiceKind.Unsupported;
    }

    public static string Name(ChoiceKind kind) => kind switch
    {
        ChoiceKind.DarkGift => "dark-gift",
        _ => kind.ToString().ToLowerInvariant(),
    };
}

/// <summary>What taking one card does to one composition.</summary>
public sealed class CompEffect
{
    public CompEffect(Composition composition, bool isKeyPiece, int targetRank, int keyBefore, int keyAfter)
    {
        Composition = composition;
        IsKeyPiece = isKeyPiece;
        TargetRank = targetRank;
        KeyBefore = keyBefore;
        KeyAfter = keyAfter;
    }

    public Composition Composition { get; }
    public bool IsKeyPiece { get; }

    /// <summary>0: the composition being played (best target); 1, 2: other targets; -1: not a target.</summary>
    public int TargetRank { get; }

    public bool IsCurrent => TargetRank == 0;

    /// <summary>Key pieces held now, and once the card is taken (a copy of a held key piece adds none).</summary>
    public int KeyBefore { get; }
    public int KeyAfter { get; }
    public int KeyTotal => Composition.CoreCards.Count;
}

/// <summary>
/// The one engine that says what a card does for the compositions: the tavern markers and the choice
/// markers both ask it, so that they can never disagree.
/// </summary>
public static class CardEffect
{
    /// <summary>
    /// Key piece of any composition playable in the lobby (held or not: a copy of a held key piece makes a
    /// triple), add-on of a target when not held. Targets first, in their rank, then better placement.
    /// </summary>
    public static IReadOnlyList<CompEffect> On(string rawCardId, IReadOnlyList<CompProgress> targets, IReadOnlyList<Composition> playable, IReadOnlyCollection<string> ownedIds)
    {
        var cardId = CardIds.Normalize(rawCardId);
        var held = ownedIds.Contains(cardId);
        var rank = targets.Select((t, i) => (t.Composition.Id, i)).ToDictionary(x => x.Id, x => x.i, StringComparer.Ordinal);
        int RankOf(Composition c) => rank.TryGetValue(c.Id, out var r) ? r : -1;

        CompEffect Effect(Composition c, bool isKey)
        {
            var before = c.CoreCards.Count(ownedIds.Contains);
            return new CompEffect(c, isKey, RankOf(c), before, isKey && !held ? before + 1 : before);
        }

        return playable
            .Where(c => c.CoreCards.Contains(cardId))
            .Select(c => Effect(c, isKey: true))
            .Concat(targets
                .Select(t => t.Composition)
                .Where(c => !held && !c.CoreCards.Contains(cardId) && c.AddonCards.Contains(cardId))
                .Select(c => Effect(c, isKey: false)))
            .OrderBy(e => e.TargetRank < 0 ? int.MaxValue : e.TargetRank)
            .ThenBy(e => e.Composition.AveragePlacement ?? double.MaxValue)
            .ThenBy(e => e.Composition.Id, StringComparer.Ordinal)
            .ToList();
    }
}

/// <summary>A trinket's placement, and how far it moves once adjusted to the compositions in reach.</summary>
public sealed class TrinketNote
{
    public TrinketNote(double? placement, double? pickRate, double adjustment, Composition? justifiedBy)
    {
        Placement = placement;
        PickRate = pickRate;
        Adjustment = adjustment;
        JustifiedBy = justifiedBy;
    }

    /// <summary>Firestone's average placement for the player's bracket; null when the trinket has no stats.</summary>
    public double? Placement { get; }

    public double? PickRate { get; }

    /// <summary>Places taken off the placement, 0 to <see cref="TrinketAffinity.MaxAdjustment"/>.</summary>
    public double Adjustment { get; }

    public Composition? JustifiedBy { get; }

    public double? Adjusted => Placement - Adjustment;
}

/// <summary>
/// Firestone publishes trinket placements per MMR bracket only, never per composition or tribe (checked on
/// trinket-stats/last-patch, 2026-09-26), so the adjustment is a heuristic, shown with "≈": a trinket whose
/// English text names a tribe of a targeted composition gains up to <see cref="MaxAdjustment"/> places,
/// fully for the composition being played, half for another target.
/// </summary>
public static class TrinketAffinity
{
    /// <summary>The bound: never more than 0.3 of a place, a third of the spread between common trinkets.</summary>
    public const double MaxAdjustment = 0.3;

    public const double CurrentWeight = 1.0;
    public const double AttainableWeight = 0.5;

    private static readonly IReadOnlyDictionary<string, Regex> TribeWords = new Dictionary<string, Regex>(StringComparer.Ordinal)
    {
        ["BEAST"] = Word("beasts?"),
        ["DEMON"] = Word("demons?"),
        ["DRAGON"] = Word("dragons?"),
        ["ELEMENTAL"] = Word("elementals?"),
        ["MECHANICAL"] = Word("mechs?|mechanicals?"),
        ["MURLOC"] = Word("murlocs?"),
        ["NAGA"] = Word("nagas?"),
        ["PIRATE"] = Word("pirates?"),
        ["QUILBOAR"] = Word("quilboars?"),
        ["UNDEAD"] = Word("undead"),
        ["ABERRATION"] = Word("aberrations?"),
    };

    private static Regex Word(string pattern) => new($@"\b(?:{pattern})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>1 when the text names one of the composition's tribes, else 0.</summary>
    public static double Affinity(string? text, Composition composition) =>
        !string.IsNullOrEmpty(text) && composition.Tribes.Any(t => TribeWords.TryGetValue(t, out var word) && word.IsMatch(text))
            ? 1
            : 0;

    public static (double Adjustment, Composition? JustifiedBy) Adjust(string? text, IReadOnlyList<CompProgress> targets)
    {
        var best = 0.0;
        Composition? justifiedBy = null;
        for (var i = 0; i < targets.Count; i++)
        {
            var weighted = Affinity(text, targets[i].Composition) * (i == 0 ? CurrentWeight : AttainableWeight);
            if (weighted > best)
            {
                best = weighted;
                justifiedBy = targets[i].Composition;
            }
        }

        return (MaxAdjustment * best, justifiedBy);
    }
}

public sealed class OptionAdvice
{
    public OptionAdvice(int position, OfferedOption option, IReadOnlyList<CompEffect> effects, TrinketNote? trinket)
    {
        Position = position;
        Option = option;
        Effects = effects;
        Trinket = trinket;
    }

    public int Position { get; }
    public OfferedOption Option { get; }
    public IReadOnlyList<CompEffect> Effects { get; }
    public TrinketNote? Trinket { get; }
}

public sealed class ChoiceAdvice
{
    public ChoiceAdvice(ChoiceKind kind, IReadOnlyList<OptionAdvice> options, IReadOnlyList<CompProgress> targets)
    {
        Kind = kind;
        Options = options;
        Targets = targets;
    }

    public ChoiceKind Kind { get; }
    public IReadOnlyList<OptionAdvice> Options { get; }
    public IReadOnlyList<CompProgress> Targets { get; }
    public bool HasMarkers => Kind is ChoiceKind.Discover or ChoiceKind.DarkGift or ChoiceKind.Trinket;
}

public static class ChoiceAdvisor
{
    public static ChoiceAdvice Advise(
        IReadOnlyList<OfferedOption> options,
        IReadOnlyList<OwnedCard> owned,
        IReadOnlyList<Composition> compositions,
        IReadOnlyCollection<string> lobbyTribes,
        Func<string, TrinketStat?>? trinketStat = null,
        int bracket = MmrBracket.EveryPlayer,
        IReadOnlyList<string>? chosen = null)
    {
        var kind = ChoiceClassifier.Kind(options);
        var playable = TavernAdvisor.Playable(compositions, lobbyTribes);
        var (targets, pool) = TavernAdvisor.Focus(compositions, playable, CompAdvisor.Rank(owned, playable), owned, chosen);
        if (kind is ChoiceKind.None or ChoiceKind.Unsupported)
        {
            return new ChoiceAdvice(kind, Array.Empty<OptionAdvice>(), targets);
        }

        var ownedIds = new HashSet<string>(owned.Select(c => c.CardId), StringComparer.Ordinal);
        var advice = options.Select((option, position) =>
        {
            TrinketNote? note = null;
            if (kind == ChoiceKind.Trinket)
            {
                var stat = trinketStat?.Invoke(option.CardId);
                var (adjustment, justifiedBy) = stat == null ? (0.0, null) : TrinketAffinity.Adjust(option.Text, targets);
                note = new TrinketNote(stat?.PlacementFor(bracket), stat?.PickRate, adjustment, justifiedBy);
            }

            return new OptionAdvice(position, option, CardEffect.On(option.CardId, targets, pool, ownedIds), note);
        }).ToList();
        return new ChoiceAdvice(kind, advice, targets);
    }

    /// <summary>
    /// The lines above one option. A composition it advances: "★ Undead Butcher 2/5→3/5" (key piece, 2 of 5
    /// held, 3 once taken), "★ … 3/5 copy" (a held key piece again), "+ Pirate Discover 1/3" (add-on of a
    /// target). A trinket: its placement, "avg 3.80 → ≈3.50" and the composition behind the "≈". Nothing:
    /// "no target comp". Built to fit <paramref name="maxChars"/>, shortest forms last, never cut by the renderer.
    /// </summary>
    public static IReadOnlyList<string> Lines(OptionAdvice option, int maxChars, bool statsLoaded, int maxLines = 2)
    {
        var inv = CultureInfo.InvariantCulture;
        if (option.Trinket is { } note)
        {
            if (note.Placement is not { } placement)
            {
                return new[] { Fit(statsLoaded ? "no data" : "loading…", maxChars) };
            }

            if (note.Adjustment > 0 && note.JustifiedBy != null)
            {
                return new[]
                {
                    Fit($"avg {placement.ToString("0.00", inv)} → ≈{note.Adjusted!.Value.ToString("0.00", inv)}", maxChars),
                    MarkerText.Label("≈", note.JustifiedBy.Name, string.Empty, maxChars),
                };
            }

            var pick = note.PickRate is { } rate ? $" · {(rate * 100).ToString("0", inv)}%" : string.Empty;
            return new[] { Fit($"avg {placement.ToString("0.00", inv)}{pick}", maxChars) };
        }

        if (option.Effects.Count == 0)
        {
            return new[] { Fit("no target comp", maxChars) };
        }

        string LabelOf(CompEffect e)
        {
            var count = !e.IsKeyPiece ? $"{e.KeyBefore}/{e.KeyTotal}"
                : e.KeyAfter > e.KeyBefore ? $"{e.KeyBefore}/{e.KeyTotal}→{e.KeyAfter}/{e.KeyTotal}"
                : $"{e.KeyBefore}/{e.KeyTotal} copy";
            return MarkerText.Label(e.IsKeyPiece ? "★" : "+", e.Composition.Name, count, maxChars);
        }

        if (option.Effects.Count <= maxLines)
        {
            return option.Effects.Select(LabelOf).ToList();
        }

        var lines = option.Effects.Take(maxLines - 1).Select(LabelOf).ToList();
        var more = $"+{option.Effects.Count - (maxLines - 1)} more";
        lines.Add(MarkerText.DisplayLength(more) <= maxChars ? more : $"+{option.Effects.Count - (maxLines - 1)}");
        return lines;
    }

    private static string Fit(string text, int maxChars) =>
        MarkerText.DisplayLength(text) <= maxChars ? text : text.Substring(0, Math.Max(0, Math.Min(text.Length, maxChars)));

    /// <summary>
    /// One line per choice in HDT's log: what the choice was, in which order HDT gave it, what was targeted,
    /// what each option said and where the first label went. An unsupported choice names its card types.
    /// </summary>
    public static string DiagnosticLine(
        IReadOnlyList<OfferedOption> options, ChoiceAdvice advice, int compositionCount, string compositionState,
        IReadOnlyList<IReadOnlyList<string>> lines, LayoutRect? firstLabel, double canvasWidth, double canvasHeight)
    {
        var inv = CultureInfo.InvariantCulture;
        string N(double v) => Math.Round(v).ToString("0", inv);
        var targets = advice.Targets.Count == 0
            ? "none"
            : "[" + string.Join("; ", advice.Targets.Select(t => $"{t.Composition.Name} {t.Score.ToString("0.#", inv)}")) + "]";
        var head = $"Bronzebeard HUD: choice kind={ChoiceClassifier.Name(advice.Kind)} options={options.Count} " +
                   $"order=[{string.Join(",", options.Select(o => o.EntityId))}] cards=[{string.Join(",", options.Select(o => o.CardId))}]";
        if (!advice.HasMarkers)
        {
            return head + $" types=[{string.Join(",", options.Select(o => o.CardType))}]";
        }

        var said = "[" + string.Join("; ", lines.Select((l, i) => $"#{i} {string.Join(" / ", l)}")) + "]";
        var first = firstLabel is { } rect ? $"x={N(rect.Left)} y={N(rect.Top)} w={N(rect.Width)} h={N(rect.Height)}" : "none";
        return head + $" comps={compositionCount} ({compositionState}) targets={targets} advice={said} first={first} canvas={N(canvasWidth)}x{N(canvasHeight)}";
    }
}
