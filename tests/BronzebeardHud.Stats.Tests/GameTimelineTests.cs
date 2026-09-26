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
    [InlineData(2291, 1360)]
    [InlineData(1920, 1080)]
    [InlineData(1440, 1080)]
    public void HistoryPanel_RightOfAFullTavern_InsideTheFrame(double width, double height)
    {
        var panel = HistoryLayout.Panel(width, height);
        var lastMinion = TavernLayout.Markers(width, height, 7)[6];

        Assert.True(panel.Left > lastMinion.CenterX + TavernLayout.MinionWidth(height) / 2, "panel covers the seventh minion");
        Assert.True(panel.Right <= width / 2 + height * 2 / 3, "panel leaves the 4:3 frame");
        Assert.True(panel.Width > 0.15 * height, "panel too narrow");
        Assert.InRange(panel.Top, 0, height);
        Assert.True(panel.Top + panel.Height < height);
    }
}
