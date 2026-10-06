using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace BronzebeardHud.Stats;

/// <summary>
/// Converts Firestone's <c>comp-stats/.../overview-from-hourly.gz.json</c> (31.5 MB) into the local
/// composition format. That file has no key-card list (Firestone takes its CORE/ADDON cards from a
/// separate editorial file, bgs-comps-strategies.gz.json, which is out of scope); the key pieces are
/// therefore derived from the data it does carry, the real final boards of each archetype:
/// a card present in at least <see cref="CoreShare"/> of them is a key piece, in at least
/// <see cref="AddonShare"/> an add-on. Archetypes with fewer than <see cref="MinimumBoards"/>
/// final boards are skipped: too few to tell a key piece from noise.
/// </summary>
public static class FirestoneCompImporter
{
    public const double CoreShare = 0.5;
    public const double AddonShare = 0.2;
    public const int MinimumBoards = 20;

    /// <summary>Final boards kept per archetype: the ones reached at the highest MMR, distinct (the finalBoards carry no placement).</summary>
    public const int FinalBoardsKept = 5;

    // Firestone archetype ids start with the tribe ("mech_glambot", "abberation_discard" [sic]).
    private static readonly Dictionary<string, string> TribeByPrefix = new(StringComparer.Ordinal)
    {
        ["beast"] = "BEAST",
        ["demon"] = "DEMON",
        ["dragon"] = "DRAGON",
        ["elemental"] = "ELEMENTAL",
        ["mech"] = "MECHANICAL",
        ["murloc"] = "MURLOC",
        ["naga"] = "NAGA",
        ["pirate"] = "PIRATE",
        ["quilboar"] = "QUILBOAR",
        ["undead"] = "UNDEAD",
        ["abberation"] = "ABERRATION",
        ["aberration"] = "ABERRATION",
    };

    public static CompositionFile Import(string firestoneJson, string sourceUrl, DateTimeOffset fetchedAt)
    {
        FsRoot? root;
        try
        {
            var serializer = JsonSerializer.Create(new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
            using var reader = new JsonTextReader(new StringReader(firestoneJson));
            root = serializer.Deserialize<FsRoot>(reader);
        }
        catch (JsonException e)
        {
            throw new StatsFormatException($"Firestone comp stats: invalid JSON: {e.Message}", e);
        }

        if (root?.CompStats == null)
        {
            throw new StatsFormatException("Firestone comp stats: compStats is missing or not an array");
        }

        var compositions = new List<Composition>();
        foreach (var comp in root.CompStats.Where(c => !string.IsNullOrWhiteSpace(c?.Archetype)))
        {
            var finalBoards = (comp.HeroStats ?? new List<FsHeroStat>())
                .SelectMany(h => h?.FinalBoards ?? new List<FsFinalBoard>())
                .Where(b => b?.FinalComp?.Board is { Count: > 0 })
                // Left to right by ZONE_POSITION (present on every minion of Firestone's boards, checked on 2026-09-26).
                .Select(b => (Mmr: b.Mmr ?? 0, Turn: b.FinalComp!.Turn, Cards: b.FinalComp!.Board!
                    .Where(m => !string.IsNullOrEmpty(m?.CardId))
                    .OrderBy(m => m!.Tags?.ZonePosition ?? int.MaxValue)
                    .Select(m => CardIds.Normalize(m!.CardId!)).Take(7).ToList()))
                .Where(b => b.Cards.Count > 0)
                .ToList();
            var boards = finalBoards.Select(b => new HashSet<string>(b.Cards, StringComparer.Ordinal)).ToList();
            if (boards.Count < MinimumBoards)
            {
                continue;
            }

            var shares = boards.SelectMany(b => b)
                .GroupBy(id => id, StringComparer.Ordinal)
                .Select(g => (Id: g.Key, Share: (double)g.Count() / boards.Count))
                .OrderByDescending(x => x.Share)
                .ThenBy(x => x.Id, StringComparer.Ordinal)
                .ToList();
            var core = shares.Where(x => x.Share >= CoreShare).Select(x => x.Id).ToList();
            if (core.Count == 0)
            {
                continue;
            }

            var addon = shares.Where(x => x.Share >= AddonShare && x.Share < CoreShare).Select(x => x.Id).ToList();
            var prefix = comp.Archetype!.Split('_')[0];
            var tribes = TribeByPrefix.TryGetValue(prefix, out var tribe) ? new[] { tribe } : Array.Empty<string>();
            var kept = finalBoards
                .OrderByDescending(b => b.Mmr)
                .GroupBy(b => string.Join(",", b.Cards), StringComparer.Ordinal)
                .Select(g => g.First())
                .Take(FinalBoardsKept)
                .Select(b => new FinalBoard(b.Mmr, b.Turn, b.Cards))
                .ToList();
            // Reference board: the final board holding the most key pieces, then the most add-ons,
            // then reached at the highest MMR; its order is the one the panel shows.
            var reference = finalBoards
                .OrderByDescending(b => b.Cards.Distinct().Count(core.Contains))
                .ThenByDescending(b => b.Cards.Distinct().Count(addon.Contains))
                .ThenByDescending(b => b.Mmr)
                .Select(b => b.Cards)
                .First();
            compositions.Add(new Composition(
                comp.Archetype,
                Humanize(comp.Archetype),
                tribes,
                core,
                addon,
                comp.AveragePlacement is >= 1 and <= 8 ? comp.AveragePlacement : null,
                comp.DataPoints,
                finalBoards: kept,
                referenceBoard: reference,
                heroStats: (comp.HeroStats ?? new List<FsHeroStat>())
                    // Pairs under HeroCompAffinity.MinimumGames are never used: not kept, the cache stays small.
                    .Where(h => !string.IsNullOrWhiteSpace(h?.HeroCardId) && h!.DataPoints >= HeroCompAffinity.MinimumGames && h.AveragePlacement is >= 1 and <= 8)
                    .Select(h => new CompHeroStat(HeroIdNormalizer.Normalize(h!.HeroCardId!), h.DataPoints!.Value, h.AveragePlacement!.Value))
                    .GroupBy(h => h.HeroCardId, StringComparer.Ordinal)
                    .Select(g => g.OrderByDescending(h => h.DataPoints).First())
                    .ToList()));
        }

        if (compositions.Count == 0)
        {
            throw new StatsFormatException("Firestone comp stats: no archetype with enough final boards");
        }

        var file = new CompositionFile(
            StatsSources.Firestone,
            compositions,
            sourceUrl,
            TryDate(root.LastUpdateDate),
            fetchedAt,
            root.TimePeriod);
        // Round-trip through the loader so that an import obeys exactly the local-format rules.
        return CompositionLoader.Parse(CompositionLoader.Serialize(file));
    }

    /// <summary>"abberation_deathrattle" → "Aberration Deathrattle".</summary>
    public static string Humanize(string archetype) =>
        string.Join(" ", archetype.Replace("abberation", "aberration").Split('_')
            .Where(w => w.Length > 0)
            .Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));

    private static DateTimeOffset? TryDate(string? text) =>
        text != null && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
            ? date
            : null;

    // Only the fields we read; Json.NET skips everything else while streaming.
    private sealed class FsRoot
    {
        [JsonProperty("compStats")] public List<FsComp>? CompStats { get; set; }
        [JsonProperty("lastUpdateDate")] public string? LastUpdateDate { get; set; }
        [JsonProperty("timePeriod")] public string? TimePeriod { get; set; }
    }

    private sealed class FsComp
    {
        [JsonProperty("archetype")] public string? Archetype { get; set; }
        [JsonProperty("dataPoints")] public int? DataPoints { get; set; }
        [JsonProperty("averagePlacement")] public double? AveragePlacement { get; set; }
        [JsonProperty("heroStats")] public List<FsHeroStat>? HeroStats { get; set; }
    }

    private sealed class FsHeroStat
    {
        [JsonProperty("heroCardId")] public string? HeroCardId { get; set; }
        [JsonProperty("dataPoints")] public int? DataPoints { get; set; }
        [JsonProperty("averagePlacement")] public double? AveragePlacement { get; set; }
        [JsonProperty("finalBoards")] public List<FsFinalBoard>? FinalBoards { get; set; }
    }

    private sealed class FsFinalBoard
    {
        [JsonProperty("mmr")] public int? Mmr { get; set; }
        [JsonProperty("finalComp")] public FsFinalComp? FinalComp { get; set; }
    }

    private sealed class FsFinalComp
    {
        [JsonProperty("board")] public List<FsMinion>? Board { get; set; }
        [JsonProperty("turn")] public int? Turn { get; set; }
    }

    private sealed class FsMinion
    {
        [JsonProperty("cardID")] public string? CardId { get; set; }
        [JsonProperty("tags")] public FsTags? Tags { get; set; }
    }

    private sealed class FsTags
    {
        [JsonProperty("ZONE_POSITION")] public int? ZonePosition { get; set; }
    }
}
