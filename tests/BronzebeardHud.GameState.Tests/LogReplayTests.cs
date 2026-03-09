namespace BronzebeardHud.GameState.Tests;

public class LogReplayTests
{
    [Fact]
    public void GameStart_CreatesGameEntity()
    {
        var engine = LogReplayHelper.ReplayFixture("game_start.txt");
        var snap = engine.Snapshot();
        Assert.Equal(4u, snap.GameEntityId);
    }

    [Fact]
    public void GameStart_RegistersTwoPlayers()
    {
        var engine = LogReplayHelper.ReplayFixture("game_start.txt");
        engine.IdentifyLocalPlayer();
        var snap = engine.Snapshot();
        Assert.Equal(2u, snap.Player.PlayerId);
        Assert.Equal("Player#1234", snap.Player.Name);
    }

    [Fact]
    public void GameStart_PhaseIsNotStarted()
    {
        var engine = LogReplayHelper.ReplayFixture("game_start.txt");
        Assert.Equal(GamePhase.NotStarted, engine.Snapshot().Phase);
    }
}
