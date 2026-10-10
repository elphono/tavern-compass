using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>A hero over the patch's games on nomi.gg: every MMR bracket mixed.</summary>
public sealed class NomiHero
{
    public NomiHero(string heroCardId, int games, double averagePlacement, double top4, double win, double pick)
    {
        HeroCardId = heroCardId;
        Games = games;
        AveragePlacement = averagePlacement;
        Top4 = top4;
        Win = win;
        Pick = pick;
    }

    public string HeroCardId { get; }
    public int Games { get; }
    public double AveragePlacement { get; }
    public double Top4 { get; }

    /// <summary>First places, as a share: nomi.gg's "win".</summary>
    public double Win { get; }

    public double Pick { get; }
}

/// <summary>A tribe before and after the patch: games and average placement of the boards of that tribe.</summary>
public sealed class NomiTribe
{
    public NomiTribe(string tribe, int preGames, double? preAverage, int postGames, double? postAverage)
    {
        Tribe = tribe;
        PreGames = preGames;
        PreAverage = preAverage;
        PostGames = postGames;
        PostAverage = postAverage;
    }

    /// <summary>nomi.gg's lower-case name, e.g. "beast".</summary>
    public string Tribe { get; }
    public int PreGames { get; }
    public double? PreAverage { get; }
    public int PostGames { get; }
    public double? PostAverage { get; }
}

/// <summary>One of the patch's winning or losing trinkets (nomi.gg lists six of each, lesser and greater).</summary>
public sealed class NomiTrinket
{
    public NomiTrinket(string trinketCardId, string kind, bool winner, int games, double averagePlacement)
    {
        TrinketCardId = trinketCardId;
        Kind = kind;
        Winner = winner;
        Games = games;
        AveragePlacement = averagePlacement;
    }

    public string TrinketCardId { get; }

    /// <summary>"lesser" or "greater".</summary>
    public string Kind { get; }

    public bool Winner { get; }
    public int Games { get; }
    public double AveragePlacement { get; }
}

/// <summary>The median turn a group of players reached a tavern tier ("top4" reached tier 5 at turn 8).</summary>
/// <summary>A kind of trinket ("lesser", "greater") over every pick: its games and their average placement.</summary>
public sealed class NomiTrinketKind
{
    public NomiTrinketKind(string kind, int games, double averagePlacement)
    {
        Kind = kind;
        Games = games;
        AveragePlacement = averagePlacement;
    }

    public string Kind { get; }
    public int Games { get; }
    public double AveragePlacement { get; }
}

public sealed class NomiTierMedian
{
    public NomiTierMedian(string scope, string group, int tier, double medianTurn, double reached)
    {
        Scope = scope;
        Group = group;
        Tier = tier;
        MedianTurn = medianTurn;
        Reached = reached;
    }

    /// <summary>"all", or "high" (nomi.gg's few players above 10,000 MMR).</summary>
    public string Scope { get; }

    /// <summary>"first", "top4" or "bottom4".</summary>
    public string Group { get; }

    public int Tier { get; }
    public double MedianTurn { get; }

    /// <summary>The share of that group's games that reached the tier at all.</summary>
    public double Reached { get; }
}

/// <summary>
/// What the plugin keeps of nomi.gg's patch analysis (chantier c): only what the helps of chantier d read — heroes,
/// tribes before and after the patch, buffed and nerfed tribes, winning and losing trinkets, the tier-up medians. Display
/// names are not kept: HearthDb gives them.
/// </summary>
public sealed class NomiAnalysisFile
{
    public NomiAnalysisFile(StatProvenance provenance, int? build, IReadOnlyList<string> buffed, IReadOnlyList<string> nerfed,
        IReadOnlyList<NomiHero> heroes, IReadOnlyList<NomiTribe> tribes, IReadOnlyList<NomiTrinket> trinkets, IReadOnlyList<NomiTierMedian> tierMedians,
        IReadOnlyList<NomiTrinketKind>? trinketKinds = null)
    {
        TrinketKinds = trinketKinds ?? new NomiTrinketKind[0];
        Provenance = provenance;
        Build = build;
        Buffed = buffed;
        Nerfed = nerfed;
        Heroes = heroes;
        Tribes = tribes;
        Trinkets = trinkets;
        TierMedians = tierMedians;
    }

    public StatProvenance Provenance { get; }

    /// <summary>The game build the analysis is for (253216); null when the file does not say.</summary>
    public int? Build { get; }

    public IReadOnlyList<string> Buffed { get; }
    public IReadOnlyList<string> Nerfed { get; }
    public IReadOnlyList<NomiHero> Heroes { get; }
    public IReadOnlyList<NomiTribe> Tribes { get; }
    public IReadOnlyList<NomiTrinket> Trinkets { get; }
    public IReadOnlyList<NomiTierMedian> TierMedians { get; }

    /// <summary>Each kind of trinket over every pick: the population the winners and losers are picked from.</summary>
    public IReadOnlyList<NomiTrinketKind> TrinketKinds { get; }

    public DateTimeOffset? FetchedAt => Provenance.FetchedAt;
}

/// <summary>
/// nomi.gg's <c>/patch/analysis/&lt;patch&gt;.json</c> into <see cref="NomiAnalysisFile"/>, and the local format (schema 1)
/// both ways. Strict like the other formats: a broken rule rejects the whole file, naming the field.
/// </summary>
public static class NomiAnalysis
{
    /// <summary>2 since 2026-10-10: the trinket kinds' averages (recentring); a cache in 1 is asked again.</summary>
    public const int CurrentSchema = 2;

    public static NomiAnalysisFile Import(string json, string sourceUrl, DateTimeOffset fetchedAt)
    {
        var root = Root(json);
        var patch = PatchOf(root.Value<string>("patch"), "patch");
        var postStart = (root["window"] as JObject)?.Value<string>("postStart");
        var provenance = new StatProvenance(StatsSources.NomiGg, sourceUrl, StatProvenance.Date(root, "generated"), fetchedAt,
            postStart != null ? "since " + postStart : null, mmrPercentile: null, patch);

        var heroes = Objects(root, "heroes").Select((h, i) => new NomiHero(
            Text(h, "id", $"heroes[{i}]"), Int(h, "games", $"heroes[{i}]"), Number(h, "avg", $"heroes[{i}]"),
            Number(h, "top4", $"heroes[{i}]"), Number(h, "win", $"heroes[{i}]"), Number(h, "pick", $"heroes[{i}]"))).ToList();

        var tribes = root["tribes"] as JObject;
        var pre = tribes?["pre"] as JObject;
        var post = tribes?["post"] as JObject;
        var names = (pre?.Properties() ?? Enumerable.Empty<JProperty>()).Concat(post?.Properties() ?? Enumerable.Empty<JProperty>())
            .Select(p => p.Name).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal);
        var tribeList = names.Select(name =>
        {
            var before = pre?[name] as JObject;
            var after = post?[name] as JObject;
            return new NomiTribe(name,
                before == null ? 0 : Int(before, "games", $"tribes.pre.{name}"), before == null ? null : Optional(before, "avg", $"tribes.pre.{name}"),
                after == null ? 0 : Int(after, "games", $"tribes.post.{name}"), after == null ? null : Optional(after, "avg", $"tribes.post.{name}"));
        }).ToList();

        var trinkets = new List<NomiTrinket>();
        var kinds = new List<NomiTrinketKind>();
        foreach (var kind in new[] { "lesser", "greater" })
        {
            if (root["trinkets"]?[kind] is not JObject group)
            {
                continue;
            }

            if (group["games"] != null && Optional(group, "avg", $"trinkets.{kind}") is { } average)
            {
                kinds.Add(new NomiTrinketKind(kind, Int(group, "games", $"trinkets.{kind}"), average));
            }

            foreach (var (list, winner) in new[] { ("winners", true), ("losers", false) })
            {
                trinkets.AddRange(Objects(group, list, required: false).Select((t, i) => new NomiTrinket(
                    Text(t, "id", $"trinkets.{kind}.{list}[{i}]"), kind, winner, Int(t, "games", $"trinkets.{kind}.{list}[{i}]"),
                    Number(t, "avg", $"trinkets.{kind}.{list}[{i}]"))));
            }
        }

        var medians = new List<NomiTierMedian>();
        foreach (var scope in new[] { "all", "high" })
        {
            if (root["curve"]?[scope]?["medians"] is not JObject groups)
            {
                continue;
            }

            foreach (var group in groups.Properties())
            {
                foreach (var tier in (group.Value as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
                {
                    var where = $"curve.{scope}.medians.{group.Name}.{tier.Name}";
                    if (!int.TryParse(tier.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var level) || tier.Value is not JObject m)
                    {
                        throw new StatsFormatException($"{where}: expected a tier number and an object");
                    }

                    medians.Add(new NomiTierMedian(scope, group.Name, level, Number(m, "median", where), Number(m, "reached", where)));
                }
            }
        }

        return new NomiAnalysisFile(provenance, root["build"]?.Type == JTokenType.Integer ? root.Value<int>("build") : null,
            Strings(root, "buffed"), Strings(root, "nerfed"), heroes, tribeList, trinkets, medians, kinds);
    }

    public static string Serialize(NomiAnalysisFile file)
    {
        var root = new JObject { ["schema"] = CurrentSchema };
        file.Provenance.WriteTo(root);
        root["build"] = file.Build.HasValue ? new JValue(file.Build.Value) : JValue.CreateNull();
        root["buffed"] = new JArray(file.Buffed);
        root["nerfed"] = new JArray(file.Nerfed);
        root["heroes"] = new JArray(file.Heroes.Select(h => new JObject
        {
            ["id"] = h.HeroCardId,
            ["games"] = h.Games,
            ["avg"] = h.AveragePlacement,
            ["top4"] = h.Top4,
            ["win"] = h.Win,
            ["pick"] = h.Pick,
        }));
        root["tribes"] = new JArray(file.Tribes.Select(t => new JObject
        {
            ["tribe"] = t.Tribe,
            ["preGames"] = t.PreGames,
            ["preAvg"] = t.PreAverage,
            ["postGames"] = t.PostGames,
            ["postAvg"] = t.PostAverage,
        }));
        root["trinkets"] = new JArray(file.Trinkets.Select(t => new JObject
        {
            ["id"] = t.TrinketCardId,
            ["kind"] = t.Kind,
            ["winner"] = t.Winner,
            ["games"] = t.Games,
            ["avg"] = t.AveragePlacement,
        }));
        root["trinketKinds"] = new JArray(file.TrinketKinds.Select(k => new JObject
        {
            ["kind"] = k.Kind,
            ["games"] = k.Games,
            ["avg"] = k.AveragePlacement,
        }));
        root["tierMedians"] = new JArray(file.TierMedians.Select(m => new JObject
        {
            ["scope"] = m.Scope,
            ["group"] = m.Group,
            ["tier"] = m.Tier,
            ["median"] = m.MedianTurn,
            ["reached"] = m.Reached,
        }));
        return root.ToString(Formatting.Indented);
    }

    public static NomiAnalysisFile Parse(string json)
    {
        var root = Root(json);
        if (root["schema"]?.Type != JTokenType.Integer || root.Value<int>("schema") != CurrentSchema)
        {
            throw new StatsFormatException($"schema: expected {CurrentSchema}");
        }

        var provenance = StatProvenance.ReadFrom(root, "nomi.gg analysis");
        PatchOf(provenance.Patch, "patch");
        return new NomiAnalysisFile(provenance, root["build"]?.Type == JTokenType.Integer ? root.Value<int>("build") : null,
            Strings(root, "buffed"), Strings(root, "nerfed"),
            Objects(root, "heroes").Select((h, i) => new NomiHero(Text(h, "id", $"heroes[{i}]"), Int(h, "games", $"heroes[{i}]"),
                Number(h, "avg", $"heroes[{i}]"), Number(h, "top4", $"heroes[{i}]"), Number(h, "win", $"heroes[{i}]"), Number(h, "pick", $"heroes[{i}]"))).ToList(),
            Objects(root, "tribes").Select((t, i) => new NomiTribe(Text(t, "tribe", $"tribes[{i}]"), Int(t, "preGames", $"tribes[{i}]"),
                Optional(t, "preAvg", $"tribes[{i}]"), Int(t, "postGames", $"tribes[{i}]"), Optional(t, "postAvg", $"tribes[{i}]"))).ToList(),
            Objects(root, "trinkets").Select((t, i) => new NomiTrinket(Text(t, "id", $"trinkets[{i}]"), Text(t, "kind", $"trinkets[{i}]"),
                t["winner"]?.Type == JTokenType.Boolean ? t.Value<bool>("winner") : throw new StatsFormatException($"trinkets[{i}].winner: expected a boolean"),
                Int(t, "games", $"trinkets[{i}]"), Number(t, "avg", $"trinkets[{i}]"))).ToList(),
            Objects(root, "tierMedians").Select((m, i) => new NomiTierMedian(Text(m, "scope", $"tierMedians[{i}]"), Text(m, "group", $"tierMedians[{i}]"),
                Int(m, "tier", $"tierMedians[{i}]"), Number(m, "median", $"tierMedians[{i}]"), Number(m, "reached", $"tierMedians[{i}]"))).ToList(),
            Objects(root, "trinketKinds").Select((k, i) => new NomiTrinketKind(Text(k, "kind", $"trinketKinds[{i}]"), Int(k, "games", $"trinketKinds[{i}]"),
                Number(k, "avg", $"trinketKinds[{i}]"))).ToList());
    }

    /// <summary>
    /// A patch name as nomi.gg writes it ("36.6.3"): digits and dots only. It comes from the network and names a URL and a
    /// file, so anything else is refused.
    /// </summary>
    public static string PatchOf(string? patch, string field)
    {
        if (patch is not { Length: > 0 } || patch.Length > 20 || patch[0] == '.' || patch[patch.Length - 1] == '.'
            || patch.Contains("..") || patch.Any(c => c != '.' && (c < '0' || c > '9')))
        {
            throw new StatsFormatException($"{field}: expected a version such as 36.6.3, got \"{patch}\"");
        }

        return patch;
    }

    private static JObject Root(string json)
    {
        try
        {
            return JToken.Parse(json) as JObject ?? throw new StatsFormatException("root: expected an object");
        }
        catch (JsonException e)
        {
            throw new StatsFormatException("root: " + e.Message);
        }
    }

    private static IEnumerable<JObject> Objects(JObject obj, string name, bool required = true) =>
        obj[name] switch
        {
            JArray array => array.Select((item, i) => item as JObject ?? throw new StatsFormatException($"{name}[{i}]: expected an object")).ToList(),
            null when !required => Enumerable.Empty<JObject>(),
            _ => throw new StatsFormatException($"{name}: expected an array"),
        };

    private static IReadOnlyList<string> Strings(JObject obj, string name) =>
        obj[name] is JArray array ? array.Select(t => t.Type == JTokenType.String ? (string)t! : throw new StatsFormatException($"{name}: expected strings")).ToList()
            : new string[0];

    private static string Text(JObject obj, string name, string where) =>
        obj[name]?.Type == JTokenType.String && obj.Value<string>(name) is { Length: > 0 } text ? text : throw new StatsFormatException($"{where}.{name}: expected a string");

    private static int Int(JObject obj, string name, string where) =>
        obj[name]?.Type == JTokenType.Integer && obj.Value<int>(name) >= 0 ? obj.Value<int>(name) : throw new StatsFormatException($"{where}.{name}: expected a count");

    private static double Number(JObject obj, string name, string where) =>
        obj[name]?.Type is JTokenType.Integer or JTokenType.Float ? obj.Value<double>(name) : throw new StatsFormatException($"{where}.{name}: expected a number");

    private static double? Optional(JObject obj, string name, string where) =>
        obj[name] == null || obj[name]!.Type == JTokenType.Null ? null : Number(obj, name, where);
}
