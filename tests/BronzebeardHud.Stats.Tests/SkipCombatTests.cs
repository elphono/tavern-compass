namespace BronzebeardHud.Stats.Tests;

public class SkipCombatTests
{
    [Fact]
    public void Button_ShowsInCombatOnly_AndActsOncePerCombat_OverThreeCombats()
    {
        var state = new SkipCombatState();
        Assert.False(state.Observe(OverlayPhase.HeroSelection));
        Assert.False(state.TryBegin()); // outside combat, a click does nothing

        for (var combat = 1; combat <= 3; combat++)
        {
            Assert.False(state.Observe(OverlayPhase.Shop));
            Assert.False(state.TryBegin());

            Assert.True(state.Observe(OverlayPhase.Combat), $"combat {combat}: shown");
            Assert.True(state.TryBegin(), $"combat {combat}: first click acts");

            // Still in combat (HDT has not noticed the client went away yet): hidden, and a second click cannot
            // kill the client just started.
            Assert.False(state.Observe(OverlayPhase.Combat), $"combat {combat}: hidden after the skip");
            Assert.False(state.TryBegin(), $"combat {combat}: second click refused");

            // HDT resets its game when Hearthstone's window goes away: the button is armed again for the next combat.
            Assert.False(state.Observe(OverlayPhase.OutOfGame));
        }
    }

    [Fact]
    public void ASkipThatLeftTheGameRunning_TheNextCombatAfterTheShopShowsItAgain_OverThreeCombats()
    {
        // Nothing was killed (executable not found, no process, Kill() refused): HDT never resets, the game goes
        // on from combat to shop; the button must come back at the next combat all the same.
        var state = new SkipCombatState();
        for (var combat = 1; combat <= 3; combat++)
        {
            Assert.True(state.Observe(OverlayPhase.Combat), $"combat {combat}: shown");
            Assert.True(state.TryBegin(), $"combat {combat}: acts");
            Assert.False(state.Observe(OverlayPhase.Combat), $"combat {combat}: hidden after the click");
            Assert.False(state.Observe(OverlayPhase.Shop));
        }
    }

    [Fact]
    public void Relaunch_AlwaysThroughBattleNet_TheParentFirst_ThenARunningOne()
    {
        var fromParent = SkipCombatPlan.Relaunch("Battle.net.exe", @"E:\JEUX\Battle.net\Battle.net.exe", @"C:\Other\Battle.net.exe");
        Assert.Equal((@"E:\JEUX\Battle.net\Battle.net.exe", "--exec=\"launch WTCG\"", (string?)null), (fromParent.File, fromParent.Arguments, fromParent.Refusal));

        // Started by something else (a shortcut, HDT): the running Battle.net, never the game's own executable.
        var fromRunning = SkipCombatPlan.Relaunch("explorer.exe", @"C:\Windows\explorer.exe", @"C:\Other\Battle.net.exe");
        Assert.Equal((@"C:\Other\Battle.net.exe", "--exec=\"launch WTCG\""), (fromRunning.File, fromRunning.Arguments));
        Assert.Equal(@"C:\Other\Battle.net.exe", SkipCombatPlan.Relaunch("BATTLE.NET.EXE", "", @"C:\Other\Battle.net.exe").File);
    }

    [Fact]
    public void Relaunch_WithoutBattleNet_IsRefused_SoNothingIsKilled()
    {
        // A direct start of Hearthstone.exe cannot log in (measured 2026-09-27): no Battle.net, no skip.
        var refused = SkipCombatPlan.Relaunch("explorer.exe", @"C:\Windows\explorer.exe", runningBattleNet: null);

        Assert.Null(refused.File);
        Assert.NotNull(refused.Refusal);
        Assert.Null(SkipCombatPlan.Relaunch(null, null, "  ").File);
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1440, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(3440, 1440)]
    public void DefaultPlace_RightOfSevenMinions_InsideTheFrame_ClearOfTheTargetPanel(double width, double height)
    {
        var button = SkipCombatLayout.Button(width, height);
        var s = TavernLayout.Scale(height);

        Assert.True(button.Left > width / 2 + 3.5 * TavernLayout.ShopCardWidth * s, "right of a full board of seven minions");
        Assert.True(button.Right <= width / 2 + height * 2 / 3, "inside Hearthstone's 4:3 frame");
        Assert.True(button.Top >= height / 2 - 0.03 * height && button.Top + button.Height <= TavernLayout.PlayerRowBottom(height), "within the player's row band");

        var targets = TavernLayout.TargetPanel(width, height);
        Assert.True(button.Top + button.Height < targets.Top, "above the target composition panel");
    }
}
