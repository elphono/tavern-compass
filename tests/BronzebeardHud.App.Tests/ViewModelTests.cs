using BronzebeardHud.App.ViewModels;
using BronzebeardHud.GameState;

namespace BronzebeardHud.App.Tests;

public class ViewModelTests
{
    [Fact]
    public void IsInGame_FalseWhenNotStarted()
    {
        var vm = new MainViewModel();
        Assert.False(vm.IsInGame);
    }

    [Fact]
    public void IsInGame_TrueWhenShopping()
    {
        var vm = new MainViewModel();
        vm.State = new GameStateSnapshot { Phase = GamePhase.Shopping };
        Assert.True(vm.IsInGame);
    }

    [Fact]
    public void IsInGame_TrueWhenCombat()
    {
        var vm = new MainViewModel();
        vm.State = new GameStateSnapshot { Phase = GamePhase.Combat };
        Assert.True(vm.IsInGame);
    }

    [Fact]
    public void IsShopping_TrueOnlyWhenShopping()
    {
        var vm = new MainViewModel();
        vm.State = new GameStateSnapshot { Phase = GamePhase.Shopping };
        Assert.True(vm.IsShopping);
        vm.State = new GameStateSnapshot { Phase = GamePhase.Combat };
        Assert.False(vm.IsShopping);
    }

    [Fact]
    public void PhaseText_ShowsTurnForShopping()
    {
        var vm = new MainViewModel();
        vm.State = new GameStateSnapshot { Phase = GamePhase.Shopping, Turn = 5 };
        Assert.Equal("Turn 5 - Shopping", vm.PhaseText);
    }

    [Fact]
    public void PhaseText_ShowsWaitingWhenNotStarted()
    {
        var vm = new MainViewModel();
        Assert.Equal("Waiting for game...", vm.PhaseText);
    }

    [Fact]
    public void PropertyChanged_FiresForAllDependentProperties()
    {
        var vm = new MainViewModel();
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        vm.State = new GameStateSnapshot { Phase = GamePhase.Shopping };
        Assert.Contains("State", changed);
        Assert.Contains("PhaseText", changed);
        Assert.Contains("IsInGame", changed);
        Assert.Contains("IsShopping", changed);
    }
}
