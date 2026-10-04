using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

public enum HighlightKind
{
    None,

    /// <summary>An enabler or an add-on of a target: dotted frame.</summary>
    Enabler,

    /// <summary>A core card of a target: solid frame.</summary>
    Commit,
}

/// <summary>What one of Bob's cards is for the targets: a core card, an enabler or add-on, or nothing.</summary>
public sealed class TavernHighlight
{
    public static readonly TavernHighlight None = new(HighlightKind.None, null, Array.Empty<GuideCardEffect>());

    public TavernHighlight(HighlightKind kind, GuideCardEffect? effect, IReadOnlyList<GuideCardEffect> others)
    {
        Kind = kind;
        Effect = effect;
        Others = others;
    }

    public HighlightKind Kind { get; }

    /// <summary>What the card does for the target that wins it; null for none.</summary>
    public GuideCardEffect? Effect { get; }

    public CompTarget? Target => Effect?.Target;

    /// <summary>The frame and label colour: the winning target's (<see cref="CompTarget.Colour"/>); null for none.</summary>
    public string? Colour => Target?.Colour;

    /// <summary>The other targets the card matters to, in <see cref="GuideCardEffects.On"/>'s order: listed under the label.</summary>
    public IReadOnlyList<GuideCardEffect> Others { get; }

    /// <summary>The word the tavern label starts with: "core", "enabler", or "+" for an add-on; empty for none.</summary>
    public string Tag => Effect?.Role switch
    {
        GuideCardRole.Core => "core",
        GuideCardRole.Enabler => "enabler",
        GuideCardRole.Addon => "+",
        _ => string.Empty,
    };
}

/// <summary>
/// On Bob's cards, the ones that matter for the targets (Ali, 2026-09-27: "highlight une carte dans la taverne qui est
/// présente dans les when to commit ou early enablers"), drawn on the card: a frame and a short label merged into the
/// tavern marker. A core card of any target is a commit piece (solid frame), even when it is only an enabler of an earlier
/// target; otherwise an enabler or add-on of a target is an enabler (dotted frame). Among targets of the same kind, the
/// first in target order wins, and gives its colour.
/// </summary>
public static class TavernHighlights
{
    /// <param name="bobCards">Bob's row, left to right (the tavern spell included: it matches nothing).</param>
    /// <param name="targets">The targets, in their order (<see cref="CompTargetTracker.Next"/>).</param>
    public static IReadOnlyList<TavernHighlight> For(IReadOnlyList<string> bobCards, IReadOnlyList<CompTarget> targets) =>
        bobCards.Select(card =>
        {
            var effects = GuideCardEffects.On(card, targets);
            if (effects.Count == 0)
            {
                return TavernHighlight.None;
            }

            var first = effects[0];
            return new TavernHighlight(first.IsCore ? HighlightKind.Commit : HighlightKind.Enabler, first, effects.Skip(1).ToList());
        }).ToList();

    /// <summary>
    /// The marker lines under one of Bob's cards, at most <paramref name="maxLines"/>: "◆ pinned" first when pinned, then
    /// the highlight ("core Undead Butcher 1/3" with the core cards held, "enabler Undead Butcher", "+ Undead Butcher" for
    /// an add-on), then the other targets the card matters to ("★ Beasts 1/2" for a core card, "+ Mechs 0/2" otherwise;
    /// the count is always core cards), "+2 more" when they do not fit. Every line is built to fit <paramref name="maxChars"/>.
    /// </summary>
    public static IReadOnlyList<string> MarkerLines(TavernHighlight highlight, bool pinned, int maxChars, int maxLines = 2)
    {
        var lines = new List<string>();
        if (pinned)
        {
            lines.Add("◆ pinned");
        }

        if (highlight.Effect is { } effect)
        {
            var count = effect.IsCore ? $"{effect.CoreBefore}/{effect.CoreTotal}" : string.Empty;
            lines.Add(MarkerText.Label(highlight.Tag, effect.Target.Guide.Name, count, maxChars));
        }

        var room = maxLines - lines.Count;
        if (room > 0 && highlight.Others.Count > 0)
        {
            lines.AddRange(MarkerText.Lines(highlight.Others.Select(e => (e.Target.Guide.Name, e.CoreBefore, e.CoreTotal, e.IsCore)).ToList(), maxChars, room));
        }

        return lines.Take(maxLines).ToList();
    }
}
