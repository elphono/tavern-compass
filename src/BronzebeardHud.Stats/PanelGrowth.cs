using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>Which edge of its box a panel keeps when it is given another height (<see cref="PanelGrowth.Place"/>).</summary>
public enum PanelAnchor
{
    /// <summary>The top stays: the panel grows and shrinks downwards.</summary>
    Top,

    /// <summary>The bottom stays: the panel grows and shrinks upwards (it is against the bottom, or out of room below).</summary>
    Bottom,
}

/// <summary>Where a panel goes vertically, and which edge it kept.</summary>
public readonly struct PanelSpan
{
    public PanelSpan(double top, double height, PanelAnchor anchor)
    {
        Top = top;
        Height = height;
        Anchor = anchor;
    }

    public double Top { get; }
    public double Height { get; }
    public double Bottom => Top + Height;
    public PanelAnchor Anchor { get; }
}

/// <summary>
/// The "Compositions" panel sized to its content (Ali, 2026-10-06: "a press on + or − resizes the window to show the N best
/// compositions"). The box Move panels gives (its place, and the size the handle gave it or the default one) says where the
/// panel is; the content says how tall it must be. The panel keeps the top of its box and grows downwards while there is room
/// above the bottom bound; a box against the bottom bound keeps its bottom instead (at the bottom of the screen it grows
/// upwards); out of room below, it grows upwards up to the first thing above it. Never onto a zone of the game (the boards,
/// the leaderboard, the hero: GuidePopupLayout.GameZones) nor another panel, never below <see cref="PanelFit.BottomLimit"/>
/// (the gold) unless its box already was, never off the screen; when even that is not enough, it takes all the room there is
/// and shows fewer lines (CompGuideLayout.Fit).
/// </summary>
public static class PanelGrowth
{
    /// <param name="box">The box Move panels gives: the layout's place, and the height the handle gave or the default one.</param>
    /// <param name="wanted">The height the content asks for: the frame with N guide lines, and the power inset under it.</param>
    /// <param name="minimum">Never less: the title, one line and the inset (a box too small for one line grows to hold it).</param>
    /// <param name="canvasHeight">The overlay's height.</param>
    /// <param name="obstacles">What the panel must not cover: the game's zones, the other panels.</param>
    public static PanelSpan Place(LayoutRect box, double wanted, double minimum, double canvasHeight, IEnumerable<LayoutRect> obstacles)
    {
        var boxBottom = box.Top + box.Height;
        var gold = PanelFit.BottomLimit * canvasHeight;
        var floor = Math.Min(canvasHeight, Math.Max(gold, boxBottom));

        // At the bottom of the screen: a box the player put below the gold line, where the default panel never goes. A box
        // that only reaches it (the handle pulled down to it) keeps its top.
        var againstBottom = boxBottom > gold + BottomMargin;
        var ceiling = 0.0;
        foreach (var o in obstacles)
        {
            if (o.Width <= 0 || o.Height <= 0 || !(o.Left < box.Right && box.Left < o.Right))
            {
                continue; // not in the panel's columns
            }

            var bottom = o.Top + o.Height;
            if (bottom <= box.Top + Slack)
            {
                ceiling = Math.Max(ceiling, bottom);
            }
            else if (o.Top >= boxBottom - Slack)
            {
                floor = Math.Min(floor, o.Top);
            }

            // Otherwise the box already covers it: that is where the player put it, and it does not stop the panel.
        }

        var height = Math.Max(minimum, Math.Min(wanted, floor - ceiling));
        var (top, anchor) = !againstBottom && box.Top + height <= floor + Slack
            ? (box.Top, PanelAnchor.Top)
            : (floor - height, PanelAnchor.Bottom);

        // Only a minimum larger than the room gets here past the ceiling: still on the screen.
        top = Math.Max(0, Math.Min(canvasHeight - height, top));
        return new PanelSpan(top, height, anchor);
    }

    /// <summary>Overlay pixels of slack: an edge on an edge touches, it does not overlap.</summary>
    private const double Slack = 1e-6;

    /// <summary>
    /// How far below the gold line a box must reach to be "at the bottom of the screen", in overlay pixels: one, so that a box
    /// the handle pulled exactly to the line (a fraction of the overlay, rounded in layout.json) is not.
    /// </summary>
    private const double BottomMargin = 1;

    /// <summary>The zones of the game a growing panel keeps off (GuidePopupLayout.GameZones), and the other panels on screen.</summary>
    public static IReadOnlyList<LayoutRect> Obstacles(double width, double height, IEnumerable<LayoutRect>? panels = null) =>
        GuidePopupLayout.GameZones(width, height).Concat(panels ?? Array.Empty<LayoutRect>()).ToList();

    /// <summary>
    /// The line written in HDT's log when + or − resized the panel:
    /// "Bronzebeard HUD: targets n=4 panel resized to (1181,557 488x463) anchor=bottom lines=4/4 shown, of 15".
    /// </summary>
    public static string ResizeLine(int count, LayoutRect panel, PanelAnchor anchor, int linesShown, int linesWanted, int linesTotal)
    {
        var inv = CultureInfo.InvariantCulture;
        return $"Bronzebeard HUD: targets n={count.ToString(inv)} panel resized to ({panel.Left.ToString("0", inv)},{panel.Top.ToString("0", inv)} "
               + $"{panel.Width.ToString("0", inv)}x{panel.Height.ToString("0", inv)}) anchor={anchor.ToString().ToLowerInvariant()} "
               + $"lines={linesShown.ToString(inv)}/{linesWanted.ToString(inv)} shown, of {linesTotal.ToString(inv)}";
    }
}
