using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// nomi.gg's patch analysis on disk, asked as its owner agreed (CLAUDE.md, "Décisions"; note § 10): latest.json, then
/// the current patch's analysis, in conditional requests, <b>one attempt a day at most</b>. Unlike StatsCache, which asks
/// Firestone at each plugin start, the day is counted from a date kept on disk (<c>nomi-state.json</c>), written before
/// the first request: HDT restarts and failures count too. A patch closed by a newer one is never asked again; its file
/// stays as it was.
/// </summary>
public sealed class NomiCache
{
    public const string LatestUrl = "https://nomi.gg/patch/data/latest.json";

    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    private const string StateFile = "nomi-state.json";

    private readonly string _directory;
    private readonly IConditionalFetcher _fetcher;
    private readonly Func<DateTimeOffset> _clock;

    public NomiCache(string directory, IConditionalFetcher fetcher, Func<DateTimeOffset> clock)
    {
        _directory = directory;
        _fetcher = fetcher;
        _clock = clock;
    }

    public static string AnalysisUrl(string patch) => $"https://nomi.gg/patch/analysis/{patch}.json";

    public string AnalysisPath(string patch) => Path.Combine(_directory, $"nomi-patch-analysis-{patch}.json");

    /// <summary>The cached analysis, asked again only when the last attempt is a day old (or the clock went back: never).</summary>
    public async Task<(NomiAnalysisFile? File, bool Downloaded, string? Error, bool Unchanged)> GetAsync(CancellationToken cancellationToken)
    {
        var state = ReadState();
        var cached = state.Patch != null ? ReadAnalysis(state.Patch) : null;
        var now = _clock();
        if (state.AskedAt is { } asked && now - asked < Interval)
        {
            return (cached, false, cached == null ? "nothing yet; next attempt after " + Format(asked + Interval) : null, false);
        }

        // The day's attempt is spent before the first byte leaves: a crash or a failure does not grant a second one.
        state = state.With(askedAt: now);
        WriteState(state);
        try
        {
            var latest = await _fetcher.FetchAsync(LatestUrl, cached != null ? state.LatestETag : null, cancellationToken).ConfigureAwait(false);
            var patch = state.Patch;
            if (!latest.IsNotModified)
            {
                patch = NomiAnalysis.PatchOf((JToken.Parse(latest.Body!) as JObject)?.Value<string>("version"), "latest.json: version");
                state = state.With(latestETag: latest.ETag);
            }

            if (patch == null)
            {
                return (cached, false, "latest.json: version: unchanged, but no patch known", false);
            }

            var same = patch == state.Patch && cached != null;
            var analysis = await _fetcher.FetchAsync(AnalysisUrl(patch), same ? state.AnalysisETag : null, cancellationToken).ConfigureAwait(false);
            if (analysis.IsNotModified)
            {
                WriteState(state);
                return (cached, false, null, true);
            }

            var file = NomiAnalysis.Import(analysis.Body!, AnalysisUrl(patch), now);
            File.WriteAllText(AnalysisPath(patch), NomiAnalysis.Serialize(file));
            WriteState(state.With(patch: patch, analysisETag: analysis.ETag));
            return (file, true, null, false);
        }
        catch (Exception e) when (e is StatsFormatException or JsonException or IOException or System.Net.Http.HttpRequestException or TaskCanceledException)
        {
            return (cached, false, e is StatsFormatException ? e.Message : $"{e.GetType().Name}: {e.Message}", false);
        }
    }

    private NomiAnalysisFile? ReadAnalysis(string patch)
    {
        try
        {
            var path = AnalysisPath(NomiAnalysis.PatchOf(patch, "state.patch"));
            return File.Exists(path) ? NomiAnalysis.Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception e) when (e is StatsFormatException or IOException)
        {
            return null;
        }
    }

    private State ReadState()
    {
        var path = Path.Combine(_directory, StateFile);
        try
        {
            if (File.Exists(path) && JToken.Parse(File.ReadAllText(path)) is JObject o)
            {
                return new State(StatProvenance.Date(o, "askedAt"), o.Value<string>("patch"), o.Value<string>("latestETag"), o.Value<string>("analysisETag"));
            }
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            // An unreadable state is a state never written; the attempt it leads to rewrites it.
        }

        return new State(null, null, null, null);
    }

    private void WriteState(State state)
    {
        var root = new JObject
        {
            ["askedAt"] = state.AskedAt is { } at ? Format(at) : null,
            ["patch"] = state.Patch,
            ["latestETag"] = state.LatestETag,
            ["analysisETag"] = state.AnalysisETag,
        };
        File.WriteAllText(Path.Combine(_directory, StateFile), root.ToString(Formatting.Indented));
    }

    private static string Format(DateTimeOffset date) =>
        date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private sealed class State
    {
        public State(DateTimeOffset? askedAt, string? patch, string? latestETag, string? analysisETag)
        {
            AskedAt = askedAt;
            Patch = patch;
            LatestETag = latestETag;
            AnalysisETag = analysisETag;
        }

        public DateTimeOffset? AskedAt { get; }
        public string? Patch { get; }
        public string? LatestETag { get; }
        public string? AnalysisETag { get; }

        public State With(DateTimeOffset? askedAt = null, string? patch = null, string? latestETag = null, string? analysisETag = null) =>
            new(askedAt ?? AskedAt, patch ?? Patch, latestETag ?? LatestETag, analysisETag ?? AnalysisETag);
    }
}
