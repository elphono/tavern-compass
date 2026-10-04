using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// What a card is to a comp guide, as HDT's guides list it: a core card ("core_cards"), a common enabler ("common_enablers")
/// or an add-on ("addon_cards"). The lists are kept apart everywhere, never summed into one: HDT never adds core cards and
/// add-ons together, and a "✓ 6 cards" summing them read as a composition of more than seven cards (2026-10-04).
/// </summary>
public enum GuideCardRole
{
    Core,
    Enabler,
    Addon,
}

/// <summary>What one card does for one target.</summary>
public sealed class GuideCardEffect
{
    public GuideCardEffect(CompTarget target, GuideCardRole role, int coreBefore, int coreAfter)
    {
        Target = target;
        Role = role;
        CoreBefore = coreBefore;
        CoreAfter = coreAfter;
    }

    public CompTarget Target { get; }
    public GuideCardRole Role { get; }
    public bool IsCore => Role == GuideCardRole.Core;

    /// <summary>Core cards of the target held now, and once the card is taken (a copy of a held core card adds none).</summary>
    public int CoreBefore { get; }

    public int CoreAfter { get; }

    /// <summary>The target's core cards (only core cards: add-ons and enablers are never counted in).</summary>
    public int CoreTotal => Target.Guide.CoreCards.Count;
}

/// <summary>
/// The one engine that says what a card does for the targets: the frames on Bob's cards (<see cref="TavernHighlights"/>)
/// and the labels of choices (<see cref="ChoiceAdvisor"/>) both ask it, so that they can never disagree.
/// </summary>
public static class GuideCardEffects
{
    /// <summary>
    /// The card's role in a guide, the strongest when it is listed twice: core, then enabler, then add-on (the order of
    /// <see cref="CompGuideMatch"/>'s weights); null when the guide does not name it. <paramref name="cardId"/> is a base id.
    /// </summary>
    public static GuideCardRole? RoleIn(CompGuide guide, string cardId) =>
        guide.CoreCards.Contains(cardId) ? GuideCardRole.Core
        : guide.Enablers.Contains(cardId) ? GuideCardRole.Enabler
        : guide.AddonCards.Contains(cardId) ? GuideCardRole.Addon
        : null;

    /// <summary>
    /// The targets a card matters to: first those it is a core card of, then those it is an enabler or an add-on of; in
    /// target order (<see cref="CompTarget.Rank"/>) within each. The first one is the card's highlight; a golden copy counts
    /// as its base card.
    /// </summary>
    /// <param name="held">
    /// Base card ids the player holds, to count the core cards held; null: each target's own count
    /// (<see cref="CompGuideProgress.KeyHeld"/>, from the board and hand it was ranked on).
    /// </param>
    public static IReadOnlyList<GuideCardEffect> On(string rawCardId, IReadOnlyList<CompTarget> targets, IReadOnlyCollection<string>? held = null)
    {
        var cardId = CardIds.Normalize(rawCardId);
        var effects = new List<GuideCardEffect>();
        foreach (var target in targets)
        {
            if (RoleIn(target.Guide, cardId) is not { } role)
            {
                continue;
            }

            var heldCore = held == null
                ? target.Progress.KeyHeld
                : target.Guide.CoreCards.Where(held.Contains).ToList();
            var before = heldCore.Count;
            var after = role == GuideCardRole.Core && !heldCore.Contains(cardId) ? before + 1 : before;
            effects.Add(new GuideCardEffect(target, role, before, after));
        }

        return effects
            .OrderBy(e => e.IsCore ? 0 : 1)
            .ThenBy(e => e.Target.Rank)
            .ToList();
    }
}
