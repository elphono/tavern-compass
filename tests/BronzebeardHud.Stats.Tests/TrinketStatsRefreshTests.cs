using TrinketLoad = System.Threading.Tasks.Task<(BronzebeardHud.Stats.TrinketStatsFile? File, bool Downloaded, string? Error, bool Unchanged)>;
using TrinketResult = (BronzebeardHud.Stats.TrinketStatsFile? File, bool Downloaded, string? Error, bool Unchanged);

namespace BronzebeardHud.Stats.Tests;

public sealed class TrinketStatsRefreshTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
    private const string What = "trinket-stats last-patch";
    private const string Trinket = "BG36_MagicItem_400";

    private readonly string _directory = Directory.CreateTempSubdirectory("bbhud-trinkets-").FullName;
    private readonly TrinketFetcher _fetcher = new();
    private DateTimeOffset _now = T0;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        Assert.Empty(_fetcher.Violations);
    }

    /// <summary>A Firestone payload whose only trinket has the given placement, so each download is recognisable.</summary>
    private static string Payload(double placement) =>
        "{\"lastUpdateDate\":\"2026-10-02T06:00:00.000Z\",\"timePeriod\":\"last-patch\",\"trinketStats\":[" +
        $"{{\"trinketCardId\":\"{Trinket}\",\"dataPoints\":35577,\"pickRate\":0.5,\"averagePlacement\":{placement.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}}";

    private static double? Placement(TrinketStatsRefresh refresh) => refresh.File?.Find(Trinket)?.AveragePlacement;

    /// <summary>
    /// The plugin's wiring: one real <see cref="StatsCache"/> for the whole HDT session, loaded with the same
    /// policy as ChoiceAdvicePanel. <see cref="Settle"/> polls as the plugin does, waits for the load the
    /// refresh started, then polls again: true when a load finished, whether it ended at once or later.
    /// </summary>
    private sealed class Session
    {
        private TrinketLoad? _last;

        public Session(StatsCache cache) =>
            Refresh = new TrinketStatsRefresh(What, () => _last = cache.GetTrinketStatsAsync("last-patch", RefreshPolicy.HeroStats, CancellationToken.None));

        public TrinketStatsRefresh Refresh { get; }

        public async Task<bool> Settle()
        {
            var finished = Refresh.Poll();
            if (_last != null)
            {
                await _last;
            }

            return Refresh.Poll() | finished;
        }
    }

    [Fact]
    public async Task ALongHdtSession_AsksTheServerAgainAtTheFirstTrinketChoiceAfterADay_NotBefore()
    {
        var session = new Session(new StatsCache(_directory, _fetcher, () => _now));

        // Plugin start: the first poll loads, and the cache's first use always asks the server.
        _fetcher.Enqueue(Payload(3.5));
        Assert.True(await session.Settle());
        Assert.Equal(3.5, Placement(session.Refresh));
        Assert.Equal(1, _fetcher.Count);
        Assert.Equal($"Bronzebeard HUD: data {What}: downloaded, fetched 2026-10-02 08:00 UTC", session.Refresh.LastLine);

        // A trinket choice within the day: the cache is asked, the server is not.
        _now = T0.AddHours(23).AddMinutes(59);
        session.Refresh.BeginChoice("1:6873,6874,6875");
        Assert.True(await session.Settle());
        Assert.Equal(1, _fetcher.Count);
        Assert.Equal(3.5, Placement(session.Refresh));
        Assert.Equal($"Bronzebeard HUD: data {What}: cached, fetched 2026-10-02 08:00 UTC", session.Refresh.LastLine);

        // The first trinket choice after a day: the server is asked again, and its stats replace the old ones.
        _now = T0.AddHours(25);
        _fetcher.Enqueue(Payload(4.25));
        session.Refresh.BeginChoice("2:7012,7013,7014");
        Assert.True(await session.Settle());
        Assert.Equal(2, _fetcher.Count);
        Assert.Equal(4.25, Placement(session.Refresh));
        Assert.Equal($"Bronzebeard HUD: data {What}: downloaded, fetched 2026-10-03 09:00 UTC", session.Refresh.LastLine);

        // The day now counts from that second download: an hour later, nothing new is asked of the server...
        _now = T0.AddHours(26);
        session.Refresh.BeginChoice("3:7120,7121,7122");
        Assert.True(await session.Settle());
        Assert.Equal(2, _fetcher.Count);
        Assert.Equal(4.25, Placement(session.Refresh));

        // ...and a day after it, the server is asked a third time.
        _now = T0.AddHours(49);
        _fetcher.Enqueue(Payload(2.75));
        session.Refresh.BeginChoice("4:8001,8002,8003");
        Assert.True(await session.Settle());
        Assert.Equal(3, _fetcher.Count);
        Assert.Equal(2.75, Placement(session.Refresh));
        Assert.Equal(5, session.Refresh.Version);
    }

    /// <summary>Hands out loads the test completes by hand, to observe the refresh between start and end.</summary>
    private sealed class Loads
    {
        public readonly List<TaskCompletionSource<TrinketResult>> Started = new();

        public TrinketLoad Next()
        {
            var load = new TaskCompletionSource<TrinketResult>();
            Started.Add(load);
            return load.Task;
        }
    }

    private static TrinketStatsFile FileWith(double placement, DateTimeOffset fetchedAt) =>
        TrinketStatsLoader.ImportFirestone(Payload(placement), "https://example.invalid/trinkets", fetchedAt);

    [Fact]
    public void TheSameChoice_AsksTheCacheOnce_HoweverOftenThePluginPollsIt()
    {
        var loads = new Loads();
        var refresh = new TrinketStatsRefresh(What, loads.Next);

        Assert.False(refresh.Poll());
        Assert.False(refresh.Poll()); // still running: no second load
        Assert.Single(loads.Started);
        loads.Started[0].SetResult((FileWith(3.5, T0), true, null, false));
        Assert.True(refresh.Poll());

        // HDT calls the plugin several times a second while a choice is on screen.
        for (var update = 0; update < 3; update++)
        {
            refresh.BeginChoice("1:6873,6874,6875");
            if (loads.Started.Count == 2)
            {
                loads.Started[1].TrySetResult((FileWith(3.5, T0), false, null, false));
            }

            refresh.Poll();
        }

        Assert.Equal(2, loads.Started.Count);
        Assert.False(refresh.Poll()); // nothing left to report once the choice's load is in
        Assert.False(refresh.Poll());

        refresh.BeginChoice("2:7012,7013,7014");
        Assert.Equal(3, loads.Started.Count);
        Assert.False(refresh.Poll());
        Assert.Equal(3, loads.Started.Count);
    }

    [Fact]
    public void AChoiceOnScreen_KeepsItsStatsWhileReloading_AndWhenAReloadBringsNone()
    {
        var loads = new Loads();
        var refresh = new TrinketStatsRefresh(What, loads.Next);
        Assert.False(refresh.Loaded);
        Assert.Null(refresh.File);

        refresh.Poll();
        loads.Started[0].SetResult((FileWith(3.5, T0), true, null, false));
        Assert.True(refresh.Poll());
        Assert.True(refresh.Loaded);
        Assert.Equal(1, refresh.Version);

        // A reload is running: the stats shown are still the previous ones, and the advice is not rebuilt.
        refresh.BeginChoice("1:6873,6874,6875");
        Assert.True(refresh.IsLoading);
        Assert.False(refresh.Poll());
        Assert.Equal(3.5, Placement(refresh));
        Assert.True(refresh.Loaded);
        Assert.Equal(1, refresh.Version);

        loads.Started[1].SetResult((FileWith(4.25, T0.AddDays(1)), true, null, false));
        Assert.True(refresh.Poll());
        Assert.Equal(4.25, Placement(refresh));
        Assert.Equal(2, refresh.Version);
        Assert.Null(refresh.Error);

        // A reload that brings no file (no usable cache and the download failed): the last stats stay.
        refresh.BeginChoice("2:7012,7013,7014");
        loads.Started[2].SetResult((null, false, "cache: missing, last download failed", false));
        Assert.True(refresh.Poll());
        Assert.Equal(4.25, Placement(refresh));
        Assert.Equal("cache: missing, last download failed", refresh.Error);
        Assert.Equal($"Bronzebeard HUD: data {What}: FAILED, cache: missing, last download failed, fetched 2026-10-03 08:00 UTC", refresh.LastLine);

        // A load that throws: the same, and the error says why.
        refresh.BeginChoice("3:7120,7121,7122");
        loads.Started[3].SetException(new IOException("disk full"));
        Assert.True(refresh.Poll());
        Assert.Equal(4.25, Placement(refresh));
        Assert.Equal("loading failed: disk full", refresh.Error);
        Assert.Equal(4, refresh.Version);

        // The next good load clears the error.
        refresh.BeginChoice("4:8001,8002,8003");
        loads.Started[4].SetResult((FileWith(2.75, T0.AddDays(2)), true, null, false));
        Assert.True(refresh.Poll());
        Assert.Equal(2.75, Placement(refresh));
        Assert.Null(refresh.Error);
    }

    [Fact]
    public void ALoaderThatThrowsAtOnce_IsReportedLikeAFailedLoad()
    {
        var refresh = new TrinketStatsRefresh(What, () => throw new InvalidOperationException("no cache directory"));
        Assert.True(refresh.Poll());
        Assert.True(refresh.Loaded);
        Assert.Null(refresh.File);
        Assert.Equal("loading failed: no cache directory", refresh.Error);
        Assert.Equal($"Bronzebeard HUD: data {What}: FAILED, loading failed: no cache directory, no data", refresh.LastLine);
    }

    /// <summary>
    /// Stands in for <see cref="HttpStatsFetcher"/> on the trinket URL: serves the queued payloads in order,
    /// without ETag. The cache turns every exception into a failed download, so misuse (another URL, a download
    /// nobody queued) is recorded and checked on Dispose instead of thrown.
    /// </summary>
    private sealed class TrinketFetcher : IConditionalFetcher
    {
        private readonly Queue<string> _bodies = new();
        public int Count { get; private set; }
        public List<string> Violations { get; } = new();

        public void Enqueue(string body) => _bodies.Enqueue(body);

        public Task<FetchedText> FetchAsync(string url, string? ifNoneMatch, CancellationToken cancellationToken)
        {
            Count++;
            if (url != FirestoneEndpoints.TrinketStats("last-patch"))
            {
                Violations.Add($"not the trinket-stats URL: {url}");
            }

            if (_bodies.Count == 0)
            {
                Violations.Add($"unexpected download #{Count} of {url}");
                return Task.FromException<FetchedText>(new InvalidOperationException("no response queued"));
            }

            return Task.FromResult(FetchedText.Changed(_bodies.Dequeue(), etag: null));
        }
    }
}
