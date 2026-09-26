using System;
using System.Collections.Generic;

namespace BronzebeardHud.Stats;

/// <summary>
/// Where to draw a marker under each minion Bob offers, and the "target composition" panel.
/// In Battlegrounds the tavern minions occupy the opponent board row, which Hearthstone Deck
/// Tracker already lays out for its mouse-over detection (HearthSim/Hearthstone-Deck-Tracker,
/// master 509bb0b):
/// - row height 0.158 × H: Windows/OverlayWindow.MouseOverDetection.cs:38;
/// - row top = H/2 − row height − 0.045 × H: Windows/OverlayWindow.Update.cs:534-535;
/// - minion width = W × 0.63 / 7 × ScreenRatio: OverlayWindow.MouseOverDetection.cs:39,
///   with ScreenRatio = (4/3) / (W/H): Windows/OverlayWindow.xaml.cs:495, i.e. (4/3) × H × 0.09;
/// - side margin W × ScreenRatio × 0.0029 on each side of a minion: OverlayWindow.MouseOverDetection.cs:57-64;
/// - minions in a horizontal StackPanel centred in a full-width grid: Windows/OverlayWindow.xaml:303-312.
/// Valid for windows at least 4:3 wide.
/// </summary>
public static class TavernLayout
{
    public const double RowHeight = 0.158;
    public const double RowOffsetAboveCentre = 0.045;
    public const double MinionWidthIn43Frame = 0.63 / 7;
    public const double MinionSideMarginIn43Frame = 0.0029;

    /// <summary>Marker height, as a fraction of H; it straddles the bottom edge of the minion.</summary>
    public const double MarkerHeight = 0.032;

    public static double MinionWidth(double height) => height * 4 / 3 * MinionWidthIn43Frame;

    public static double MinionPitch(double height) => height * 4 / 3 * (MinionWidthIn43Frame + 2 * MinionSideMarginIn43Frame);

    public static double RowTop(double height) => height / 2 - RowHeight * height - RowOffsetAboveCentre * height;

    /// <summary>One marker per tavern minion, centred under it.</summary>
    public static IReadOnlyList<LayoutRect> Markers(double width, double height, int minionCount)
    {
        if (width <= 0 || height <= 0 || minionCount <= 0)
        {
            return Array.Empty<LayoutRect>();
        }

        var pitch = MinionPitch(height);
        var markerWidth = MinionWidth(height) * 0.95;
        var centerY = RowTop(height) + RowHeight * height;
        var rects = new LayoutRect[minionCount];
        for (var i = 0; i < minionCount; i++)
        {
            rects[i] = new LayoutRect(width / 2 + (i + 0.5 - minionCount / 2.0) * pitch, centerY, markerWidth, MarkerHeight * height);
        }

        return rects;
    }

    /// <summary>Player board row, same source: top = H/2 − 0.03 × H (OverlayWindow.Update.cs:537-538).</summary>
    public static double PlayerRowBottom(double height) => height / 2 - 0.03 * height + RowHeight * height;

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
