using System;
using System.Collections.Generic;

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
/// Where to draw one badge per offered hero: centred under each portrait, on the empty grey
/// plate below the name banner. Pure function of the overlay size and the number of heroes, so
/// it is recomputed whenever the Hearthstone window is resized.
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
/// The vertical position and the plate size are not in HDT (its stats sit above the portraits). They were
/// measured on Ali's 2026-09-26 screenshot of a 2291 × 1360 window (Hearthstone's options.txt), with the
/// window located on the screenshot by HDT's hero-power tooltip region (RegionDrawer.cs:379-384, predicted
/// left edge 1261 px, measured 1262 px): plate centre at 0.663 to 0.670 of the height, plate about
/// 0.175 × 0.062 of the height. The badge uses slightly smaller values so it never spills off the plate.
/// Valid for windows at least 4:3 wide, the same assumption as HDT's height-only scaling.
/// </summary>
public static class HeroPickLayout
{
    public const double DesignHeight = 1080;
    public const double HeroPitch = 340;
    public const double GridShiftRight = 7;

    /// <summary>Badge centre, as a fraction of the overlay height.</summary>
    public const double PlateCenterY = 0.667;

    /// <summary>Badge size, as fractions of the overlay height.</summary>
    public const double BadgeWidth = 0.17;
    public const double BadgeHeight = 0.06;

    /// <summary>Scale factor HDT applies to its hero-picking overlay; also the font scale of the badges.</summary>
    public static double Scale(double overlayHeight) => overlayHeight / DesignHeight;

    public static IReadOnlyList<LayoutRect> Compute(double width, double height, int heroCount)
    {
        if (width <= 0 || height <= 0 || heroCount <= 0)
        {
            return Array.Empty<LayoutRect>();
        }

        var scale = Scale(height);
        var gridCenter = width / 2 + GridShiftRight * scale;
        var rects = new LayoutRect[heroCount];
        for (var i = 0; i < heroCount; i++)
        {
            var offset = (i + 0.5 - heroCount / 2.0) * HeroPitch * scale;
            rects[i] = new LayoutRect(gridCenter + offset, PlateCenterY * height, BadgeWidth * height, BadgeHeight * height);
        }

        return rects;
    }
}
