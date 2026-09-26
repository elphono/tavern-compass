namespace BronzebeardHud.Stats.Tests;

public class TavernLayoutTests
{
    [Theory]
    [InlineData(3, 2000, 1220)]
    [InlineData(4, 2000, 1220)]
    [InlineData(5, 2000, 1220)]
    [InlineData(6, 2000, 1220)]
    [InlineData(7, 2000, 1220)]
    [InlineData(7, 1440, 1080)] // 4:3, the narrowest window HDT's layout supports
    public void Markers_AreCentredUnderEachMinion_EquallySpaced_WithoutOverlap(int minions, double width, double height)
    {
        var markers = TavernLayout.Markers(width, height, minions);
        var s = height / 1080;
        var pitch = 138 * s; // HDT's shop card slot

        Assert.Equal(minions, markers.Count);
        for (var i = 0; i < minions; i++)
        {
            Assert.Equal(width, markers[i].CenterX + markers[minions - 1 - i].CenterX, precision: 6);
            Assert.Equal(height / 2 - 145 * s + 95 * s, markers[i].CenterY, precision: 6); // bottom edge of the card
            Assert.InRange(markers[i].Left, 0, width);
            Assert.InRange(markers[i].Right, 0, width);
        }

        for (var i = 0; i + 1 < minions; i++)
        {
            Assert.Equal(pitch, markers[i + 1].CenterX - markers[i].CenterX, precision: 6);
            Assert.True(markers[i].Right < markers[i + 1].Left, $"markers {i} and {i + 1} overlap");
        }
    }

    [Fact]
    public void Markers_FollowTheWindowWhenItIsResized()
    {
        var small = TavernLayout.Markers(1600, 900, 5);
        var large = TavernLayout.Markers(3200, 1800, 5);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(2 * (small[i].CenterX - 800), large[i].CenterX - 1600, precision: 6);
            Assert.Equal(2 * small[i].CenterY, large[i].CenterY, precision: 6);
        }
    }

    [Theory]
    [InlineData(2000, 1220)]
    [InlineData(1440, 1080)]
    [InlineData(3440, 1440)]
    public void TargetPanel_StaysClearOfTheTavernTheBoardsAndTheLeaderboard(double width, double height)
    {
        var panel = TavernLayout.TargetPanel(width, height);
        var markers = TavernLayout.Markers(width, height, 7);

        Assert.True(panel.Width > 0.15 * height, "panel too narrow to be read");
        Assert.True(panel.Top > TavernLayout.PlayerRowBottom(height), "panel overlaps the player's board row");
        Assert.True(panel.Top > markers[0].Top + markers[0].Height, "panel overlaps the tavern markers");
        Assert.True(panel.Right <= width / 2 - 0.2 * height, "panel reaches the hero portrait");
        Assert.True(panel.Left >= width / 2 - height * 2 / 3 + 0.12 * height * 4 / 3, "panel overlaps the leaderboard");
        Assert.True(panel.Top + panel.Height < height, "panel leaves the window");
    }

    [Fact]
    public void NothingToLayOut_IsEmpty()
    {
        Assert.Empty(TavernLayout.Markers(2000, 1220, 0));
        Assert.Empty(TavernLayout.Markers(0, 1220, 5));
    }

    [Fact]
    public void Markers_GrowWithTheirLines_AndCardSlotsFollowHdtsShop()
    {
        const double width = 2291, height = 1360;
        var s = height / 1080;
        var one = TavernLayout.Markers(width, height, 4, lines: 1)[0];
        var two = TavernLayout.Markers(width, height, 4, lines: 2)[0];
        Assert.Equal((13 * 1.3 + 8) * s, one.Height, precision: 6);
        Assert.Equal((2 * 13 * 1.3 + 8) * s, two.Height, precision: 6);

        var slots = TavernLayout.CardSlots(width, height, 3);
        Assert.Equal(width / 2 - 138 * s, slots[0].CenterX, precision: 6);
        Assert.Equal(width / 2, slots[1].CenterX, precision: 6);
        Assert.Equal(width / 2 + 138 * s, slots[2].CenterX, precision: 6);
        Assert.All(slots, r =>
        {
            Assert.Equal(height / 2 - 145 * s, r.CenterY, precision: 6);
            Assert.Equal(138 * s, r.Width, precision: 6);
            Assert.Equal(190 * s, r.Height, precision: 6);
        });
    }
}
