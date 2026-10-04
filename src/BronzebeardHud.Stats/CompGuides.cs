using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// One of the Battlegrounds "Comp Guides" that Hearthstone Deck Tracker shows in its overlay (written by HSReplay,
/// served by hsreplay.net/api/v1/battlegrounds/comp_guides/, deserialized by HDT into HSReplay.Responses.BattlegroundsCompGuide).
/// Card lists hold card ids (HDT hands over dbf ids; the parser resolves them), golden copies mapped to the base card.
/// </summary>
public sealed class CompGuide
{
    public CompGuide(string name, int tier, int tierRank, IReadOnlyList<string> coreCards, IReadOnlyList<string> addonCards,
        IReadOnlyList<string> enablers, IReadOnlyList<string> commitCards, string? whenToCommit = null, string? commonEnablers = null,
        string? howToPlay = null, int difficulty = 0, int primaryTribe = 0, string? representativeCard = null)
    {
        Name = name;
        Tier = tier;
        TierRank = tierRank;
        CoreCards = coreCards;
        AddonCards = addonCards;
        Enablers = enablers;
        CommitCards = commitCards;
        WhenToCommit = whenToCommit;
        CommonEnablers = commonEnablers;
        HowToPlay = howToPlay;
        Difficulty = difficulty;
        PrimaryTribe = primaryTribe;
        RepresentativeCard = representativeCard;
    }

    public string Name { get; }

    /// <summary>HSReplay's tier: 1 = S, 2 = A, 3 = B, 4 = C, 5 = D (HDT: BattlegroundsCompGuideViewModel.TierText).</summary>
    public int Tier { get; }

    /// <summary>Order within the tier, lowest first (HDT's Tier 7 list: OrderBy(comp => comp.TierRank)).</summary>
    public int TierRank { get; }

    public string TierLetter => CompGuideTiers.Letter(Tier);

    /// <summary>The key pieces ("core_cards"), in the guide's order.</summary>
    public IReadOnlyList<string> CoreCards { get; }

    /// <summary>The add-ons ("addon_cards"), key pieces left out.</summary>
    public IReadOnlyList<string> AddonCards { get; }

    /// <summary>The cards named in "common_enablers", in order of appearance.</summary>
    public IReadOnlyList<string> Enablers { get; }

    /// <summary>The cards named in "when_to_commit", in order of appearance.</summary>
    public IReadOnlyList<string> CommitCards { get; }

    /// <summary>"when_to_commit" as plain text, card references replaced by the names written in them; null when absent.</summary>
    public string? WhenToCommit { get; }

    /// <summary>"common_enablers" as plain text; null when absent.</summary>
    public string? CommonEnablers { get; }

    /// <summary>"how_to_play" as plain text; null when absent.</summary>
    public string? HowToPlay { get; }

    /// <summary>1 = hard, 2 = medium, 3 = easy, as HDT reads it.</summary>
    public int Difficulty { get; }

    /// <summary>HearthDb Race value of the guide's main tribe (0 when none).</summary>
    public int PrimaryTribe { get; }

    public string? RepresentativeCard { get; }
}

public static class CompGuideTiers
{
    /// <summary>The letter HDT shows for a tier: BattlegroundsCompGuideViewModel.TierText and BattlegroundsCompsGuidesViewModel.GetCompText.</summary>
    public static string Letter(int tier) => tier switch
    {
        1 => "S",
        2 => "A",
        3 => "B",
        4 => "C",
        5 => "D",
        _ => "?",
    };
}

/// <summary>One tier of guides, in HDT's order.</summary>
public sealed class CompGuideTier
{
    public CompGuideTier(int tier, IReadOnlyList<CompGuide> guides)
    {
        Tier = tier;
        Guides = guides;
    }

    public int Tier { get; }
    public string Letter => CompGuideTiers.Letter(Tier);
    public IReadOnlyList<CompGuide> Guides { get; }
}

/// <summary>
/// The comp guides HDT shows, grouped the way its Tier 7 list groups them (BattlegroundsCompsGuidesViewModel.GetPremiumCompGuides):
/// tiers in ascending order (S, A, B, C, D), and in each tier the guides by tier rank. HDT's free list is one flat list sorted
/// by name, each guide carrying its tier badge; ties in tier rank therefore keep HDT's free order, by name.
/// </summary>
public sealed class CompGuideSet
{
    public CompGuideSet(string source, IReadOnlyList<CompGuideTier> tiers, int unknownCards = 0)
    {
        Source = source;
        Tiers = tiers;
        UnknownCards = unknownCards;
    }

    public static CompGuideSet Empty(string source) => new(source, Array.Empty<CompGuideTier>());

    /// <summary>Where the guides came from (<see cref="CompGuideSources"/>).</summary>
    public string Source { get; }

    public IReadOnlyList<CompGuideTier> Tiers { get; }

    /// <summary>Every guide, in display order.</summary>
    public IReadOnlyList<CompGuide> All => Tiers.SelectMany(t => t.Guides).ToList();

    public int Count => Tiers.Sum(t => t.Guides.Count);

    /// <summary>Card references (dbf ids) the card database could not resolve: left out of the guides, counted for the log.</summary>
    public int UnknownCards { get; }

    /// <summary>"S=4 A=13 B=6", for the log line.</summary>
    public string TierSummary => string.Join(" ", Tiers.Select(t => $"{t.Letter}={t.Guides.Count}"));
}

public static class CompGuideSources
{
    /// <summary>HDT's free list: BattlegroundsCompsGuidesViewModel.Comps (hsreplay.net/api/v1/battlegrounds/comp_guides/).</summary>
    public const string HdtFree = "hdt-free";

    /// <summary>HDT's Tier 7 list, when Tier 7 or a trial is active: BattlegroundsCompsGuidesViewModel.CompsByTier.</summary>
    public const string HdtTier7 = "hdt-tier7";
}

/// <summary>
/// Reads comp guides in the wire format of hsreplay.net, which is also what HDT's HSReplay.dll deserializes
/// (HSReplay.Responses.BattlegroundsCompGuide; measured on 2026-10-04, docs/journal/2026-10-04-comp-guides-hdt.md):
/// <code>
/// [ { "name": "…", "tier": 2, "tier_rank": 1, "difficulty": 1, "primary_tribe": 17,
///     "core_cards": [dbf, …], "addon_cards": [dbf, …], "representative_card": "BG…",
///     "how_to_play": "…", "when_to_commit": "[[Card name||dbf]] + …", "common_enablers": "[[Card name||dbf]]\n…" } ]
/// </code>
/// or, for the Tier 7 list, <c>{"by_tier": {"1": [guide, …], "2": […]}}</c>, where the key is the tier. Other fields are ignored.
/// A guide that breaks a rule rejects the whole list with a message naming it: a doubtful list is never shown.
/// </summary>
public static class CompGuideParser
{
    /// <param name="cardIdOf">dbf id → card id, null when unknown (HearthDb in the plugin).</param>
    public static CompGuideSet Parse(string json, Func<int, string?> cardIdOf, string source)
    {
        JToken root;
        try
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
            root = JToken.ReadFrom(reader);
        }
        catch (JsonException e)
        {
            throw new StatsFormatException($"comp guides: invalid JSON ({e.Message})", e);
        }

        return root switch
        {
            JArray list => Build(list.Select(g => ((int?)null, g)), cardIdOf, source),
            JObject { } o when o["by_tier"] is JObject byTier => Build(TieredEntries(byTier), cardIdOf, source),
            _ => throw new StatsFormatException("comp guides: expected a list of guides or {\"by_tier\": {...}}"),
        };
    }

    /// <summary>
    /// HDT's free list: each object (an HSReplay.Responses.BattlegroundsCompGuide) is read through its own JSON attributes,
    /// which give the wire names above.
    /// </summary>
    public static CompGuideSet FromObjects(IEnumerable<object> guides, Func<int, string?> cardIdOf, string source) =>
        Build(guides.Select(g => ((int?)null, ToToken(g))), cardIdOf, source);

    /// <summary>HDT's Tier 7 list: tier → guides; the tier is the key, as HDT groups them.</summary>
    public static CompGuideSet FromTiers(IEnumerable<KeyValuePair<int, IEnumerable<object>>> byTier, Func<int, string?> cardIdOf, string source) =>
        Build(byTier.SelectMany(kv => kv.Value.Select(g => ((int?)kv.Key, ToToken(g)))), cardIdOf, source);

    private static JToken ToToken(object? guide) => guide == null ? JValue.CreateNull() : JToken.FromObject(guide);

    private static IEnumerable<(int?, JToken)> TieredEntries(JObject byTier)
    {
        foreach (var property in byTier.Properties())
        {
            if (!int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tier))
            {
                throw new StatsFormatException($"comp guides: by_tier key \"{property.Name}\" is not a tier number");
            }

            if (property.Value is not JArray guides)
            {
                throw new StatsFormatException($"comp guides: by_tier \"{property.Name}\" must be a list");
            }

            foreach (var guide in guides)
            {
                yield return (tier, guide);
            }
        }
    }

    private static CompGuideSet Build(IEnumerable<(int? Tier, JToken Guide)> entries, Func<int, string?> cardIdOf, string source)
    {
        var unknown = 0;
        string? Resolve(int dbfId)
        {
            var id = cardIdOf(dbfId);
            if (string.IsNullOrEmpty(id))
            {
                unknown++;
                return null;
            }

            return CardIds.Normalize(id!);
        }

        var guides = new List<CompGuide>();
        var index = 0;
        foreach (var (keyTier, token) in entries)
        {
            index++;
            guides.Add(Guide(token, keyTier, index, Resolve));
        }

        var tiers = guides
            .GroupBy(g => g.Tier)
            .OrderBy(g => g.Key)
            .Select(g => new CompGuideTier(g.Key, g
                .OrderBy(x => x.TierRank)
                .ThenBy(x => x.Name, StringComparer.InvariantCulture)
                .ToList()))
            .ToList();
        return new CompGuideSet(source, tiers, unknown);
    }

    private static CompGuide Guide(JToken token, int? keyTier, int index, Func<int, string?> resolve)
    {
        if (token is not JObject guide)
        {
            throw new StatsFormatException($"comp guide #{index}: expected an object");
        }

        var name = guide["name"]?.Type == JTokenType.String ? guide.Value<string>("name")!.Trim() : string.Empty;
        if (name.Length == 0)
        {
            throw new StatsFormatException($"comp guide #{index}: name must be a non-empty string");
        }

        var where = $"comp guide #{index} \"{name}\"";
        int Int(string field, int? fallback)
        {
            var value = guide[field];
            if (value == null || value.Type == JTokenType.Null)
            {
                return fallback ?? throw new StatsFormatException($"{where}: {field} is required");
            }

            return value.Type == JTokenType.Integer
                ? value.Value<int>()
                : throw new StatsFormatException($"{where}: {field} must be an integer");
        }

        List<string> Cards(string field, bool required)
        {
            var value = guide[field];
            if (value == null || value.Type == JTokenType.Null)
            {
                return required ? throw new StatsFormatException($"{where}: {field} is required") : new List<string>();
            }

            if (value is not JArray array || array.Any(v => v.Type != JTokenType.Integer))
            {
                throw new StatsFormatException($"{where}: {field} must be a list of card dbf ids");
            }

            return array.Select(v => resolve(v.Value<int>())).Where(id => id != null).Select(id => id!).Distinct(StringComparer.Ordinal).ToList();
        }

        string? Text(string field)
        {
            var value = guide[field];
            if (value == null || value.Type == JTokenType.Null)
            {
                return null;
            }

            return value.Type == JTokenType.String ? value.Value<string>() : throw new StatsFormatException($"{where}: {field} must be a string");
        }

        var tier = keyTier ?? Int("tier", null);
        var core = Cards("core_cards", required: true);
        var addon = Cards("addon_cards", required: false).Except(core, StringComparer.Ordinal).ToList();
        var commit = CompGuideText.Parse(Text("when_to_commit"), resolve);
        var enablers = CompGuideText.Parse(Text("common_enablers"), resolve);
        var howToPlay = CompGuideText.Parse(Text("how_to_play"), resolve);
        var representative = Text("representative_card");
        return new CompGuide(name, tier, Int("tier_rank", 0), core, addon, enablers.CardIds, commit.CardIds,
            commit.PlainText, enablers.PlainText, howToPlay.PlainText, Int("difficulty", 0), Int("primary_tribe", 0),
            string.IsNullOrEmpty(representative) ? null : representative);
    }
}

/// <summary>A guide's text with its card references: the plain text, and the cards referenced, in order.</summary>
public sealed class CompGuideTextParts
{
    public CompGuideTextParts(string? plainText, IReadOnlyList<string> cardIds)
    {
        PlainText = plainText;
        CardIds = cardIds;
    }

    public string? PlainText { get; }
    public IReadOnlyList<string> CardIds { get; }
}

/// <summary>
/// Card references in a guide's text, the way HDT reads them (Controls/Overlay/Battlegrounds/Guides/ReferencedCardRun.cs,
/// ParseCardsFromText): <c>[[Card name||dbf id]]</c>, or <c>[[Card name]]</c> without a card; an unclosed <c>[[</c> stays text.
/// </summary>
public static class CompGuideText
{
    public static CompGuideTextParts Parse(string? text, Func<int, string?> resolve)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new CompGuideTextParts(null, Array.Empty<string>());
        }

        var plain = new StringBuilder();
        var cards = new List<string>();
        var lines = text!.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        for (var l = 0; l < lines.Length; l++)
        {
            if (l > 0)
            {
                plain.Append('\n');
            }

            var line = lines[l];
            var index = 0;
            while (index < line.Length)
            {
                var open = line.IndexOf("[[", index, StringComparison.Ordinal);
                if (open == -1)
                {
                    plain.Append(line, index, line.Length - index);
                    break;
                }

                plain.Append(line, index, open - index);
                var start = open + 2;
                var close = line.IndexOf("]]", start, StringComparison.Ordinal);
                if (close == -1)
                {
                    plain.Append(line, open, line.Length - open);
                    break;
                }

                var separator = line.IndexOf("||", start, StringComparison.Ordinal);
                if (separator > close)
                {
                    separator = -1;
                }

                var nameEnd = separator != -1 ? separator : close;
                plain.Append(line, start, nameEnd - start);
                if (separator != -1
                    && int.TryParse(line.Substring(separator + 2, close - separator - 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var dbfId)
                    && resolve(dbfId) is { } cardId
                    && !cards.Contains(cardId))
                {
                    cards.Add(cardId);
                }

                index = close + 2;
            }
        }

        return new CompGuideTextParts(plain.ToString(), cards);
    }
}
