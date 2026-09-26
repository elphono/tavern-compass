namespace BronzebeardHud.Stats.Tests;

public class CompositionRefreshTests
{
    private static CompositionFile FileOf(params string[] ids) => new(StatsSources.Firestone,
        ids.Select(id => new Composition(id, id, Array.Empty<string>(), new[] { "BG25_010" }, Array.Empty<string>())).ToList());

    private sealed class Loads
    {
        public readonly List<TaskCompletionSource<CompositionCacheResult>> Started = new();

        public Task<CompositionCacheResult> Next()
        {
            var load = new TaskCompletionSource<CompositionCacheResult>();
            Started.Add(load);
            return load.Task;
        }
    }

    [Fact]
    public void NeverRequested_TheFirstPollStartsTheLoadByItself_AsAfterAPluginReloadMidGame()
    {
        var loads = new Loads();
        var refresh = new CompositionRefresh(loads.Next);
        Assert.Equal("not started", refresh.State);

        Assert.False(refresh.Poll());
        Assert.Single(loads.Started);
        Assert.Equal("loading", refresh.State);

        loads.Started[0].SetResult(new CompositionCacheResult(FileOf("undead_butcher", "beast_lobster"), downloaded: false, error: null));
        Assert.True(refresh.Poll());
        Assert.Equal("ok", refresh.State);
        Assert.Equal(2, refresh.Last!.File!.Compositions.Count);

        // Once loaded, polling again does not reload: only a new game does.
        Assert.False(refresh.Poll());
        Assert.False(refresh.Poll());
        Assert.Single(loads.Started);
    }

    [Fact]
    public void ThreeGames_OneLoadEach_AFailureKeepsTheLastFileAndSaysWhy()
    {
        var loads = new Loads();
        var refresh = new CompositionRefresh(loads.Next);

        refresh.Request();
        refresh.Request(); // already running: no second load
        loads.Started[0].SetResult(new CompositionCacheResult(FileOf("undead_butcher"), downloaded: true, error: null));
        Assert.True(refresh.Poll());
        Assert.Equal(new[] { "undead_butcher" }, refresh.Last!.File!.Compositions.Select(c => c.Id));

        refresh.Request();
        loads.Started[1].SetException(new IOException("disk full"));
        Assert.True(refresh.Poll());
        Assert.Equal("loading failed: disk full", refresh.State);
        Assert.Equal(new[] { "undead_butcher" }, refresh.Last!.File!.Compositions.Select(c => c.Id));

        refresh.Request();
        loads.Started[2].SetResult(new CompositionCacheResult(null, downloaded: false, error: "cache: schema 1 ≠ 2, redownload failed: HTTP 503"));
        Assert.True(refresh.Poll());
        Assert.Equal("cache: schema 1 ≠ 2, redownload failed: HTTP 503", refresh.State);
        Assert.Equal(3, loads.Started.Count);
    }

    [Fact]
    public void ALoaderThatThrowsAtOnce_IsReportedLikeAFailedLoad()
    {
        var refresh = new CompositionRefresh(() => throw new InvalidOperationException("no cache directory"));
        Assert.True(refresh.Poll());
        Assert.Equal("loading failed: no cache directory", refresh.State);
        Assert.Null(refresh.Last!.File);
    }
}
