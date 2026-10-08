using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

public enum HighlightKind
{
    None,

    /// <summary>An enabler or an add-on of a target, or a card on the top boards of a target's bridged comp: dotted frame.</summary>
    Enabler,

    /// <summary>A core card of a target: solid frame.</summary>
    Commit,
}

/// <summary>What one of Bob's cards is for the targets: a core card, an enabler or add-on, a top-board card, or nothing.</summary>
public sealed class TavernHighlight
{
    public static readonly TavernHighlight None = new(HighlightKind.None, null, Array.Empty<GuideCardEffect>());

    /// <param name="boards">For a card no target's guide lists: what the top boards of a target's bridged comp say of it; null otherwise.</param>
    public TavernHighlight(HighlightKind kind, GuideCardEffect? effect, IReadOnlyList<GuideCardEffect> others, BoardEvidence? boards = null)
    {
        Kind = kind;
        Effect = effect;
        Others = others;
        Boards = boards;
    }

    public HighlightKind Kind { get; }

    /// <summary>What the card does for the target that wins it; null for none and for a top-board card.</summary>
    public GuideCardEffect? Effect { get; }

    /// <summary>
    /// When no target's guide lists the card: the first target (in target order) whose bridged comp has it on at least
    /// <see cref="CardEvidence.MinimumBoards"/> of its final boards; null otherwise.
    /// </summary>
    public BoardEvidence? Boards { get; }

    public CompTarget? Target => Effect?.Target ?? Boards?.Target;

    /// <summary>The frame and label colour: the winning target's (<see cref="CompTarget.Colour"/>); null for none.</summary>
    public string? Colour => Target?.Colour;

    /// <summary>The other targets the card matters to, in <see cref="GuideCardEffects.On"/>'s order: listed under the label.</summary>
    public IReadOnlyList<GuideCardEffect> Others { get; }

    /// <summary>The word the tavern label starts with: "core", "enabler", or "+" for an add-on or a top-board card; empty for none.</summary>
    public string Tag => Effect?.Role switch
    {
        GuideCardRole.Core => "core",
        GuideCardRole.Enabler => "enabler",
        GuideCardRole.Addon => "+",
        _ => Boards != null ? "+" : string.Empty,
    };
}

/// <summary>
/// On Bob's cards, the ones that matter for the targets (Ali, 2026-09-27: "highlight une carte dans la taverne qui est
/// présente dans les when to commit ou early enablers"), drawn on the card: a frame and a short label merged into the
/// tavern marker. A core card of any target is a commit piece (solid frame), even when it is only an enabler of an earlier
/// target; otherwise an enabler or add-on of a target is an enabler (dotted frame). Among targets of the same kind, the
/// first in target order wins, and gives its colour. Last, with a bridge (<see cref="GuideBridge"/>), a card that no
/// target's guide lists but that stands on at least <see cref="CardEvidence.MinimumBoards"/> of the final boards of a
/// target's bridged comp is dotted too, "+ Undead Butcher 3/5" (the first such target in target order). With a guide ticked,
/// the ticked guides alone frame (<see cref="Framing"/>).
/// </summary>
public static class TavernHighlights
{
    /// <param name="bobCards">Bob's row, left to right (the tavern spell included: it matches nothing).</param>
    /// <param name="targets">The targets, in their order (<see cref="CompTargetTracker.Next"/>); narrowed by <see cref="Framing"/>.</param>
    /// <param name="bridge">Guide id → its Firestone comp (<see cref="GuideBridge.For"/>); null: the guides alone, as before the bridge.</param>
    public static IReadOnlyList<TavernHighlight> For(IReadOnlyList<string> bobCards, IReadOnlyList<CompTarget> targets,
        IReadOnlyDictionary<string, GuideEvidence>? bridge = null)
    {
        var framing = Framing(targets);
        return bobCards.Select(card =>
        {
            var effects = GuideCardEffects.On(card, framing);
            if (effects.Count > 0)
            {
                var first = effects[0];
                return new TavernHighlight(first.IsCore ? HighlightKind.Commit : HighlightKind.Enabler, first, effects.Skip(1).ToList());
            }

            var top = BoardEvidence.For(card, framing, bridge).FirstOrDefault(b => b.Card.IsTop);
            return top != null
                ? new TavernHighlight(HighlightKind.Enabler, null, Array.Empty<GuideCardEffect>(), top)
                : TavernHighlight.None;
        }).ToList();
    }

    /// <summary>
    /// The marker lines under one of Bob's cards, at most <paramref name="maxLines"/>: "◆ pinned" first when pinned, then
    /// the highlight ("core Undead Butcher 1/3" with the core cards held, "enabler Undead Butcher", "+ Undead Butcher" for
    /// an add-on, "+ Undead Butcher 3/5" for a card on 3 of the 5 top boards of its bridged comp), then the other targets the
    /// card matters to ("★ Beasts 1/2" for a core card, "+ Mechs 0/2" otherwise; the count is always core cards), "+2 more"
    /// when they do not fit; last, the card's value at this turn (<paramref name="value"/>, CardTurnValue.Label) if a line is
    /// left: it never pushes a role out. Every line is built to fit <paramref name="maxChars"/>.
    /// </summary>
    public static IReadOnlyList<string> MarkerLines(TavernHighlight highlight, bool pinned, int maxChars, int maxLines = 2, string? value = null)
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
        else if (highlight.Boards is { } boards)
        {
            lines.Add(MarkerText.Label(highlight.Tag, boards.Target.Guide.Name, boards.Card.Count ?? string.Empty, maxChars));
        }

        var room = maxLines - lines.Count;
        if (room > 0 && highlight.Others.Count > 0)
        {
            lines.AddRange(MarkerText.Lines(highlight.Others.Select(e => (e.Target.Guide.Name, e.CoreBefore, e.CoreTotal, e.IsCore)).ToList(), maxChars, room));
        }

        if (value != null && lines.Count < maxLines)
        {
            lines.Add(value);
        }

        return lines.Take(maxLines).ToList();
    }

    /// <summary>
    /// The targets whose cards Bob's row frames. With a guide ticked, the ticked guides alone (Ali, 2026-10-07: "quand on
    /// sélectionne des compos vers lesquelles on veut tendre, on ne devrait plus surligner aucun autre sbire dans le shop"):
    /// a guide in progress stays a target, listed in the panel, but neither its cards nor its bridged comp's top boards are
    /// framed, nor named under a ticked guide's label. Nothing ticked: every target, as before. The choice labels
    /// (<see cref="ChoiceAdvisor"/>) keep every target.
    /// </summary>
    public static IReadOnlyList<CompTarget> Framing(IReadOnlyList<CompTarget> targets) =>
        targets.Any(t => t.Ticked) ? targets.Where(t => t.Ticked).ToList() : targets;

    /// <summary>
    /// The line written in HDT's log when the highlights change: "Bronzebeard HUD: tavern highlights=[<paramref name="summary"/>]
    /// targets=[…]", then, when a guide is ticked, the targets that frame (<see cref="Framing"/>): "frames from ticked=[…]".
    /// </summary>
    /// <param name="summary">The highlights, <see cref="Summary"/>.</param>
    public static string LogLine(string summary, IReadOnlyList<CompTarget> targets)
    {
        var framing = Framing(targets);
        var from = framing.Count < targets.Count ? $" frames from ticked={CompTargets.Summary(framing)}" : string.Empty;
        return $"Bronzebeard HUD: tavern highlights=[{summary}] targets={CompTargets.Summary(targets)}{from}";
    }

    /// <summary>
    /// The highlighted cards for HDT's log, "card:role:guide id" as the plugin writes them ("U2:core:Undead Butcher/11"),
    /// and "card:boards k/n:guide id" for a top-board card ("TOP:boards 3/5:Undead Butcher/11"); the others left out.
    /// </summary>
    public static string Summary(IReadOnlyList<string> bobCards, IReadOnlyList<TavernHighlight> highlights) =>
        string.Join(",", bobCards.Zip(highlights, (card, h) => (card, h))
            .Where(x => x.h.Target != null)
            .Select(x => x.h.Effect is { } effect
                ? $"{x.card}:{effect.Role.ToString().ToLowerInvariant()}:{effect.Target.Guide.Id}"
                : $"{x.card}:boards {x.h.Boards!.Card.Count}:{x.h.Boards.Target.Guide.Id}"));
}
