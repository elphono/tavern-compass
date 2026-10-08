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
            Assert.Equal(kind != ChoiceKind.Trinket, cover.PanelHidden);
            lines.Add(cover.Observe(OverlayPhase.Shop, kind)); // the next updates of the same choice: no line
            lines.Add(cover.Observe(OverlayPhase.Shop, kind));
            lines.Add(cover.Observe(OverlayPhase.Shop, ChoiceKind.None));
            Assert.False(cover.Hidden);
            Assert.False(cover.PanelHidden);
            lines.Add(cover.Observe(OverlayPhase.Shop, ChoiceKind.None));
        }

        Assert.Equal(new[]
        {
            ChoiceCover.HiddenLine, null, null, ChoiceCover.RestoredLine, null,
            ChoiceCover.HiddenLine, null, null, ChoiceCover.RestoredLine, null,
            ChoiceCover.PanelKeptLine, null, null, ChoiceCover.RestoredLine, null,
        }, lines);
        Assert.Equal("Bronzebeard HUD: choice open (trinkets): markers hidden, panel kept", ChoiceCover.PanelKeptLine);
        Assert.Equal("Bronzebeard HUD: choice open: markers and panel hidden", ChoiceCover.HiddenLine);
        Assert.Equal("Bronzebeard HUD: choice closed: restored", ChoiceCover.RestoredLine);
    }

    [Fact]
    public void Trinkets_HideBobsMarkers_ButKeepThePanel()
    {
        // Ali, 2026-10-08: the "Compositions" panel must not disappear while choosing a trinket. The markers stay hidden:
        // the trinket shop covers Bob's row, and their ◇ would fall inside the options.
        var cover = new ChoiceCover();

        Assert.Equal(ChoiceCover.PanelKeptLine, cover.Observe(OverlayPhase.Shop, ChoiceKind.Trinket));
        Assert.True(cover.Hidden);
        Assert.False(cover.PanelHidden);

        // Another kind while still open (a discover from a trinket): the panel goes, one line; back to trinkets, it returns.
        Assert.Equal(ChoiceCover.HiddenLine, cover.Observe(OverlayPhase.Shop, ChoiceKind.Discover));
        Assert.True(cover.PanelHidden);
        Assert.Equal(ChoiceCover.PanelKeptLine, cover.Observe(OverlayPhase.Shop, ChoiceKind.Trinket));
        Assert.False(cover.PanelHidden);
        Assert.Null(cover.Observe(OverlayPhase.Shop, ChoiceKind.Trinket));
    }

    [Fact]
    public void AChoiceWithoutKnownLayoutHidesToo_ItsOptionsCoverBobsRowAsWell()
    {
        var cover = new ChoiceCover();

        Assert.Equal(ChoiceCover.HiddenLine, cover.Observe(OverlayPhase.Shop, ChoiceKind.Unsupported));
        Assert.True(cover.Hidden);
        Assert.True(cover.PanelHidden);
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
        Assert.False(cover.PanelHidden);
        Assert.Null(cover.Observe(OverlayPhase.Shop, ChoiceKind.None));
        Assert.Equal(ChoiceCover.HiddenLine, cover.Observe(OverlayPhase.Shop, ChoiceKind.DarkGift));
    }
}
