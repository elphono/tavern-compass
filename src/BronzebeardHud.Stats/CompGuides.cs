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
    /// <param name="howToPlayLines">"how_to_play" as lines of runs, card references kept apart; null: <paramref name="howToPlay"/> split into lines of plain text.</param>
    /// <param name="whenToCommitLines">"when_to_commit" the same way; null: <paramref name="whenToCommit"/> split into lines of plain text.</param>
    public CompGuide(string name, int tier, int tierRank, IReadOnlyList<string> coreCards, IReadOnlyList<string> addonCards,
        IReadOnlyList<string> enablers, IReadOnlyList<string> commitCards, string? whenToCommit = null, string? commonEnablers = null,
        string? howToPlay = null, int difficulty = 0, int primaryTribe = 0, string? representativeCard = null,
        IReadOnlyList<IReadOnlyList<CompGuideTextRun>>? howToPlayLines = null, IReadOnlyList<IReadOnlyList<CompGuideTextRun>>? whenToCommitLines = null)
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
        Id = name + "/" + primaryTribe.ToString(CultureInfo.InvariantCulture);
        HowToPlayLines = CompGuideText.NonBlank(howToPlayLines ?? CompGuideText.PlainLines(howToPlay));
        WhenToCommitLines = CompGuideText.NonBlank(whenToCommitLines ?? CompGuideText.PlainLines(whenToCommit));
    }

    /// <summary>"how_to_play" line by line, blank lines left out, card names apart (drawn in bold, as HDT draws them).</summary>
    public IReadOnlyList<IReadOnlyList<CompGuideTextRun>> HowToPlayLines { get; }

    /// <summary>
    /// The first line of "how_to_play", the one the panel shows (HDT's guides open on a one-line summary; what follows is
    /// detail the overlay has no room for); empty when there is none.
    /// </summary>
    public IReadOnlyList<CompGuideTextRun> HowToPlayFirstLine => HowToPlayLines.Count > 0 ? HowToPlayLines[0] : Array.Empty<CompGuideTextRun>();

    /// <summary>"when_to_commit" line by line, blank lines left out: one condition per line, as HDT lists them.</summary>
    public IReadOnlyList<IReadOnlyList<CompGuideTextRun>> WhenToCommitLines { get; }

    /// <summary>
    /// The guide's id for the plugin, stable across HDT's reloads of its list: the name and the tribe, "Undead Butcher/11".
    /// HSReplay's wire format carries an "id", but the objects HDT hands over do not (HSReplay.Responses.BattlegroundsCompGuide
    /// in HSReplay.dll 1.58.6: Name, Tier, TierRank, Difficulty, PrimaryTribe, RepresentativeCard, CoreCards, AddonCards,
    /// HowToPlay, WhenToCommit, CommonEnablers, LastUpdated), so the id is derived from what both carry.
    /// </summary>
    public string Id { get; }

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

    /// <summary>1 = hard, 2 = medium, 3 = easy, as HDT reads it (<see cref="CompGuideDifficulty"/>).</summary>
    public int Difficulty { get; }

    /// <summary>HearthDb Race value of the guide's main tribe (0 when none); its name: <see cref="GuideTribes.NameOf"/>.</summary>
    public int PrimaryTribe { get; }

    public string? RepresentativeCard { get; }
}

/// <summary>
/// A guide's <see cref="CompGuide.PrimaryTribe"/> (a HearthDb.Enums.Race value) as the tribe name the rest of the
/// plugin uses: <see cref="Tribes.All"/>, which is also what HDT's lobby tribes give (HdtEntityAdapter.LobbyTribeNames:
/// Race.ToString()). Values read from HearthDb.dll of HDT 1.58.6 (enum constants, not a guess): UNDEAD = 11,
/// MURLOC = 14, DEMON = 15, MECHANICAL = 17, ELEMENTAL = 18, BEAST = 20 (PET = 20 too), PIRATE = 23, DRAGON = 24,
/// QUILBOAR = 43, NAGA = 92, ABERRATION = 126. The table maps values, never enum names: Race.ToString() on 20 may as
/// well say "PET", since two names share it.
/// </summary>
public static class GuideTribes
{
    private static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
    {
        [11] = "UNDEAD",
        [14] = "MURLOC",
        [15] = "DEMON",
        [17] = "MECHANICAL",
        [18] = "ELEMENTAL",
        [20] = "BEAST",
        [23] = "PIRATE",
        [24] = "DRAGON",
        [43] = "QUILBOAR",
        [92] = "NAGA",
        [126] = "ABERRATION",
    };

    /// <summary>"MECHANICAL" for 17; null for 0 (no tribe) and for a value that is no Battlegrounds tribe.</summary>
    public static string? NameOf(int race) => Names.TryGetValue(race, out var name) ? name : null;

    /// <summary>
    /// The name of a race read from the game (HdtEntityAdapter: a minion's tribe, the lobby's tribes): the Battlegrounds
    /// name of a Battlegrounds tribe, whatever name the runtime gives the value, otherwise <paramref name="enumName"/>
    /// (Race.ToString(), e.g. "ALL" for an amalgam). Measured on 2026-10-04 with HearthDb.dll of HDT 1.58.6: ((Race)20)
    /// .ToString() is "BEAST" under .NET Framework 4.8, the runtime HDT runs on, but "PET" under .NET 8. Two names share
    /// the value, and which one comes out is the runtime's choice: the lobby's beasts would read as "PET", which no tribe
    /// list knows, and every beast guide would be taken for one the lobby cannot play.
    /// </summary>
    public static string NameOrEnum(int race, string enumName) => NameOf(race) ?? enumName;

    /// <summary>
    /// Whether a guide of this tribe can be played in the lobby. True when the lobby is unknown (empty), when the guide
    /// has no tribe, and when its tribe is not one this table knows: a guide is left out only on a positive mismatch.
    /// </summary>
    public static bool InLobby(int race, IReadOnlyCollection<string> lobbyTribes) =>
        lobbyTribes.Count == 0 || NameOf(race) is not { } name || lobbyTribes.Contains(name);
}

/// <summary>
/// The difficulty badge of a guide, as HDT draws it: BattlegroundsCompGuideViewModel's constructor (HDT 1.58.6, read from
/// its IL on 2026-10-04) switches on Difficulty − 1 into "Hard", "Medium", "Easy", else "Unknown", and the colours
/// #7f303e, #917b43, #49634b, else #404040.
/// </summary>
public static class CompGuideDifficulty
{
    public static string Text(int difficulty) => difficulty switch
    {
        1 => "Hard",
        2 => "Medium",
        3 => "Easy",
        _ => "Unknown",
    };

    /// <summary>"#RRGGBB", lower case as HDT writes it.</summary>
    public static string Colour(int difficulty) => difficulty switch
    {
        1 => "#7f303e",
        2 => "#917b43",
        3 => "#49634b",
        _ => "#404040",
    };
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
            string.IsNullOrEmpty(representative) ? null : representative, howToPlay.Lines, commit.Lines);
    }
}

/// <summary>A piece of one line of a guide's text: plain text, or a card reference (its name, drawn in bold).</summary>
public sealed class CompGuideTextRun
{
    public CompGuideTextRun(string text, bool isCard, string? cardId = null)
    {
        Text = text;
        IsCard = isCard;
        CardId = cardId;
    }

    public string Text { get; }

    /// <summary>A card reference, <c>[[Name||dbf]]</c> or <c>[[Name]]</c>: its name is the text.</summary>
    public bool IsCard { get; }

    /// <summary>The card, when the reference carries a dbf id the card database knows; null otherwise.</summary>
    public string? CardId { get; }
}

/// <summary>A guide's text with its card references: the plain text, the cards referenced, in order, and the lines as runs.</summary>
public sealed class CompGuideTextParts
{
    public CompGuideTextParts(string? plainText, IReadOnlyList<string> cardIds, IReadOnlyList<IReadOnlyList<CompGuideTextRun>>? lines = null)
    {
        PlainText = plainText;
        CardIds = cardIds;
        Lines = lines ?? CompGuideText.PlainLines(plainText);
    }

    public string? PlainText { get; }
    public IReadOnlyList<string> CardIds { get; }

    /// <summary>Line by line (blank lines kept), each line its plain text and card names in order; empty when there is no text.</summary>
    public IReadOnlyList<IReadOnlyList<CompGuideTextRun>> Lines { get; }
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
        var runLines = new List<IReadOnlyList<CompGuideTextRun>>();
        var lines = text!.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        for (var l = 0; l < lines.Length; l++)
        {
            if (l > 0)
            {
                plain.Append('\n');
            }

            var line = lines[l];
            var runs = new List<CompGuideTextRun>();
            void Plain(int from, int length)
            {
                if (length <= 0)
                {
                    return;
                }

                plain.Append(line, from, length);
                runs.Add(new CompGuideTextRun(line.Substring(from, length), isCard: false));
            }

            var index = 0;
            while (index < line.Length)
            {
                var open = line.IndexOf("[[", index, StringComparison.Ordinal);
                if (open == -1)
                {
                    Plain(index, line.Length - index);
                    break;
                }

                Plain(index, open - index);
                var start = open + 2;
                var close = line.IndexOf("]]", start, StringComparison.Ordinal);
                if (close == -1)
                {
                    Plain(open, line.Length - open);
                    break;
                }

                var separator = line.IndexOf("||", start, StringComparison.Ordinal);
                if (separator > close)
                {
                    separator = -1;
                }

                var nameEnd = separator != -1 ? separator : close;
                var cardName = line.Substring(start, nameEnd - start);
                plain.Append(cardName);
                string? resolved = null;
                if (separator != -1
                    && int.TryParse(line.Substring(separator + 2, close - separator - 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var dbfId)
                    && resolve(dbfId) is { } cardId)
                {
                    resolved = cardId;
                    if (!cards.Contains(cardId))
                    {
                        cards.Add(cardId);
                    }
                }

                if (cardName.Length > 0)
                {
                    runs.Add(new CompGuideTextRun(cardName, isCard: true, resolved));
                }

                index = close + 2;
            }

            runLines.Add(runs);
        }

        return new CompGuideTextParts(plain.ToString(), cards, runLines);
    }

    /// <summary>Plain text split into lines of one plain run each (blank lines kept); empty for no text.</summary>
    public static IReadOnlyList<IReadOnlyList<CompGuideTextRun>> PlainLines(string? text) =>
        string.IsNullOrEmpty(text)
            ? Array.Empty<IReadOnlyList<CompGuideTextRun>>()
            : text!.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
                .Select(line => (IReadOnlyList<CompGuideTextRun>)(line.Length == 0 ? Array.Empty<CompGuideTextRun>() : new[] { new CompGuideTextRun(line, isCard: false) }))
                .ToList();

    /// <summary>The lines that say something: a line whose runs are all blank is left out.</summary>
    public static IReadOnlyList<IReadOnlyList<CompGuideTextRun>> NonBlank(IReadOnlyList<IReadOnlyList<CompGuideTextRun>> lines) =>
        lines.Where(line => line.Any(run => !string.IsNullOrWhiteSpace(run.Text))).ToList();
}
