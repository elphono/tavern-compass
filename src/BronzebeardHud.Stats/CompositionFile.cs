using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>Card id helpers shared by the composition code.</summary>
public static class CardIds
{
    /// <summary>Golden copies (<c>…_G</c>) count as the base card.</summary>
    public static string Normalize(string cardId) =>
        cardId.EndsWith("_G", StringComparison.Ordinal) ? cardId.Substring(0, cardId.Length - 2) : cardId;
}

/// <summary>Battlegrounds tribes as GameTag/Race names (python-hearthstone enums.Race).</summary>
public static class Tribes
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "BEAST", "DEMON", "DRAGON", "ELEMENTAL", "MECHANICAL", "MURLOC", "NAGA", "PIRATE", "QUILBOAR", "UNDEAD", "ABERRATION",
    };

    /// <summary>Amalgam-style minions: every tribe at once.</summary>
    public const string Any = "ALL";
}

/// <summary>A target end-game composition: the cards that define it, and how well it does.</summary>
public sealed class Composition
{
    public Composition(
        string id,
        string name,
        IReadOnlyList<string> tribes,
        IReadOnlyList<string> coreCards,
        IReadOnlyList<string> addonCards,
        double? averagePlacement = null,
        int? dataPoints = null,
        string? tier = null,
        IReadOnlyList<IReadOnlyList<string>>? inspirationBoards = null)
    {
        InspirationBoards = inspirationBoards ?? Array.Empty<IReadOnlyList<string>>();
        Id = id;
        Name = name;
        Tribes = tribes;
        CoreCards = coreCards;
        AddonCards = addonCards;
        AveragePlacement = averagePlacement;
        DataPoints = dataPoints;
        Tier = tier;
    }

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<string> Tribes { get; }

    /// <summary>Key pieces: the cards the composition is built around.</summary>
    public IReadOnlyList<string> CoreCards { get; }

    /// <summary>Useful additions, less decisive than the key pieces.</summary>
    public IReadOnlyList<string> AddonCards { get; }

    public double? AveragePlacement { get; }
    public int? DataPoints { get; }
    public string? Tier { get; }

    /// <summary>Real final boards of this composition (card ids, left to right), best MMR first; may be empty.</summary>
    public IReadOnlyList<IReadOnlyList<string>> InspirationBoards { get; }
}

public sealed class CompositionFile
{
    public const int CurrentSchema = 1;

    public CompositionFile(
        string source,
        IReadOnlyList<Composition> compositions,
        string? sourceUrl = null,
        DateTimeOffset? generatedAt = null,
        DateTimeOffset? fetchedAt = null,
        string? timePeriod = null)
    {
        Source = source;
        Compositions = compositions;
        SourceUrl = sourceUrl;
        GeneratedAt = generatedAt;
        FetchedAt = fetchedAt;
        TimePeriod = timePeriod;
    }

    public string Source { get; }
    public IReadOnlyList<Composition> Compositions { get; }
    public string? SourceUrl { get; }
    public DateTimeOffset? GeneratedAt { get; }
    public DateTimeOffset? FetchedAt { get; }
    public string? TimePeriod { get; }
}

/// <summary>
/// Reads and writes the local composition format (schema 1). Same policy as the hero stats:
/// a file that breaks a rule is rejected as a whole, with the offending field named.
/// </summary>
public static class CompositionLoader
{
    private static readonly string[] AllowedTiers = { "S", "A", "B", "C", "D", "E", "F" };

    public static CompositionFile Load(string path) => Parse(File.ReadAllText(path));

    public static CompositionFile Parse(string json)
    {
        JToken root;
        try
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
            root = JToken.ReadFrom(reader);
        }
        catch (JsonException e)
        {
            throw new StatsFormatException($"invalid JSON: {e.Message}", e);
        }

        if (root is not JObject obj)
        {
            throw new StatsFormatException("the root must be a JSON object");
        }

        if (obj["schema"] is not { Type: JTokenType.Integer } schema || schema.Value<int>() != CompositionFile.CurrentSchema)
        {
            throw new StatsFormatException($"schema: expected {CompositionFile.CurrentSchema}");
        }

        var source = obj.Value<string>("source");
        if (source == null || !StatsSources.All.Contains(source))
        {
            throw new StatsFormatException($"source: expected one of {string.Join(", ", StatsSources.All)}");
        }

        if (obj["compositions"] is not JArray array || array.Count == 0)
        {
            throw new StatsFormatException("compositions: expected a non-empty array");
        }

        var compositions = new List<Composition>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < array.Count; i++)
        {
            var path = $"compositions[{i}]";
            if (array[i] is not JObject comp)
            {
                throw new StatsFormatException($"{path}: expected an object");
            }

            var id = comp.Value<string>("id");
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id!))
            {
                throw new StatsFormatException($"{path}.id: missing, empty or duplicated");
            }

            var name = comp.Value<string>("name");
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new StatsFormatException($"{path}.name: expected a non-empty string");
            }

            var tribes = StringList(comp, "tribes", $"{path}.tribes", allowEmpty: true);
            var unknownTribe = tribes.FirstOrDefault(t => !Tribes.All.Contains(t));
            if (unknownTribe != null)
            {
                throw new StatsFormatException($"{path}.tribes: unknown tribe \"{unknownTribe}\"");
            }

            var core = StringList(comp, "coreCards", $"{path}.coreCards", allowEmpty: false);
            var addon = StringList(comp, "addonCards", $"{path}.addonCards", allowEmpty: true);
            var both = core.Intersect(addon, StringComparer.Ordinal).FirstOrDefault();
            if (both != null)
            {
                throw new StatsFormatException($"{path}: \"{both}\" is both a core and an addon card");
            }

            double? averagePlacement = null;
            if (comp["averagePlacement"] is { Type: JTokenType.Float or JTokenType.Integer } placementToken)
            {
                averagePlacement = placementToken.Value<double>();
                if (averagePlacement < 1 || averagePlacement > 8)
                {
                    throw new StatsFormatException(
                        $"{path}.averagePlacement: {averagePlacement.Value.ToString(CultureInfo.InvariantCulture)} is outside [1, 8]");
                }
            }

            int? dataPoints = comp["dataPoints"] is { Type: JTokenType.Integer } dataToken ? dataToken.Value<int>() : null;
            var tier = comp["tier"]?.Type == JTokenType.String ? comp.Value<string>("tier") : null;
            if (tier != null && !AllowedTiers.Contains(tier))
            {
                throw new StatsFormatException($"{path}.tier: expected one of {string.Join(", ", AllowedTiers)}");
            }

            var boards = new List<IReadOnlyList<string>>();
            if (comp["inspirationBoards"] is { Type: not JTokenType.Null } boardsToken)
            {
                if (boardsToken is not JArray boardArray || boardArray.Any(b => b is not JArray board || board.Count == 0 || board.Count > 7
                        || board.Any(c => c.Type != JTokenType.String || string.IsNullOrWhiteSpace(c.Value<string>()))))
                {
                    throw new StatsFormatException($"{path}.inspirationBoards: expected boards of 1 to 7 card ids");
                }

                boards.AddRange(boardArray.Select(b => (IReadOnlyList<string>)b.Select(c => c.Value<string>()!).ToList()));
            }

            compositions.Add(new Composition(id!, name!, tribes, core, addon, averagePlacement, dataPoints, tier, boards));
        }

        return new CompositionFile(
            source,
            compositions,
            obj["sourceUrl"]?.Type == JTokenType.String ? obj.Value<string>("sourceUrl") : null,
            Date(obj, "generatedAt"),
            Date(obj, "fetchedAt"),
            obj["timePeriod"]?.Type == JTokenType.String ? obj.Value<string>("timePeriod") : null);
    }

    public static string Serialize(CompositionFile file)
    {
        var compositions = new JArray(file.Compositions.Select(c => new JObject
        {
            ["id"] = c.Id,
            ["name"] = c.Name,
            ["tribes"] = new JArray(c.Tribes.Cast<object>().ToArray()),
            ["coreCards"] = new JArray(c.CoreCards.Cast<object>().ToArray()),
            ["addonCards"] = new JArray(c.AddonCards.Cast<object>().ToArray()),
            ["averagePlacement"] = c.AveragePlacement.HasValue ? new JValue(c.AveragePlacement.Value) : JValue.CreateNull(),
            ["dataPoints"] = c.DataPoints.HasValue ? new JValue(c.DataPoints.Value) : JValue.CreateNull(),
            ["tier"] = c.Tier != null ? new JValue(c.Tier) : JValue.CreateNull(),
            ["inspirationBoards"] = new JArray(c.InspirationBoards.Select(b => new JArray(b.Cast<object>().ToArray()))),
        }));
        return new JObject
        {
            ["schema"] = CompositionFile.CurrentSchema,
            ["source"] = file.Source,
            ["sourceUrl"] = file.SourceUrl != null ? new JValue(file.SourceUrl) : JValue.CreateNull(),
            ["generatedAt"] = FormatDate(file.GeneratedAt),
            ["fetchedAt"] = FormatDate(file.FetchedAt),
            ["timePeriod"] = file.TimePeriod != null ? new JValue(file.TimePeriod) : JValue.CreateNull(),
            ["compositions"] = compositions,
        }.ToString(Formatting.Indented);
    }

    private static IReadOnlyList<string> StringList(JObject obj, string name, string path, bool allowEmpty)
    {
        if (obj[name] is not JArray array || array.Any(t => t.Type != JTokenType.String || string.IsNullOrWhiteSpace(t.Value<string>())))
        {
            throw new StatsFormatException($"{path}: expected an array of non-empty strings");
        }

        var values = array.Select(t => t.Value<string>()!).ToList();
        if (!allowEmpty && values.Count == 0)
        {
            throw new StatsFormatException($"{path}: expected at least one card");
        }

        if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            throw new StatsFormatException($"{path}: duplicated entry");
        }

        return values;
    }

    private static DateTimeOffset? Date(JObject obj, string name)
    {
        var text = obj[name]?.Type == JTokenType.String ? obj.Value<string>(name) : null;
        if (text == null)
        {
            return null;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
            ? date
            : throw new StatsFormatException($"{name}: \"{text}\" is not an ISO 8601 date");
    }

    private static JToken FormatDate(DateTimeOffset? date) =>
        date.HasValue
            ? new JValue(date.Value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))
            : JValue.CreateNull();
}
