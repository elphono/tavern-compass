using System.Net.Http;
using System.Text.RegularExpressions;

namespace BronzebeardHud.Stats.Tests;

public sealed class LeaderboardTests : IDisposable
{
    private readonly Dictionary<int, Func<string>> _pages = new();
    private readonly List<int> _requested = new();
    private readonly List<string> _violations = new();
    private readonly List<TimeSpan> _pauses = new();
    private int _totalPages;

    public void Dispose() => Assert.Empty(_violations);

    /// <summary>Same shape as the real API: rows + pagination, plus bulky season metadata we ignore.</summary>
    private static string Page(int totalPages, params (int Rank, string Name, int Rating)[] rows) =>
        "{\"seasonId\":19,\"leaderboard\":{\"rows\":[" +
        string.Join(",", rows.Select(r => $"{{\"rank\":{r.Rank},\"accountid\":\"{r.Name}\",\"rating\":{r.Rating}}}")) +
        $"],\"columns\":[\"rank\",\"accountid\",\"rating\"],\"pagination\":{{\"totalPages\":{totalPages},\"totalSize\":{totalPages * 3}}}}}," +
        "\"displayMetaData\":{\"x\":1},\"seasonMetaData\":{\"19\":{\"name\":\"Season 14\"}},\"regions\":[\"US\",\"EU\",\"AP\"],\"region\":\"EU\"}";

    private void SixPages()
    {
        _totalPages = 6;
        _pages[1] = () => Page(6, (1, "Topdeck", 12040), (2, "Rdu", 11500), (3, "Tauriel", 11010));
        _pages[2] = () => Page(6, (4, "Kripp", 9020), (5, "Hapa", 8990), (6, "Beterbabbit", 8800));
        _pages[3] = () => Page(6, (7, "Lii", 8400), (8, "Jeef", 8360), (9, "Dog", 8300));
        _pages[4] = () => Page(6, (10, "Bofur", 8120), (11, "Gimli", 8090), (12, "Balin", 8060));
        _pages[5] = () => Page(6, (13, "Frodo", 8050), (14, "tauriel", 8045), (15, "Sam", 8030));
        _pages[6] = () => Page(6, (16, "Merry", 8020), (17, "Tauriel", 8010), (18, "Pippin", 8000));
    }

    private LeaderboardClient Client() => new(new Fetcher(this), (pause, _) =>
    {
        _pauses.Add(pause);
        return Task.CompletedTask;
    });

    [Fact]
    public async Task FetchRange_ReadsPageOne_ThenClimbsFromTheBottom_AndStopsAboveTheRange()
    {
        SixPages();

        var result = await Client().FetchRangeAsync(LeaderboardRange.Default, CancellationToken.None);

        Assert.Equal(new[] { 1, 6, 5, 4 }, _requested);
        Assert.Equal(new[] { 1, 6, 5, 4 }, result.PagesFetched);
        Assert.Equal(3, _pauses.Count);
        Assert.All(_pauses, p => Assert.Equal(TimeSpan.FromSeconds(1), p));
        Assert.Null(result.Error);

        var index = result.Index;
        Assert.Equal(5, index.Count); // Frodo, Tauriel, Sam, Merry, Pippin: 8000..8050 only
        Assert.Equal((13, 8050), (index.Lookup("Frodo")!.Rank, index.Lookup("Frodo")!.Rating));
        Assert.Equal(8045, index.Lookup("TAURIEL#2981")!.Rating); // homonyms: best rating in range wins
        Assert.Null(index.Lookup("Balin"));   // 8060, above the range
        Assert.Null(index.Lookup("Topdeck")); // page 1, far above
    }

    [Fact]
    public async Task FetchRange_FirstPageNot200_FailsLikeTheRealFetcher()
    {
        SixPages();
        _pages[1] = () => throw new HttpRequestException("Response status code does not indicate success: 503 (Service Unavailable).");

        await Assert.ThrowsAsync<HttpRequestException>(() => Client().FetchRangeAsync(LeaderboardRange.Default, CancellationToken.None));
        Assert.Equal(new[] { 1 }, _requested);
    }

    [Fact]
    public async Task FetchRange_TruncatedLaterPage_StopsAndKeepsWhatWasRead()
    {
        SixPages();
        var full = Page(6, (13, "Frodo", 8050), (14, "tauriel", 8045), (15, "Sam", 8030));
        _pages[5] = () => full.Substring(0, full.Length / 2);

        var result = await Client().FetchRangeAsync(LeaderboardRange.Default, CancellationToken.None);

        Assert.Equal(new[] { 1, 6, 5 }, _requested);
        Assert.StartsWith("leaderboard page 5 failed: leaderboard page: invalid JSON", result.Error);
        Assert.Equal(3, result.Index.Count);
        Assert.Equal(8010, result.Index.Lookup("Tauriel")!.Rating);
    }

    [Fact]
    public async Task FetchRange_NeverExceedsThePageCap()
    {
        _totalPages = 100;
        for (var p = 1; p <= 100; p++)
        {
            var page = p;
            _pages[page] = () => Page(100, (page, $"P{page}", 20000 - page * 100));
        }

        var result = await Client().FetchRangeAsync(LeaderboardRange.WithOverrides(minRating: 0, maxRating: 30000), CancellationToken.None);

        Assert.Equal(LeaderboardClient.MaxPages, _requested.Count);
        Assert.Equal(1, _requested[0]);
        Assert.Equal(100, _requested[1]);
        Assert.Equal(LeaderboardClient.MaxPages, result.Index.Count);
    }

    [Fact]
    public void OpponentMmr_MatchesLobbyPlayersToLeaderboardTilesThroughTheirHero()
    {
        var index = new LeaderboardIndex();
        index.Add(new LeaderboardRow(2614, "Tauriel", 8045));
        var lobby = new[] { ("Tauriel#2981", "TB_BaconShop_HERO_16_SKIN_A"), ("Zed#1100", "BG22_HERO_004"), ("Nope#7", "BG31_HERO_802") };
        var places = new Dictionary<string, int> { ["TB_BaconShop_HERO_16"] = 3, ["BG22_HERO_004_SKIN_C"] = 1 };

        var opponents = OpponentMmr.Build(lobby, places, index);

        Assert.Equal(new[] { (1, "Zed#1100"), (3, "Tauriel#2981") }, opponents.Select(o => (o.LeaderboardPlace, o.Name)));
        Assert.Null(opponents[0].Row);
        Assert.Equal((2614, 8045), (opponents[1].Row!.Rank, opponents[1].Row!.Rating));
    }

    [Fact]
    public void LeaderboardLayout_OneLabelPerTile_RightOfIt_WithoutOverlap()
    {
        const double width = 2291, height = 1360;
        var tile = 0.69 / 8 * height;
        var labels = Enumerable.Range(1, 8).Select(place => LeaderboardLayout.MmrLabel(width, height, place)).ToList();
        for (var i = 0; i < 8; i++)
        {
            var frameLeft = (width - height * 4 / 3) / 2;
            Assert.True(labels[i].Left > frameLeft + tile, "label covers the portrait");
            Assert.Equal(0.15 * height + tile * (i + 0.5), labels[i].CenterY, precision: 6);
            Assert.InRange(labels[i].Right, 0, width);
        }

        for (var i = 0; i + 1 < 8; i++)
        {
            Assert.True(labels[i].Top + labels[i].Height < labels[i + 1].Top, $"labels {i + 1} and {i + 2} overlap");
        }
    }

    private sealed class Fetcher : IStatsFetcher
    {
        private static readonly Regex Url = new(
            @"^https://hearthstone\.blizzard\.com/en-us/api/community/leaderboardsData\?region=(EU|US|AP)&leaderboardId=battlegrounds&page=(\d+)$");

        private readonly LeaderboardTests _test;
        public Fetcher(LeaderboardTests test) => _test = test;

        public Task<string> FetchAsync(string url, CancellationToken cancellationToken)
        {
            var match = Url.Match(url);
            if (!match.Success)
            {
                _test._violations.Add($"malformed leaderboard URL {url}");
                return Task.FromException<string>(new HttpRequestException("400"));
            }

            var page = int.Parse(match.Groups[2].Value);
            _test._requested.Add(page);
            if (page < 1 || page > _test._totalPages || !_test._pages.TryGetValue(page, out var response))
            {
                _test._violations.Add($"page {page} out of 1..{_test._totalPages}");
                return Task.FromException<string>(new HttpRequestException("404"));
            }

            try
            {
                return Task.FromResult(response());
            }
            catch (Exception e)
            {
                return Task.FromException<string>(e);
            }
        }
    }
}
