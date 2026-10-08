using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>How a card did when played at one turn: how often, and the average final placement of those games.</summary>
public sealed class CardTurnStat
{
    public CardTurnStat(int turn, int played, double averagePlacement)
    {
        Turn = turn;
        Played = played;
        AveragePlacement = averagePlacement;
    }

    public int Turn { get; }

    /// <summary>Times played at this turn: the sample size of <see cref="AveragePlacement"/>.</summary>
    public int Played { get; }

    public double AveragePlacement { get; }
}

public sealed class CardStat
{
    public CardStat(string cardId, IReadOnlyList<CardTurnStat> turns)
    {
        CardId = cardId;
        Turns = turns;
    }

    public string CardId { get; }

    public IReadOnlyList<CardTurnStat> Turns { get; }

    public CardTurnStat? At(int turn) => Turns.FirstOrDefault(t => t.Turn == turn);
}

/// <summary>Card stats for one MMR bracket, with their provenance: the first source written in the common format.</summary>
public sealed class CardStatsFile
{
    public const int CurrentSchema = 1;
    private const string GoldenSuffix = "_G";

    private readonly Dictionary<string, CardStat> _byId;
    private readonly Dictionary<int, double> _turnAverages;

    public CardStatsFile(StatProvenance provenance, IReadOnlyList<CardStat> cards)
    {
        Provenance = provenance;
        Cards = cards;
        _byId = cards.ToDictionary(c => c.CardId, StringComparer.Ordinal);
        _turnAverages = cards
            .SelectMany(c => c.Turns)
            .GroupBy(t => t.Turn)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Played * t.AveragePlacement) / g.Sum(t => t.Played));
    }

    public StatProvenance Provenance { get; }

    public IReadOnlyList<CardStat> Cards { get; }

    public DateTimeOffset? FetchedAt => Provenance.FetchedAt;

    /// <summary>A card by id; a golden copy ("…_G") is found under its base card, the only one Firestone lists.</summary>
    public CardStat? Find(string cardId)
    {
        if (_byId.TryGetValue(cardId, out var card))
        {
            return card;
        }

        return cardId.EndsWith(GoldenSuffix, StringComparison.Ordinal)
            && _byId.TryGetValue(cardId.Substring(0, cardId.Length - GoldenSuffix.Length), out var normal)
            ? normal
            : null;
    }

    /// <summary>The average placement of every card played at this turn, weighted by times played; null when none was.</summary>
    public double? TurnAverage(int turn) => _turnAverages.TryGetValue(turn, out var average) ? average : null;
}

/// <summary>
/// Local card stats format (schema 1) and the import of Firestone's
/// <c>card-stats/mmr-{p}/{period}/overview-from-hourly.gz.json</c>. Strict like the other formats: a rule broken anywhere
/// rejects the whole file. Firestone's <c>averagePlacementOther</c> is not kept: measured on 2026-10-08, every card
/// played does better than the games where it was not (320 cards of 320 at turn 6), so it tells no card from another.
/// </summary>
public static class CardStatsLoader
{
    public static CardStatsFile Load(string path) => Parse(File.ReadAllText(path));

    public static CardStatsFile Parse(string json)
    {
        var root = ReadObject(json, "card stats");
        if (root["schema"] is not { Type: JTokenType.Integer } schema || schema.Value<int>() != CardStatsFile.CurrentSchema)
        {
            throw new StatsFormatException($"card stats: schema: expected {CardStatsFile.CurrentSchema}");
        }

        var provenance = StatProvenance.ReadFrom(root, "card stats");
        if (root["cards"] is not JArray array || array.Count == 0)
        {
            throw new StatsFormatException("card stats: cards: expected a non-empty array");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var cards = new List<CardStat>();
        for (var i = 0; i < array.Count; i++)
        {
            var path = $"cards[{i}]";
            if (array[i] is not JObject item || item.Value<string>("cardId") is not { Length: > 0 } id || !seen.Add(id))
            {
                throw new StatsFormatException($"{path}.cardId: missing, empty or duplicated");
            }

            var turns = new List<CardTurnStat>();
            foreach (var (turn, j) in (item["turns"] as JArray ?? throw new StatsFormatException($"{path}.turns: expected an array")).Select((t, j) => (t, j)))
            {
                var at = $"{path}.turns[{j}]";
                if (turn is not JObject t || t["turn"]?.Type != JTokenType.Integer || t.Value<int>("turn") < 1)
                {
                    throw new StatsFormatException($"{at}.turn: expected an integer >= 1");
                }

                if (t["played"]?.Type != JTokenType.Integer || t.Value<int>("played") < 1)
                {
                    throw new StatsFormatException($"{at}.played: expected an integer >= 1");
                }

                if (t["averagePlacement"]?.Type is not (JTokenType.Float or JTokenType.Integer) || t.Value<double>("averagePlacement") is < 1 or > 8)
                {
                    throw new StatsFormatException($"{at}.averagePlacement: expected a number in [1, 8]");
                }

                turns.Add(new CardTurnStat(t.Value<int>("turn"), t.Value<int>("played"), t.Value<double>("averagePlacement")));
            }

            cards.Add(new CardStat(id, turns));
        }

        return new CardStatsFile(provenance, cards);
    }

    public static string Serialize(CardStatsFile file)
    {
        var root = new JObject { ["schema"] = CardStatsFile.CurrentSchema };
        file.Provenance.WriteTo(root);
        root["cards"] = new JArray(file.Cards.Select(c => new JObject
        {
            ["cardId"] = c.CardId,
            ["turns"] = new JArray(c.Turns.Select(t => new JObject
            {
                ["turn"] = t.Turn,
                ["played"] = t.Played,
                ["averagePlacement"] = t.AveragePlacement,
            })),
        }));
        return root.ToString(Formatting.None);
    }

    /// <summary>Firestone cardStats[] → local format; a turn without a number, never played or with an impossible placement is skipped.</summary>
    public static CardStatsFile ImportFirestone(string firestoneJson, string sourceUrl, DateTimeOffset fetchedAt, int mmrPercentile)
    {
        var root = ReadObject(firestoneJson, "Firestone card stats");
        if (root["cardStats"] is not JArray array)
        {
            throw new StatsFormatException("Firestone card stats: cardStats is missing or not an array");
        }

        var cards = new List<CardStat>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in array.OfType<JObject>())
        {
            if (item.Value<string>("cardId") is not { Length: > 0 } id || !seen.Add(id))
            {
                continue;
            }

            var turns = (item["turnStats"] as JArray ?? new JArray()).OfType<JObject>()
                .Where(t => t["turn"]?.Type == JTokenType.Integer && t.Value<int>("turn") >= 1
                    && t["totalPlayed"]?.Type == JTokenType.Integer && t.Value<int>("totalPlayed") >= 1
                    && t["averagePlacement"]?.Type is JTokenType.Float or JTokenType.Integer
                    && t.Value<double>("averagePlacement") is >= 1 and <= 8)
                .Select(t => new CardTurnStat(t.Value<int>("turn"), t.Value<int>("totalPlayed"), t.Value<double>("averagePlacement")))
                .ToList();
            cards.Add(new CardStat(id, turns));
        }

        var provenance = new StatProvenance(StatsSources.Firestone, sourceUrl, StatProvenance.Date(root, "lastUpdateDate"), fetchedAt,
            root.Value<string>("timePeriod"), mmrPercentile, patch: null);
        // Through the strict parser, so that an import never yields a file the cache would refuse to read back.
        return Parse(Serialize(new CardStatsFile(provenance, cards)));
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
}
