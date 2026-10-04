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

/// <summary>A trinket's placement, and how far it moves once adjusted to the targets.</summary>
public sealed class TrinketNote
{
    public TrinketNote(double? placement, double? pickRate, double adjustment, CompTarget? justifiedBy)
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

    /// <summary>The target whose tribe the trinket names; null when it names none.</summary>
    public CompTarget? JustifiedBy { get; }

    public double? Adjusted => Placement - Adjustment;
}

/// <summary>
/// Firestone publishes trinket placements per MMR bracket only, never per composition or tribe (checked on
/// trinket-stats/last-patch, 2026-09-26), so the adjustment is a heuristic, shown with "≈": a trinket whose
/// English text names the main tribe of a target (<see cref="CompGuide.PrimaryTribe"/>) gains up to
/// <see cref="MaxAdjustment"/> places, fully for the first target, half for another one.
/// </summary>
public static class TrinketAffinity
{
    /// <summary>The bound: never more than 0.3 of a place, a third of the spread between common trinkets.</summary>
    public const double MaxAdjustment = 0.3;

    /// <summary>Weight of the first target (<see cref="CompTarget.Rank"/> 1), and of the others.</summary>
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

    /// <summary>1 when the text names the tribe (a HearthDb Race value, <see cref="GuideTribes"/>), else 0; 0 for no tribe.</summary>
    public static double Affinity(string? text, int primaryTribe) =>
        !string.IsNullOrEmpty(text) && GuideTribes.NameOf(primaryTribe) is { } tribe && TribeWords.TryGetValue(tribe, out var word) && word.IsMatch(text)
            ? 1
            : 0;

    /// <summary>The largest weighted affinity among the targets, never their sum: the bound holds.</summary>
    public static (double Adjustment, CompTarget? JustifiedBy) Adjust(string? text, IReadOnlyList<CompTarget> targets)
    {
        var best = 0.0;
        CompTarget? justifiedBy = null;
        for (var i = 0; i < targets.Count; i++)
        {
            var weighted = Affinity(text, targets[i].Guide.PrimaryTribe) * (i == 0 ? CurrentWeight : AttainableWeight);
            if (weighted > best)
            {
                best = weighted;
                justifiedBy = targets[i];
            }
        }

        return (MaxAdjustment * best, justifiedBy);
    }
}

/// <summary>
/// Why an option's label says what it says (the diagnostic line names it). For a minion or a spell, the first that applies, in
/// this order: <see cref="Target"/>, <see cref="TopBoards"/>, <see cref="Pivot"/>, <see cref="Guide"/>, <see cref="None"/>.
/// </summary>
public enum ChoiceReason
{
    /// <summary>Nothing specific: "—".</summary>
    None,

    /// <summary>A core card, enabler or add-on of a target: in the target's colour, with the boards of its bridged comp when known.</summary>
    Target,

    /// <summary>A core card of a guide that is no target, playable in the lobby: neutral.</summary>
    Guide,

    /// <summary>A trinket: its placement, adjusted when it names a target's tribe.</summary>
    Trinket,

    /// <summary>
    /// No guide of a target lists the card, but it stands on at least <see cref="CardEvidence.MinimumBoards"/> of the final
    /// boards of a target's bridged comp (<see cref="GuideBridge"/>): "+ Undead Butcher 3/5 boards", in the target's colour.
    /// </summary>
    TopBoards,

    /// <summary>A core card of a guide a target can pivot to (<see cref="GuidePivots"/>): "pivot → Naga Spells (S)", neutral.</summary>
    Pivot,
}

/// <summary>A guide a target can turn to (<see cref="GuidePivots.For"/>), of which the offered card is a core card.</summary>
public sealed class ChoicePivot
{
    public ChoicePivot(CompTarget from, GuidePivot pivot)
    {
        From = from;
        Pivot = pivot;
    }

    public CompTarget From { get; }

    public GuidePivot Pivot { get; }

    public CompGuide To => Pivot.To;
}

public sealed class OptionAdvice
{
    /// <param name="boards">What the targets' bridged comps say of the card (<see cref="BoardEvidence.For"/>); null: nothing known.</param>
    /// <param name="pivots">Guides a target can pivot to that the card is a core card of; null: none.</param>
    public OptionAdvice(int position, OfferedOption option, IReadOnlyList<GuideCardEffect> effects, IReadOnlyList<CompGuide> guides, TrinketNote? trinket,
        IReadOnlyList<BoardEvidence>? boards = null, IReadOnlyList<ChoicePivot>? pivots = null)
    {
        Position = position;
        Option = option;
        Effects = effects;
        Guides = guides;
        Trinket = trinket;
        Boards = boards ?? Array.Empty<BoardEvidence>();
        Pivots = pivots ?? Array.Empty<ChoicePivot>();
        TopBoards = Boards.Where(b => b.Card.IsTop && GuideCardEffects.RoleIn(b.Target.Guide, option.CardId) == null).ToList();
    }

    /// <summary>One per target whose bridged comp has final boards, in target order: on how many of them the card stands.</summary>
    public IReadOnlyList<BoardEvidence> Boards { get; }

    /// <summary>
    /// Of <see cref="Boards"/>, the targets whose guide does not list the card but whose boards hold it at least
    /// <see cref="CardEvidence.MinimumBoards"/> times. Said only when the card does nothing for any target (<see cref="Reason"/>).
    /// </summary>
    public IReadOnlyList<BoardEvidence> TopBoards { get; }

    /// <summary>
    /// When the card does nothing for the targets: the guides a target can pivot to that it is a core card of (playable in
    /// the lobby, no target), in target order then pivot order; empty otherwise.
    /// </summary>
    public IReadOnlyList<ChoicePivot> Pivots { get; }

    /// <summary>What the target's bridged comp says of the card; null when it has no bridge or no boards.</summary>
    public BoardEvidence? BoardsFor(CompTarget target) => Boards.FirstOrDefault(b => b.Target.Guide.Id == target.Guide.Id);

    public int Position { get; }
    public OfferedOption Option { get; }

    /// <summary>What the option does for the targets (<see cref="GuideCardEffects.On"/>): core cards first, then the rest.</summary>
    public IReadOnlyList<GuideCardEffect> Effects { get; }

    /// <summary>
    /// When it does nothing for the targets: the guides it is a core card of, among the guides playable in the lobby that
    /// are no target, the best tier first (then HDT's order); empty otherwise.
    /// </summary>
    public IReadOnlyList<CompGuide> Guides { get; }

    public TrinketNote? Trinket { get; }

    public ChoiceReason Reason =>
        Trinket != null ? ChoiceReason.Trinket
        : Effects.Count > 0 ? ChoiceReason.Target
        : TopBoards.Count > 0 ? ChoiceReason.TopBoards
        : Pivots.Count > 0 ? ChoiceReason.Pivot
        : Guides.Count > 0 ? ChoiceReason.Guide
        : ChoiceReason.None;

    /// <summary>
    /// The label's colour: the first target's (<see cref="CompTarget.Colour"/>) it serves or whose boards hold it; null for a
    /// neutral label or a trinket.
    /// </summary>
    public string? Colour => Reason switch
    {
        ChoiceReason.Target => Effects[0].Target.Colour,
        ChoiceReason.TopBoards => TopBoards[0].Target.Colour,
        _ => null,
    };
}

public sealed class ChoiceAdvice
{
    public ChoiceAdvice(ChoiceKind kind, IReadOnlyList<OptionAdvice> options, IReadOnlyList<CompTarget> targets)
    {
        Kind = kind;
        Options = options;
        Targets = targets;
    }

    public ChoiceKind Kind { get; }
    public IReadOnlyList<OptionAdvice> Options { get; }
    public IReadOnlyList<CompTarget> Targets { get; }
    public bool HasMarkers => Kind is ChoiceKind.Discover or ChoiceKind.DarkGift or ChoiceKind.Trinket;
}

/// <summary>
/// The labels above the options of a choice, from the comp guide targets: what a card does for a target first (with, when
/// the target's guide is bridged to a Firestone comp, on how many of its top final boards the card stands); failing that,
/// a target's bridged comp whose top boards hold it although its guide does not list it; failing that, a guide a target
/// can pivot to that it is a core card of; failing that, which other guide it is a core card of; failing that, nothing.
/// Never "no target comp": with nothing ticked, the targets are the most probable guides, and a card that serves none of
/// them still says which guide it would start. Without a bridge (null) the labels are those from before the bridge, pivots
/// included: none.
/// </summary>
public static class ChoiceAdvisor
{
    /// <param name="owned">The player's board and hand: the core cards held are counted on them.</param>
    /// <param name="targets">The targets (<see cref="CompTargetTracker.Next"/>), in their order.</param>
    /// <param name="guides">Every guide HDT lists, for the pivots and the fallback; null when HDT gives none.</param>
    /// <param name="lobbyTribes">The lobby's tribes as <see cref="Tribes.All"/> names; empty when unknown (no guide is then left out).</param>
    /// <param name="bridge">
    /// Guide id → its Firestone comp (<see cref="GuideBridge.For"/>). Given, even empty (no Firestone data), it turns on the
    /// enriched labels: boards where a comp is bridged, and pivots, which need no Firestone data. Null: the labels from before
    /// the bridge, word for word (no boards, no pivot), so that a caller that does not pass it sees nothing change.
    /// </param>
    public static ChoiceAdvice Advise(
        IReadOnlyList<OfferedOption> options,
        IReadOnlyList<OwnedCard> owned,
        IReadOnlyList<CompTarget> targets,
        CompGuideSet? guides,
        IReadOnlyCollection<string> lobbyTribes,
        Func<string, TrinketStat?>? trinketStat = null,
        int bracket = MmrBracket.EveryPlayer,
        IReadOnlyDictionary<string, GuideEvidence>? bridge = null)
    {
        var kind = ChoiceClassifier.Kind(options);
        if (kind is ChoiceKind.None or ChoiceKind.Unsupported)
        {
            return new ChoiceAdvice(kind, Array.Empty<OptionAdvice>(), targets);
        }

        var held = new HashSet<string>(owned.Select(c => c.CardId), StringComparer.Ordinal);
        var targetIds = new HashSet<string>(targets.Select(t => t.Guide.Id), StringComparer.Ordinal);
        var others = (guides?.All ?? (IReadOnlyList<CompGuide>)Array.Empty<CompGuide>())
            .Where(g => !targetIds.Contains(g.Id) && GuideTribes.InLobby(g.PrimaryTribe, lobbyTribes))
            .ToList();
        var order = new Dictionary<CompGuide, int>();
        foreach (var guide in others)
        {
            order[guide] = order.Count;
        }

        var otherIds = new HashSet<string>(others.Select(g => g.Id), StringComparer.Ordinal);
        var targetPivots = guides == null || bridge == null
            ? new List<(CompTarget Target, IReadOnlyList<GuidePivot> Pivots)>()
            : targets.Select(t => (Target: t, Pivots: GuidePivots.For(t.Guide, guides, held))).ToList();

        var advice = options.Select((option, position) =>
        {
            if (kind == ChoiceKind.Trinket)
            {
                var stat = trinketStat?.Invoke(option.CardId);
                var (adjustment, justifiedBy) = stat == null ? (0.0, null) : TrinketAffinity.Adjust(option.Text, targets);
                var note = new TrinketNote(stat?.PlacementFor(bracket), stat?.PickRate, adjustment, justifiedBy);
                return new OptionAdvice(position, option, Array.Empty<GuideCardEffect>(), Array.Empty<CompGuide>(), note);
            }

            var effects = GuideCardEffects.On(option.CardId, targets, held);
            var fallback = effects.Count > 0
                ? (IReadOnlyList<CompGuide>)Array.Empty<CompGuide>()
                : others
                    .Where(g => g.CoreCards.Contains(option.CardId))
                    .OrderBy(g => g.Tier)
                    .ThenBy(g => order[g])
                    .ToList();
            var pivots = effects.Count > 0
                ? (IReadOnlyList<ChoicePivot>)Array.Empty<ChoicePivot>()
                : targetPivots
                    .SelectMany(x => x.Pivots
                        .Where(p => otherIds.Contains(p.To.Id) && p.To.CoreCards.Contains(option.CardId))
                        .Select(p => new ChoicePivot(x.Target, p)))
                    .GroupBy(p => p.To.Id, StringComparer.Ordinal)
                    .Select(g => g.First())
                    .ToList();
            return new OptionAdvice(position, option, effects, fallback, null, BoardEvidence.For(option.CardId, targets, bridge), pivots);
        }).ToList();
        return new ChoiceAdvice(kind, advice, targets);
    }

    /// <summary>
    /// The lines above one option, by its <see cref="OptionAdvice.Reason"/>. For a target: "★ core Undead Butcher 2/3→3/3"
    /// (a core card, 2 of 3 held, 3 once taken), "★ core … 3/3 copy" (a held core card again), "+ Undead Butcher" (an enabler
    /// or add-on), each followed by " · 4/5 boards" when the target's bridged comp has final boards and the suffix fits
    /// (<see cref="MarkerText.LabelWithSuffix"/>). Top boards: "+ Undead Butcher 3/5 boards". A pivot: "pivot → Naga Spells (S)".
    /// For another guide: "core Naga Spells (S)" with its tier. A trinket: its placement, "avg 3.80 → ≈3.50" and the target
    /// behind the "≈". Nothing: "—". Several targets or guides: as many lines as fit <paramref name="maxLines"/>, the last one
    /// saying how many are left ("+2 more"). Built to fit <paramref name="maxChars"/>, shortest forms last, never cut by the renderer.
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
                    MarkerText.Label("≈", note.JustifiedBy.Guide.Name, string.Empty, maxChars),
                };
            }

            var pick = note.PickRate is { } rate ? $" · {(rate * 100).ToString("0", inv)}%" : string.Empty;
            return new[] { Fit($"avg {placement.ToString("0.00", inv)}{pick}", maxChars) };
        }

        string EffectLabel(GuideCardEffect e)
        {
            var suffix = option.BoardsFor(e.Target)?.Card.Text is { } boards ? "· " + boards : null;
            if (!e.IsCore)
            {
                return MarkerText.LabelWithSuffix("+", e.Target.Guide.Name, string.Empty, suffix, maxChars);
            }

            var count = e.CoreAfter > e.CoreBefore
                ? $"{e.CoreBefore}/{e.CoreTotal}→{e.CoreAfter}/{e.CoreTotal}"
                : $"{e.CoreBefore}/{e.CoreTotal} copy";
            return MarkerText.LabelWithSuffix("★ core", e.Target.Guide.Name, count, suffix, maxChars);
        }

        var labels = option.Reason switch
        {
            ChoiceReason.Target => option.Effects.Select(EffectLabel).ToList(),
            ChoiceReason.TopBoards => option.TopBoards.Select(b => MarkerText.Label("+", b.Target.Guide.Name, b.Card.Text!, maxChars)).ToList(),
            ChoiceReason.Pivot => option.Pivots.Select(p => MarkerText.Label("pivot →", p.To.Name, $"({p.To.TierLetter})", maxChars)).ToList(),
            ChoiceReason.Guide => option.Guides.Select(g => MarkerText.Label("core", g.Name, $"({g.TierLetter})", maxChars)).ToList(),
            _ => new List<string>(),
        };
        if (labels.Count == 0)
        {
            return new[] { Fit("—", maxChars) };
        }

        if (labels.Count <= maxLines)
        {
            return labels;
        }

        var lines = labels.Take(maxLines - 1).ToList();
        var more = $"+{labels.Count - (maxLines - 1)} more";
        lines.Add(MarkerText.DisplayLength(more) <= maxChars ? more : $"+{labels.Count - (maxLines - 1)}");
        return lines;
    }

    private static string Fit(string text, int maxChars) =>
        MarkerText.DisplayLength(text) <= maxChars ? text : text.Substring(0, Math.Max(0, Math.Min(text.Length, maxChars)));

    /// <summary>
    /// One line per choice in HDT's log: what the choice was, in which order HDT gave it, the targets and their colours,
    /// what each option said and why (target, topboards, pivot, guide, trinket or none) and on what evidence — the bridged
    /// comp, the boards and the usual position ("(Undead Butcher → undead_butcher 4/5 pos 2)") for a target or top boards,
    /// the pivot and its shared cards ("(Undead Butcher → Naga Spells 2 shared)") for a pivot, nothing otherwise — and
    /// where the first label went. An unsupported choice names its card types.
    /// </summary>
    /// <param name="guideCount">Guides HDT lists (CompGuideSet.Count).</param>
    /// <param name="guideState">Where they come from or why there are none (source, or HDT's state).</param>
    public static string DiagnosticLine(
        IReadOnlyList<OfferedOption> options, ChoiceAdvice advice, int guideCount, string guideState,
        IReadOnlyList<IReadOnlyList<string>> lines, LayoutRect? firstLabel, double canvasWidth, double canvasHeight)
    {
        var inv = CultureInfo.InvariantCulture;
        string N(double v) => Math.Round(v).ToString("0", inv);
        var head = $"Bronzebeard HUD: choice kind={ChoiceClassifier.Name(advice.Kind)} options={options.Count} " +
                   $"order=[{string.Join(",", options.Select(o => o.EntityId))}] cards=[{string.Join(",", options.Select(o => o.CardId))}]";
        if (!advice.HasMarkers)
        {
            return head + $" types=[{string.Join(",", options.Select(o => o.CardType))}]";
        }

        string Reason(int i) => i < advice.Options.Count ? advice.Options[i].Reason.ToString().ToLowerInvariant() : "?";
        string Evidence(int i)
        {
            if (i >= advice.Options.Count)
            {
                return string.Empty;
            }

            var o = advice.Options[i];
            var evidence = o.Reason switch
            {
                ChoiceReason.Target => o.Effects.Select(e => o.BoardsFor(e.Target)).Where(b => b != null).Select(b => b!.Diagnostic),
                ChoiceReason.TopBoards => o.TopBoards.Select(b => b.Diagnostic),
                ChoiceReason.Pivot => o.Pivots.Select(p => $"{p.From.Guide.Name} → {p.To.Name} {p.Pivot.Shared.Count.ToString(inv)} shared"),
                _ => Enumerable.Empty<string>(),
            };
            var text = string.Join(", ", evidence);
            return text.Length > 0 ? $" ({text})" : string.Empty;
        }

        var said = "[" + string.Join("; ", lines.Select((l, i) => $"#{i} {Reason(i)}: {string.Join(" / ", l)}{Evidence(i)}")) + "]";
        var first = firstLabel is { } rect ? $"x={N(rect.Left)} y={N(rect.Top)} w={N(rect.Width)} h={N(rect.Height)}" : "none";
        return head + $" guides={guideCount} ({guideState}) targets={CompTargets.Summary(advice.Targets)} advice={said} first={first} canvas={N(canvasWidth)}x{N(canvasHeight)}";
    }
}
