using System.Net.Http;

namespace BronzebeardHud.Stats.Tests;

public sealed class CompositionCacheTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 26, 21, 0, 0, TimeSpan.Zero);
    private const string ExpectedUrl = "https://static.zerotoheroes.com/api/bgs/comp-stats/past-seven/overview-from-hourly.gz.json";

    private readonly string _directory = Directory.CreateTempSubdirectory("bbhud-comps-").FullName;
    private readonly List<string> _urls = new();
    private readonly List<string> _violations = new();
    private readonly Queue<Func<string>> _responses = new();
    private DateTimeOffset _now = T0;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        Assert.Empty(_violations);
    }

    private Task<CompositionCacheResult> Get() =>
        new StatsCache(_directory, new Fetcher(this), () => _now)
            .GetCompositionsAsync("past-seven", RefreshPolicy.CompStats, CancellationToken.None);

    /// <summary>A payload with one archetype whose only key piece is <paramref name="keyPiece"/> (20 boards).</summary>
    private static string Payload(string keyPiece)
    {
        var boards = string.Join(",", Enumerable.Range(0, 20).Select(i =>
            $"{{\"mmr\":7000,\"finalComp\":{{\"turn\":11,\"board\":[{{\"cardID\":\"{keyPiece}\"}},{{\"cardID\":\"TOKEN_{i}\"}}]}}}}"));
        return "{\"lastUpdateDate\":\"2026-09-26T14:10:43Z\",\"timePeriod\":\"past-seven\",\"compStats\":[{\"archetype\":\"beast_lobster\"," +
               $"\"dataPoints\":512,\"averagePlacement\":4.1,\"heroStats\":[{{\"heroCardId\":\"BG22_HERO_004\",\"finalBoards\":[{boards}]}}]}}]}}";
    }

    private static string KeyPiece(CompositionCacheResult result) => Assert.Single(result.File!.Compositions).CoreCards.Single();

    [Fact]
    public async Task RefreshesOnlyOnceTheCacheIsAWeekOld_OverThreeWeeks()
    {
        _responses.Enqueue(() => Payload("BG26_300"));
        var first = await Get();
        Assert.True(first.Downloaded);
        Assert.Equal("BG26_300", KeyPiece(first));

        _now = T0.AddDays(7).AddMinutes(-1);
        var cached = await Get();
        Assert.False(cached.Downloaded);
        Assert.Equal("BG26_300", KeyPiece(cached));
        Assert.Single(_urls);

        _now = T0.AddDays(7);
        _responses.Enqueue(() => Payload("BG31_808"));
        Assert.Equal("BG31_808", KeyPiece(await Get()));

        _now = T0.AddDays(14);
        _responses.Enqueue(() => throw new HttpRequestException("Response status code does not indicate success: 502"));
        var failed = await Get();
        Assert.Equal("BG31_808", KeyPiece(failed));
        Assert.Contains("502", failed.Error);
        Assert.Equal(new[] { ExpectedUrl, ExpectedUrl, ExpectedUrl }, _urls);
    }

    private sealed class Fetcher : IStatsFetcher
    {
        private readonly CompositionCacheTests _test;
        public Fetcher(CompositionCacheTests test) => _test = test;

        public Task<string> FetchAsync(string url, CancellationToken cancellationToken)
        {
            _test._urls.Add(url);
            if (url != ExpectedUrl)
            {
                _test._violations.Add($"unexpected URL {url}");
            }

            if (_test._responses.Count == 0)
            {
                _test._violations.Add($"unexpected download #{_test._urls.Count}");
                return Task.FromException<string>(new InvalidOperationException("no response queued"));
            }

            try
            {
                return Task.FromResult(_test._responses.Dequeue()());
            }
            catch (Exception e)
            {
                return Task.FromException<string>(e);
            }
        }
    }
}
