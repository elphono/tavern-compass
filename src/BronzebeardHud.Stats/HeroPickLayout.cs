using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>A rectangle in overlay pixels (WPF device-independent units).</summary>
public readonly struct LayoutRect
{
    public LayoutRect(double centerX, double centerY, double width, double height)
    {
        CenterX = centerX;
        CenterY = centerY;
        Width = width;
        Height = height;
    }

    public double CenterX { get; }
    public double CenterY { get; }
    public double Width { get; }
    public double Height { get; }
    public double Left => CenterX - Width / 2;
    public double Right => CenterX + Width / 2;
    public double Top => CenterY - Height / 2;
}

/// <summary>
/// Where to draw one badge per offered hero: under the game's reroll button ("Réinitialiser") below each
/// hero, in the hero's column. Pure function of the overlay size and the number of heroes, so it is
/// recomputed whenever the Hearthstone window is resized.
///
/// Horizontal placement reuses Hearthstone Deck Tracker's own method for its hero-picking stats
/// (HearthSim/Hearthstone-Deck-Tracker, master 509bb0b):
/// - scale = overlay height / 1080: Windows/OverlayWindow.Update.cs:748, applied at lines 802-804;
/// - one hero every 340 design units (Width 266 + Margin 37 on each side), in a single-row
///   UniformGrid centred in the overlay: Controls/Overlay/Battlegrounds/HeroPicking/BattlegroundsHeroPicking.xaml:30-47;
/// - the grid's Margin="14,57,0,0" with HorizontalAlignment="Center" shifts it 7 units right (line 30).
/// The pitch agrees with HDT's game-geometry constants for four heroes (Utility/RegionDrawer/RegionDrawer.cs:42-43
/// and 374-378: (0.1725 + 0.0635) of a 4:3 frame = 0.3147 × height, against 340 / 1080 = 0.3148 × height).
///
/// The vertical placement is not in HDT: its source has no geometry for the reroll button (searched on
/// 2026-09-27). It comes from Hearthstone's own capture of Ali's hero selection, 2026-09-26 18:29:44, window
/// 2291 × 1360, in fractions of the height H: name plate 0.527 to 0.638, reroll button 0.632 to 0.718 (from
/// 0.125 H left to 0.087 H right of the hero's centre), OK button 0.751 to 0.825 (0.065 H left to 0.075 H right
/// of the window's middle). The first badges (centre 0.667) sat on the reroll button, read at the time as an
/// empty plate (Ali, 2026-09-27: "il cache le bouton du jeu pour changer le héros"). Now each badge starts under
/// the reroll button; one that would come within <see cref="Margin"/> of the OK button is moved sideways, away
/// from it, when that takes at most <see cref="MaxShift"/>, or else (the middle hero of three) under it.
/// Valid for windows at least 4:3 wide, the same assumption as HDT's height-only scaling.
/// </summary>
public static class HeroPickLayout
{
    public const double DesignHeight = 1080;
    public const double HeroPitch = 340;
    public const double GridShiftRight = 7;

    /// <summary>Badge top, as a fraction of the overlay height: under the reroll button (bottom 0.718).</summary>
    public const double BadgeTop = 0.725;

    /// <summary>
    /// Badge size, as fractions of the overlay height: the width of before; the height holds a source line,
    /// the odds line and the comp line on two lines at their own size (<see cref="ContentHeight"/>), nothing shrunk.
    /// </summary>
    public const double BadgeWidth = 0.17;
    public const double BadgeHeight = 0.08;

    /// <summary>Least gap between a badge and the game's OK button, × H.</summary>
    public const double Margin = 0.01;

    /// <summary>Largest sideways move that keeps a badge in its hero's column, × H.</summary>
    public const double MaxShift = 0.03;

    /// <summary>The game's OK button, as measured (see the class summary); the badges stay clear of it.</summary>
    public static LayoutRect OkButton(double width, double height) =>
        FromEdges(width / 2 - 0.065 * height, 0.751 * height, width / 2 + 0.075 * height, 0.825 * height);

    /// <summary>Scale factor HDT applies to its hero-picking overlay; also the font scale of the badges.</summary>
    public static double Scale(double overlayHeight) => overlayHeight / DesignHeight;

    /// <summary>Each offered hero's centre, left to right (HDT's grid, see the class summary).</summary>
    public static IReadOnlyList<double> HeroCenters(double width, double height, int heroCount)
    {
        if (width <= 0 || height <= 0 || heroCount <= 0)
        {
            return Array.Empty<double>();
        }

        var scale = Scale(height);
        var gridCenter = width / 2 + GridShiftRight * scale;
        var centers = new double[heroCount];
        for (var i = 0; i < heroCount; i++)
        {
            centers[i] = gridCenter + (i + 0.5 - heroCount / 2.0) * HeroPitch * scale;
        }

        return centers;
    }

    public static IReadOnlyList<LayoutRect> Compute(double width, double height, int heroCount)
    {
        var ok = OkButton(width, height);
        var guard = FromEdges(ok.Left - Margin * height, ok.Top - Margin * height, ok.Right + Margin * height, ok.Top + ok.Height + Margin * height);
        return HeroCenters(width, height, heroCount).Select(center =>
        {
            var badge = new LayoutRect(center, BadgeTop * height + BadgeHeight * height / 2, BadgeWidth * height, BadgeHeight * height);
            if (!Overlaps(badge, guard))
            {
                return badge;
            }

            // Sideways, away from the OK button, when a small move clears it; else under it.
            var shift = center < ok.CenterX ? guard.Left - badge.Right : guard.Right - badge.Left;
            return Math.Abs(shift) <= MaxShift * height
                ? new LayoutRect(badge.CenterX + shift, badge.CenterY, badge.Width, badge.Height)
                : new LayoutRect(badge.CenterX, guard.Top + guard.Height + badge.Height / 2, badge.Width, badge.Height);
        }).ToList();
    }

    /// <summary>
    /// The status line (stats loading, errors): centred on the window, under the lowest of the badges and the
    /// OK button, <see cref="Margin"/> apart; two lines of text at most.
    /// </summary>
    public static LayoutRect Status(double width, double height, IReadOnlyList<LayoutRect> badges)
    {
        var ok = OkButton(width, height);
        var top = badges.Select(b => b.Top + b.Height).Append(ok.Top + ok.Height).Max() + Margin * height;
        return FromEdges(width / 2 - 0.2 * height, top, width / 2 + 0.2 * height, top + 0.04 * height);
    }

    /// <summary>Badge frame, in design pixels (× scale): border and padding on each side.</summary>
    public const double BadgeBorder = 2;
    public const double BadgePaddingX = 4;
    public const double BadgePaddingY = 2;

    /// <summary>
    /// Line heights inside a badge, in design pixels (× scale): a source line (its <see cref="PanelTypography.HeroTier"/>
    /// tier letter), a <see cref="PanelTypography.Body"/> line, "no data".
    /// </summary>
    public const double SourceLine = 28;
    public const double BodyLine = 17;
    public const double NoDataLine = 18;

    /// <summary>Room for the lines inside a badge, in design pixels.</summary>
    public static double ContentHeight => BadgeHeight * DesignHeight - 2 * (BadgeBorder + BadgePaddingY);

    /// <summary>Width of a line inside a badge, in design pixels.</summary>
    public static double ContentWidth => BadgeWidth * DesignHeight - 2 * (BadgeBorder + BadgePaddingX);

    /// <summary>
    /// Which of a badge's items (a line, or a wrapped line and its continuation) to show, in order, given each
    /// one's height and the room inside the badge: every item that still fits, the others left out. Nothing is
    /// ever shrunk.
    /// </summary>
    public static IReadOnlyList<int> ItemsThatFit(IReadOnlyList<double> itemHeights, double room)
    {
        var shown = new List<int>();
        var used = 0.0;
        for (var i = 0; i < itemHeights.Count; i++)
        {
            if (used + itemHeights[i] <= room + 1e-9)
            {
                used += itemHeights[i];
                shown.Add(i);
            }
        }

        return shown;
    }

    private static bool Overlaps(LayoutRect a, LayoutRect b) =>
        a.Left < b.Right && b.Left < a.Right && a.Top < b.Top + b.Height && b.Top < a.Top + a.Height;

    private static LayoutRect FromEdges(double left, double top, double right, double bottom) =>
        new((left + right) / 2, (top + bottom) / 2, right - left, bottom - top);
}
