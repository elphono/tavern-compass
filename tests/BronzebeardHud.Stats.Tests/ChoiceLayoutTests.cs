namespace BronzebeardHud.Stats.Tests;

public class ChoiceLayoutTests
{
    public static IEnumerable<object[]> Cases() =>
        from kind in new[] { ChoiceKind.Discover, ChoiceKind.DarkGift, ChoiceKind.Trinket }
        from size in new[] { (2000.0, 1220.0), (2291.0, 1360.0) }
        select new object[] { kind, size.Item1, size.Item2 };

    [Theory]
    [MemberData(nameof(Cases))]
    public void TwoThreeFourOptions_Symmetric_EquallySpaced_NoOverlap_LabelsAboveTheirCardInsideTheCanvas(ChoiceKind kind, double width, double height)
    {
        double? axis = null;
        foreach (var count in new[] { 2, 3, 4 })
        {
            var cards = ChoiceLayout.Cards(kind, count, width, height);
            var labels = ChoiceLayout.Labels(kind, count, width, height, lines: 2);
            Assert.Equal(count, cards.Count);
            Assert.Equal(count, labels.Count);

            // Same axis whatever the number of options: the row is symmetric about it.
            var middle = (cards[0].Left + cards[count - 1].Right) / 2;
            axis ??= middle;
            Assert.Equal(axis.Value, middle, precision: 6);

            var pitch = cards[1].CenterX - cards[0].CenterX;
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    Assert.Equal(pitch, cards[i].CenterX - cards[i - 1].CenterX, precision: 6);
                    Assert.True(cards[i - 1].Right < cards[i].Left, $"{kind} ×{count}: cards {i - 1} and {i} overlap");
                    Assert.True(labels[i - 1].Right < labels[i].Left, $"{kind} ×{count}: labels {i - 1} and {i} overlap");
                }

                Assert.Equal(cards[i].CenterX, labels[i].CenterX, precision: 6);
                Assert.True(labels[i].Top + labels[i].Height <= cards[i].Top, $"{kind}: label {i} covers its card");
                Assert.True(labels[i].Top >= 0 && cards[i].Left >= 0 && cards[i].Right <= width, $"{kind} ×{count}: off the canvas");
            }
        }
    }

    [Fact]
    public void HdtsConstants_AtFullHd()
    {
        // Discover, 3 options, 1920 × 1080: frame 1440 wide from x=240; first card at 0.53 − 1.5 × 0.27 = 0.125.
        var discover = ChoiceLayout.Cards(ChoiceKind.Discover, 3, 1920, 1080);
        Assert.Equal((420.0, 313.2, 302.4, 421.2), (Math.Round(discover[0].Left, 6), Math.Round(discover[0].Top, 6), Math.Round(discover[0].Width, 6), Math.Round(discover[0].Height, 6)));
        Assert.Equal(0.27 * 1440, discover[1].Left - discover[0].Left, precision: 6);

        // Dark Gift, 3 options: 0.519 − 1.5 × 0.287 = 0.0885; cards 0.605 × H tall from 0.185 × H.
        var darkGift = ChoiceLayout.Cards(ChoiceKind.DarkGift, 3, 1920, 1080);
        Assert.Equal((367.44, 199.8, 653.4), (Math.Round(darkGift[0].Left, 6), Math.Round(darkGift[0].Top, 6), Math.Round(darkGift[0].Height, 6)));

        // Trinkets, 4 options: 0.51 − 2 × 0.192 = 0.126; the same 277-unit pitch as HDT's trinket overlay.
        var trinkets = ChoiceLayout.Cards(ChoiceKind.Trinket, 4, 1920, 1080);
        Assert.Equal((421.44, 345.6), (Math.Round(trinkets[0].Left, 6), Math.Round(trinkets[0].Top, 6)));
        Assert.Equal(276.48, trinkets[1].Left - trinkets[0].Left, precision: 6);

        // Hero selection is another grid (0.236 pitch): four discover options do not line up with four heroes.
        Assert.NotEqual(0.236 * 1440, ChoiceLayout.Cards(ChoiceKind.Discover, 4, 1920, 1080)[1].Left - ChoiceLayout.Cards(ChoiceKind.Discover, 4, 1920, 1080)[0].Left, precision: 3);
        Assert.Empty(ChoiceLayout.Cards(ChoiceKind.Unsupported, 3, 1920, 1080));
    }
}
