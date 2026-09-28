using System.Net.Http;
using static BronzebeardHud.Stats.Tests.TestData;

namespace BronzebeardHud.Stats.Tests;

public sealed class StatsCacheTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 26, 18, 0, 0, TimeSpan.Zero);
    private const string ExpectedUrl = "https://static.zerotoheroes.com/api/bgs/hero-stats/mmr-50/last-patch/overview-from-hourly.gz.json";

    private readonly string _directory = Directory.CreateTempSubdirectory("bbhud-cache-").FullName;
    private readonly FakeFetcher _fetcher = new();
    private DateTimeOffset _now = T0;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        Assert.Empty(_fetcher.Violations);
    }

    private StatsCache NewCache() => new(_directory, _fetcher, () => _now);

    /// <summary>The cache of the running plugin session; <see cref="Restart"/> starts a new one, as HDT's OnLoad does.</summary>
    private StatsCache _session;

    public StatsCacheTests() => _session = NewCache();

    private void Restart() => _session = NewCache();

    private Task<CacheResult> Get() =>
        _session.GetHeroStatsAsync(50, "last-patch", RefreshPolicy.HeroStats, CancellationToken.None);

    /// <summary>A payload whose only hero has the given placement, so each download is recognisable.</summary>
    private static string Payload(double placement) =>
        FirestonePayload(FirestoneHero("BG22_HERO_004", placement, dataPoints: 500, offered: 4000, picked: 700));

    private static double Placement(CacheResult result) => result.File!.Find("BG22_HERO_004")!.AveragePlacement;

    [Fact]
    public async Task EmptyCache_DownloadsOnceAndWritesTheFile()
    {
        _fetcher.Enqueue(Payload(3.4));

        var result = await Get();

        Assert.True(result.Downloaded);
        Assert.Null(result.Error);
        Assert.Equal(3.4, Placement(result));
        Assert.Equal(new[] { ExpectedUrl }, _fetcher.Urls);
        var onDisk = HeroStatsLoader.Load(NewCache().HeroStatsPath(50, "last-patch"));
        Assert.Equal(T0, onDisk.FetchedAt);
        Assert.Equal(3.4, onDisk.Find("BG22_HERO_004")!.AveragePlacement);
    }

    [Fact]
    public async Task RefreshesOnlyOnceTheCacheIsADayOld_OverThreeDays()
    {
        _fetcher.Enqueue(Payload(3.4));
        Assert.Equal(3.4, Placement(await Get()));

        _now = T0.AddHours(23).AddMinutes(59);
        var fresh = await Get();
        Assert.False(fresh.Downloaded);
        Assert.Equal(3.4, Placement(fresh));
        Assert.Single(_fetcher.Urls);

        _now = T0.AddHours(24);
        _fetcher.Enqueue(Payload(4.1));
        var second = await Get();
        Assert.True(second.Downloaded);
        Assert.Equal(4.1, Placement(second));
        Assert.Equal(2, _fetcher.Urls.Count);

        _now = T0.AddHours(47);
        Assert.Equal(4.1, Placement(await Get()));
        Assert.Equal(2, _fetcher.Urls.Count);

        _now = T0.AddHours(48);
        _fetcher.Enqueue(Payload(5.2));
        Assert.Equal(5.2, Placement(await Get()));
        Assert.Equal(3, _fetcher.Urls.Count);
    }

    [Fact]
    public async Task FailedDownload_KeepsTheOldStats_AndWaitsAnHourBeforeRetrying()
    {
        _fetcher.Enqueue(Payload(3.4));
        await Get();

        _now = T0.AddHours(25);
        _fetcher.EnqueueFailure(new HttpRequestException("Response status code does not indicate success: 503"));
        var failed = await Get();
        Assert.False(failed.Downloaded);
        Assert.Equal(3.4, Placement(failed));
        Assert.Contains("503", failed.Error);
        Assert.Equal(2, _fetcher.Urls.Count);

        _now = T0.AddHours(25).AddMinutes(59);
        var waiting = await Get();
        Assert.Equal(2, _fetcher.Urls.Count);
        Assert.Contains("next attempt after 2026-09-27 20:00 UTC", waiting.Error);
        Assert.Equal(3.4, Placement(waiting));

        _now = T0.AddHours(26);
        _fetcher.Enqueue(Payload(4.7));
        var recovered = await Get();
        Assert.True(recovered.Downloaded);
        Assert.Null(recovered.Error);
        Assert.Equal(4.7, Placement(recovered));
        Assert.Equal(3, _fetcher.Urls.Count);
    }

    [Fact]
    public async Task MalformedDownload_NeverOverwritesAValidCache()
    {
        _fetcher.Enqueue(Payload(3.4));
        await Get();
        var path = NewCache().HeroStatsPath(50, "last-patch");
        var before = File.ReadAllText(path);

        _now = T0.AddDays(2);
        _fetcher.Enqueue("{\"heroStats\": 5}");
        var result = await Get();

        Assert.False(result.Downloaded);
        Assert.Equal(3.4, Placement(result));
        Assert.Contains("heroStats", result.Error);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public async Task Timeout_IsAFailedDownload_NotACrash()
    {
        _fetcher.EnqueueFailure(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));

        var result = await Get();

        Assert.Null(result.File);
        Assert.False(result.Downloaded);
        Assert.Contains("Timeout", result.Error);
    }

    [Fact]
    public async Task CancellationByTheCaller_Propagates()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        _fetcher.EnqueueFailure(new OperationCanceledException(cancelled.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _session.GetHeroStatsAsync(50, "last-patch", RefreshPolicy.HeroStats, cancelled.Token));
    }

    [Fact]
    public async Task CorruptCacheFile_IsReplacedByTheNextDownload()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(NewCache().HeroStatsPath(50, "last-patch"), "{ truncated");
        _fetcher.Enqueue(Payload(6.3));

        var result = await Get();

        Assert.True(result.Downloaded);
        Assert.Equal(6.3, Placement(result));
    }

    [Fact]
    public async Task PluginStart_AsksTheServerEvenForAFreshCache_AndKeepsItOn304()
    {
        _fetcher.Enqueue(Payload(3.4));
        await Get();
        var path = NewCache().HeroStatsPath(50, "last-patch");
        var before = File.ReadAllText(path);

        _now = T0.AddHours(1);
        Restart();
        _fetcher.EnqueueUnchanged();
        var confirmed = await Get();

        Assert.False(confirmed.Downloaded);
        Assert.True(confirmed.Unchanged);
        Assert.Null(confirmed.Error);
        Assert.Equal(3.4, Placement(confirmed));
        Assert.Equal(new[] { null, _fetcher.CurrentETag }, _fetcher.SentETags);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public async Task PluginStart_TakesWhatChangedAtOnce_EvenAMinuteAfterTheLastDownload()
    {
        _fetcher.Enqueue(Payload(3.4));
        await Get();

        _now = T0.AddMinutes(1);
        Restart();
        _fetcher.Enqueue(Payload(4.1));
        var fresh = await Get();

        Assert.True(fresh.Downloaded);
        Assert.False(fresh.Unchanged);
        Assert.Equal(4.1, Placement(fresh));
        Assert.Equal(_now, HeroStatsLoader.Load(NewCache().HeroStatsPath(50, "last-patch")).FetchedAt);
    }

    [Fact]
    public async Task ThreeRestarts_EachSendsTheETagOfWhatTheLastOneKept()
    {
        _fetcher.Enqueue(Payload(3.4));
        await Get();
        var first = _fetcher.CurrentETag;

        _now = T0.AddHours(1);
        Restart();
        _fetcher.Enqueue(Payload(4.1));
        Assert.Equal(4.1, Placement(await Get()));
        var second = _fetcher.CurrentETag;

        _now = T0.AddHours(2);
        Restart();
        _fetcher.EnqueueUnchanged();
        Assert.Equal(4.1, Placement(await Get()));

        _now = T0.AddHours(3);
        Restart();
        _fetcher.Enqueue(Payload(5.2));
        Assert.Equal(5.2, Placement(await Get()));

        Assert.NotEqual(first, second);
        Assert.Equal(new[] { null, first, second, second }, _fetcher.SentETags);
    }

    [Fact]
    public async Task WithinASession_OnlyTheFirstUseGoesToTheServer_ThenTheDailyRuleHolds()
    {
        _fetcher.Enqueue(Payload(3.4));
        await Get();

        _now = T0.AddHours(1);
        Restart();
        _fetcher.EnqueueUnchanged();
        await Get();

        _now = T0.AddHours(5);
        var cached = await Get();
        Assert.False(cached.Downloaded);
        Assert.False(cached.Unchanged);
        Assert.Equal(2, _fetcher.Urls.Count);
    }

    [Fact]
    public async Task PluginStart_RetriesAtOnce_EvenWithinTheHourAfterAFailure_AndAFailureKeepsTheCache()
    {
        _fetcher.Enqueue(Payload(3.4));
        await Get();

        _now = T0.AddHours(25);
        _fetcher.EnqueueFailure(new HttpRequestException("Response status code does not indicate success: 503"));
        Assert.Contains("503", (await Get()).Error);

        _now = T0.AddHours(25).AddMinutes(10);
        Restart();
        _fetcher.EnqueueFailure(new HttpRequestException("Response status code does not indicate success: 502"));
        var failed = await Get();
        Assert.Equal(3, _fetcher.Urls.Count);
        Assert.Contains("502", failed.Error);
        Assert.Equal(3.4, Placement(failed));

        _now = T0.AddHours(25).AddMinutes(20);
        Restart();
        _fetcher.Enqueue(Payload(4.7));
        Assert.Equal(4.7, Placement(await Get()));
    }

    [Fact]
    public async Task A304WithoutAnyCache_IsAnError_NotAnEmptySuccess()
    {
        var cache = new StatsCache(_directory, new Always304(), () => _now);

        var result = await cache.GetHeroStatsAsync(50, "last-patch", RefreshPolicy.HeroStats, CancellationToken.None);

        Assert.Null(result.File);
        Assert.False(result.Unchanged);
        Assert.Contains("304", result.Error);
    }

    [Fact]
    public async Task ADownloadWithoutETag_ForgetsThePreviousOne()
    {
        _fetcher.Enqueue(Payload(3.4));
        await Get();

        _now = T0.AddHours(1);
        Restart();
        _fetcher.EnqueueWithoutETag(Payload(4.1));
        await Get();

        _now = T0.AddHours(2);
        Restart();
        _fetcher.Enqueue(Payload(5.2));
        Assert.Equal(5.2, Placement(await Get()));
        Assert.Null(_fetcher.SentETags[^1]);
    }

    [Fact]
    public async Task AnUnusableCache_IsAskedInFull_NeverConfirmedByItsOldETag()
    {
        _fetcher.Enqueue(Payload(3.4));
        await Get();
        File.WriteAllText(NewCache().HeroStatsPath(50, "last-patch"), "{ truncated");

        _now = T0.AddHours(1);
        Restart();
        _fetcher.Enqueue(Payload(6.3));
        var result = await Get();

        Assert.True(result.Downloaded);
        Assert.Equal(6.3, Placement(result));
        Assert.Equal(new string?[] { null, null }, _fetcher.SentETags);
    }

    private sealed class Always304 : IConditionalFetcher
    {
        public Task<FetchedText> FetchAsync(string url, string? ifNoneMatch, CancellationToken cancellationToken) =>
            Task.FromResult(FetchedText.NotModified);
    }

    /// <summary>
    /// Stands in for <see cref="HttpStatsFetcher"/>. Fails like HttpClient does: through a faulted
    /// task, with HttpRequestException for an HTTP error and TaskCanceledException for a timeout.
    /// Answers 304 only to the ETag of what it last served, as the CDN does. Misuse (a non-Firestone
    /// URL, an unexpected download) cannot be thrown from here, since the cache turns every exception
    /// into a failed download: it is recorded and checked on Dispose.
    /// </summary>
    private sealed class FakeFetcher : IConditionalFetcher
    {
        private const string NoETag = "\u0000no-etag:";
        private readonly Queue<Func<string>> _responses = new();
        private int _served;
        public List<string> Urls { get; } = new();
        public List<string?> SentETags { get; } = new();
        public List<string> Violations { get; } = new();

        /// <summary>The server's current version: the ETag it answers 304 to.</summary>
        public string CurrentETag => $"W/\"v{_served}\"";

        public void Enqueue(string body) => _responses.Enqueue(() => body);
        public void EnqueueWithoutETag(string body) => _responses.Enqueue(() => NoETag + body);
        public void EnqueueFailure(Exception e) => _responses.Enqueue(() => throw e);

        /// <summary>The server has nothing new: a conditional request with the current ETag gets 304.</summary>
        public void EnqueueUnchanged() => _responses.Enqueue(() => null!);

        public Task<FetchedText> FetchAsync(string url, string? ifNoneMatch, CancellationToken cancellationToken)
        {
            Urls.Add(url);
            SentETags.Add(ifNoneMatch);
            if (!url.StartsWith("https://static.zerotoheroes.com/api/bgs/hero-stats/mmr-", StringComparison.Ordinal)
                || !url.EndsWith("/overview-from-hourly.gz.json", StringComparison.Ordinal))
            {
                Violations.Add($"not a Firestone hero-stats URL: {url}");
            }

            if (_responses.Count == 0)
            {
                Violations.Add($"unexpected download #{Urls.Count} of {url}");
                return Task.FromException<FetchedText>(new InvalidOperationException("no response queued"));
            }

            try
            {
                var body = _responses.Dequeue()();
                if (body == null)
                {
                    if (ifNoneMatch != CurrentETag)
                    {
                        Violations.Add($"unchanged queued, but the request carried ETag {ifNoneMatch ?? "none"}, not {CurrentETag}");
                    }

                    return Task.FromResult(FetchedText.NotModified);
                }

                _served++;
                return Task.FromResult(body.StartsWith(NoETag, StringComparison.Ordinal)
                    ? FetchedText.Changed(body.Substring(NoETag.Length), etag: null)
                    : FetchedText.Changed(body, CurrentETag));
            }
            catch (Exception e)
            {
                return Task.FromException<FetchedText>(e);
            }
        }
    }
}
