using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>A local stats file that breaks one of the format rules. The whole file is rejected.</summary>
public sealed class StatsFormatException : Exception
{
    public StatsFormatException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>A file of a stats directory that could not be loaded, and why.</summary>
public sealed class StatsLoadError
{
    public StatsLoadError(string path, string message)
    {
        Path = path;
        Message = message;
    }

    public string Path { get; }
    public string Message { get; }
}

/// <summary>
/// Reads and writes the local hero stats format (schema 1). Validation is strict: a file that
/// breaks any rule is rejected as a whole, never partially shown, because a wrong stat on the
/// hero picker is worse than no stat.
/// </summary>
public static class HeroStatsLoader
{
    private static readonly string[] AllowedTiers = { "S", "A", "B", "C", "D", "E", "F" };

    public static HeroStatsFile Load(string path) => Parse(File.ReadAllText(path));

    /// <summary>
    /// Load every <c>*.json</c> file of a directory. A bad file is reported and skipped; it
    /// never prevents the other files from loading.
    /// </summary>
    public static (IReadOnlyList<HeroStatsFile> Files, IReadOnlyList<StatsLoadError> Errors) LoadDirectory(string directory)
    {
        var files = new List<HeroStatsFile>();
        var errors = new List<StatsLoadError>();
        if (!Directory.Exists(directory))
        {
            return (files, errors);
        }

        foreach (var path in Directory.GetFiles(directory, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            try
            {
                files.Add(Load(path));
            }
            catch (StatsFormatException e)
            {
                errors.Add(new StatsLoadError(path, e.Message));
            }
            catch (IOException e)
            {
                errors.Add(new StatsLoadError(path, e.Message));
            }
        }

        return (files, errors);
    }

    public static HeroStatsFile Parse(string json)
    {
        JToken root;
        try
        {
            // DateParseHandling.None keeps dates as strings so that we parse them ourselves,
            // culture-invariant, instead of letting Json.NET guess.
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

        var schema = obj["schema"];
        if (schema == null || schema.Type != JTokenType.Integer || schema.Value<int>() != HeroStatsFile.CurrentSchema)
        {
            throw new StatsFormatException($"schema: expected {HeroStatsFile.CurrentSchema}, got {Describe(schema)}");
        }

        var source = RequiredString(obj, "source", "source");
        if (!StatsSources.All.Contains(source))
        {
            throw new StatsFormatException(
                $"source: expected one of {string.Join(", ", StatsSources.All)}, got \"{source}\"");
        }

        if (obj["heroes"] is not JArray heroesArray)
        {
            throw new StatsFormatException("heroes: expected an array");
        }

        if (heroesArray.Count == 0)
        {
            throw new StatsFormatException("heroes: the array is empty");
        }

        var heroes = new List<HeroStat>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < heroesArray.Count; i++)
        {
            var path = $"heroes[{i}]";
            if (heroesArray[i] is not JObject hero)
            {
                throw new StatsFormatException($"{path}: expected an object");
            }

            var heroCardId = RequiredString(hero, "heroCardId", $"{path}.heroCardId");
            if (heroCardId.IndexOf("_SKIN_", StringComparison.Ordinal) >= 0)
            {
                throw new StatsFormatException(
                    $"{path}.heroCardId: \"{heroCardId}\" is a skin, the file must use the base hero");
            }

            if (!seen.Add(heroCardId))
            {
                throw new StatsFormatException($"{path}.heroCardId: \"{heroCardId}\" appears twice");
            }

            var averagePlacement = RequiredNumber(hero, "averagePlacement", $"{path}.averagePlacement");
            if (averagePlacement < 1 || averagePlacement > 8)
            {
                throw new StatsFormatException(
                    $"{path}.averagePlacement: {averagePlacement.ToString(CultureInfo.InvariantCulture)} is outside [1, 8]");
            }

            var dataPointsToken = hero["dataPoints"];
            if (dataPointsToken == null || dataPointsToken.Type != JTokenType.Integer
                || dataPointsToken.Value<long>() < 0 || dataPointsToken.Value<long>() > int.MaxValue)
            {
                throw new StatsFormatException($"{path}.dataPoints: expected an integer >= 0, got {Describe(dataPointsToken)}");
            }

            var pickRate = OptionalNumber(hero, "pickRate", $"{path}.pickRate");
            if (pickRate is < 0 or > 1)
            {
                throw new StatsFormatException(
                    $"{path}.pickRate: {pickRate.Value.ToString(CultureInfo.InvariantCulture)} is outside [0, 1]");
            }

            var tier = OptionalString(hero, "tier", $"{path}.tier");
            if (tier != null && !AllowedTiers.Contains(tier))
            {
                throw new StatsFormatException(
                    $"{path}.tier: expected one of {string.Join(", ", AllowedTiers)}, got \"{tier}\"");
            }

            var distribution = OptionalDistribution(hero, $"{path}.placementDistribution");

            heroes.Add(new HeroStat(
                heroCardId,
                averagePlacement,
                dataPointsToken.Value<int>(),
                pickRate,
                tier,
                distribution));
        }

        var mmrToken = obj["mmrPercentile"];
        int? mmrPercentile = null;
        if (mmrToken != null && mmrToken.Type != JTokenType.Null)
        {
            if (mmrToken.Type != JTokenType.Integer)
            {
                throw new StatsFormatException($"mmrPercentile: expected an integer, got {Describe(mmrToken)}");
            }

            mmrPercentile = mmrToken.Value<int>();
        }

        return new HeroStatsFile(
            source,
            heroes,
            OptionalString(obj, "sourceUrl", "sourceUrl"),
            OptionalDate(obj, "generatedAt"),
            OptionalDate(obj, "fetchedAt"),
            mmrPercentile,
            OptionalString(obj, "timePeriod", "timePeriod"),
            Thresholds(obj["mmrThresholds"]));
    }

    public static string Serialize(HeroStatsFile file)
    {
        var heroes = new JArray(file.Heroes.Select(h => new JObject
        {
            ["heroCardId"] = h.HeroCardId,
            ["averagePlacement"] = h.AveragePlacement,
            ["dataPoints"] = h.DataPoints,
            ["pickRate"] = h.PickRate.HasValue ? new JValue(h.PickRate.Value) : JValue.CreateNull(),
            ["tier"] = h.Tier != null ? new JValue(h.Tier) : JValue.CreateNull(),
            ["placementDistribution"] = h.PlacementDistribution != null
                ? new JArray(h.PlacementDistribution.Select(p => (object)p).ToArray())
                : JValue.CreateNull(),
        }));

        var root = new JObject
        {
            ["schema"] = HeroStatsFile.CurrentSchema,
            ["source"] = file.Source,
            ["sourceUrl"] = file.SourceUrl != null ? new JValue(file.SourceUrl) : JValue.CreateNull(),
            ["generatedAt"] = FormatDate(file.GeneratedAt),
            ["fetchedAt"] = FormatDate(file.FetchedAt),
            ["mmrPercentile"] = file.MmrPercentile.HasValue ? new JValue(file.MmrPercentile.Value) : JValue.CreateNull(),
            ["timePeriod"] = file.TimePeriod != null ? new JValue(file.TimePeriod) : JValue.CreateNull(),
            ["mmrThresholds"] = new JArray(file.MmrThresholds.Select(m => new JObject { ["percentile"] = m.Percentile, ["mmr"] = m.Mmr })),
            ["heroes"] = heroes,
        };
        return root.ToString(Formatting.Indented);
    }

    /// <summary>Optional [{percentile, mmr}] table; each percentile at most once, both integers >= 0.</summary>
    internal static IReadOnlyList<MmrThreshold> Thresholds(JToken? token)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return Array.Empty<MmrThreshold>();
        }

        if (token is not JArray array)
        {
            throw new StatsFormatException("mmrThresholds: expected an array");
        }

        var thresholds = new List<MmrThreshold>();
        foreach (var item in array)
        {
            if (item is not JObject entry
                || entry["percentile"] is not { Type: JTokenType.Integer } percentile
                || entry["mmr"] is not { Type: JTokenType.Integer } mmr
                || percentile.Value<int>() <= 0 || percentile.Value<int>() > 100 || mmr.Value<int>() < 0)
            {
                throw new StatsFormatException("mmrThresholds: expected {percentile: 1..100, mmr: >= 0} entries");
            }

            thresholds.Add(new MmrThreshold(percentile.Value<int>(), mmr.Value<int>()));
        }

        if (thresholds.Select(t => t.Percentile).Distinct().Count() != thresholds.Count)
        {
            throw new StatsFormatException("mmrThresholds: a percentile appears twice");
        }

        return thresholds;
    }

    private static JToken FormatDate(DateTimeOffset? date) =>
        date.HasValue
            ? new JValue(date.Value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))
            : JValue.CreateNull();

    private static string RequiredString(JObject obj, string name, string path)
    {
        var token = obj[name];
        if (token == null || token.Type != JTokenType.String || string.IsNullOrWhiteSpace(token.Value<string>()))
        {
            throw new StatsFormatException($"{path}: expected a non-empty string, got {Describe(token)}");
        }

        return token.Value<string>()!;
    }

    private static string? OptionalString(JObject obj, string name, string path)
    {
        var token = obj[name];
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        if (token.Type != JTokenType.String)
        {
            throw new StatsFormatException($"{path}: expected a string, got {Describe(token)}");
        }

        return token.Value<string>();
    }

    private static double RequiredNumber(JObject obj, string name, string path)
    {
        var token = obj[name];
        if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
        {
            throw new StatsFormatException($"{path}: expected a number, got {Describe(token)}");
        }

        return token.Value<double>();
    }

    private static double? OptionalNumber(JObject obj, string name, string path)
    {
        var token = obj[name];
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        return RequiredNumber(obj, name, path);
    }

    private static IReadOnlyList<double>? OptionalDistribution(JObject hero, string path)
    {
        var token = hero["placementDistribution"];
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        if (token is not JArray array || array.Count != 8)
        {
            throw new StatsFormatException($"{path}: expected an array of 8 percentages");
        }

        var values = new List<double>();
        foreach (var item in array)
        {
            if (item.Type != JTokenType.Float && item.Type != JTokenType.Integer)
            {
                throw new StatsFormatException($"{path}: expected numbers, got {Describe(item)}");
            }

            var value = item.Value<double>();
            if (value < 0 || value > 100)
            {
                throw new StatsFormatException(
                    $"{path}: {value.ToString(CultureInfo.InvariantCulture)} is outside [0, 100]");
            }

            values.Add(value);
        }

        return values;
    }

    private static DateTimeOffset? OptionalDate(JObject obj, string name)
    {
        var text = OptionalString(obj, name, name);
        if (text == null)
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
        {
            throw new StatsFormatException($"{name}: \"{text}\" is not an ISO 8601 date");
        }

        return date;
    }

    private static string Describe(JToken? token) =>
        token == null ? "nothing" : token.Type == JTokenType.Null ? "null" : $"{token.Type.ToString().ToLowerInvariant()} {token.ToString(Formatting.None)}";
}
