namespace BronzebeardHud.Stats.Tests;

public class TargetPanelViewTests
{
    [Fact]
    public void ALineClick_OpensThatCompositionsDetail_BackReturnsToTheList_OverThreeRounds()
    {
        var view = new TargetPanelView();
        Assert.False(view.ShowsDetail);

        foreach (var id in new[] { "undead_butcher", "pirate_discover", "mech_magnet" })
        {
            Assert.True(view.LineClicked(id), $"{id}: the click opens the detail");
            Assert.True(view.ShowsDetail);
            Assert.Equal(id, view.DetailId);

            view.Back();
            Assert.False(view.ShowsDetail);
            Assert.Null(view.DetailId);
        }
    }

    [Fact]
    public void InTheDetail_ACardClick_ChangesNothing()
    {
        var view = new TargetPanelView();
        view.LineClicked("undead_butcher");

        // A card of the detail (an enabler, a key piece, a pivot card) belongs to no line: the detail stays as it is.
        Assert.False(view.LineClicked("pirate_discover"));
        Assert.Equal("undead_butcher", view.DetailId);
    }
}
