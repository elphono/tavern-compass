using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>Firestone's figures for one trinket, with its placement in each MMR bracket.</summary>
public sealed class TrinketStat
{
    public TrinketStat(string trinketCardId, double averagePlacement, int dataPoints, double? pickRate, IReadOnlyDictionary<int, double> placementByPercentile)
    {
        TrinketCardId = trinketCardId;
        AveragePlacement = averagePlacement;
        DataPoints = dataPoints;
        PickRate = pickRate;
        PlacementByPercentile = placementByPercentile;
    }

    public string TrinketCardId { get; }

    /// <summary>Every player (percentile 100).</summary>
    public double AveragePlacement { get; }

    public int DataPoints { get; }
    public double? PickRate { get; }

    /// <summary>Average placement per MMR percentile (100, 50, 25, 10, 1), when published.</summary>
    public IReadOnlyDictionary<int, double> PlacementByPercentile { get; }

    /// <summary>The placement for a bracket, falling back to every player when the bracket is missing.</summary>
    public double PlacementFor(int percentile) =>
        PlacementByPercentile.TryGetValue(percentile, out var placement) ? placement : AveragePlacement;
}

public sealed class TrinketStatsFile
{
    public const int CurrentSchema = 1;

    public TrinketStatsFile(IReadOnlyList<TrinketStat> trinkets, string? sourceUrl, DateTimeOffset? generatedAt, DateTimeOffset? fetchedAt, string? timePeriod)
    {
        Trinkets = trinkets;
        SourceUrl = sourceUrl;
        GeneratedAt = generatedAt;
        FetchedAt = fetchedAt;
        TimePeriod = timePeriod;
    }

    public IReadOnlyList<TrinketStat> Trinkets { get; }
    public string? SourceUrl { get; }
    public DateTimeOffset? GeneratedAt { get; }
    public DateTimeOffset? FetchedAt { get; }
    public string? TimePeriod { get; }

    public TrinketStat? Find(string trinketCardId) =>
        Trinkets.FirstOrDefault(t => string.Equals(t.TrinketCardId, trinketCardId, StringComparison.Ordinal));
}

/// <summary>
/// Local trinket stats format (schema 1, source always Firestone) and the import of Firestone's
/// <c>trinket-stats/.../overview-from-hourly.gz.json</c> (libs/battlegrounds/services/src/lib/services/bgs-trinkets.service.ts).
/// Strict like the other formats: a rule broken anywhere rejects the whole file.
/// </summary>
public static class TrinketStatsLoader
{
    public static TrinketStatsFile Load(string path) => Parse(File.ReadAllText(path));

    public static TrinketStatsFile Parse(string json)
    {
        var root = ReadObject(json, "trinket stats");
        if (root["schema"] is not { Type: JTokenType.Integer } schema || schema.Value<int>() != TrinketStatsFile.CurrentSchema)
        {
            throw new StatsFormatException($"schema: expected {TrinketStatsFile.CurrentSchema}");
        }

        if (root["trinkets"] is not JArray array || array.Count == 0)
        {
            throw new StatsFormatException("trinkets: expected a non-empty array");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var trinkets = new List<TrinketStat>();
        for (var i = 0; i < array.Count; i++)
        {
            var path = $"trinkets[{i}]";
            if (array[i] is not JObject item || item.Value<string>("trinketCardId") is not { Length: > 0 } id || !seen.Add(id))
            {
                throw new StatsFormatException($"{path}.trinketCardId: missing, empty or duplicated");
            }

            var placement = item["averagePlacement"]?.Type is JTokenType.Float or JTokenType.Integer ? item.Value<double>("averagePlacement") : 0;
            if (placement < 1 || placement > 8)
            {
                throw new StatsFormatException($"{path}.averagePlacement: expected a number in [1, 8]");
            }

            if (item["dataPoints"]?.Type != JTokenType.Integer || item.Value<int>("dataPoints") < 0)
            {
                throw new StatsFormatException($"{path}.dataPoints: expected an integer >= 0");
            }

            double? pickRate = item["pickRate"]?.Type is JTokenType.Float or JTokenType.Integer ? item.Value<double>("pickRate") : null;
            if (pickRate is < 0 or > 1)
            {
                throw new StatsFormatException($"{path}.pickRate: outside [0, 1]");
            }

            var byPercentile = new Dictionary<int, double>();
            if (item["placementByPercentile"] is JObject brackets)
            {
                foreach (var bracket in brackets.Properties())
                {
                    if (!int.TryParse(bracket.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var percentile)
                        || bracket.Value.Type is not (JTokenType.Float or JTokenType.Integer)
                        || bracket.Value.Value<double>() is < 1 or > 8)
                    {
                        throw new StatsFormatException($"{path}.placementByPercentile: expected {{\"percentile\": placement in [1, 8]}}");
                    }

                    byPercentile[percentile] = bracket.Value.Value<double>();
                }
            }

            trinkets.Add(new TrinketStat(id, placement, item.Value<int>("dataPoints"), pickRate, byPercentile));
        }

        return new TrinketStatsFile(trinkets, root.Value<string>("sourceUrl"), Date(root, "generatedAt"), Date(root, "fetchedAt"), root.Value<string>("timePeriod"));
    }

    public static string Serialize(TrinketStatsFile file) => new JObject
    {
        ["schema"] = TrinketStatsFile.CurrentSchema,
        ["source"] = StatsSources.Firestone,
        ["sourceUrl"] = file.SourceUrl,
        ["generatedAt"] = Format(file.GeneratedAt),
        ["fetchedAt"] = Format(file.FetchedAt),
        ["timePeriod"] = file.TimePeriod,
        ["trinkets"] = new JArray(file.Trinkets.Select(t => new JObject
        {
            ["trinketCardId"] = t.TrinketCardId,
            ["averagePlacement"] = t.AveragePlacement,
            ["dataPoints"] = t.DataPoints,
            ["pickRate"] = t.PickRate.HasValue ? new JValue(t.PickRate.Value) : JValue.CreateNull(),
            ["placementByPercentile"] = new JObject(t.PlacementByPercentile.OrderByDescending(kv => kv.Key)
                .Select(kv => new JProperty(kv.Key.ToString(CultureInfo.InvariantCulture), kv.Value))),
        })),
    }.ToString(Formatting.Indented);

    /// <summary>Firestone trinketStats[] → local format; entries with an impossible placement are skipped.</summary>
    public static TrinketStatsFile ImportFirestone(string firestoneJson, string sourceUrl, DateTimeOffset fetchedAt)
    {
        var root = ReadObject(firestoneJson, "Firestone trinket stats");
        if (root["trinketStats"] is not JArray array)
        {
            throw new StatsFormatException("Firestone trinket stats: trinketStats is missing or not an array");
        }

        var trinkets = new JArray();
        foreach (var item in array.OfType<JObject>())
        {
            var placement = item["averagePlacement"]?.Type is JTokenType.Float or JTokenType.Integer ? item.Value<double>("averagePlacement") : 0;
            if (item.Value<string>("trinketCardId") is not { Length: > 0 } id || placement < 1 || placement > 8)
            {
                continue;
            }

            var brackets = new JObject();
            foreach (var bracket in (item["averagePlacementAtMmr"] as JArray ?? new JArray()).OfType<JObject>())
            {
                if (bracket["mmr"]?.Type == JTokenType.Integer && bracket["placement"]?.Type is JTokenType.Float or JTokenType.Integer
                    && bracket.Value<double>("placement") is >= 1 and <= 8)
                {
                    brackets[bracket.Value<int>("mmr").ToString(CultureInfo.InvariantCulture)] = bracket.Value<double>("placement");
                }
            }

            var pickRate = item["pickRate"]?.Type is JTokenType.Float or JTokenType.Integer ? item.Value<double>("pickRate") : (double?)null;
            trinkets.Add(new JObject
            {
                ["trinketCardId"] = id,
                ["averagePlacement"] = placement,
                ["dataPoints"] = item["dataPoints"]?.Type == JTokenType.Integer ? Math.Max(0, item.Value<int>("dataPoints")) : 0,
                ["pickRate"] = pickRate is >= 0 and <= 1 ? new JValue(pickRate.Value) : JValue.CreateNull(),
                ["placementByPercentile"] = brackets,
            });
        }

        var local = new JObject
        {
            ["schema"] = TrinketStatsFile.CurrentSchema,
            ["source"] = StatsSources.Firestone,
            ["sourceUrl"] = sourceUrl,
            ["generatedAt"] = root["lastUpdateDate"]?.Type == JTokenType.String ? root["lastUpdateDate"] : JValue.CreateNull(),
            ["fetchedAt"] = Format(fetchedAt),
            ["timePeriod"] = root["timePeriod"]?.Type == JTokenType.String ? root["timePeriod"] : JValue.CreateNull(),
            ["trinkets"] = trinkets,
        };
        return Parse(local.ToString(Formatting.None));
    }

    private static JObject ReadObject(string json, string what)
    {
        try
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
            return JToken.ReadFrom(reader) as JObject ?? throw new StatsFormatException($"{what}: the root must be a JSON object");
        }
        catch (JsonException e)
        {
            throw new StatsFormatException($"{what}: invalid JSON: {e.Message}", e);
        }
    }

    private static DateTimeOffset? Date(JObject obj, string name) =>
        obj.Value<string>(name) is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
            ? date
            : null;

    private static JToken Format(DateTimeOffset? date) =>
        date.HasValue
            ? new JValue(date.Value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))
            : JValue.CreateNull();
}
