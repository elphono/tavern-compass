namespace BronzebeardHud.Stats.Tests;

/// <summary>card-stats follow the chosen MMR bracket: a file is only ever shown for the bracket it was loaded for.</summary>
public class CardStatsRefreshTests
{
    private readonly Dictionary<int, TaskCompletionSource<(CardStatsFile?, bool, string?, bool)>> _pending = new();
    private readonly List<int> _asked = new();

    private CardStatsRefresh NewRefresh() => new(bracket =>
    {
        _asked.Add(bracket);
        var load = new TaskCompletionSource<(CardStatsFile?, bool, string?, bool)>();
        _pending[bracket] = load;
        return load.Task;
    });

    private static CardStatsFile File(int bracket) =>
        new(new StatProvenance(StatsSources.Firestone, null, null, new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero), "last-patch", bracket, null),
            new[] { new CardStat("BG_TEST_A", new[] { new CardTurnStat(6, 300, 3.0 + bracket / 100.0) }) });

    private void Finish(int bracket) => _pending[bracket].SetResult((File(bracket), true, null, false));

    [Fact]
    public void LoadsTheChosenBracket()
    {
        var refresh = NewRefresh();
        refresh.SetBracket(25);
        Assert.False(refresh.Poll());

        Finish(25);

        Assert.True(refresh.Poll());
        Assert.Equal(25, refresh.File!.Provenance.MmrPercentile);
        Assert.Equal("Bronzebeard HUD: data card-stats mmr-25 last-patch: downloaded, fetched 2026-10-08 09:00 UTC", refresh.LastLine);
        Assert.Equal(new[] { 25 }, _asked);
    }

    [Fact]
    public void ABracketLeftWhileLoading_IsNeverShown()
    {
        var refresh = NewRefresh();
        refresh.SetBracket(25);
        refresh.Poll();
        refresh.SetBracket(10);
        refresh.Poll();

        Finish(25);
        refresh.Poll();
        Assert.Null(refresh.File);

        Finish(10);
        Assert.True(refresh.Poll());
        Assert.Equal(10, refresh.File!.Provenance.MmrPercentile);
    }

    [Fact]
    public void BackAndForth_OverThreeSwitches_EachBracketLoadsOnce_AndShowsItsOwnFile()
    {
        var refresh = NewRefresh();
        var versions = new List<int>();
        foreach (var bracket in new[] { 25, 10, 25, 10 })
        {
            refresh.SetBracket(bracket);
            refresh.Poll();
            if (_pending.TryGetValue(bracket, out var load) && !load.Task.IsCompleted)
            {
                Finish(bracket);
                refresh.Poll();
            }

            Assert.Equal(bracket, refresh.File!.Provenance.MmrPercentile);
            versions.Add(refresh.Version);
        }

        Assert.Equal(new[] { 25, 10 }, _asked);
        Assert.Equal(versions.Distinct().Count(), versions.Count);
    }
}
