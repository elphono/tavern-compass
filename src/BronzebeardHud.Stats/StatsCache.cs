using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace BronzebeardHud.Stats;

/// <summary>Downloads one URL as text. Throws on any network or HTTP failure.</summary>
public interface IStatsFetcher
{
    Task<string> FetchAsync(string url, CancellationToken cancellationToken);
}

/// <summary>What a conditional GET brought back: new text with its ETag, or "not modified" (HTTP 304).</summary>
public sealed class FetchedText
{
    private FetchedText(string? body, string? etag)
    {
        Body = body;
        ETag = etag;
    }

    /// <summary>The server confirmed that the copy named by the ETag sent is still the current one.</summary>
    public static FetchedText NotModified { get; } = new(null, null);

    public static FetchedText Changed(string body, string? etag) =>
        new(body ?? throw new ArgumentNullException(nameof(body)), etag);

    public bool IsNotModified => Body == null;

    /// <summary>The downloaded text; null only for <see cref="NotModified"/>.</summary>
    public string? Body { get; }

    /// <summary>The server's ETag for <see cref="Body"/>, verbatim (static.zerotoheroes.com sends a weak one, W/"…"); null when none.</summary>
    public string? ETag { get; }
}

/// <summary>
/// Downloads one URL as text, conditionally: with an ETag, the server may answer "not modified" instead of
/// the whole file (measured on static.zerotoheroes.com on 2026-09-28: 304 to If-None-Match). Throws on any
/// network or HTTP failure, as <see cref="IStatsFetcher"/> does.
/// </summary>
public interface IConditionalFetcher
{
    /// <param name="ifNoneMatch">The ETag of the cached copy; null for a plain GET, which never gets 304.</param>
    Task<FetchedText> FetchAsync(string url, string? ifNoneMatch, CancellationToken cancellationToken);
}

/// <summary>The real fetcher: plain HTTPS GET, gzip accepted, identified by its user agent.</summary>
public sealed class HttpStatsFetcher : IStatsFetcher, IConditionalFetcher, IDisposable
{
    private readonly HttpClient _client;

    public HttpStatsFetcher()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    /// <summary>
    /// Names the plugin, its version (Directory.Build.props) and its repository on every request, as promised to nomi.gg's
    /// owner, so that a site can tell us apart and reach us.
    /// </summary>
    public static string UserAgent
    {
        get
        {
            var v = typeof(HttpStatsFetcher).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return $"TavernCompass/{v.Major}.{v.Minor}.{v.Build} (+https://github.com/elphono/tavern-compass)";
        }
    }

    public async Task<string> FetchAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    public async Task<FetchedText> FetchAsync(string url, string? ifNoneMatch, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (ifNoneMatch != null)
        {
            request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(ifNoneMatch));
        }

        using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotModified && ifNoneMatch != null)
        {
            return FetchedText.NotModified;
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return FetchedText.Changed(body, response.Headers.ETag?.ToString());
    }

    public void Dispose() => _client.Dispose();
}

/// <summary>How often a cached stats file may be downloaded again.</summary>
public sealed class RefreshPolicy
{
    public RefreshPolicy(TimeSpan maxAge, TimeSpan retryAfterFailure)
    {
        MaxAge = maxAge;
        RetryAfterFailure = retryAfterFailure;
    }

    /// <summary>
    /// A cached file younger than this is served without any network call, except at its first use by a
    /// <see cref="StatsCache"/>, which always asks the server (see there).
    /// </summary>
    public TimeSpan MaxAge { get; }

    /// <summary>After a failed download, no new attempt before this delay (first use by a <see cref="StatsCache"/> excepted).</summary>
    public TimeSpan RetryAfterFailure { get; }

    /// <summary>Hero stats: about 0.5 MB, regenerated hourly by Firestone, slow-moving over a patch.</summary>
    public static RefreshPolicy HeroStats { get; } = new(TimeSpan.FromHours(24), TimeSpan.FromHours(1));

    /// <summary>Composition stats: 31.5 MB per download, so a week.</summary>
    public static RefreshPolicy CompStats { get; } = new(TimeSpan.FromDays(7), TimeSpan.FromHours(1));
}

/// <summary>What <see cref="StatsCache.GetHeroStatsAsync"/> hands back.</summary>
public sealed class CacheResult
{
    public CacheResult(HeroStatsFile? file, bool downloaded, string? error, bool unchanged = false)
    {
        File = file;
        Downloaded = downloaded;
        Error = error;
        Unchanged = unchanged;
    }

    /// <summary>The stats to show; null only when nothing valid was ever cached.</summary>
    public HeroStatsFile? File { get; }

    /// <summary>True when this call went to the network and succeeded.</summary>
    public bool Downloaded { get; }

    /// <summary>True when this call asked the server, which confirmed the cached copy is current (HTTP 304).</summary>
    public bool Unchanged { get; }

    /// <summary>Why the stats may be stale or missing; null when all is well.</summary>
    public string? Error { get; }
}

/// <summary>What <see cref="StatsCache.GetCompositionsAsync"/> hands back.</summary>
public sealed class CompositionCacheResult
{
    public CompositionCacheResult(CompositionFile? file, bool downloaded, string? error, bool unchanged = false)
    {
        File = file;
        Downloaded = downloaded;
        Error = error;
        Unchanged = unchanged;
    }

    public CompositionFile? File { get; }
    public bool Downloaded { get; }
    public bool Unchanged { get; }
    public string? Error { get; }
}

/// <summary>
/// Local cache of Firestone stats: hero stats (one file per MMR percentile and time period) and
/// composition stats (one file per time period).
/// Rules:
/// - one instance lives as long as a plugin session (HDT's OnLoad creates it); the FIRST use of each file
///   by an instance always asks the server, whatever the age of the cache or a recent failure, so that
///   starting HDT brings the freshest stats (Ali, 2026-09-28). The request carries the cached copy's ETag
///   (kept beside it, <c>*.etag</c>): an unchanged file costs a 304 and no download;
/// - afterwards, no request while the cached file is younger than <see cref="RefreshPolicy.MaxAge"/>, and
///   after a failure, none before <see cref="RefreshPolicy.RetryAfterFailure"/>;
/// - a failed or malformed download never replaces a valid cached file.
/// </summary>
public sealed class StatsCache
{
    private readonly string _directory;
    private readonly IConditionalFetcher _fetcher;
    private readonly Func<DateTimeOffset> _clock;
    private readonly ConcurrentDictionary<string, bool> _checkedWithServer = new(StringComparer.Ordinal);

    public StatsCache(string directory, IConditionalFetcher fetcher, Func<DateTimeOffset> clock)
    {
        _directory = directory;
        _fetcher = fetcher;
        _clock = clock;
    }

    public string HeroStatsPath(int mmrPercentile, string timePeriod) =>
        Path.Combine(_directory, $"firestone-hero-stats-mmr-{mmrPercentile}-{timePeriod}.json");

    public string CompositionsPath(string timePeriod) =>
        Path.Combine(_directory, $"firestone-comp-stats-{timePeriod}.json");

    public async Task<CacheResult> GetHeroStatsAsync(
        int mmrPercentile, string timePeriod, RefreshPolicy policy, CancellationToken cancellationToken)
    {
        var (file, downloaded, error, unchanged) = await GetAsync(
            FirestoneEndpoints.HeroStats(mmrPercentile, timePeriod),
            HeroStatsPath(mmrPercentile, timePeriod),
            policy,
            path => HeroStatsLoader.RequireCurrent(HeroStatsLoader.Load(path)),
            f => f.FetchedAt,
            FirestoneHeroStatsImporter.Import,
            HeroStatsLoader.Serialize,
            cancellationToken).ConfigureAwait(false);
        return new CacheResult(file, downloaded, error, unchanged);
    }

    /// <summary>Firestone composition stats, converted to the local composition format (31.5 MB download, see <see cref="RefreshPolicy.CompStats"/>).</summary>
    public async Task<CompositionCacheResult> GetCompositionsAsync(
        string timePeriod, RefreshPolicy policy, CancellationToken cancellationToken)
    {
        var (file, downloaded, error, unchanged) = await GetAsync(
            FirestoneEndpoints.CompStats(timePeriod),
            CompositionsPath(timePeriod),
            policy,
            CompositionLoader.Load,
            f => f.FetchedAt,
            FirestoneCompImporter.Import,
            CompositionLoader.Serialize,
            cancellationToken).ConfigureAwait(false);
        return new CompositionCacheResult(file, downloaded, error, unchanged);
    }

    public string TrinketStatsPath(string timePeriod) =>
        Path.Combine(_directory, $"firestone-trinket-stats-{timePeriod}.json");

    /// <summary>Firestone trinket stats (about 0.2 MB), same daily policy as the hero stats.</summary>
    public async Task<(TrinketStatsFile? File, bool Downloaded, string? Error, bool Unchanged)> GetTrinketStatsAsync(
        string timePeriod, RefreshPolicy policy, CancellationToken cancellationToken) =>
        await GetAsync(
            FirestoneEndpoints.TrinketStats(timePeriod),
            TrinketStatsPath(timePeriod),
            policy,
            TrinketStatsLoader.Load,
            f => f.FetchedAt,
            TrinketStatsLoader.ImportFirestone,
            TrinketStatsLoader.Serialize,
            cancellationToken).ConfigureAwait(false);

    public string CardStatsPath(int mmrPercentile, string timePeriod) =>
        Path.Combine(_directory, $"firestone-card-stats-mmr-{mmrPercentile}-{timePeriod}.json");

    /// <summary>Firestone card stats for one MMR bracket (about 0.6 MB once converted), same daily policy as the hero stats.</summary>
    public async Task<(CardStatsFile? File, bool Downloaded, string? Error, bool Unchanged)> GetCardStatsAsync(
        int mmrPercentile, string timePeriod, RefreshPolicy policy, CancellationToken cancellationToken) =>
        await GetAsync(
            FirestoneEndpoints.CardStats(mmrPercentile, timePeriod),
            CardStatsPath(mmrPercentile, timePeriod),
            policy,
            CardStatsLoader.Load,
            f => f.FetchedAt,
            (body, url, at) => CardStatsLoader.ImportFirestone(body, url, at, mmrPercentile),
            CardStatsLoader.Serialize,
            cancellationToken).ConfigureAwait(false);

    private async Task<(T? File, bool Downloaded, string? Error, bool Unchanged)> GetAsync<T>(
        string url,
        string path,
        RefreshPolicy policy,
        Func<string, T> load,
        Func<T, DateTimeOffset?> fetchedAtOf,
        Func<string, string, DateTimeOffset, T> import,
        Func<T, string> serialize,
        CancellationToken cancellationToken)
        where T : class
    {
        var failurePath = path + ".failed";
        var etagPath = path + ".etag";
        var now = _clock();
        var firstUse = _checkedWithServer.TryAdd(path, true);

        var (cached, cacheProblem) = TryLoad(path, load);
        if (!firstUse && cached != null && fetchedAtOf(cached) is { } fetchedAt && now - fetchedAt < policy.MaxAge)
        {
            return (cached, false, null, false);
        }

        // A cache that exists but cannot be used (older format, damaged file) is downloaded again like a
        // missing one; if that fails too, the error names both, so that no caller shows an unexplained zero.
        var prefix = cacheProblem == null ? string.Empty : $"cache: {cacheProblem}, ";
        var lastFailure = TryReadFailure(failurePath);
        if (!firstUse && lastFailure is { } failedAt && now - failedAt < policy.RetryAfterFailure)
        {
            var retryAt = failedAt + policy.RetryAfterFailure;
            return (cached, false, $"{prefix}last download failed at {Format(failedAt)}; next attempt after {Format(retryAt)}", false);
        }

        try
        {
            // Only a usable cache may be confirmed by a 304: without one, ask for the whole file.
            var etag = cached != null && File.Exists(etagPath) ? File.ReadAllText(etagPath).Trim() : null;
            var fetched = await _fetcher.FetchAsync(url, string.IsNullOrEmpty(etag) ? null : etag, cancellationToken).ConfigureAwait(false);
            if (fetched.IsNotModified)
            {
                if (cached == null)
                {
                    throw new InvalidOperationException("the server answered 304 (not modified) but there is no cached copy");
                }

                DeleteIfExists(failurePath);
                return (cached, false, null, true);
            }

            var imported = import(fetched.Body!, url, now);
            Directory.CreateDirectory(_directory);
            WriteAtomically(path, serialize(imported));
            if (fetched.ETag != null)
            {
                WriteAtomically(etagPath, fetched.ETag);
            }
            else
            {
                DeleteIfExists(etagPath);
            }

            DeleteIfExists(failurePath);
            return (imported, true, null, false);
        }
        // An HttpClient timeout is also an OperationCanceledException: only a cancellation the
        // caller asked for escapes; a timeout is a failed download like any other.
        catch (Exception e) when (!(e is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(failurePath, now.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            return (cached, false, cacheProblem == null ? $"download of {url} failed: {e.Message}" : $"{prefix}redownload failed: {e.Message}", false);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// The cached file, or why it cannot be used: a corrupt cache, or one in an older format, counts as no
    /// cache and is replaced by the next good download. (null, null) when there is no file at all.
    /// </summary>
    private static (T? File, string? Problem) TryLoad<T>(string path, Func<string, T> load)
        where T : class
    {
        if (!File.Exists(path))
        {
            return (null, null);
        }

        try
        {
            return (load(path), null);
        }
        catch (StatsFormatException e)
        {
            return (null, e.Message);
        }
    }

    private static DateTimeOffset? TryReadFailure(string failurePath)
    {
        if (!File.Exists(failurePath))
        {
            return null;
        }

        return DateTimeOffset.TryParse(File.ReadAllText(failurePath).Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var failedAt)
            ? failedAt
            : null;
    }

    /// <summary>Write a sibling temp file, then swap it in, so a crash never leaves half a file.</summary>
    private static void WriteAtomically(string path, string content)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        if (File.Exists(path))
        {
            File.Replace(temp, path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temp, path);
        }
    }

    private static string Format(DateTimeOffset date) =>
        date.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
