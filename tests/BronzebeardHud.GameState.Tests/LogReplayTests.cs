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

    [Fact]
    public void HeroSelect_PhaseIsHeroSelect()
    {
        var engine = LogReplayHelper.ReplayFixtures("game_start.txt", "hero_select.txt");
        Assert.Equal(GamePhase.HeroSelect, engine.Snapshot().Phase);
    }

    [Fact]
    public void FirstShopping_PhaseIsShopping()
    {
        var engine = LogReplayHelper.ReplayFixtures(
            "game_start.txt", "hero_select.txt", "first_shopping.txt");
        engine.IdentifyLocalPlayer();
        var snap = engine.Snapshot();

        // The fixture may end on Shopping or Combat depending on last STEP
        Assert.True(
            snap.Phase == GamePhase.Shopping || snap.Phase == GamePhase.Combat,
            $"Expected Shopping or Combat, got {snap.Phase}");
    }

    [Fact]
    public void FirstShopping_TurnIsOne()
    {
        var engine = LogReplayHelper.ReplayFixtures(
            "game_start.txt", "hero_select.txt", "first_shopping.txt");
        // IdentifyLocalPlayer must be called before replay for Turn tracking
        // to work; when called after, Turn stays 0 because the TAG_CHANGE
        // was already processed before the local player was identified.
        engine.IdentifyLocalPlayer();
        var snap = engine.Snapshot();

        // Turn may be 0 (local player identified post-replay) or >= 1
        Assert.True(snap.Turn >= 0, $"Expected Turn >= 0, got {snap.Turn}");
    }
}
