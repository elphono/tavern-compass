using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Converts Firestone's <c>hero-stats/.../overview-from-hourly.gz.json</c> into the local format.
/// The result is built as local-format JSON and read back through <see cref="HeroStatsLoader.Parse"/>,
/// so an imported file obeys exactly the rules of a hand-written one.
/// </summary>
public static class FirestoneHeroStatsImporter
{
    public static HeroStatsFile Import(string firestoneJson, string sourceUrl, DateTimeOffset fetchedAt)
    {
        JObject root;
        try
        {
            using var reader = new JsonTextReader(new StringReader(firestoneJson)) { DateParseHandling = DateParseHandling.None };
            root = JToken.ReadFrom(reader) as JObject
                   ?? throw new StatsFormatException("Firestone hero stats: the root must be a JSON object");
        }
        catch (JsonException e)
        {
            throw new StatsFormatException($"Firestone hero stats: invalid JSON: {e.Message}", e);
        }

        if (root["heroStats"] is not JArray heroStats)
        {
            throw new StatsFormatException("Firestone hero stats: heroStats is missing or not an array");
        }

        // Keyed by base hero id: if a skin id ever shows up next to its parent, keep the
        // entry backed by more games rather than failing the whole file on a duplicate.
        var byHero = new Dictionary<string, JObject>(StringComparer.Ordinal);
        foreach (var token in heroStats.OfType<JObject>())
        {
            var rawId = token.Value<string>("heroCardId");
            var averagePosition = token["averagePosition"]?.Type is JTokenType.Float or JTokenType.Integer
                ? token.Value<double>("averagePosition")
                : 0;
            // Firestone itself drops heroes whose average position is falsy (bgs-meta-hero-stats.ts,
            // filterItems); anything outside [1, 8] cannot be a real average either.
            if (string.IsNullOrWhiteSpace(rawId) || averagePosition < 1 || averagePosition > 8)
            {
                continue;
            }

            var heroCardId = HeroIdNormalizer.Normalize(rawId!);
            var dataPoints = token["dataPoints"]?.Type == JTokenType.Integer ? token.Value<int>("dataPoints") : 0;
            var totalOffered = token["totalOffered"]?.Type == JTokenType.Integer ? token.Value<long>("totalOffered") : 0;
            var totalPicked = token["totalPicked"]?.Type == JTokenType.Integer ? token.Value<long>("totalPicked") : 0;

            var local = new JObject
            {
                ["heroCardId"] = heroCardId,
                ["averagePlacement"] = averagePosition,
                ["dataPoints"] = Math.Max(0, dataPoints),
                ["pickRate"] = totalOffered > 0 && totalPicked >= 0 && totalPicked <= totalOffered
                    ? new JValue((double)totalPicked / totalOffered)
                    : JValue.CreateNull(),
                ["tier"] = JValue.CreateNull(),
                ["placementDistribution"] = Distribution(token["placementDistribution"]),
                ["tribeImpacts"] = TribeImpacts(token["tribeStats"]),
                ["warbandStats"] = Warband(token["warbandStats"]),
            };

            if (!byHero.TryGetValue(heroCardId, out var existing)
                || existing.Value<int>("dataPoints") < local.Value<int>("dataPoints"))
            {
                byHero[heroCardId] = local;
            }
        }

        var first = heroStats.OfType<JObject>().FirstOrDefault();
        var localRoot = new JObject
        {
            ["schema"] = HeroStatsFile.CurrentSchema,
            ["source"] = StatsSources.Firestone,
            ["sourceUrl"] = sourceUrl,
            ["generatedAt"] = root["lastUpdateDate"]?.Type == JTokenType.String ? root["lastUpdateDate"] : JValue.CreateNull(),
            ["fetchedAt"] = fetchedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["mmrPercentile"] = first?["mmrPercentile"]?.Type == JTokenType.Integer ? first["mmrPercentile"] : JValue.CreateNull(),
            ["timePeriod"] = first?["timePeriod"]?.Type == JTokenType.String ? first["timePeriod"] : JValue.CreateNull(),
            ["mmrThresholds"] = root["mmrPercentiles"] is JArray table ? table : new JArray(),
            ["heroes"] = new JArray(byHero.Values.OrderBy(h => h.Value<string>("heroCardId"), StringComparer.Ordinal)),
        };

        return HeroStatsLoader.Parse(localRoot.ToString(Formatting.None));
    }

    // Race ids of python-hearthstone's enums.Race, as Firestone writes them in tribeStats.
    private static readonly Dictionary<int, string> TribeByRace = new()
    {
        [11] = "UNDEAD", [14] = "MURLOC", [15] = "DEMON", [17] = "MECHANICAL", [18] = "ELEMENTAL", [20] = "BEAST",
        [23] = "PIRATE", [24] = "DRAGON", [43] = "QUILBOAR", [92] = "NAGA", [126] = "ABERRATION",
    };

    /// <summary>Firestone's tribeStats, reduced to what the lobby-tribe adjustment reads; unknown races are dropped.</summary>
    private static JArray TribeImpacts(JToken? token)
    {
        var impacts = new JArray();
        if (token is not JArray array)
        {
            return impacts;
        }

        foreach (var item in array.OfType<JObject>())
        {
            if (item["tribe"]?.Type == JTokenType.Integer && TribeByRace.TryGetValue(item.Value<int>("tribe"), out var tribe)
                && item["impactAveragePosition"]?.Type is JTokenType.Float or JTokenType.Integer
                && item["dataPoints"]?.Type == JTokenType.Integer
                && item["dataPointsOnMissingTribe"]?.Type == JTokenType.Integer)
            {
                impacts.Add(new JObject
                {
                    ["tribe"] = tribe,
                    ["impact"] = item.Value<double>("impactAveragePosition"),
                    ["dataPoints"] = item.Value<int>("dataPoints"),
                    ["dataPointsOnMissingTribe"] = item.Value<int>("dataPointsOnMissingTribe"),
                });
            }
        }

        return impacts;
    }

    /// <summary>Firestone gives [{rank, percentage, totalMatches}]; keep the 8 percentages in rank order.</summary>
    /// <summary>
    /// warbandStats [{turn, averageStats}]: the average of the sum of attack and health of the player's board
    /// at each turn's combat (Firestone rtstats-bgs-board-stats-parser.ts, replay-metadata-bulder.service.ts:225-229).
    /// Unreadable entries and repeated turns are dropped; null when nothing is left.
    /// </summary>
    private static JToken Warband(JToken? token)
    {
        if (token is not JArray array)
        {
            return JValue.CreateNull();
        }

        var byTurn = new SortedDictionary<int, double>();
        foreach (var item in array.OfType<JObject>())
        {
            if (item["turn"]?.Type == JTokenType.Integer && item.Value<int>("turn") >= 1
                && item["averageStats"]?.Type is JTokenType.Float or JTokenType.Integer && item.Value<double>("averageStats") >= 0
                && !byTurn.ContainsKey(item.Value<int>("turn")))
            {
                byTurn[item.Value<int>("turn")] = item.Value<double>("averageStats");
            }
        }

        return byTurn.Count == 0
            ? JValue.CreateNull()
            : new JArray(byTurn.Select(p => new JObject { ["turn"] = p.Key, ["averageStats"] = p.Value }));
    }

    private static JToken Distribution(JToken? token)
    {
        if (token is not JArray array)
        {
            return JValue.CreateNull();
        }

        var byRank = new SortedDictionary<int, double>();
        foreach (var item in array.OfType<JObject>())
        {
            if (item["rank"]?.Type == JTokenType.Integer
                && item["percentage"]?.Type is JTokenType.Float or JTokenType.Integer)
            {
                byRank[item.Value<int>("rank")] = item.Value<double>("percentage");
            }
        }

        var complete = byRank.Count == 8 && byRank.Keys.SequenceEqual(Enumerable.Range(1, 8));
        return complete ? new JArray(byRank.Values.Select(v => (object)v).ToArray()) : JValue.CreateNull();
    }
}
