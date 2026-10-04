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
/// How much of each panel fits, in design pixels at 1080p (× TavernLayout.Scale): the panel grows downward from
/// its top and must stop above the player's gold at the bottom of the window; what does not fit is left out,
/// and the panel says how many lines it shows ("5 of 8 shown").
/// </summary>
public static class PanelFit
{
    public const double Border = 2;
    public const double Padding = 8;

    /// <summary>
    /// Design pixels of slack in every "does it fit?" comparison. A box dragged to exactly the height of its
    /// content, divided by a scale that is not a binary fraction, comes back a hair short (2.9999999 lines): without
    /// this, the panel would show one line fewer than the box holds (measured: at 72 % of the window heights from 600
    /// to 2200, 22 % of the height × line count pairs).
    /// </summary>
    private const double Tolerance = 1e-6;

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
    /// One guide line: tick box column, name column (the name may take two lines, the line is as tall as an oval), then
    /// <see cref="CoreOvalsPerRow"/> ovals. The name column is what gave way to the larger ovals (118 → 96): the panel keeps
    /// its width, the widest its default place allows (<see cref="PanelWidth"/>).
    /// </summary>
    public const double BoxColumn = 24;
    public const double NameColumn = 96;
    public static double RowHeight => OvalHeight;
    public const double RowGap = 6;

    /// <summary>The warband line under the list, and the loading status line.</summary>
    public const double FooterLine = 20;
    public const double StatusLine = 16;

    /// <summary>
    /// Panel width: the tick box, the name and six ovals, 488 design pixels. Not wider: at its default place
    /// (TavernLayout.TargetPanel) the panel ends 0.01 × H inside the 4:3 frame's right edge and must start right of the hero
    /// power (W/2 + 0.2 × H), which leaves 0.4567 × H, 493 design pixels.
    /// </summary>
    public const double PanelWidth = 2 * Border + 2 * Padding + BoxColumn + NameColumn + CoreOvalsPerRow * (OvalWidth + OvalGap);

    /// <summary>Lowest point a panel may reach, × H: above the player's gold, at the bottom right of the board.</summary>
    public const double BottomLimit = 0.945;

    /// <summary>
    /// Detail view pieces: "← back" and name, the meta line (up to two lines), a section (its 21 px title, then a line of
    /// ovals), a pivot line (4 px above a line of ovals).
    /// </summary>
    public const double DetailHeader = 36;
    public const double DetailMeta = 34;
    public static double DetailSection => 21 + OvalHeight;
    public static double PivotLine => 4 + OvalHeight;

    private static double Chrome(bool footer, bool status) =>
        2 * Border + 2 * Padding + TitleBar + (footer ? FooterLine : 0) + (status ? StatusLine : 0);

    /// <summary>
    /// Room below a panel's top, in design pixels: down to <paramref name="bottom"/> (overlay pixels, the lower edge
    /// of the box the player gave the panel) or, when it has none, down to <see cref="BottomLimit"/>.
    /// </summary>
    public static double Room(double height, double top, double? bottom = null) => ((bottom ?? BottomLimit * height) - top) / TavernLayout.Scale(height);

    /// <summary>
    /// Smallest box of the target panel when resized (design pixels): the title and one guide line, as wide as
    /// its six ovals need. A footer or a status line under that one line does not widen it: the panel then grows
    /// to hold what it shows, as it always showed at least one line.
    /// </summary>
    public const double TargetMinWidth = PanelWidth;
    public static double TargetMinHeight => ListHeight(1, footer: false, status: false);

    /// <summary>
    /// What the detail view always shows: "← back" and the name, the meta line and the two sections (enablers, key
    /// pieces), under the title bar. Only the pivots below it give way to a smaller box.
    /// </summary>
    public static double DetailMinHeight => 2 * Border + 2 * Padding + TitleBar + DetailHeader + DetailMeta + 2 * DetailSection;

    /// <summary>
    /// How many composition lines the list shows: as many as fit between the panel's top and
    /// <see cref="BottomLimit"/>, at most <paramref name="wanted"/>, at least one when there is one to show.
    /// </summary>
    public static int Rows(double height, double top, int wanted, bool footer, bool status, double? bottom = null)
    {
        if (wanted <= 0)
        {
            return 0;
        }

        var available = Room(height, top, bottom) - Chrome(footer, status) + RowGap;
        var fit = (int)Math.Floor((available + Tolerance) / (RowHeight + RowGap));
        return Math.Max(1, Math.Min(wanted, fit));
    }

    /// <summary>Height of a list of <paramref name="rows"/> lines, in design pixels, chrome included.</summary>
    public static double ListHeight(int rows, bool footer, bool status) =>
        Chrome(footer, status) + rows * RowHeight + Math.Max(0, rows - 1) * RowGap;

    /// <summary>
    /// How many pivot lines the detail view shows under its header, meta line and two sections (enablers, key
    /// pieces), which always show; at most <paramref name="pivots"/>.
    /// </summary>
    public static int DetailPivots(double height, double top, int pivots, double? bottom = null)
    {
        var available = Room(height, top, bottom) - DetailMinHeight;
        return Math.Max(0, Math.Min(pivots, (int)Math.Floor((available + Tolerance) / PivotLine)));
    }

    /// <summary>Ovals per line of cards in a panel <paramref name="panelWidth"/> wide (overlay pixels), padding and borders aside.</summary>
    public static int OvalsPerLine(double panelWidth, double height) =>
        Math.Max(1, (int)Math.Floor((panelWidth / TavernLayout.Scale(height) - 2 * Border - 2 * Padding + Tolerance) / (OvalWidth + OvalGap)));
}
