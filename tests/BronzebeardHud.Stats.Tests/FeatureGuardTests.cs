namespace BronzebeardHud.Stats.Tests;

public class FeatureGuardTests
{
    private readonly List<(string Name, Exception Error)> _log = new();

    private FeatureGuard Guard(string name) => new(name, (n, e) => _log.Add((n, e)));

    [Fact]
    public void AFailingFeatureIsDisabledAlone_TheOtherRunsEveryTick()
    {
        var mmr = Guard("opponent-mmr");
        var heroes = Guard("hero-selection");
        var heroTicks = 0;

        for (var tick = 0; tick < 3; tick++)
        {
            mmr.Run(() => throw new MissingMethodException("HearthMirror.Objects.BattlegroundsLobbyInfo.get_Players"));
            heroes.Run(() => heroTicks++);
        }

        Assert.True(mmr.IsDisabled);
        Assert.False(heroes.IsDisabled);
        Assert.Equal(3, heroTicks);
    }

    [Fact]
    public void TheFailureIsReportedOnce_AndTheActionIsNotCalledAgain()
    {
        var mmr = Guard("opponent-mmr");
        var calls = 0;

        for (var tick = 0; tick < 3; tick++)
        {
            mmr.Run(() =>
            {
                calls++;
                throw new TypeLoadException("HearthMirror.Objects.BattlegroundsLobbyPlayer");
            });
        }

        var entry = Assert.Single(_log);
        Assert.Equal("opponent-mmr", entry.Name);
        Assert.IsType<TypeLoadException>(entry.Error);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void AFeatureThatNeverThrowsIsNeverDisabled()
    {
        var history = Guard("history");
        var ticks = 0;

        for (var tick = 0; tick < 3; tick++)
        {
            history.Run(() => ticks++);
        }

        Assert.False(history.IsDisabled);
        Assert.Equal(3, ticks);
        Assert.Empty(_log);
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(OperationCanceledException))]
    public void TransientFailures_SkipTheTickOnly(Type transient)
    {
        var tavern = Guard("tavern-advice");
        var calls = 0;

        for (var tick = 0; tick < 3; tick++)
        {
            tavern.Run(() =>
            {
                calls++;
                if (calls < 3)
                {
                    throw (Exception)Activator.CreateInstance(transient, "Collection was modified")!;
                }
            });
        }

        Assert.False(tavern.IsDisabled);
        Assert.Equal(3, calls);
        Assert.Empty(_log);
    }
}
