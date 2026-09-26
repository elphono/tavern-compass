using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BronzebeardHud.Stats;

/// <summary>Downloads one URL as text. Throws on any network or HTTP failure.</summary>
public interface IStatsFetcher
{
    Task<string> FetchAsync(string url, CancellationToken cancellationToken);
}

/// <summary>The real fetcher: plain HTTPS GET, gzip accepted, identified by its user agent.</summary>
public sealed class HttpStatsFetcher : IStatsFetcher, IDisposable
{
    private readonly HttpClient _client;

    public HttpStatsFetcher()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("BronzebeardHud-HdtPlugin/0.1");
    }

    public async Task<string> FetchAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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

    /// <summary>A cached file younger than this is served without any network call.</summary>
    public TimeSpan MaxAge { get; }

    /// <summary>After a failed download, no new attempt before this delay.</summary>
    public TimeSpan RetryAfterFailure { get; }

    /// <summary>Hero stats: about 0.5 MB, regenerated hourly by Firestone, slow-moving over a patch.</summary>
    public static RefreshPolicy HeroStats { get; } = new(TimeSpan.FromHours(24), TimeSpan.FromHours(1));

    /// <summary>Composition stats: 31.5 MB per download, so a week.</summary>
    public static RefreshPolicy CompStats { get; } = new(TimeSpan.FromDays(7), TimeSpan.FromHours(1));
}

/// <summary>What <see cref="StatsCache.GetHeroStatsAsync"/> hands back.</summary>
public sealed class CacheResult
{
    public CacheResult(HeroStatsFile? file, bool downloaded, string? error)
    {
        File = file;
        Downloaded = downloaded;
        Error = error;
    }

    /// <summary>The stats to show; null only when nothing valid was ever cached.</summary>
    public HeroStatsFile? File { get; }

    /// <summary>True when this call went to the network and succeeded.</summary>
    public bool Downloaded { get; }

    /// <summary>Why the stats may be stale or missing; null when all is well.</summary>
    public string? Error { get; }
}

/// <summary>
/// Local cache of Firestone hero stats, one file per MMR percentile and time period.
/// Rules (see the spec): no download while the cached file is younger than
/// <see cref="RefreshPolicy.MaxAge"/>; after a failure, no retry before
/// <see cref="RefreshPolicy.RetryAfterFailure"/>; a failed or malformed download never
/// replaces a valid cached file.
/// </summary>
public sealed class StatsCache
{
    private readonly string _directory;
    private readonly IStatsFetcher _fetcher;
    private readonly Func<DateTimeOffset> _clock;

    public StatsCache(string directory, IStatsFetcher fetcher, Func<DateTimeOffset> clock)
    {
        _directory = directory;
        _fetcher = fetcher;
        _clock = clock;
    }

    public string HeroStatsPath(int mmrPercentile, string timePeriod) =>
        Path.Combine(_directory, $"firestone-hero-stats-mmr-{mmrPercentile}-{timePeriod}.json");

    public async Task<CacheResult> GetHeroStatsAsync(
        int mmrPercentile, string timePeriod, RefreshPolicy policy, CancellationToken cancellationToken)
    {
        var url = FirestoneEndpoints.HeroStats(mmrPercentile, timePeriod);
        var path = HeroStatsPath(mmrPercentile, timePeriod);
        var failurePath = path + ".failed";
        var now = _clock();

        var cached = TryLoad(path);
        if (cached?.FetchedAt is { } fetchedAt && now - fetchedAt < policy.MaxAge)
        {
            return new CacheResult(cached, downloaded: false, error: null);
        }

        var lastFailure = TryReadFailure(failurePath);
        if (lastFailure is { } failedAt && now - failedAt < policy.RetryAfterFailure)
        {
            var retryAt = failedAt + policy.RetryAfterFailure;
            return new CacheResult(cached, downloaded: false,
                error: $"last download failed at {Format(failedAt)}; next attempt after {Format(retryAt)}");
        }

        try
        {
            var body = await _fetcher.FetchAsync(url, cancellationToken).ConfigureAwait(false);
            var imported = FirestoneHeroStatsImporter.Import(body, url, now);
            Directory.CreateDirectory(_directory);
            WriteAtomically(path, HeroStatsLoader.Serialize(imported));
            if (File.Exists(failurePath))
            {
                File.Delete(failurePath);
            }

            return new CacheResult(imported, downloaded: true, error: null);
        }
        // An HttpClient timeout is also an OperationCanceledException: only a cancellation the
        // caller asked for escapes; a timeout is a failed download like any other.
        catch (Exception e) when (!(e is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(failurePath, now.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            return new CacheResult(cached, downloaded: false, error: $"download of {url} failed: {e.Message}");
        }
    }

    private static HeroStatsFile? TryLoad(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return HeroStatsLoader.Load(path);
        }
        catch (StatsFormatException)
        {
            // A corrupt cache counts as no cache; it will be replaced by the next good download.
            return null;
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
