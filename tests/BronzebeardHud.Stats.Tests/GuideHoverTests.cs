namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// When the popup of a hovered guide line is asked, shown and hidden (GuideHover). HDT's probe raises MouseEnter and
/// MouseLeave on the line, and WPF raises its own as well while the cursor is over a clickable element: both go through
/// the same calls, so a second "enter" must change nothing, and a "leave" while the cursor is still on the line (WPF's,
/// when HDT makes its window click-through again) must not hide the popup.
/// </summary>
public class GuideHoverTests
{
    [Fact]
    public void Enter_StartsTheDelay_ThenTheTimerShowsThatGuide_Once()
    {
        var hover = new GuideHover();

        Assert.Equal(GuideHoverAction.StartDelay, hover.Enter("a", blocked: false));
        Assert.Null(hover.Shown);
        Assert.Equal("a", hover.Elapsed(blocked: false));
        Assert.Equal("a", hover.Shown);
        Assert.Null(hover.Elapsed(blocked: false)); // a second tick shows nothing again
    }

    [Fact]
    public void ASecondEnterOnTheSameLine_ChangesNothing_AskedOrShown()
    {
        var hover = new GuideHover();
        hover.Enter("a", blocked: false);

        Assert.Equal(GuideHoverAction.None, hover.Enter("a", blocked: false)); // the probe's, then WPF's: the delay is not restarted
        Assert.Equal("a", hover.Elapsed(blocked: false));
        Assert.Equal(GuideHoverAction.None, hover.Enter("a", blocked: false)); // shown: not hidden and asked again
        Assert.Equal("a", hover.Shown);
    }

    [Fact]
    public void LeaveBeforeTheDelay_ShowsNothing()
    {
        var hover = new GuideHover();
        hover.Enter("a", blocked: false);

        Assert.Equal(GuideHoverAction.Hide, hover.Leave("a", cursorStillOver: false));
        Assert.Null(hover.Elapsed(blocked: false));
        Assert.Null(hover.Shown);
    }

    [Fact]
    public void ALeaveWhileTheCursorIsStillOnTheLine_IsIgnored()
    {
        var hover = new GuideHover();
        hover.Enter("a", blocked: false);
        hover.Elapsed(blocked: false);

        Assert.Equal(GuideHoverAction.None, hover.Leave("a", cursorStillOver: true));
        Assert.Equal("a", hover.Shown);
        Assert.Equal(GuideHoverAction.Hide, hover.Leave("a", cursorStillOver: false));
        Assert.Null(hover.Shown);
    }

    [Fact]
    public void ALeaveOfAnotherLine_ChangesNothing()
    {
        var hover = new GuideHover();
        hover.Enter("a", blocked: false);
        hover.Elapsed(blocked: false);

        Assert.Equal(GuideHoverAction.None, hover.Leave("b", cursorStillOver: false));
        Assert.Equal("a", hover.Shown);
    }

    [Fact]
    public void AnotherLine_HidesTheFirstAndStartsTheDelayAgain()
    {
        var hover = new GuideHover();
        hover.Enter("a", blocked: false);
        hover.Elapsed(blocked: false);

        Assert.Equal(GuideHoverAction.StartDelay, hover.Enter("b", blocked: false));
        Assert.Null(hover.Shown);
        Assert.Equal(GuideHoverAction.None, hover.Leave("a", cursorStillOver: false)); // the probe's leave of the first line comes after
        Assert.Equal("b", hover.Elapsed(blocked: false));
    }

    /// <summary>Three rounds on the same line: the state does not trap itself after a hide.</summary>
    [Fact]
    public void ThreeRoundsOnTheSameLine_EachShowsIt()
    {
        var hover = new GuideHover();
        for (var round = 0; round < 3; round++)
        {
            Assert.Equal(GuideHoverAction.StartDelay, hover.Enter("a", blocked: false));
            Assert.Equal("a", hover.Elapsed(blocked: false));
            Assert.Equal(GuideHoverAction.Hide, hover.Leave("a", cursorStillOver: false));
            Assert.Null(hover.Shown);
        }
    }

    [Fact]
    public void Blocked_NothingIsAsked_NorShown()
    {
        var hover = new GuideHover();

        Assert.Equal(GuideHoverAction.None, hover.Enter("a", blocked: true)); // move mode, a detail open
        Assert.Null(hover.Elapsed(blocked: false));

        hover.Enter("a", blocked: false);
        Assert.Null(hover.Elapsed(blocked: true)); // the panel switched to move mode during the delay
        Assert.Null(hover.Shown);
    }

    [Fact]
    public void Reset_ForgetsTheLine_SoTheSameEnterAsksAgain()
    {
        var hover = new GuideHover();
        hover.Enter("a", blocked: false);
        hover.Elapsed(blocked: false);

        hover.Reset();

        Assert.Null(hover.Shown);
        Assert.Equal(GuideHoverAction.StartDelay, hover.Enter("a", blocked: false));
    }

    /// <summary>A redraw that leaves the hovered guide out of the list hides it; one that keeps it changes nothing.</summary>
    [Fact]
    public void Listed_HidesAGuideNoLongerShown_KeepsOneStillShown()
    {
        var hover = new GuideHover();
        hover.Enter("a", blocked: false);
        hover.Elapsed(blocked: false);

        Assert.Equal(GuideHoverAction.None, hover.Listed(new[] { "a", "b" }));
        Assert.Equal("a", hover.Shown);
        Assert.Equal(GuideHoverAction.Hide, hover.Listed(new[] { "b" }));
        Assert.Null(hover.Shown);
        Assert.Equal(GuideHoverAction.StartDelay, hover.Enter("a", blocked: false));
    }
}
