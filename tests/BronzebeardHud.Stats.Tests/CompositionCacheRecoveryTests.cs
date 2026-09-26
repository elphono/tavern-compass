using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// A cache written by an older plugin, or damaged, must be downloaded again rather than served as is;
/// and when that download fails, the error must say why, never leave an unexplained zero.
/// </summary>
public sealed class CompositionCacheRecoveryTests : IDisposable
{
    // The exact shape of Ali's cache at the time of the report (written at 19:58 CEST by the plugin of
    // 3572dc0, before compositions carried the order of the final board; tavern line logged at 20:31:15
    // CEST): same keys, schema 1, 24 compositions, no referenceBoard, same dates. Names, card ids and
    // numbers are replaced, since the repository is public and takes no real Firestone data (CLAUDE.md).
    private static readonly string AliCache1958 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "comp-cache-schema1-shape-2026-09-26-1958.json");
    private static readonly DateTimeOffset At2031 = new(2026, 9, 26, 18, 31, 15, TimeSpan.Zero);
    private const string Url = "https://static.zerotoheroes.com/api/bgs/comp-stats/last-patch/overview-from-hourly.gz.json";
    private const string Http503 = "Response status code does not indicate success: 503 (Service Unavailable).";

    private readonly string _directory = Directory.CreateTempSubdirectory("bbhud-recovery-").FullName;
    private readonly List<string> _urls = new();
    private readonly List<string> _violations = new();
    private readonly Queue<Func<string>> _responses = new();
    private DateTimeOffset _now = At2031;

    // StatsCache turns any fetch exception into a download error: an assertion thrown inside the fake
    // would be swallowed the same way, hence violations recorded here and asserted at the end.
    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        Assert.Empty(_violations);
    }

    private string CachePath => Path.Combine(_directory, "firestone-comp-stats-last-patch.json");

    private Task<CompositionCacheResult> Get() =>
        new StatsCache(_directory, new Fetcher(this), () => _now)
            .GetCompositionsAsync("last-patch", RefreshPolicy.CompStats, CancellationToken.None);

    /// <summary>One composition whose final boards list their minions out of order: only ZONE_POSITION gives the order.</summary>
    private static string Payload(string keyPiece)
    {
        string Minion(string id, int position) => $"{{\"cardID\":\"{id}\",\"tags\":{{\"ZONE_POSITION\":{position}}}}}";
        var boards = string.Join(",", Enumerable.Range(0, 20).Select(i =>
            $"{{\"mmr\":7000,\"finalComp\":{{\"board\":[{Minion($"TOKEN_{i}", 2)},{Minion(keyPiece, 1)}]}}}}"));
        return "{\"lastUpdateDate\":\"2026-09-26T14:10:43Z\",\"timePeriod\":\"last-patch\",\"compStats\":[{\"archetype\":\"beast_lobster\"," +
               $"\"dataPoints\":512,\"averagePlacement\":4.1,\"heroStats\":[{{\"heroCardId\":\"BG22_HERO_004\",\"finalBoards\":[{boards}]}}]}}]}}";
    }

    private int SchemaOnDisk() => JObject.Parse(File.ReadAllText(CachePath)).Value<int>("schema");

    [Fact]
    public async Task AliCacheOf1958_LoadsButHasNoBoardOrder_SoItIsDownloadedAgainAtOnce()
    {
        // What the file holds: 24 compositions, schema 1, not one final-board order.
        var old = JObject.Parse(File.ReadAllText(AliCache1958));
        Assert.Equal(1, old.Value<int>("schema"));
        Assert.Equal(24, old["compositions"]!.Count());
        Assert.All(old["compositions"]!, c => Assert.Null(c["referenceBoard"]));
        File.Copy(AliCache1958, CachePath);
        _responses.Enqueue(() => Payload("BG26_300"));

        var result = await Get();

        Assert.True(result.Downloaded, "a cache from an older format must not be served for a week");
        Assert.Null(result.Error);
        var comp = Assert.Single(result.File!.Compositions);
        Assert.Equal(new[] { "BG26_300" }, comp.ReferenceBoard!.Take(1));
        Assert.Equal(new[] { Url }, _urls);
        Assert.Equal(CompositionFile.CurrentSchema, SchemaOnDisk());
    }

    [Fact]
    public async Task OlderSchema_DownloadFails_ThenWaits_ThenRecovers_TheErrorSaysWhyEachTime()
    {
        File.Copy(AliCache1958, CachePath);
        var older = $"cache: schema 1 ≠ {CompositionFile.CurrentSchema}";

        _responses.Enqueue(() => throw new HttpRequestException(Http503));
        var failed = await Get();
        Assert.Null(failed.File);
        Assert.Equal($"{older}, redownload failed: {Http503}", failed.Error);

        _now = At2031.AddMinutes(30);
        var waiting = await Get();
        Assert.Null(waiting.File);
        Assert.Equal($"{older}, last download failed at 2026-09-26 18:31 UTC; next attempt after 2026-09-26 19:31 UTC", waiting.Error);

        _now = At2031.AddHours(1);
        _responses.Enqueue(() => Payload("BG31_808"));
        var recovered = await Get();
        Assert.True(recovered.Downloaded);
        Assert.Null(recovered.Error);
        Assert.Equal("BG31_808", Assert.Single(recovered.File!.Compositions).CoreCards.Single());
        Assert.Equal(CompositionFile.CurrentSchema, SchemaOnDisk());
        Assert.Equal(2, _urls.Count);
    }

    [Theory]
    [InlineData("{ \"schema\": 2, \"source\": \"firestone\", \"compositions\": [", "cache: invalid JSON")]
    [InlineData("", "cache: invalid JSON")]
    [InlineData("{ \"schema\": \"two\", \"source\": \"firestone\", \"compositions\": [] }", "cache: schema \"two\" ≠ 3")]
    public async Task UnreadableCache_IsDownloadedAgain_AndAFailureNamesTheCacheProblem(string content, string problem)
    {
        File.WriteAllText(CachePath, content);
        _responses.Enqueue(() => throw new HttpRequestException(Http503));
        var failed = await Get();
        Assert.Null(failed.File);
        Assert.StartsWith(problem, failed.Error);
        Assert.EndsWith($"redownload failed: {Http503}", failed.Error);

        _now = At2031.AddHours(1);
        _responses.Enqueue(() => Payload("BG26_300"));
        var recovered = await Get();
        Assert.True(recovered.Downloaded);
        Assert.Equal(CompositionFile.CurrentSchema, SchemaOnDisk());
    }

    private sealed class Fetcher : IStatsFetcher
    {
        private readonly CompositionCacheRecoveryTests _test;
        public Fetcher(CompositionCacheRecoveryTests test) => _test = test;

        public Task<string> FetchAsync(string url, CancellationToken cancellationToken)
        {
            _test._urls.Add(url);
            if (url != Url)
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
