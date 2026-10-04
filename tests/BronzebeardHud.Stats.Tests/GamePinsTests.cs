namespace BronzebeardHud.Stats.Tests;

public class GamePinsTests
{
    private static readonly TavernPins File = TavernPins.Of(new[] { "BG26_817", "BG32_324" });

    private static string[] Pinned(GamePins pins) => pins.Merge(File).CardIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();

    [Fact]
    public void ThreeShops_PinUnpinByClick_MergedWithPinsTxt_ThenANewGameForgets()
    {
        var pins = new GamePins();
        pins.BeginGame(1);
        Assert.Equal(new[] { "BG26_817", "BG32_324" }, Pinned(pins)); // pins.txt alone

        // Shop 1: a click pins a minion; a golden copy counts as the same card.
        Assert.True(pins.Toggle("BG24_022_G", File));
        Assert.Equal(new[] { "BG24_022", "BG26_817", "BG32_324" }, Pinned(pins));

        // Shop 2: a second click unpins it; a click on a card of pins.txt unpins it for this game.
        Assert.False(pins.Toggle("BG24_022", File));
        Assert.False(pins.Toggle("BG26_817", File));
        Assert.Equal(new[] { "BG32_324" }, Pinned(pins));

        // Shop 3: clicked again, the pins.txt card is back; another minion is pinned.
        Assert.True(pins.Toggle("BG26_817", File));
        Assert.True(pins.Toggle("BG31_808", File));
        Assert.Equal(new[] { "BG26_817", "BG31_808", "BG32_324" }, Pinned(pins));

        pins.BeginGame(1);
        Assert.Equal(3, Pinned(pins).Length); // same game: kept
        pins.BeginGame(2);
        Assert.Equal(new[] { "BG26_817", "BG32_324" }, Pinned(pins)); // next game: pins.txt only
        Assert.Equal("# Tavern pins: one card per line, by name or id\nBG26_817\nBG32_324\n", File.Serialize()); // the file is untouched
    }

    public static IEnumerable<object[]> Sizes() =>
        new[] { (1920.0, 1080.0), (2290.0, 1359.0), (2560.0, 1080.0), (1600.0, 1200.0) }.Select(s => new object[] { s.Item1, s.Item2 });

    [Theory]
    [MemberData(nameof(Sizes))]
    public void PinButtons_ThreeToSevenCards_NeverOnACard_NorOnHdtsPin_InsideTheWindow(double width, double height)
    {
        static bool Overlap(LayoutRect a, LayoutRect b) =>
            a.Left < b.Right && b.Left < a.Right && a.Top < b.Top + b.Height && b.Top < a.Top + a.Height;

        foreach (var count in new[] { 3, 4, 5, 6, 7 })
        {
            var slots = TavernLayout.CardSlots(width, height, count);
            var buttons = TavernLayout.PinButtons(width, height, count);
            Assert.Equal(count, buttons.Count);
            for (var i = 0; i < count; i++)
            {
                // The pin left of the card's centre, where it stood beside the retired "?" button.
                Assert.True(buttons[i].Right < slots[i].CenterX, $"{count} cards: button {i} not left of the centre");
                Assert.True(buttons[i].Top + buttons[i].Height <= slots[i].Top, $"{count} cards: button {i} reaches its card");
                Assert.True(buttons[i].Top >= 0 && buttons[i].Left >= 0 && buttons[i].Right <= width, $"{count} cards: button {i} off the window");
                Assert.All(slots, slot => Assert.False(Overlap(buttons[i], slot), $"{count} cards: button {i} on a card"));
                Assert.All(slots, slot => Assert.False(Overlap(buttons[i], TavernLayout.HdtPinIcon(slot, height)), $"{count} cards: button {i} on HDT's pin"));
                if (i > 0)
                {
                    Assert.False(Overlap(buttons[i - 1], buttons[i]));
                }
            }
        }
    }

    [Fact]
    public void HdtsPinIcon_IsInsideTheCard_TopRight()
    {
        var slot = TavernLayout.CardSlots(1920, 1080, 3)[1];
        var icon = TavernLayout.HdtPinIcon(slot, 1080);
        Assert.Equal((slot.Right - 17 - 30, slot.Top + 35, 30.0), (Math.Round(icon.Left, 6), Math.Round(icon.Top, 6), Math.Round(icon.Width, 6)));
    }
}
