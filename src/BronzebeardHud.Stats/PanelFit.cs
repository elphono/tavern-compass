using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Every font size of the plugin's text: the target composition panel, the tavern and choice
/// markers, the Skip combat button, the hero badges and the opponents' MMR, in design pixels at 1080p
/// (× TavernLayout.Scale). Ali, 2026-09-27: no text under 12 px at 1080p,
/// after any scaling. Nothing is ever shrunk to fit any more: when content runs out of room, less is shown
/// (PanelFit), never smaller.
/// </summary>
public static class PanelTypography
{
    /// <summary>The floor: the smallest size any text of these panels may have, at 1080p.</summary>
    public const double Floor = 12;

    public const double PanelTitle = 14;
    public const double CompositionName = 14;
    public const double Button = 13;

    /// <summary>Body text: "No composition reachable yet", a hero badge's figures and odds.</summary>
    public const double Body = 13;

    /// <summary>
    /// Placement, counts, section titles and hints, pivots, the meta line, MMR lines, footer, status; the opponents'
    /// MMR labels, a hero badge's composition line and the hero status line.
    /// </summary>
    public const double Small = 12;

    /// <summary>The tick and the tier on an oval.</summary>
    public const double Badge = 12;

    public const double Marker = TavernLayout.MarkerFontSize;
    public const double RoundButton = 14;
    public const double SkipCombat = 15;

    /// <summary>A hero badge's tier letter, and its "no data".</summary>
    public const double HeroTier = 22;
    public const double HeroNoData = 14;

    public static readonly IReadOnlyList<double> All = new[] { PanelTitle, CompositionName, Button, Body, Small, Badge, Marker, RoundButton, SkipCombat, HeroTier, HeroNoData };

    /// <summary>A size in overlay pixels for a window <paramref name="height"/> tall.</summary>
    public static double Px(double size, double height) => size * TavernLayout.Scale(height);
}

/// <summary>
/// The measures of the "Compositions" panel, in design pixels at 1080p (× TavernLayout.Scale): its frame, its ovals, its
/// width, its default and minimum heights, and the lowest point it may reach (above the player's gold). Which pieces
/// fit is decided by CompGuideLayout, on the heights the panel measures in place; what does not fit is left out, and
/// the panel says how many lines it shows ("5 of 8 shown").
/// </summary>
public static class PanelFit
{
    public const double Border = 2;
    public const double Padding = 8;

    /// <summary>Title bar: "Target compositions", Meta, − n +; buttons 22 tall, a rule under them.</summary>
    public const double TitleBar = 32;

    /// <summary>
    /// Oval card vignettes, everywhere: width, and height = width × TavernLayout.OvalAspect. 54 since the comp guides became
    /// the targets (2026-10-04): a guide has 2 to 6 core cards (measured on HSReplay's list), so a line shows six ovals at
    /// most, larger, where it showed a Firestone board of seven.
    /// </summary>
    public const double OvalWidth = 54;
    public const double OvalGap = 4;
    public static double OvalHeight => OvalWidth * TavernLayout.OvalAspect;

    /// <summary>Ovals on one guide line: its core cards, never more (add-ons and enablers are never mixed into that line).</summary>
    public const int CoreOvalsPerRow = 6;

    /// <summary>
    /// How a guide line shows <paramref name="coreCards"/> core cards: all of them up to <see cref="CoreOvalsPerRow"/>;
    /// beyond, one oval fewer and a "+k" in the last place (k = the cards not shown), since six ovals fill the line and
    /// a text after them would be cut. The detail shows them all.
    /// </summary>
    public static (int Ovals, int More) ListOvals(int coreCards) =>
        coreCards <= CoreOvalsPerRow ? (Math.Max(0, coreCards), 0) : (CoreOvalsPerRow - 1, coreCards - (CoreOvalsPerRow - 1));

    /// <summary>
    /// One guide line: tick box column, name column (the name may take two lines, the line is as tall as an oval), then
    /// <see cref="CoreOvalsPerRow"/> ovals. The name column is what gave way to the larger ovals (118 → 96): the panel keeps
    /// its width, the widest its default place allows (<see cref="PanelWidth"/>).
    /// </summary>
    public const double BoxColumn = 24;
    public const double NameColumn = 96;
    public static double RowHeight => OvalHeight;

    /// <summary>
    /// The gap <see cref="ListHeight"/> counts between two lines, hence the default box (TavernLayout.TargetPanel) and the
    /// resize minimum (<see cref="TargetMinHeight"/>). Not the gap the panel draws: it measures its own lines in place
    /// (CompsPanel).
    /// </summary>
    public const double RowGap = 6;

    /// <summary>The loading status line.</summary>
    public const double StatusLine = 16;

    /// <summary>
    /// The power inset under the frame (PowerInset; it left the frame on 2026-10-06), in design pixels: 2 apart from the
    /// frame, a 1 px border, 2 of padding, two rows 16 tall (the player's board, the opponent's) 2 apart, 2 of padding and the
    /// border. What the panel draws is measured in place; this sizes the default box and the minimum. Not more: at the
    /// default place in 1080p, three targets in two tiers and the inset take all the room between the boards and the gold.
    /// </summary>
    public const double PowerInset = InsetGap + 2 * InsetBorder + 2 * InsetPadding + 2 * InsetRow + InsetRowGap;

    public const double InsetGap = 2;
    public const double InsetBorder = 1;
    public const double InsetPadding = 2;
    public const double InsetRow = 16;
    public const double InsetRowGap = 2;

    /// <summary>
    /// Panel width: the tick box, the name and six ovals, 488 design pixels. Not wider: at its default place
    /// (TavernLayout.TargetPanel) the panel ends 0.01 × H inside the 4:3 frame's right edge and must start right of the hero
    /// power (W/2 + 0.2 × H), which leaves 0.4567 × H, 493 design pixels.
    /// </summary>
    public const double PanelWidth = 2 * Border + 2 * Padding + BoxColumn + NameColumn + CoreOvalsPerRow * (OvalWidth + OvalGap);

    /// <summary>Lowest point a panel may reach, × H: above the player's gold, at the bottom right of the board.</summary>
    public const double BottomLimit = 0.945;

    private static double Chrome(bool status) =>
        2 * Border + 2 * Padding + TitleBar + (status ? StatusLine : 0);

    /// <summary>
    /// Smallest box of the target panel when resized (design pixels): the title and one guide line, as wide as
    /// its six ovals need, and the power inset under them. A status line under that one line does not widen it: the panel
    /// then grows to hold what it shows, as it always showed at least one line.
    /// </summary>
    public const double TargetMinWidth = PanelWidth;
    public static double TargetMinHeight => ListHeight(1) + PowerInset;

    /// <summary>Height of a list of <paramref name="rows"/> lines in its frame, in design pixels, chrome included (not the inset).</summary>
    public static double ListHeight(int rows, bool status = false) =>
        Chrome(status) + rows * RowHeight + Math.Max(0, rows - 1) * RowGap;
}
