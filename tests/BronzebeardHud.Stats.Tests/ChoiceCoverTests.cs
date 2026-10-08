namespace BronzebeardHud.Stats.Tests;

public class ChoiceCoverTests
{
    [Fact]
    public void NothingIsHiddenAndNothingLoggedWhileNoChoiceIsOpen()
    {
        var cover = new ChoiceCover();

        Assert.Null(cover.Observe(OverlayPhase.Shop, ChoiceKind.None));
        Assert.Null(cover.Observe(OverlayPhase.Shop, ChoiceKind.None));
        Assert.False(cover.Hidden);
    }

    [Fact]
    public void OneLinePerTransition_NeverPerUpdate_OverThreeChoices()
    {
        var cover = new ChoiceCover();
        var lines = new List<string?>();
        foreach (var kind in new[] { ChoiceKind.Discover, ChoiceKind.DarkGift, ChoiceKind.Trinket })
        {
            lines.Add(cover.Observe(OverlayPhase.Shop, kind));
            Assert.True(cover.Hidden);
            lines.Add(cover.Observe(OverlayPhase.Shop, kind)); // the next updates of the same choice: no line
            lines.Add(cover.Observe(OverlayPhase.Shop, kind));
            lines.Add(cover.Observe(OverlayPhase.Shop, ChoiceKind.None));
            Assert.False(cover.Hidden);
            lines.Add(cover.Observe(OverlayPhase.Shop, ChoiceKind.None));
        }

        Assert.Equal(new[]
        {
            ChoiceCover.HiddenLine, null, null, ChoiceCover.RestoredLine, null,
            ChoiceCover.HiddenLine, null, null, ChoiceCover.RestoredLine, null,
            ChoiceCover.HiddenLine, null, null, ChoiceCover.RestoredLine, null,
        }, lines);
        Assert.Equal("Bronzebeard HUD: choice open: markers hidden, panel kept", ChoiceCover.HiddenLine);
        Assert.Equal("Bronzebeard HUD: choice closed: markers restored", ChoiceCover.RestoredLine);
    }

    [Fact]
    public void AChoiceWithoutKnownLayoutHidesToo_ItsOptionsCoverBobsRowAsWell()
    {
        var cover = new ChoiceCover();

        Assert.Equal(ChoiceCover.HiddenLine, cover.Observe(OverlayPhase.Shop, ChoiceKind.Unsupported));
        Assert.True(cover.Hidden);
    }

    [Fact]
    public void OnlyInTheShop_HeroSelectionAndCombatHideNothing()
    {
        var cover = new ChoiceCover();

        Assert.Null(cover.Observe(OverlayPhase.HeroSelection, ChoiceKind.Unsupported)); // the heroes offered
        Assert.Null(cover.Observe(OverlayPhase.Combat, ChoiceKind.Discover));
        Assert.Null(cover.Observe(OverlayPhase.OutOfGame, ChoiceKind.Discover));
        Assert.False(cover.Hidden);

        // A choice still listed when the shop ends: the combat shows everything again.
        Assert.Equal(ChoiceCover.HiddenLine, cover.Observe(OverlayPhase.Shop, ChoiceKind.Discover));
        Assert.Equal(ChoiceCover.RestoredLine, cover.Observe(OverlayPhase.Combat, ChoiceKind.Discover));
        Assert.False(cover.Hidden);
    }

    [Fact]
    public void Reset_ShowsEverythingWithoutALine_AndTheNextChoiceHidesAgain()
    {
        var cover = new ChoiceCover();
        cover.Observe(OverlayPhase.Shop, ChoiceKind.Discover);

        cover.Reset();

        Assert.False(cover.Hidden);
        Assert.Null(cover.Observe(OverlayPhase.Shop, ChoiceKind.None));
        Assert.Equal(ChoiceCover.HiddenLine, cover.Observe(OverlayPhase.Shop, ChoiceKind.Trinket));
    }
}
