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
/// <summary>Which side of the target panel the card preview goes.</summary>
public enum PreviewSide
{
    Left,
    Right,
    Above,
    Below,
}

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

    /// <summary>
    /// The tavern card slots, left to right. <paramref name="cardCount"/> counts every card of Bob's row, the
    /// tavern spell included (<see cref="TavernRow"/>). The row is centred on W/2 and scaled by H only, as
    /// HDT's shop overlay is (full-canvas grid, Windows/OverlayWindow.Update.cs:746-748, 822-824): the
    /// window's ratio does not move it.
    /// </summary>
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

    /// <summary>Side of the pin button, and its gap above the card, in design units (× s).</summary>
    public const double PinButtonSize = 24;
    public const double PinButtonGap = 4;

    /// <summary>
    /// One pin button per tavern card, just above the card's slot, left of its centre: never on the card, so
    /// that buying (a click-and-drag on the card) is never caught by the overlay, and clear of the pin icon
    /// HDT draws on pinned cards (<see cref="HdtPinIcon"/>).
    /// </summary>
    public static IReadOnlyList<LayoutRect> PinButtons(double width, double height, int cardCount) => ButtonsAbove(width, height, cardCount, -1);

    /// <summary>The "?" button beside each pin button (how top players field the minion), right of the card's centre.</summary>
    public static IReadOnlyList<LayoutRect> LineupButtons(double width, double height, int cardCount) => ButtonsAbove(width, height, cardCount, +1);

    private static IReadOnlyList<LayoutRect> ButtonsAbove(double width, double height, int cardCount, int side)
    {
        var s = Scale(height);
        var shift = side * (PinButtonSize / 2 + PinButtonGap / 2) * s;
        return CardSlots(width, height, cardCount)
            .Select(card => new LayoutRect(card.CenterX + shift, card.Top - (PinButtonGap + PinButtonSize / 2) * s, PinButtonSize * s, PinButtonSize * s))
            .ToList();
    }

    /// <summary>
    /// Where HDT's Tavern Markers draw their pin on a card: 30 × 30 design units, 35 from the top and 17 from
    /// the right of the 138 × 190 slot (Controls/Overlay/Battlegrounds/MinionPinning/BattlegroundsMinionPinningCard.xaml:20-29).
    /// </summary>
    public static LayoutRect HdtPinIcon(LayoutRect slot, double height)
    {
        var s = Scale(height);
        return new LayoutRect(slot.Right - (17 + 15) * s, slot.Top + (35 + 15) * s, 30 * s, 30 * s);
    }

    /// <summary>Board row height, 0.158 × H: Windows/OverlayWindow.MouseOverDetection.cs:38.</summary>
    public const double BoardRowHeight = 0.158;

    /// <summary>Bottom of the player's board row: top = H/2 − 0.03 × H (OverlayWindow.Update.cs:537-538), plus the row height.</summary>
    public static double PlayerRowBottom(double height) => height / 2 - 0.03 * height + BoardRowHeight * height;

    /// <summary>Vignette width in the detail and lineups views, as a fraction of H.</summary>
    public const double VignetteSize = 0.052;

    /// <summary>
    /// Card vignettes are ovals, cut like the game's minion portraits: height = width × <see cref="OvalAspect"/>.
    /// </summary>
    public const double OvalAspect = 1.25;

    /// <summary>Oval width on a composition's line of the target panel, × H: seven of them, after its name.</summary>
    public const double RowOvalWidth = 0.040;

    /// <summary>Width of the name and placement column of a composition's line, × H; the text wraps inside it.</summary>
    public const double NameColumn = 0.10;

    /// <summary>Width of the tick box column, × H.</summary>
    public const double BoxColumn = 0.025;

    /// <summary>
    /// Height of the full-card preview shown when a vignette is hovered: HDT draws a Battlegrounds card
    /// 0.39 × H tall (Utility/RegionDrawer/RegionDrawer.cs:11); a little smaller, so that it fits above or
    /// beside the panel.
    /// </summary>
    public const double PreviewHeight = 0.36;

    /// <summary>Width over height of a Battlegrounds card render (256 × 388).</summary>
    public const double PreviewAspect = 256.0 / 388;

    /// <summary>Space between the preview and the panel, × H.</summary>
    public const double PreviewGap = 0.008;

    /// <summary>
    /// Where the full-card preview of a hovered vignette goes: beside the whole panel, never over it (so it
    /// hides neither the vignette nor its neighbours), towards the middle of the window first, then on the
    /// other side, then above or below the panel; always inside the window, vertically centred on the
    /// vignette as far as the window allows. When nothing fits (a panel as wide as the window), the side
    /// with the most room, kept inside the window.
    /// </summary>
    public static (PreviewSide Side, LayoutRect Rect) PreviewRect(LayoutRect panel, LayoutRect vignette, double width, double height)
    {
        var h = PreviewHeight * height;
        var w = h * PreviewAspect;
        var gap = PreviewGap * height;
        var top = Math.Max(0, Math.Min(height - h, vignette.CenterY - h / 2));
        LayoutRect At(double left, double t) => new(left + w / 2, t + h / 2, w, h);

        var leftFits = panel.Left - gap - w >= 0;
        var rightFits = panel.Right + gap + w <= width;
        var towardsLeft = panel.CenterX > width / 2;
        if (towardsLeft ? leftFits : rightFits)
        {
            return towardsLeft ? (PreviewSide.Left, At(panel.Left - gap - w, top)) : (PreviewSide.Right, At(panel.Right + gap, top));
        }

        if (towardsLeft ? rightFits : leftFits)
        {
            return towardsLeft ? (PreviewSide.Right, At(panel.Right + gap, top)) : (PreviewSide.Left, At(panel.Left - gap - w, top));
        }

        var left = Math.Max(0, Math.Min(width - w, vignette.CenterX - w / 2));
        if (panel.Top - gap - h >= 0)
        {
            return (PreviewSide.Above, At(left, panel.Top - gap - h));
        }

        if (panel.Top + panel.Height + gap + h <= height)
        {
            return (PreviewSide.Below, At(left, panel.Top + panel.Height + gap));
        }

        return panel.Left > width - panel.Right
            ? (PreviewSide.Left, At(Math.Max(0, panel.Left - gap - w), top))
            : (PreviewSide.Right, At(Math.Min(width - w, panel.Right + gap), top));
    }

    /// <summary>
    /// The offsets that make HDT's overlay tooltip land on <paramref name="preview"/>: HDT places it from the
    /// hovered element's rectangle, the placement and these offsets (Windows/OverlayWindow.Tooltips.cs:142-150),
    /// then keeps it inside the window (171-172). Our rectangle never makes HDT flip the side (121-137): it
    /// lies beyond the panel, hence beyond the vignette, on the chosen side.
    /// </summary>
    public static (double OffsetX, double OffsetY) HdtTooltipOffsets(PreviewSide side, LayoutRect preview, LayoutRect target) => side switch
    {
        PreviewSide.Left => (target.Left - preview.Width - preview.Left, preview.Top - (target.Top + target.Height / 2 - preview.Height / 2)),
        PreviewSide.Right => (preview.Left - target.Right, preview.Top - (target.Top + target.Height / 2 - preview.Height / 2)),
        PreviewSide.Above => (preview.Left - (target.CenterX - preview.Width / 2), target.Top - preview.Height - preview.Top),
        _ => (preview.Left - (target.CenterX - preview.Width / 2), preview.Top - (target.Top + target.Height)),
    };

    /// <summary>
    /// The target composition panel's default place: the lower right part of Hearthstone's 4:3 frame,
    /// below the player's board row (OverlayWindow.Update.cs:537-538 + MouseOverDetection.cs:38), right of
    /// the hero and hero power (which sit within W/2 ± 0.2 × H), above the gold at the bottom. Sized for
    /// three composition lines, each a tick box, a name column and seven ovals. It can be moved (PanelLayout).
    /// </summary>
    public static LayoutRect TargetPanel(double width, double height)
    {
        var frameRight = width / 2 + height * 2 / 3;
        var panelWidth = (0.02 + BoxColumn + NameColumn + 7 * RowOvalWidth * 1.08) * height;
        var panelHeight = 3 * (RowOvalWidth * OvalAspect * height + 0.012 * height) + 0.03 * height;
        var right = frameRight - 0.01 * height;
        var top = PlayerRowBottom(height) + 0.012 * height;
        return new LayoutRect(right - panelWidth / 2, top + panelHeight / 2, panelWidth, panelHeight);
    }

    /// <summary>
    /// The "how top boards field it" panel's default place: over the target composition panel's default place,
    /// the one area the plugin already takes below the boards. It opens on a click and closes on its × or when
    /// the shop ends, so covering the target panel for that time hides nothing of the game. It can be moved
    /// (PanelLayout, id "lineups").
    /// </summary>
    public static LayoutRect LineupsPanel(double width, double height) => TargetPanel(width, height);
}
