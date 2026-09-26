using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Where to draw a marker on each minion Bob offers, and the "target composition" panel. Positions reuse
/// Hearthstone Deck Tracker's own Battlegrounds shop overlay, the one its minion pinning draws over the
/// tavern cards (HearthSim/Hearthstone-Deck-Tracker, master 509bb0b):
/// - scale s = H / 1080: Windows/OverlayWindow.Update.cs:748, applied to the pinning view model at line 822;
/// - one card slot 138 × 190 design units: Controls/Overlay/Battlegrounds/MinionPinning/BattlegroundsMinionPinningCard.xaml:15-18;
/// - slots in a horizontal StackPanel centred in a full-canvas grid with Margin="0,-290,0,0", empty slots
///   collapsed: BattlegroundsMinionPinningShop.xaml:14-23 (+ Windows/OverlayWindow.xaml:545-553), so a card
///   centre is at x = W/2 + (i + 0.5 − n/2) × 138 s and y = H/2 − 145 s.
/// The first version used the constructed opponent-board row (OverlayWindow.MouseOverDetection.cs:38-39):
/// same pitch within 0.05 %, card bottom within 2 px; the shop constants are the ones HDT uses for this scene.
/// Valid for windows at least 4:3 wide.
/// </summary>
public static class TavernLayout
{
    public const double ShopCardWidth = 138;
    public const double ShopCardHeight = 190;
    public const double ShopGridShiftUp = 290;

    /// <summary>Marker font size and padding, in design units (× s).</summary>
    public const double MarkerFontSize = 13;
    public const double MarkerPadding = 4;

    public static double Scale(double height) => height / HeroPickLayout.DesignHeight;

    public static double MinionWidth(double height) => ShopCardWidth * Scale(height);

    public static double MinionPitch(double height) => ShopCardWidth * Scale(height);

    /// <summary>The tavern card slots, left to right.</summary>
    public static IReadOnlyList<LayoutRect> CardSlots(double width, double height, int cardCount)
    {
        if (width <= 0 || height <= 0 || cardCount <= 0)
        {
            return Array.Empty<LayoutRect>();
        }

        var s = Scale(height);
        var rects = new LayoutRect[cardCount];
        for (var i = 0; i < cardCount; i++)
        {
            rects[i] = new LayoutRect(width / 2 + (i + 0.5 - cardCount / 2.0) * ShopCardWidth * s,
                height / 2 - ShopGridShiftUp / 2 * s, ShopCardWidth * s, ShopCardHeight * s);
        }

        return rects;
    }

    /// <summary>
    /// One marker per tavern card, as wide as the card, straddling its bottom edge, tall enough for
    /// <paramref name="lines"/> lines of <see cref="MarkerFontSize"/>.
    /// </summary>
    public static IReadOnlyList<LayoutRect> Markers(double width, double height, int cardCount, int lines = 1)
    {
        var s = Scale(height);
        var markerHeight = (Math.Max(1, lines) * MarkerFontSize * 1.3 + 2 * MarkerPadding) * s;
        return CardSlots(width, height, cardCount)
            .Select(card => new LayoutRect(card.CenterX, card.Top + card.Height, card.Width * 0.95, markerHeight))
            .ToList();
    }

    /// <summary>Board row height, 0.158 × H: Windows/OverlayWindow.MouseOverDetection.cs:38.</summary>
    public const double BoardRowHeight = 0.158;

    /// <summary>Bottom of the player's board row: top = H/2 − 0.03 × H (OverlayWindow.Update.cs:537-538), plus the row height.</summary>
    public static double PlayerRowBottom(double height) => height / 2 - 0.03 * height + BoardRowHeight * height;

    /// <summary>
    /// The "target composition" panel, bottom left: below the player's board row, left of the hero
    /// portrait (which sits around the centre), and right of the leaderboard column (the first 12 %
    /// of the 4:3 frame). Seven tavern minions nearly fill the 4:3 frame, so there is no room beside them.
    /// </summary>
    public static LayoutRect TargetPanel(double width, double height)
    {
        var frameLeft = width / 2 - height * 2 / 3;
        var left = frameLeft + 0.12 * height * 4 / 3 + 0.01 * height;
        var right = width / 2 - 0.22 * height;
        var top = PlayerRowBottom(height) + 0.03 * height;
        var panelHeight = 0.12 * height;
        return new LayoutRect((left + right) / 2, top + panelHeight / 2, Math.Max(0, right - left), panelHeight);
    }
}
