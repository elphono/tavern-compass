using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

public enum HighlightKind
{
    None,
    Enabler,
    Commit,
}

/// <summary>What one of Bob's cards is for the compositions aimed at: a key piece to commit on, an early enabler, or nothing.</summary>
public sealed class TavernHighlight
{
    public static readonly TavernHighlight None = new(HighlightKind.None, null, null);

    public TavernHighlight(HighlightKind kind, Composition? composition, string? colour)
    {
        Kind = kind;
        Composition = composition;
        Colour = colour;
    }

    public HighlightKind Kind { get; }
    public Composition? Composition { get; }

    /// <summary>The frame and label colour: the composition's palette colour when ticked, else <see cref="TavernHighlights.SuggestionColour"/>.</summary>
    public string? Colour { get; }

    /// <summary>"commit" or "enabler", the word the tavern label starts with; empty for none.</summary>
    public string Tag => Kind switch
    {
        HighlightKind.Commit => "commit",
        HighlightKind.Enabler => "enabler",
        _ => string.Empty,
    };
}

/// <summary>
/// On Bob's cards, the ones that matter for the compositions aimed at (Ali, 2026-09-27: "highlight une carte
/// dans la taverne qui est présente dans les when to commit ou early enablers"). Same content as HSReplay
/// Tier7's hover section "Comps – Enabler / Commit Piece" (HDT BattlegroundsMinionPinningCard.xaml:87-100), but
/// drawn on the card: a frame and a short label merged into the tavern marker.
/// Rules: the ticked compositions come first; with none ticked, the suggestions shown in the panel. A card that
/// is a key piece ("When to commit") of one composition and an early enabler of another is a commit piece:
/// commit wins. Among compositions of the same kind, the first in the given order wins.
/// </summary>
public static class TavernHighlights
{
    /// <summary>Suggestions have no palette colour: one vivid neutral for all of them, the markers' own white.</summary>
    public const string SuggestionColour = CompositionSelection.AutoColour;

    /// <param name="bobCards">Bob's row, left to right (the tavern spell included: it matches nothing).</param>
    /// <param name="ticked">Compositions ticked by the player, in the panel's order.</param>
    /// <param name="suggestions">Suggestions shown in the panel, in its order; used only when nothing is ticked.</param>
    /// <param name="detailFor">A composition's derived detail (CompDetail); null when it cannot be computed.</param>
    /// <param name="colourOf">A ticked composition's palette colour (CompositionSelection.ColourOf).</param>
    public static IReadOnlyList<TavernHighlight> For(IReadOnlyList<string> bobCards, IReadOnlyList<Composition> ticked, IReadOnlyList<Composition> suggestions,
        Func<Composition, CompDetail?> detailFor, Func<string, string?> colourOf)
    {
        var fromTicked = ticked.Count > 0;
        var aimed = (fromTicked ? ticked : suggestions)
            .Select(c => (Composition: c, Detail: detailFor(c)))
            .Where(x => x.Detail != null)
            .ToList();
        string Colour(Composition c) => fromTicked ? colourOf(c.Id) ?? SuggestionColour : SuggestionColour;

        return bobCards.Select(card =>
        {
            var id = CardIds.Normalize(card);
            foreach (var (composition, detail) in aimed)
            {
                if (detail!.CommitCards.Any(c => c.CardId == id))
                {
                    return new TavernHighlight(HighlightKind.Commit, composition, Colour(composition));
                }
            }

            foreach (var (composition, detail) in aimed)
            {
                if (detail!.EarlyEnablers.Any(c => c.CardId == id))
                {
                    return new TavernHighlight(HighlightKind.Enabler, composition, Colour(composition));
                }
            }

            return TavernHighlight.None;
        }).ToList();
    }

    /// <summary>
    /// The marker lines under one of Bob's cards, at most two: "◆ pinned" first when pinned, then the highlight
    /// ("commit UD 1/2" with the key pieces held, "enabler UD"), then the card's other compositions as before
    /// ("★ Beasts 1/2", "+ Mechs 0/2"), the highlighted composition never twice, "+2 more" when they do not fit.
    /// </summary>
    /// <param name="advances">The card's compositions (TavernAdvice): each composition, and whether the card is one of its key pieces.</param>
    /// <param name="keyOwned">How many of a composition's key pieces the player holds.</param>
    public static IReadOnlyList<string> MarkerLines(TavernHighlight highlight, IReadOnlyList<(Composition Composition, bool IsKeyPiece)> advances,
        Func<Composition, int> keyOwned, bool pinned, int maxChars, int maxLines = 2)
    {
        var lines = new List<string>();
        if (pinned)
        {
            lines.Add("◆ pinned");
        }

        var others = advances.Where(a => highlight.Kind == HighlightKind.None || a.Composition.Id != highlight.Composition!.Id).ToList();
        if (highlight.Kind != HighlightKind.None)
        {
            var composition = highlight.Composition!;
            var count = highlight.Kind == HighlightKind.Commit ? $"{keyOwned(composition)}/{composition.CoreCards.Count}" : string.Empty;
            lines.Add(MarkerText.Label(highlight.Tag, composition.Name, count, maxChars));
        }

        var room = maxLines - lines.Count;
        if (room > 0 && others.Count > 0)
        {
            lines.AddRange(MarkerText.Lines(others.Select(a => (a.Composition.Name, keyOwned(a.Composition), a.Composition.CoreCards.Count, a.IsKeyPiece)).ToList(), maxChars, room));
        }

        return lines.Take(maxLines).ToList();
    }
}
