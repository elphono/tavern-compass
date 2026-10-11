using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>A small line of a hero badge, under its note: the sources, the odds, another source and its tier.</summary>
public sealed class HeroNoteLine
{
    public HeroNoteLine(string text, string colour, string? tier = null)
    {
        Text = text;
        Colour = colour;
        Tier = tier;
    }

    public string Text { get; }

    /// <summary>"#RRGGBB".</summary>
    public string Colour { get; }

    /// <summary>Another source's tier letter, drawn in a chip of its colour before the text; null: no chip.</summary>
    public string? Tier { get; }
}

/// <summary>
/// What a hero badge shows, as the game shows a hero's armour (Ali, 2026-10-11: "les couleurs, les notes, c'est meilleur"): a
/// note read at a glance — the tier letter in a shield of its colour (<see cref="TierColour"/>), the average placement large
/// beside it (<see cref="PanelTypography.HeroNote"/>), white like the armour, amber with both figures when two sources
/// disagree — then, small (<see cref="PanelTypography.Small"/>), its caption under the figure (the sample, or "contested"),
/// the sources (nomi.gg's credit stays on screen, a promise to its owner) and the top-4 and first-place odds. Without a
/// consolidated line, the first source's figure is the note and a second source gets a small line with its tier chip. The
/// same facts as the lines of before (<see cref="HeroConsensus.For"/>, <see cref="HeroFigures"/>): only their drawing changes.
/// Forms too wide for their place (estimated as <see cref="MarkerText"/> does, generously) give way to shorter ones, never cut.
/// </summary>
public sealed class HeroNote
{
    /// <summary>The figure of a hero, white like the game's armour.</summary>
    public const string Plain = "#FFFFFF";

    /// <summary>Two sources apart: the figures, the caption and the badge's rim.</summary>
    public const string Contested = "#FFB03B";

    /// <summary>The small lines: sample, sources, odds.</summary>
    public const string Secondary = "#C8CDD8";

    /// <summary>No figure: "no data", its shield and "–".</summary>
    public const string Missing = "#9AA0AA";

    /// <summary>The badge's rim, silver like the armour's shield.</summary>
    public const string Rim = "#C9CED8";

    private HeroNote(string tier, string? figure, string figureColour, string caption, string captionColour, string rim, IReadOnlyList<HeroNoteLine> details)
    {
        Tier = tier;
        Figure = figure;
        FigureColour = figureColour;
        Caption = caption;
        CaptionColour = captionColour;
        RimColour = rim;
        Details = details;
    }

    /// <summary>"S" to "E", or "–" when no source gives one.</summary>
    public string Tier { get; }

    public string TierFill => TierColour(Tier);

    /// <summary>"3.41", or a contest's two figures "2.9↔3.8"; null when no source knows the hero.</summary>
    public string? Figure { get; }

    public string FigureColour { get; }

    /// <summary>Under the figure: "4,102 games", "contested", "18% pick · FS 25%", or "no data".</summary>
    public string Caption { get; }

    public string CaptionColour { get; }

    public string RimColour { get; }

    /// <summary>The small lines under the note, in order; the badge leaves out those that do not fit, never cuts them.</summary>
    public IReadOnlyList<HeroNoteLine> Details { get; }

    /// <summary>HeroPickPanel's colours of before, one per tier letter; grey for E and for none.</summary>
    public static string TierColour(string? tier) => tier switch
    {
        "S" => "#FF4D4D",
        "A" => "#FF9F1C",
        "B" => "#FFE03D",
        "C" => "#7CD95C",
        "D" => "#4DA6FF",
        _ => Missing,
    };

    /// <summary>Characters of a text <paramref name="fontSize"/> design pixels high that surely fit <paramref name="width"/>.</summary>
    public static int Chars(double width, double fontSize) => (int)Math.Floor(width / (fontSize * MarkerText.GlyphWidthEm));

    public static HeroNote For(HeroPickRow row)
    {
        if (!row.HasData)
        {
            return new HeroNote("–", null, Missing, "no data", Missing, Rim, Array.Empty<HeroNoteLine>());
        }

        var first = row.Figures[0];
        var tier = first.Tier ?? "–";
        var figureChars = Chars(HeroPickLayout.NoteWidth, PanelTypography.HeroNote);
        if (row.Consensus is { } consensus)
        {
            var details = new List<HeroNoteLine> { new(consensus.Sources, Secondary) };
            if (first.OddsText is { } odds)
            {
                details.Add(new HeroNoteLine(odds, Secondary));
            }

            if (consensus.Contested)
            {
                // Both figures, never their mean: "2.9 ↔ 3.8", or without its spaces where that is too wide.
                var figure = consensus.Note.Length <= figureChars ? consensus.Note : consensus.Note.Replace(" ↔ ", "↔");
                return new HeroNote(tier, figure, Contested, "contested", Contested, Contested, details);
            }

            return new HeroNote(tier, consensus.Note, Plain, consensus.Sample ?? string.Empty, Secondary, Rim, details);
        }

        // No consolidated line: the first source's figure is the note, a second source a small line with its tier.
        var captionChars = Chars(HeroPickLayout.NoteWidth, PanelTypography.Small);
        var perSource = new List<HeroNoteLine>();
        if (first.OddsText is { } firstOdds)
        {
            perSource.Add(new HeroNoteLine(firstOdds, Secondary));
        }

        if (row.Figures.Count > 1)
        {
            var second = row.Figures[1];
            // The chip and its gap take about three characters of the line.
            var lineChars = Chars(HeroPickLayout.ContentWidth, PanelTypography.Small) - 3;
            var text = FirstFitting(lineChars, F("{0:0.00}", second.AveragePlacement), Pick(second, " pick"), SourceOf(second));
            perSource.Add(new HeroNoteLine(text, Secondary, second.Tier ?? "–"));
        }

        var caption = FirstFitting(captionChars, null, Pick(first, " pick"), SourceOf(first));
        return new HeroNote(tier, F("{0:0.00}", first.AveragePlacement), Plain, caption, Secondary, Rim, perSource);
    }

    private static string? Pick(HeroFigures figures, string word) =>
        figures.PickRate is { } rate ? F("{0:0}%", rate * 100) + word : null;

    private static string SourceOf(HeroFigures figures) =>
        StatsSources.Label(figures.Source) + (figures.MmrPercentile is { } percentile && percentile < MmrBracket.EveryPlayer ? $" {percentile}%" : string.Empty);

    /// <summary>
    /// "3.10 · 12% pick · HSR 10%", or shorter where it does not fit: "3.10 · 12% · HSR 10%", then without the pick rate;
    /// the last form is given whatever its length (the estimate is generous).
    /// </summary>
    private static string FirstFitting(int chars, string? figure, string? pick, string source)
    {
        var head = figure == null ? Array.Empty<string>() : new[] { figure };
        var forms = new List<string>();
        if (pick != null)
        {
            forms.Add(string.Join(" · ", head.Append(pick).Append(source)));
            forms.Add(string.Join(" · ", head.Append(pick.Replace(" pick", string.Empty)).Append(source)));
        }

        forms.Add(string.Join(" · ", head.Append(source)));
        return forms.FirstOrDefault(f => MarkerText.DisplayLength(f) <= chars) ?? forms[forms.Count - 1];
    }

    private static string F(string format, params object[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
