namespace BronzebeardHud.Stats.Tests;

public class GameTimelineTests
{
    private const int Me = 2;

    private static HeroHealth[] Heroes(int me, int p3, int p5, int p7) => new[]
    {
        new HeroHealth(Me, "BG22_HERO_004", me), new HeroHealth(3, "TB_BaconShop_HERO_39", p3),
        new HeroHealth(5, "BG31_HERO_802", p5), new HeroHealth(7, "TB_BaconShop_HERO_16", p7),
    };

    /// <summary>
    /// Three combats: turn 2 a win against player 3 (4 damage dealt), turn 3 a loss against player 5
    /// (7 taken), turn 4 a tie against player 7. Each phase is observed several times, as the plugin
    /// does on every HDT update, and the next opponent changes during combat to check it is not read then.
    /// </summary>
    private static GameTimeline ThreeCombats()
    {
        var t = new GameTimeline();
        for (var i = 0; i < 3; i++) t.Observe(2, inCombat: false, Me, nextOpponentPlayerId: 3, Heroes(30, 30, 30, 30));
        for (var i = 0; i < 3; i++) t.Observe(2, inCombat: true, Me, nextOpponentPlayerId: 5, Heroes(30, 26, 30, 30));
        for (var i = 0; i < 3; i++) t.Observe(3, inCombat: false, Me, nextOpponentPlayerId: 5, Heroes(30, 26, 30, 30));
        for (var i = 0; i < 3; i++) t.Observe(3, inCombat: true, Me, nextOpponentPlayerId: 7, Heroes(23, 26, 30, 30));
        for (var i = 0; i < 3; i++) t.Observe(4, inCombat: false, Me, nextOpponentPlayerId: 7, Heroes(23, 26, 30, 21));
        t.Observe(4, inCombat: true, Me, nextOpponentPlayerId: 3, Heroes(23, 26, 30, 21));
        t.Observe(5, inCombat: false, Me, nextOpponentPlayerId: 3, Heroes(23, 26, 30, 21));
        return t;
    }

    [Fact]
    public void Observe_RecordsOneCombatPerFight_AgainstTheOpponentAnnouncedInTheShop()
    {
        var combats = ThreeCombats().Combats;

        Assert.Equal(new[] { (2, 3, CombatResult.Win), (3, 5, CombatResult.Loss), (4, 7, CombatResult.Tie) },
            combats.Select(c => (c.Turn, c.OpponentPlayerId, c.Result)));
        Assert.Equal((4, 0), (combats[0].DamageDealt, combats[0].DamageTaken));
        Assert.Equal((0, 7), (combats[1].DamageDealt, combats[1].DamageTaken));
        Assert.Equal("BG31_HERO_802", combats[1].OpponentHeroCardId);
    }

    [Fact]
    public void Observe_HealthCurves_OnePointPerShopTurn_LastValueWins()
    {
        var curves = ThreeCombats().HealthByPlayer;

        Assert.Equal(new[] { (2, 30), (3, 30), (4, 23), (5, 23) }, curves[Me]);
        Assert.Equal(new[] { (2, 30), (3, 26), (4, 26), (5, 26) }, curves[3]);
        Assert.Equal(new[] { (2, 30), (3, 30), (4, 21), (5, 21) }, curves[7]);
    }

    [Fact]
    public void Reset_StartsANewGameFromScratch()
    {
        var t = ThreeCombats();
        t.Reset();
        t.Observe(1, inCombat: false, Me, 5, Heroes(30, 30, 30, 30));

        Assert.Empty(t.Combats);
        Assert.Equal(new[] { (1, 30) }, t.HealthByPlayer[Me]);
    }

    [Fact]
    public void HealthChart_ScalesTurnsAndHealth_ClampingOverAndUnder()
    {
        var points = HealthChart.Points(new[] { (1, 40), (5, 20), (10, -3) }, maxTurn: 10, maxHealth: 40, width: 200, height: 100);

        Assert.Equal(new[] { (20.0, 0.0), (100.0, 50.0), (200.0, 100.0) }, points);
        Assert.Empty(HealthChart.Points(Array.Empty<(int, int)>(), 10, 40, 200, 100));
    }

    [Theory]
    [InlineData(1920, 1080)] // 16:9
    [InlineData(2291, 1360)] // Ali's window
    [InlineData(2560, 1080)] // 21:9
    [InlineData(1600, 1200)] // 4:3
    public void CombatsPanel_Default_WiderThanTall_ClearOfBoardsLeaderboardHero_AndOfTheCompositionPanel(double width, double height)
    {
        var combats = HistoryLayout.Panel(width, height);
        var compositions = TavernLayout.TargetPanel(width, height);

        foreach (var (name, zone) in NoGoZones.For(width, height))
        {
            Assert.False(NoGoZones.Overlaps(combats, zone), $"combats panel covers the {name}");
        }

        Assert.False(NoGoZones.Overlaps(combats, compositions), "the two default panels overlap");
        Assert.True(combats.Width > combats.Height, "combats panel is taller than wide");
        Assert.True(combats.Left >= 0 && combats.Top >= 0 && combats.Right <= width && combats.Top + combats.Height <= height, "outside the window");
        // HDT's Bob's Buddy and opponent info sit at the top centre (OverlayWindow.xaml.cs:279-280, 289-290).
        Assert.True(combats.Left > width / 2 + 0.2 * height, "combats panel reaches the top centre");
    }

    private static HeroHealth H(int id, string hero, int health, int place) => new(id, hero, health, place);

    [Fact]
    public void Standings_ByHealth_TiesShareARank_DeadLast_NextOpponentMarked_DeltaOverLastCombat()
    {
        var timeline = ThreeCombats(); // player 2: 30, 30, 23, 23; player 7: 30, 30, 21, 21; player 3: 30, 26, 26, 26
        var now = new[]
        {
            H(Me, "BG22_HERO_004", 23, 3), H(3, "TB_BaconShop_HERO_39", 26, 1), H(5, "BG31_HERO_802", 23, 2),
            H(7, "TB_BaconShop_HERO_16", 21, 4), H(9, "BG25_HERO_103", 0, 8),
        };

        var rows = Standings.Build(now, timeline, localPlayerId: Me, nextOpponentPlayerId: 7);

        Assert.Equal(new[] { 3, 5, Me, 7, 9 }, rows.Select(r => r.Hero.PlayerId)); // 23-23 tie: leaderboard place 2 before 3
        Assert.Equal(new[] { 1, 2, 2, 4, 5 }, rows.Select(r => r.Rank));
        Assert.Equal(new[] { false, false, true, false, false }, rows.Select(r => r.IsLocal));
        Assert.Equal(new[] { false, false, false, true, false }, rows.Select(r => r.IsNextOpponent));
        Assert.True(rows[4].IsDead);
        Assert.Equal(0, rows.Single(r => r.Hero.PlayerId == Me).Delta);  // 23 → 23 over the last recorded turns
        Assert.Equal(0, rows.Single(r => r.Hero.PlayerId == 5).Delta);
    }

    [Fact]
    public void Standings_DeltaIsTheLastCombatsDamage()
    {
        var t = new GameTimeline();
        t.Observe(3, false, Me, 5, new[] { new HeroHealth(Me, "A", 30), new HeroHealth(5, "B", 28) });
        t.Observe(3, true, Me, 5, new[] { new HeroHealth(Me, "A", 30), new HeroHealth(5, "B", 28) });
        t.Observe(4, false, Me, 3, new[] { new HeroHealth(Me, "A", 24), new HeroHealth(5, "B", 28) });

        var rows = Standings.Build(new[] { new HeroHealth(Me, "A", 24, 2), new HeroHealth(5, "B", 28, 1) }, t, Me, 0);

        Assert.Equal(new[] { (5, 0), (Me, -6) }, rows.Select(r => (r.Hero.PlayerId, r.Delta)));
    }

    [Fact]
    public void CombatText_ReadsWithoutExplanation_OverThreeTurns()
    {
        var combats = ThreeCombats().Combats;
        Assert.Equal("Turn 2 · vs Queen Wagtoggle · Win, 4 damage dealt", CombatText.Label(combats[0], "Queen Wagtoggle"));
        Assert.Equal("Turn 3 · vs Drek'Thar · Loss, 7 damage taken", CombatText.Label(combats[1], "Drek'Thar"));
        Assert.Equal("Turn 4 · vs Varden Dawngrasp · Tie", CombatText.Label(combats[2], "Varden Dawngrasp"));
    }

    [Theory]
    [InlineData(40, new[] { 0, 10, 20, 30, 40 })]
    [InlineData(34, new[] { 0, 10, 20, 30, 40 })]
    [InlineData(60, new[] { 0, 20, 40, 60 })]
    public void HealthTicks(int max, int[] expected) => Assert.Equal(expected, ChartAxes.HealthTicks(max));

    [Theory]
    [InlineData(3, new[] { 1, 2, 3 })]
    [InlineData(8, new[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    [InlineData(15, new[] { 1, 3, 5, 7, 9, 11, 13, 15 })]
    public void TurnTicks(int turns, int[] expected) => Assert.Equal(expected, ChartAxes.TurnTicks(turns));

    [Fact]
    public void InGame_OneHeroPerPlayer_LatestEntity_UnpickedOffersLeftOut()
    {
        EntitySnapshot Hero(int id, string card, int player, int place, int health) => new(id, card, isHero: true,
            new Dictionary<string, int> { ["PLAYER_ID"] = player, ["PLAYER_LEADERBOARD_PLACE"] = place, ["HEALTH"] = health, ["DAMAGE"] = 0 });
        var entities = new[]
        {
            Hero(40, "BG22_HERO_004", Me, 3, 30),
            Hero(41, "TB_BaconShop_HERO_39", Me, 0, 30),   // offered, not picked: no place
            Hero(50, "BG31_HERO_802", 5, 2, 24),
            Hero(90, "BG31_HERO_802t", 5, 2, 19),          // player 5's hero replaced later
            Hero(60, "TB_BaconShop_HERO_16", 7, 1, 28),
        };

        var heroes = HeroHealth.InGame(entities);

        Assert.Equal(new[] { (Me, "BG22_HERO_004", 30), (5, "BG31_HERO_802t", 19), (7, "TB_BaconShop_HERO_16", 28) },
            heroes.Select(h => (h.PlayerId, h.HeroCardId, h.Health)));
    }
}
