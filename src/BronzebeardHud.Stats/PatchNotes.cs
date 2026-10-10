using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// What nomi.gg says of the current patch (issue #11, components 4 to 7, docs/plans/2026-10-08-stats-multi-sources.html
/// § 11): the banner under the heroes for the first games of a patch, a guide's main tribe since the patch, and in HDT's
/// log the tier-up pace and the data's freshness (decision 9: the log first, to judge whether they deserve a place).
/// </summary>
public static class PatchNotes
{
    /// <summary>The games of a patch that show its banner; after them it goes by itself.</summary>
    public const int BannerGames = 3;

    /// <summary>One game's placement varies by about 2.3 places (StatsConsolidation.PlacementSpread).</summary>
    private const double Spread = StatsConsolidation.PlacementSpread;

    /// <summary>"Patch 36.6.3 · buffed Quilboar, Mech · nerfed Aberration · nomi.gg"; null without nomi.gg's analysis.</summary>
    public static string? Banner(NomiAnalysisFile? nomi)
    {
        if (nomi?.Provenance.Patch is not { } patch)
        {
            return null;
        }

        var parts = new List<string> { "Patch " + patch };
        if (nomi.Buffed.Count > 0)
        {
            parts.Add("buffed " + string.Join(", ", nomi.Buffed.Select(Display)));
        }

        if (nomi.Nerfed.Count > 0)
        {
            parts.Add("nerfed " + string.Join(", ", nomi.Nerfed.Select(Display)));
        }

        parts.Add(StatsSources.NomiGg);
        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Counts one more game of <paramref name="patch"/> in <paramref name="state"/> (the JSON kept between games):
    /// whether that game shows the banner, and the state to keep. A new patch starts again; an unreadable state is a
    /// state never written.
    /// </summary>
    public static (bool Show, string State) CountGame(string? state, string patch)
    {
        var games = 0;
        try
        {
            if (state != null && JToken.Parse(state) is JObject o && o.Value<string>("patch") == patch && o["games"]?.Type == JTokenType.Integer)
            {
                games = o.Value<int>("games");
            }
        }
        catch (JsonException)
        {
            // Unreadable: counted from zero, and rewritten below.
        }

        games++;
        return (games <= BannerGames, new JObject { ["schema"] = 1, ["patch"] = patch, ["games"] = games }.ToString(Formatting.None));
    }

    /// <summary>
    /// Component 7: "Undead ▲ 4.48 → 3.93 since 36.6.3 (nomi.gg, 900 games)", the guide's main tribe before and after the
    /// patch; ▲ (better) or ▼ only when the gap is beyond twice its standard error, "within noise" otherwise; null when
    /// the guide has no main tribe or nomi.gg has no games for it on either side.
    /// </summary>
    public static string? TribeSince(CompGuide guide, NomiAnalysisFile? nomi)
    {
        if (nomi?.Provenance.Patch is not { } patch || GuideTribes.NameOf(guide.PrimaryTribe) is not { } name)
        {
            return null;
        }

        var key = Key(name);
        var tribe = nomi.Tribes.FirstOrDefault(t => t.Tribe == key);
        if (tribe is not { PreAverage: { } before, PostAverage: { } after } || tribe.PreGames == 0 || tribe.PostGames == 0)
        {
            return null;
        }

        var noise = 2 * Spread * Math.Sqrt(1.0 / tribe.PreGames + 1.0 / tribe.PostGames);
        var arrow = Math.Abs(after - before) < noise ? string.Empty : after < before ? "▲ " : "▼ ";
        var games = tribe.PostGames.ToString(CultureInfo.InvariantCulture) + " games" + (arrow.Length == 0 ? ", within noise" : string.Empty);
        return string.Format(CultureInfo.InvariantCulture, "{0} {1}{2:0.00} → {3:0.00} since {4} ({5}, {6})", Display(key), arrow, before, after, patch,
            StatsSources.NomiGg, games);
    }

    /// <summary>Component 6, for HDT's log: "tier pace tier=5 turn=9 · nomi.gg medians: top4 8 (99%), first 7 (100%), high top4 7.5 (100%)".</summary>
    public static string TierPace(int tier, int turn, NomiAnalysisFile? nomi)
    {
        var head = $"tier pace tier={tier} turn={turn}";
        if (nomi == null)
        {
            return head + " · nomi.gg not loaded";
        }

        var medians = nomi.TierMedians.Where(m => m.Tier == tier && m.Group is "top4" or "first" && m.Scope is "all" or "high").ToList();
        if (medians.Count == 0)
        {
            return $"{head} · nomi.gg medians: none for tier {tier}";
        }

        var parts = medians
            .OrderBy(m => m.Scope == "all" ? 0 : 1).ThenBy(m => m.Group == "top4" ? 0 : 1)
            .Select(m => string.Format(CultureInfo.InvariantCulture, "{0}{1} {2:0.#} ({3:0%})", m.Scope == "all" ? string.Empty : m.Scope + " ", m.Group,
                m.MedianTurn, m.Reached));
        return $"{head} · nomi.gg medians: {string.Join(", ", parts)}";
    }

    /// <summary>Component 5, for HDT's log: the game's build against the build of nomi.gg's patch.</summary>
    public static string Freshness(int? gameBuild, NomiAnalysisFile? nomi)
    {
        var game = gameBuild?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
        if (nomi == null)
        {
            return $"freshness game build={game} nomi.gg not loaded";
        }

        var data = nomi.Build?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
        var verdict = gameBuild is { } g && nomi.Build is { } d ? g == d ? "same build" : g > d ? "game newer" : "data newer" : "unknown";
        return $"freshness game build={game} nomi.gg build={data} (patch {nomi.Provenance.Patch}): {verdict}";
    }

    /// <summary>nomi.gg's key for a tribe: "mech" for MECHANICAL, the lower-case name otherwise.</summary>
    private static string Key(string tribe) => tribe == "MECHANICAL" ? "mech" : tribe.ToLowerInvariant();

    private static string Display(string key) => key.Length == 0 ? key : char.ToUpperInvariant(key[0]) + key.Substring(1);
}
