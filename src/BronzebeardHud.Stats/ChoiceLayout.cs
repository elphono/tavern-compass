using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Where the game draws the options of a Battlegrounds choice, with the constants HDT uses to mask them
/// (HearthSim/Hearthstone-Deck-Tracker, master 509bb0b, Utility/RegionDrawer/RegionDrawer.cs):
///
/// | kind      | card height × H | width / height       | spacing | layout centre | top × H | lines          |
/// |-----------|-----------------|----------------------|---------|---------------|---------|----------------|
/// | discover  | 0.39            | 28 / 39              | 0.27    | 0.53          | 0.29    | 11-12, 406-430 |
/// | Dark Gift | 0.605           | 33.2 / 60.5          | 0.287   | 0.519         | 0.185   | 51-54, 406-430 |
/// | trinket   | 0.32            | 25 / 36.5            | 0.192   | 0.51          | 0.32    | 37-39, 432-451 |
///
/// x values are fractions of the centred 4:3 frame: Helper.GetScaledXPos (Utility/Helper.cs:429) with
/// ScreenRatio = (4/3) / (W/H) (Windows/OverlayWindow.xaml.cs:495), i.e. x = (W − 4H/3)/2 + x × 4H/3. Card
/// i starts at centre − n × spacing / 2 + i × spacing, for n options. HDT picks the layout from the card
/// types and the Dark Gift tag (Windows/OverlayWindow.xaml.cs:1707-1717), as <see cref="ChoiceClassifier"/> does.
///
/// Hero selection uses another grid (RegionDrawer.cs:41-42, 374-378: heroes 0.1725 wide every 0.236,
/// centred on 0.50): HDT does not share it with discovers, and neither does this layout.
/// </summary>
public static class ChoiceLayout
{
    private readonly struct Grid
    {
        public Grid(double cardHeight, double aspect, double spacing, double centre, double top, double overhang = 0)
        {
            Overhang = overhang;
            CardHeight = cardHeight;
            Aspect = aspect;
            Spacing = spacing;
            Centre = centre;
            Top = top;
        }

        public double CardHeight { get; }
        public double Aspect { get; }
        public double Spacing { get; }
        public double Centre { get; }
        public double Top { get; }

        /// <summary>How far above the card's top the game draws over it, in fractions of the height; labels go above that.</summary>
        public double Overhang { get; }
    }

    private static readonly Grid Discover = new(0.39, 28 / 39.0, 0.27, 0.53, 0.29);
    private static readonly Grid DarkGift = new(0.605, 33.2 / 60.5, 0.287, 0.519, 0.185);
    // The price coin sticks out 0.042 × H above a trinket's frame (screenshot of the trinket shop, 2026-10-08, 3439 × 1368).
    private static readonly Grid Trinket = new(0.32, 25 / 36.5, 0.192, 0.51, 0.32, overhang: 0.042);

    /// <summary>Gap between a label and the top of its card, × H.</summary>
    public const double LabelGap = 0.006;

    private static Grid? GridOf(ChoiceKind kind) => kind switch
    {
        ChoiceKind.Discover => Discover,
        ChoiceKind.DarkGift => DarkGift,
        ChoiceKind.Trinket => Trinket,
        _ => null,
    };

    /// <summary>The cards on screen, left to right; empty for a kind without a known layout.</summary>
    public static IReadOnlyList<LayoutRect> Cards(ChoiceKind kind, int count, double width, double height)
    {
        if (GridOf(kind) is not { } grid || count <= 0 || width <= 0 || height <= 0)
        {
            return Array.Empty<LayoutRect>();
        }

        var frameWidth = height * 4 / 3;
        var frameLeft = (width - frameWidth) / 2;
        var cardHeight = grid.CardHeight * height;
        var cardWidth = cardHeight * grid.Aspect;
        var leftEdge = grid.Centre - count * grid.Spacing / 2;
        return Enumerable.Range(0, count)
            .Select(i =>
            {
                var left = frameLeft + frameWidth * (leftEdge + i * grid.Spacing);
                return new LayoutRect(left + cardWidth / 2, grid.Top * height + cardHeight / 2, cardWidth, cardHeight);
            })
            .ToList();
    }

    /// <summary>One label per card, as wide as the card, just above it, tall enough for <paramref name="lines"/> lines.</summary>
    public static IReadOnlyList<LayoutRect> Labels(ChoiceKind kind, int count, double width, double height, int lines)
    {
        var s = TavernLayout.Scale(height);
        var labelHeight = (Math.Max(1, lines) * TavernLayout.MarkerFontSize * 1.3 + 2 * TavernLayout.MarkerPadding) * s;
        var overhang = GridOf(kind)?.Overhang ?? 0;
        return Cards(kind, count, width, height)
            .Select(card => new LayoutRect(card.CenterX, card.Top - (overhang + LabelGap) * height - labelHeight / 2, card.Width, labelHeight))
            .ToList();
    }
}
