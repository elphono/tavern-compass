namespace BronzebeardHud.GameState.Tests;

public class OpponentTrackingTests
{
    [Fact]
    public void OpponentsAppear_SixOpponentsDetectedInSetaside()
    {
        // In the fixture data, 7 heroes have PLAYER_LEADERBOARD_PLACE set.
        // The local player's hero (Faelin, id=86) is in zone=PLAY, so 6
        // opponents remain in SETASIDE.  One opponent hero is not present
        // in the fixture because it was created before the captured window.
        var engine = LogReplayHelper.ReplayFixtures(
            "game_start.txt", "hero_select.txt", "opponents_appear.txt");
        engine.IdentifyLocalPlayer();
        var snap = engine.Snapshot();
        Assert.True(snap.Opponents.Count >= 6,
            $"Expected at least 6 opponents, got {snap.Opponents.Count}");
    }

    [Fact]
    public void OpponentsAppear_OpponentsHaveHeroCardIds()
    {
        var engine = LogReplayHelper.ReplayFixtures(
            "game_start.txt", "hero_select.txt", "opponents_appear.txt");
        engine.IdentifyLocalPlayer();
        var snap = engine.Snapshot();
        foreach (var opp in snap.Opponents)
            Assert.False(string.IsNullOrEmpty(opp.HeroCardId),
                $"Opponent EntityId={opp.EntityId} has empty HeroCardId");
    }

    [Fact]
    public void OpponentsAppear_FullEntityOpponentsHaveHealth()
    {
        // Only opponents created via FULL_ENTITY have HEALTH tags.
        // Opponents known only from BracketRef TAG_CHANGE won't have
        // health data until a FULL_ENTITY or SHOW_ENTITY reveals them.
        var engine = LogReplayHelper.ReplayFixtures(
            "game_start.txt", "hero_select.txt", "opponents_appear.txt");
        engine.IdentifyLocalPlayer();
        var snap = engine.Snapshot();
        var withHealth = snap.Opponents.Where(o => o.Health > 0).ToList();
        Assert.True(withHealth.Count >= 1,
            "Expected at least 1 opponent with known health");
    }

    [Fact]
    public void OpponentsAppear_LocalPlayerNotInOpponents()
    {
        var engine = LogReplayHelper.ReplayFixtures(
            "game_start.txt", "hero_select.txt", "opponents_appear.txt");
        engine.IdentifyLocalPlayer();
        var snap = engine.Snapshot();
        Assert.DoesNotContain(snap.Opponents,
            o => o.HeroCardId == "BG22_HERO_201_SKIN_F");
    }

    [Fact]
    public void BuildOpponentStates_FindsHeroesInSetaside()
    {
        var engine = new GameStateEngine();
        var lines = new LogLine[]
        {
            Line(0, new RawPacket.CreateGame()),
            Line(1, new RawPacket.GameEntity(1)),
            Tag(2, "CARDTYPE", "GAME"),
            Line(1, new RawPacket.PlayerEntity(2, 1)),
            Tag(2, "CONTROLLER", "1"),
            Tag(2, "CARDTYPE", "PLAYER"),
            Tag(2, "HERO_ENTITY", "10"),
            Tag(2, "PLAYER_TECH_LEVEL", "1"),
            Line(1, new RawPacket.PlayerEntity(3, 2)),
            Tag(2, "CONTROLLER", "2"),
            Tag(2, "CARDTYPE", "PLAYER"),
            Tag(2, "BACON_DUMMY_PLAYER", "1"),
            Line(0, new RawPacket.FullEntityCreate(10, "HERO_LOCAL")),
            Tag(1, "CONTROLLER", "1"),
            Tag(1, "CARDTYPE", "HERO"),
            Tag(1, "HEALTH", "30"),
            Tag(1, "ZONE", "PLAY"),
            Line(0, new RawPacket.FullEntityCreate(20, "HERO_OPP_1")),
            Tag(1, "CONTROLLER", "2"),
            Tag(1, "CARDTYPE", "HERO"),
            Tag(1, "HEALTH", "30"),
            Tag(1, "ZONE", "SETASIDE"),
            Tag(1, "PLAYER_LEADERBOARD_PLACE", "2"),
            Line(0, new RawPacket.FullEntityCreate(21, "HERO_OPP_2")),
            Tag(1, "CONTROLLER", "2"),
            Tag(1, "CARDTYPE", "HERO"),
            Tag(1, "HEALTH", "28"),
            Tag(1, "ZONE", "SETASIDE"),
            Tag(1, "PLAYER_LEADERBOARD_PLACE", "3"),
        };
        foreach (var l in lines) engine.Process(l);
        engine.IdentifyLocalPlayer();

        var snap = engine.Snapshot();
        Assert.Equal(2, snap.Opponents.Count);
        Assert.Contains(snap.Opponents, o => o.HeroCardId == "HERO_OPP_1");
        Assert.Contains(snap.Opponents, o => o.HeroCardId == "HERO_OPP_2");
        Assert.Equal(30, snap.Opponents.First(o => o.HeroCardId == "HERO_OPP_1").Health);
        Assert.Equal(28, snap.Opponents.First(o => o.HeroCardId == "HERO_OPP_2").Health);
    }

    private static LogLine Line(uint indent, RawPacket packet) => new()
    {
        Timestamp = "00:00:00.0000000", Indent = indent, IsGameState = true, Packet = packet,
    };

    private static LogLine Tag(uint indent, string tag, string value) =>
        Line(indent, new RawPacket.TagValue(tag, value));
}
