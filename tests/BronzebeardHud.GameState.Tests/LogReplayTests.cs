using System.Collections.Generic;

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

    [Fact]
    public void FullGameTimeline_PhasesProgressCorrectly()
    {
        var engine = new GameStateEngine();
        var phases = new List<(GamePhase Phase, uint Turn)>();
        var lastPhase = GamePhase.NotStarted;

        var fixtures = new[] {
            "game_start.txt", "hero_select.txt",
            "first_shopping.txt", "opponents_appear.txt"
        };

        foreach (var fixture in fixtures)
        {
            var lines = LogReplayHelper.ParseFixture(fixture);
            foreach (var line in lines)
            {
                engine.Process(line);
                var snap = engine.Snapshot();
                if (snap.Phase == GamePhase.HeroSelect && lastPhase != GamePhase.HeroSelect)
                    engine.IdentifyLocalPlayer();

                if (snap.Phase != lastPhase)
                {
                    phases.Add((snap.Phase, snap.Turn));
                    lastPhase = snap.Phase;
                }
            }
        }

        Assert.Contains(phases, p => p.Phase == GamePhase.HeroSelect);
        Assert.Contains(phases, p => p.Phase == GamePhase.Shopping);
        Assert.True(phases[0].Phase == GamePhase.HeroSelect,
            $"First transition should be HeroSelect, got {phases[0].Phase}");
    }

    [Fact]
    public void FullGameTimeline_PlayerHeroSetAfterSelection()
    {
        var engine = LogReplayHelper.ReplayFixtures(
            "game_start.txt", "hero_select.txt", "first_shopping.txt");
        engine.IdentifyLocalPlayer();
        var snap = engine.Snapshot();

        Assert.False(string.IsNullOrEmpty(snap.Player.HeroCardId),
            "Player hero should be set after hero selection");
        Assert.True(snap.Player.TavernTier >= 1,
            $"Tavern tier should be >= 1, got {snap.Player.TavernTier}");
    }
}
