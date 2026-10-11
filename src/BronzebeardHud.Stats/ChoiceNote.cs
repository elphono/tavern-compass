using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace BronzebeardHud.Stats;

/// <summary>What a line of a choice's label says about the card, hence the colour of its pill.</summary>
public enum ChoiceRole
{
    /// <summary>A core card of a target: "★ core", gold.</summary>
    Core,

    /// <summary>An enabler or add-on of a target: "+", silver.</summary>
    Support,

    /// <summary>On the top final boards of a target's bridged comp, in no list of its guide: "+", silver, "3/5 boards".</summary>
    Boards,

    /// <summary>A core card of a guide a target can pivot to: "pivot →", lavender.</summary>
    Pivot,

    /// <summary>A core card of another guide of the lobby: "core", an outline only.</summary>
    Guide,

    /// <summary>Card-stats: better than the cards played at this turn, "t6 ▲", green.</summary>
    CardBetter,

    /// <summary>Card-stats: worse, "t6 ▼", red.</summary>
    CardWorse,

    /// <summary>"+2 more": no pill.</summary>
    More,

    /// <summary>"—": no pill, discreet.</summary>
    Nothing,
}

/// <summary>
/// One line of a choice's label, in pieces drawn apart: a pill that says the role at a glance, the guide's name (in its
/// target's colour), a count, a suffix and a tier chip. Joined by spaces, the tier in parentheses, they say what the line
/// of before said (<see cref="Text"/>).
/// </summary>
public sealed class ChoiceNoteLine
{
    public ChoiceNoteLine(ChoiceRole role, string pill, string name, string count, string suffix, string? tier, string nameColour)
    {
        Role = role;
        Pill = pill;
        Name = name;
        Count = count;
        Suffix = suffix;
        Tier = tier;
        NameColour = nameColour;
    }

    public ChoiceRole Role { get; }

    /// <summary>"★ core", "+", "pivot →", "core", "t6 ▲"; empty for "+2 more" and "—".</summary>
    public string Pill { get; }

    /// <summary>The guide's name, or a shorter form of it; empty when none.</summary>
    public string Name { get; }

    /// <summary>"2/3→3/3", "3/5 boards", "3.6"; drawn white and bold.</summary>
    public string Count { get; }

    /// <summary>"· 4/5 boards", "vs 3.9", "+2 more", "—"; drawn small and grey.</summary>
    public string Suffix { get; }

    /// <summary>A guide's tier letter, in a chip of HDT's tier colour after the name; null: none.</summary>
    public string? Tier { get; }

    /// <summary>"#RRGGBB": the target's colour, white for a guide that is no target.</summary>
    public string NameColour { get; }

    /// <summary>The pill's fill; null for an outlined pill ("core") or none.</summary>
    public string? PillFill => ChoiceNote.PillFill(Role);

    /// <summary>"★ core Undead Butcher 2/3→3/3 · 4/5 boards", "pivot → Beast Pack (A)": the line as text.</summary>
    public string Text => string.Join(" ", new[] { Pill, Name, Count, Suffix, Tier == null ? string.Empty : $"({Tier})" }.Where(p => p.Length > 0));

    /// <summary>Its width in marker characters (MarkerText's generous estimate): the text, and the pill's padding.</summary>
    public int Width => MarkerText.DisplayLength(Text) + (Pill.Length > 0 ? ChoiceNote.PillExtra : 0);
}

/// <summary>A trinket's note: its placement large, in its band's colour, and the small lines beside it.</summary>
public sealed class TrinketFigure
{
    public TrinketFigure(string? figure, PlacementBand band, bool contested, IReadOnlyList<HeroNoteLine> details)
    {
        Figure = figure;
        Band = band;
        Contested = contested;
        Details = details;
    }

    /// <summary>"3.66", "≈3.41" (adjusted to a target), "3.3↔4.1" (a contest); null: no stats ("no data", "loading…").</summary>
    public string? Figure { get; }

    public PlacementBand Band { get; }

    public bool Contested { get; }

    /// <summary>Beside the figure, one per line of the label: "avg place · 44% picked", "nomi.gg 3.75 (55)", "contested"…</summary>
    public IReadOnlyList<HeroNoteLine> Details { get; }

    /// <summary>Amber for a contest, the band's colour otherwise (<see cref="PlacementGrade.Colour"/>).</summary>
    public string Colour => Contested ? HeroNote.Contested : Figure == null ? HeroNote.Missing : PlacementGrade.Colour(Band);
}

/// <summary>
/// The label above one option of a choice, as a note (Ali, 2026-10-11: "ni sexy, ni ergonomique… les couleurs, les notes"):
/// for a minion or a spell, each line a pill of its role's colour, read without reading (<see cref="ChoiceRole"/>), then the
/// guide's name in its target's colour; for a trinket, its placement large in the colour of its band among the trinkets of
/// the player's bracket (<see cref="PlacementGrade"/>), amber with both figures when two sources disagree, and the rest small
/// beside it. The same facts and the same order as <see cref="ChoiceAdvisor.Lines"/>, which still writes HDT's log: only the
/// drawing changes. Built to fit <c>maxChars</c> marker characters, shorter forms last, never cut.
/// </summary>
public sealed class ChoiceNote
{
    /// <summary>A pill's padding, in marker characters.</summary>
    public const int PillExtra = 2;

    /// <summary>The rim of a label that names no target (a pivot, another guide, a card's value, "—").</summary>
    public const string NeutralRim = "#6E6884";

    /// <summary>A guide that is no target, and the counts.</summary>
    public const string White = "#FFFFFF";

    /// <summary>The text on a filled pill.</summary>
    public const string PillInk = "#16131F";

    public const string CoreFill = "#FFC93C";
    public const string SupportFill = "#D5DBE6";
    public const string PivotFill = "#B69CFF";
    public const string BetterFill = "#3DDC84";
    public const string WorseFill = "#FF6B6B";

    /// <summary>The outline of the "core" pill of another guide, and its text.</summary>
    public const string GuideOutline = "#9AA0AA";

    private ChoiceNote(string rim, IReadOnlyList<ChoiceNoteLine> lines, TrinketFigure? trinket)
    {
        Rim = rim;
        Lines = lines;
        Trinket = trinket;
    }

    /// <summary>The label's rim and the tint of its plate: the target's colour, neutral, or the trinket's band.</summary>
    public string Rim { get; }

    /// <summary>The lines of a minion or a spell; empty for a trinket.</summary>
    public IReadOnlyList<ChoiceNoteLine> Lines { get; }

    /// <summary>A trinket's note; null for a minion or a spell.</summary>
    public TrinketFigure? Trinket { get; }

    public static string? PillFill(ChoiceRole role) => role switch
    {
        ChoiceRole.Core => CoreFill,
        ChoiceRole.Support or ChoiceRole.Boards => SupportFill,
        ChoiceRole.Pivot => PivotFill,
        ChoiceRole.CardBetter => BetterFill,
        ChoiceRole.CardWorse => WorseFill,
        _ => null,
    };

    /// <param name="maxChars">The label's width in marker characters (<see cref="MarkerText.MaxChars"/>), as for the lines of before.</param>
    /// <param name="reference">The trinkets of the player's bracket, to grade a trinket; null: not graded.</param>
    public static ChoiceNote For(OptionAdvice option, int maxChars, bool statsLoaded, int maxLines = 2, TrinketReference? reference = null)
    {
        if (option.Trinket is { } note)
        {
            var figure = TrinketOf(note, option.Option.CardId, maxChars, statsLoaded, maxLines, reference);
            return new ChoiceNote(figure.Figure == null ? NeutralRim : figure.Colour, Array.Empty<ChoiceNoteLine>(), figure);
        }

        ChoiceNoteLine EffectLine(GuideCardEffect e)
        {
            var suffix = option.BoardsFor(e.Target)?.Card.Text is { } boards ? "· " + boards : null;
            if (!e.IsCore)
            {
                return Fit(ChoiceRole.Support, "+", e.Target.Guide.Name, string.Empty, suffix, null, e.Target.Colour, maxChars);
            }

            var count = e.CoreAfter > e.CoreBefore
                ? $"{e.CoreBefore}/{e.CoreTotal}→{e.CoreAfter}/{e.CoreTotal}"
                : $"{e.CoreBefore}/{e.CoreTotal} copy";
            return Fit(ChoiceRole.Core, "★ core", e.Target.Guide.Name, count, suffix, null, e.Target.Colour, maxChars);
        }

        var lines = option.Reason switch
        {
            ChoiceReason.Target => option.Effects.Select(EffectLine).ToList(),
            ChoiceReason.TopBoards => option.TopBoards.Select(b => Fit(ChoiceRole.Boards, "+", b.Target.Guide.Name, b.Card.Text!, null, null, b.Target.Colour, maxChars)).ToList(),
            ChoiceReason.Pivot => option.Pivots.Select(p => Fit(ChoiceRole.Pivot, "pivot →", p.To.Name, string.Empty, null, p.To.TierLetter, White, maxChars)).ToList(),
            ChoiceReason.Guide => option.Guides.Select(g => Fit(ChoiceRole.Guide, "core", g.Name, string.Empty, null, g.TierLetter, White, maxChars)).ToList(),
            ChoiceReason.CardValue when CardLine(option.CardValue, maxChars) is { } value => new List<ChoiceNoteLine> { value },
            _ => new List<ChoiceNoteLine>(),
        };
        if (lines.Count == 0)
        {
            lines.Add(new ChoiceNoteLine(ChoiceRole.Nothing, string.Empty, string.Empty, string.Empty, "—", null, White));
        }
        else if (lines.Count > maxLines)
        {
            var left = lines.Count - (maxLines - 1);
            var more = $"+{left} more";
            lines = lines.Take(maxLines - 1).ToList();
            lines.Add(new ChoiceNoteLine(ChoiceRole.More, string.Empty, string.Empty, string.Empty,
                MarkerText.DisplayLength(more) <= maxChars ? more : $"+{left}", null, White));
        }

        return new ChoiceNote(option.Colour ?? NeutralRim, lines, null);
    }

    /// <summary>
    /// The first form that fits: every form of the name with the suffix, then without it, then without the name — as
    /// <see cref="MarkerText.LabelWithSuffix"/> chooses, the pill's padding counted.
    /// </summary>
    private static ChoiceNoteLine Fit(ChoiceRole role, string pill, string name, string count, string? suffix, string? tier, string colour, int maxChars)
    {
        var forms = MarkerText.NameForms(name);
        var candidates = new List<ChoiceNoteLine>();
        if (!string.IsNullOrEmpty(suffix))
        {
            candidates.AddRange(forms.Select(n => new ChoiceNoteLine(role, pill, n, count, suffix!, tier, colour)));
        }

        candidates.AddRange(forms.Select(n => new ChoiceNoteLine(role, pill, n, count, string.Empty, tier, colour)));
        candidates.Add(new ChoiceNoteLine(role, pill, string.Empty, count, string.Empty, tier, colour));
        return candidates.FirstOrDefault(c => c.Width <= maxChars) ?? candidates[candidates.Count - 1];
    }

    /// <summary>"t6 ▲ 3.6 vs 3.9", or "t6 ▲ 3.6" when the comparison does not fit (<see cref="CardTurnValue.Label"/>); null when neither does.</summary>
    private static ChoiceNoteLine? CardLine(CardTurnNote? note, int maxChars)
    {
        if (note == null)
        {
            return null;
        }

        var inv = CultureInfo.InvariantCulture;
        var better = note.Verdict == CardTurnVerdict.Better;
        var role = better ? ChoiceRole.CardBetter : ChoiceRole.CardWorse;
        var pill = $"t{note.Turn.ToString(inv)} {(better ? "▲" : "▼")}";
        var placement = note.Placement.ToString("0.0", inv);
        var full = new ChoiceNoteLine(role, pill, string.Empty, placement, "vs " + note.TurnAverage.ToString("0.0", inv), null, White);
        var brief = new ChoiceNoteLine(role, pill, string.Empty, placement, string.Empty, null, White);
        return full.Width <= maxChars ? full : brief.Width <= maxChars ? brief : null;
    }

    /// <summary>
    /// A trinket's figure and its small lines, by the case <see cref="ChoiceAdvisor.Lines"/> tells apart: no stats; a contest
    /// (both figures, "contested", the two sources); a placement adjusted to a target ("≈3.41", "avg 3.71", "≈ Target" in
    /// its colour); a placement ("3.66", "avg place · 44% picked", and the sources that agree when the label has a second line).
    /// </summary>
    private static TrinketFigure TrinketOf(TrinketNote note, string cardId, int maxChars, bool statsLoaded, int maxLines, TrinketReference? reference)
    {
        var inv = CultureInfo.InvariantCulture;
        if (note.Placement is not { } placement)
        {
            return new TrinketFigure(null, PlacementBand.None, false, new[] { new HeroNoteLine(statsLoaded ? "no data" : "loading…", HeroNote.Missing) });
        }

        PlacementBand Grade(double shown) => reference == null ? PlacementBand.None : PlacementGrade.Of(shown, reference.Mean, reference.GamesOf(cardId));

        if (HeroConsensus.For(note.Consolidated) is { Contested: true } contest)
        {
            // Both figures, never one of them alone: "3.3 ↔ 4.1", or without its spaces where the sources would not fit.
            var compact = contest.Note.Replace(" ↔ ", "↔");
            var figure = MarkerText.DisplayLength(contest.Sources) <= DetailChars(maxChars, contest.Note) ? contest.Note : compact;
            return new TrinketFigure(figure, PlacementBand.None, true,
                new[] { new HeroNoteLine("contested", HeroNote.Contested), new HeroNoteLine(contest.Sources, HeroNote.Secondary) });
        }

        if (note.Adjustment > 0 && note.JustifiedBy != null)
        {
            var adjusted = note.Adjusted!.Value;
            var figure = "≈" + adjusted.ToString("0.00", inv);
            var chars = DetailChars(maxChars, figure);
            return new TrinketFigure(figure, Grade(adjusted), false, new[]
            {
                new HeroNoteLine("avg " + placement.ToString("0.00", inv), HeroNote.Secondary),
                new HeroNoteLine(MarkerText.Label("≈", note.JustifiedBy.Guide.Name, string.Empty, chars), note.JustifiedBy.Colour),
            });
        }

        var plain = placement.ToString("0.00", inv);
        var room = DetailChars(maxChars, plain);
        var pick = note.PickRate is { } rate ? (rate * 100).ToString("0", inv) + "% picked" : null;
        var forms = pick != null ? new[] { "avg place · " + pick, "avg · " + pick, pick } : new[] { "avg place", "avg" };
        var details = new List<HeroNoteLine> { new(forms.FirstOrDefault(f => MarkerText.DisplayLength(f) <= room) ?? forms[forms.Length - 1], HeroNote.Secondary) };
        if (note.OthersAgreeing is { Length: > 0 } others && maxLines > 1)
        {
            // "nomi.gg 3.75 (55)", without the games where that is too long.
            var shorter = Regex.Replace(others, @" \(\d+\)", string.Empty);
            details.Add(new HeroNoteLine(MarkerText.DisplayLength(others) <= room ? others : shorter, HeroNote.Secondary));
        }

        return new TrinketFigure(plain, Grade(placement), false, details);
    }

    /// <summary>
    /// Small characters (<see cref="PanelTypography.Small"/>) left beside a figure (<see cref="PanelTypography.ChoiceNote"/>)
    /// and its gap (a marker character) in a label <paramref name="maxChars"/> marker characters wide.
    /// </summary>
    public static int DetailChars(int maxChars, string figure) =>
        (int)Math.Floor((maxChars * PanelTypography.Marker - MarkerText.DisplayLength(figure) * PanelTypography.ChoiceNote - PanelTypography.Marker) / PanelTypography.Small);
}
