using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// The pieces of a guide drawn both by the panel's detail (CompsPanel, on a click) and by the popup of a hovered line
/// (GuidePopup): texts, HDT's tier gradients, the tier and difficulty badges, and the six sections in HDT's order — HOW TO
/// PLAY, CORE CARDS, ADDON CARDS, WHEN TO COMMIT, COMMON ENABLERS, PIVOTS —, each left out when the guide has nothing for
/// it. One definition, so that the two never drift apart. Every font size comes from PanelTypography (× scale).
/// </summary>
internal static class GuideView
{
    public static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xCD, 0xD8));
    private static readonly Brush PillBrush = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));

    // One colour per kind of card, the same in every section title: core (solid frame in the tavern), add-on, enabler, pivot.
    public static readonly Brush CoreBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x3F));
    private static readonly Brush AddonBrush = new SolidColorBrush(Color.FromRgb(0x7C, 0xE3, 0x8B));
    private static readonly Brush EnablerBrush = new SolidColorBrush(Color.FromRgb(0x5C, 0xE1, 0xFF));
    private static readonly Brush PivotBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x43));

    public static TextBlock Text(string text, double size, double scale, Brush brush, bool bold = false) => new()
    {
        Text = text,
        FontSize = size * scale,
        Foreground = brush,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>One line of a guide's text, card names in bold as HDT draws them (CompGuideText).</summary>
    public static TextBlock Runs(IReadOnlyList<CompGuideTextRun> runs, double size, double scale, Brush brush)
    {
        var text = new TextBlock { FontSize = size * scale, Foreground = brush, TextWrapping = TextWrapping.Wrap };
        foreach (var run in runs)
        {
            text.Inlines.Add(new Run(run.Text) { FontWeight = run.IsCard ? FontWeights.Bold : FontWeights.Normal });
        }

        return text;
    }

    /// <summary>HDT's tier colours: BattlegroundsCompGuideViewModel.TierColor, a left-to-right gradient.</summary>
    public static Brush TierBrush(int tier)
    {
        var (from, to) = tier switch
        {
            1 => (Color.FromRgb(64, 138, 191), Color.FromRgb(56, 95, 122)),
            2 => (Color.FromRgb(107, 160, 54), Color.FromRgb(88, 121, 55)),
            3 => (Color.FromRgb(146, 160, 54), Color.FromRgb(104, 121, 55)),
            4 => (Color.FromRgb(160, 124, 54), Color.FromRgb(121, 95, 55)),
            5 => (Color.FromRgb(160, 72, 54), Color.FromRgb(121, 66, 55)),
            _ => (Color.FromRgb(112, 112, 112), Color.FromRgb(64, 64, 64)),
        };
        return new LinearGradientBrush(from, to, new Point(0, 0.5), new Point(1, 0.5));
    }

    /// <summary>A guide's tier (its letter on HDT's gradient) and difficulty (HDT's colours), right of its name.</summary>
    public static FrameworkElement Badges(CompGuide guide, double scale)
    {
        var badges = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        badges.Children.Add(new Border
        {
            Background = TierBrush(guide.Tier),
            CornerRadius = new CornerRadius(3 * scale),
            Padding = new Thickness(6 * scale, 0, 6 * scale, 1 * scale),
            Margin = new Thickness(6 * scale, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Text(guide.TierLetter, PanelTypography.PanelTitle, scale, Brushes.White, bold: true),
        });
        if (guide.Difficulty is >= 1 and <= 3)
        {
            var label = Text(CompGuideDifficulty.Text(guide.Difficulty), PanelTypography.Small, scale, Brushes.White, bold: true);
            label.TextWrapping = TextWrapping.NoWrap;
            badges.Children.Add(new Border
            {
                Background = HexBrush.Of(CompGuideDifficulty.Colour(guide.Difficulty)),
                CornerRadius = new CornerRadius(3 * scale),
                Padding = new Thickness(6 * scale, 2 * scale, 6 * scale, 2 * scale),
                Margin = new Thickness(4 * scale, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = label,
            });
        }

        return badges;
    }

    /// <summary>
    /// A guide's sections, in HDT's order, each a title in its colour (and a short hint in grey) over its content; a
    /// section the guide has nothing for is left out. Core cards always show (a guide has at least two).
    /// </summary>
    /// <param name="held">Base card ids of the player's board and hand: "k/N held" and the green rings.</param>
    /// <param name="pivots">The guide's pivots (GuidePivots); null or empty: no PIVOTS section.</param>
    /// <param name="oval">Draws one card as an oval: with its preview on hover in the panel, without in the popup.</param>
    public static List<FrameworkElement> Sections(CompGuide guide, ICollection<string> held, IReadOnlyList<GuidePivot>? pivots, double scale,
        Func<string, FrameworkElement> oval)
    {
        var sections = new List<FrameworkElement>();
        if (guide.HowToPlayFirstLine.Count > 0)
        {
            sections.Add(Section("How to play", null, Brushes.White, scale, Runs(guide.HowToPlayFirstLine, PanelTypography.Body, scale, Brushes.White)));
        }

        var coreHeld = guide.CoreCards.Count(held.Contains);
        sections.Add(Section("Core cards", $"{coreHeld}/{guide.CoreCards.Count} held · solid frame in the tavern", CoreBrush, scale, OvalLines(guide.CoreCards, scale, oval)));
        if (guide.AddonCards.Count > 0)
        {
            sections.Add(Section("Addon cards", "dotted frame", AddonBrush, scale, OvalLines(guide.AddonCards, scale, oval)));
        }

        if (guide.WhenToCommitLines.Count > 0)
        {
            var pills = new StackPanel();
            foreach (var line in guide.WhenToCommitLines)
            {
                pills.Children.Add(new Border
                {
                    Background = PillBrush,
                    BorderBrush = CoreBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(9 * scale),
                    Padding = new Thickness(8 * scale, 2 * scale, 8 * scale, 3 * scale),
                    Margin = new Thickness(0, 3 * scale, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = Runs(line, PanelTypography.Small, scale, Brushes.White),
                });
            }

            sections.Add(Section("When to commit", null, CoreBrush, scale, pills));
        }

        if (guide.Enablers.Count > 0)
        {
            sections.Add(Section("Common enablers", "dotted frame", EnablerBrush, scale, OvalLines(guide.Enablers, scale, oval)));
        }

        if (pivots is { Count: > 0 })
        {
            var block = new StackPanel();
            foreach (var pivot in pivots)
            {
                var label = new TextBlock { FontSize = PanelTypography.Small * scale, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2 * scale, 0, 0) };
                label.Inlines.Add(new Run("→ ") { Foreground = PivotBrush, FontWeight = FontWeights.Bold });
                label.Inlines.Add(new Run(pivot.To.Name) { Foreground = Brushes.White, FontWeight = FontWeights.Bold });
                label.Inlines.Add(new Run($" · {pivot.Shared.Count.ToString(CultureInfo.InvariantCulture)} shared") { Foreground = MutedBrush });
                block.Children.Add(label);
                block.Children.Add(OvalLines(pivot.Shared, scale, oval));
            }

            sections.Add(Section("Pivots", "≈ guides sharing core or add-on cards", PivotBrush, scale, block));
        }

        return sections;
    }

    /// <summary>"k of n sections", under sections when some are left out: its widest text while measuring.</summary>
    public static TextBlock MoreSections(double scale)
    {
        var more = Text("9 of 9 sections", PanelTypography.Small, scale, MutedBrush);
        more.Margin = new Thickness(0, 6 * scale, 0, 0);
        return more;
    }

    public static string SectionsShown(SectionFit fit) =>
        $"{fit.Shown.Count.ToString(CultureInfo.InvariantCulture)} of {fit.Total.ToString(CultureInfo.InvariantCulture)} sections";

    /// <summary>A section's title in its colour, then a short hint in grey, on one line that wraps.</summary>
    private static TextBlock SectionTitle(string title, string? hint, Brush colour, double scale)
    {
        var text = new TextBlock { FontSize = PanelTypography.Small * scale, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6 * scale, 0, 0) };
        text.Inlines.Add(new Run(title.ToUpperInvariant()) { Foreground = colour, FontWeight = FontWeights.Bold });
        if (!string.IsNullOrEmpty(hint))
        {
            text.Inlines.Add(new Run("  " + hint) { Foreground = MutedBrush });
        }

        return text;
    }

    private static FrameworkElement Section(string title, string? hint, Brush colour, double scale, FrameworkElement content)
    {
        var section = new StackPanel();
        section.Children.Add(SectionTitle(title, hint, colour, scale));
        section.Children.Add(content);
        return section;
    }

    /// <summary>Cards as ovals, <see cref="PanelFit.CoreOvalsPerRow"/> to a line, as many lines as needed.</summary>
    private static FrameworkElement OvalLines(IReadOnlyList<string> cards, double scale, Func<string, FrameworkElement> oval)
    {
        var block = new StackPanel();
        for (var i = 0; i < cards.Count; i += PanelFit.CoreOvalsPerRow)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4 * scale, 0, 0) };
            foreach (var card in cards.Skip(i).Take(PanelFit.CoreOvalsPerRow))
            {
                line.Children.Add(oval(card));
            }

            block.Children.Add(line);
        }

        return block;
    }
}
