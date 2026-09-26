using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Short labels that fit a marker of known width, instead of text cut by the renderer. Ali saw
/// "★ Undead Butcher · + Murloc Sc…" cut on 2026-09-26: the label is now built to fit, shortest form last.
/// </summary>
public static class MarkerText
{
    /// <summary>
    /// Average advance of a bold UI glyph, in em. Deliberately generous (Segoe UI bold averages about
    /// 0.55), so the estimate errs on the short side; the plugin also shrinks, never clips, as a safety net.
    /// </summary>
    public const double GlyphWidthEm = 0.62;

    /// <summary>How many characters fit on one line of the marker.</summary>
    public static int MaxChars(double markerWidth, double fontSize, double horizontalPadding) =>
        Math.Max(3, (int)Math.Floor((markerWidth - 2 * horizontalPadding) / (fontSize * GlyphWidthEm)));

    /// <summary>Length on screen, in average glyphs: the star is about two glyphs wide.</summary>
    public static int DisplayLength(string text) => text.Length + text.Count(c => c == '★');

    /// <summary>
    /// "★ Undead Butcher 2/5" (key piece; "+" for an add-on; 2 of the 5 key pieces held), shortened until
    /// it fits <paramref name="maxChars"/>: full name, then initials for every word but the last
    /// ("★ U. Butcher 2/5"), then initials only ("★ UB 2/5"), then mark and count ("★ 2/5", then "★2/5"), then cut.
    /// </summary>
    public static string Label(string compositionName, int owned, int total, bool isKeyPiece, int maxChars) =>
        Label(isKeyPiece ? "★" : "+", compositionName, $"{owned}/{total}", maxChars);

    /// <summary>
    /// The same shortening for any mark and count: "★ Undead Butcher 2/5→3/5" (a choice), "≈ Undead Butcher"
    /// (no count). Parts left empty are skipped.
    /// </summary>
    public static string Label(string mark, string compositionName, string count, int maxChars)
    {
        string Join(params string[] parts) => string.Join(" ", parts.Where(p => p.Length > 0));
        var words = compositionName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var candidates = new List<string> { Join(mark, compositionName, count) };
        if (words.Length > 1)
        {
            candidates.Add(Join(mark, string.Join(" ", words.Take(words.Length - 1).Select(w => char.ToUpperInvariant(w[0]) + ".")) + " " + words[words.Length - 1], count));
        }

        if (words.Length > 0)
        {
            candidates.Add(Join(mark, string.Concat(words.Select(w => char.ToUpperInvariant(w[0]))), count));
        }

        candidates.Add(Join(mark, count));
        candidates.Add(mark + count);
        var fitting = candidates.FirstOrDefault(c => DisplayLength(c) <= maxChars);
        if (fitting != null)
        {
            return fitting;
        }

        var shortest = candidates[candidates.Count - 1];
        while (shortest.Length > 0 && DisplayLength(shortest) > maxChars)
        {
            shortest = shortest.Substring(0, shortest.Length - 1);
        }

        return shortest;
    }

    /// <summary>
    /// The lines of one tavern marker: one per composition, at most <paramref name="maxLines"/>; when more
    /// compositions share the card, the last line says how many are left ("+3 more").
    /// </summary>
    public static IReadOnlyList<string> Lines(
        IReadOnlyList<(string Name, int Owned, int Total, bool IsKeyPiece)> compositions, int maxChars, int maxLines = 2)
    {
        if (compositions.Count <= maxLines)
        {
            return compositions.Select(c => Label(c.Name, c.Owned, c.Total, c.IsKeyPiece, maxChars)).ToList();
        }

        var lines = compositions.Take(maxLines - 1).Select(c => Label(c.Name, c.Owned, c.Total, c.IsKeyPiece, maxChars)).ToList();
        var more = $"+{compositions.Count - (maxLines - 1)} more";
        lines.Add(DisplayLength(more) <= maxChars ? more : $"+{compositions.Count - (maxLines - 1)}");
        return lines;
    }
}
