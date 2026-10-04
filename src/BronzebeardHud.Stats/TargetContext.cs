using System.Collections.Generic;
using System.Globalization;

namespace BronzebeardHud.Stats;

/// <summary>
/// One short line of Firestone context for a comp guide bridged to a Firestone comp (<see cref="GuideBridge"/>), for its
/// detail or its tooltip: "≈ 3,5 with your hero (23) · final turn ≈ 14 · 5 top boards". The hero's estimate is
/// <see cref="HeroCompAffinity"/>'s, with its number of games (small samples are its stated caveat); the final turn is
/// <see cref="CompDetail.MedianFinalTurn"/>; the boards are the comp's final boards. Never the comp's own average placement
/// (Ali set it aside). Each piece is left out when it is missing.
/// </summary>
public static class TargetContext
{
    /// <param name="guide">Any guide of HDT's list: a target, or one whose detail is open.</param>
    /// <param name="bridge">Guide id → its Firestone comp (<see cref="GuideBridge.For"/>); null: no line.</param>
    /// <param name="heroEffects">Comp id → the hero's estimate (<see cref="HeroCompAffinity.Effects"/>); null: no hero piece.</param>
    /// <returns>The line; null when the guide has no bridge or nothing to say.</returns>
    public static string? For(CompGuide guide, IReadOnlyDictionary<string, GuideEvidence>? bridge, IReadOnlyDictionary<string, HeroCompPick>? heroEffects = null)
    {
        if (bridge == null || !bridge.TryGetValue(guide.Id, out var evidence))
        {
            return null;
        }

        var comp = evidence.Composition;
        var parts = new List<string>();
        if (heroEffects != null && heroEffects.TryGetValue(comp.Id, out var pick))
        {
            parts.Add(pick.ShopText);
        }

        if (CompDetail.MedianFinalTurn(comp) is { } turn)
        {
            parts.Add(CompDetail.FinalTurnText(turn));
        }

        var boards = comp.FinalBoards.Count;
        if (boards > 0)
        {
            parts.Add($"{boards.ToString(CultureInfo.InvariantCulture)} top board{(boards > 1 ? "s" : string.Empty)}");
        }

        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }
}
