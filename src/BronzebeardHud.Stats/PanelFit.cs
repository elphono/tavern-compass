using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Every font size of the plugin's text: the target composition panel, the lineups panel, the tavern and choice
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

    /// <summary>Body text: lineups headings, "No composition reachable yet", a hero badge's figures and odds.</summary>
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

    /// <summary>Title bar: "Target compositions", Meta, − n +; buttons 22 tall, a rule under them.</summary>
    public const double TitleBar = 32;

    /// <summary>Oval card vignettes, everywhere: width, and height = width × TavernLayout.OvalAspect.</summary>
    public const double OvalWidth = 40;
    public const double OvalGap = 4;
    public static double OvalHeight => OvalWidth * TavernLayout.OvalAspect;

    /// <summary>One composition line: tick box column, name and placement column, seven ovals.</summary>
    public const double BoxColumn = 24;
    public const double NameColumn = 118;
    public const double RowHeight = 50;
    public const double RowGap = 6;

    /// <summary>The warband line under the list, and the loading status line.</summary>
    public const double FooterLine = 20;
    public const double StatusLine = 16;

    /// <summary>Panel width: 488 design pixels, so that its default place stays right of the hero power.</summary>
    public const double PanelWidth = 2 * Border + 2 * Padding + 468;

    /// <summary>Lowest point a panel may reach, × H: above the player's gold, at the bottom right of the board.</summary>
    public const double BottomLimit = 0.945;

    /// <summary>Detail view pieces: "← back" and name, the meta line (up to two lines), a section (title and ovals), a pivot line.</summary>
    public const double DetailHeader = 36;
    public const double DetailMeta = 34;
    public const double DetailSection = 15 + 50 + 4;
    public const double PivotLine = 54;

    private static double Chrome(bool footer, bool status) =>
        2 * Border + 2 * Padding + TitleBar + (footer ? FooterLine : 0) + (status ? StatusLine : 0);

    /// <summary>Room below a panel's top, in design pixels: down to <see cref="BottomLimit"/>.</summary>
    public static double Room(double height, double top) => (BottomLimit * height - top) / TavernLayout.Scale(height);

    /// <summary>
    /// How many composition lines the list shows: as many as fit between the panel's top and
    /// <see cref="BottomLimit"/>, at most <paramref name="wanted"/>, at least one when there is one to show.
    /// </summary>
    public static int Rows(double height, double top, int wanted, bool footer, bool status)
    {
        if (wanted <= 0)
        {
            return 0;
        }

        var available = Room(height, top) - Chrome(footer, status) + RowGap;
        var fit = (int)Math.Floor(available / (RowHeight + RowGap));
        return Math.Max(1, Math.Min(wanted, fit));
    }

    /// <summary>Height of a list of <paramref name="rows"/> lines, in design pixels, chrome included.</summary>
    public static double ListHeight(int rows, bool footer, bool status) =>
        Chrome(footer, status) + rows * RowHeight + Math.Max(0, rows - 1) * RowGap;

    /// <summary>
    /// How many pivot lines the detail view shows under its header, meta line and two sections (enablers, key
    /// pieces), which always show; at most <paramref name="pivots"/>.
    /// </summary>
    public static int DetailPivots(double height, double top, int pivots)
    {
        var available = Room(height, top) - Chrome(false, false) - DetailHeader - DetailMeta - 2 * DetailSection;
        return Math.Max(0, Math.Min(pivots, (int)Math.Floor(available / PivotLine)));
    }

    /// <summary>
    /// The lineups panel's default place: the right-hand column, right of seven tavern cards (with their ◇ and ?
    /// buttons), below HDT's top bar (top right corner of the window), above the Skip combat button and the
    /// target composition panel; on a window wider than 4:3 it may reach past Hearthstone's frame, over the
    /// board's side decoration. It never covers another plugin panel nor the game's cards, hero or leaderboard.
    /// </summary>
    public static LayoutRect LineupsPanel(double width, double height)
    {
        var s = TavernLayout.Scale(height);
        var left = width / 2 + 3.5 * TavernLayout.ShopCardWidth * s + 8 * s;
        var right = Math.Min(width - 0.01 * height, width / 2 + height * 2 / 3 - 0.01 * height + 0.25 * height);
        var top = 0.07 * height;
        var bottom = SkipCombatLayout.Button(width, height).Top - 0.01 * height;
        return new LayoutRect((left + right) / 2, (top + bottom) / 2, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    /// <summary>Lineups panel pieces: title and headline (two lines each at most), a composition label, an MMR line.</summary>
    public const double LineupsHeader = 2 * Border + 2 * Padding + 36 + 32;
    public const double LineupLabel = 32;
    public const double LineupMmr = 16;

    /// <summary>Ovals per line of a board in a panel <paramref name="panelWidth"/> wide (overlay pixels).</summary>
    public static int OvalsPerLine(double panelWidth, double height) =>
        Math.Max(1, (int)Math.Floor((panelWidth / TavernLayout.Scale(height) - 2 * Border - 2 * Padding) / (OvalWidth + OvalGap)));

    /// <summary>How many compositions of <paramref name="boardSizes"/> (their best board's card count) the lineups panel shows.</summary>
    public static int LineupCompositions(LayoutRect panel, double height, IReadOnlyList<int> boardSizes)
    {
        var perLine = OvalsPerLine(panel.Width, height);
        var room = panel.Height / TavernLayout.Scale(height) - LineupsHeader;
        var shown = 0;
        foreach (var size in boardSizes)
        {
            var lines = (int)Math.Ceiling(Math.Max(1, size) / (double)perLine);
            var need = LineupLabel + lines * (OvalHeight + RowGap) + LineupMmr;
            if (need > room)
            {
                break;
            }

            room -= need;
            shown++;
        }

        return shown;
    }
}
