using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// One of the comp guides the plugin aims at: drawn in its colour in the panel, and the only guides the frames on Bob's
/// cards (<see cref="TavernHighlights"/>) and the labels of choices (<see cref="ChoiceAdvisor"/>) speak for.
/// </summary>
public sealed class CompTarget
{
    public CompTarget(CompGuideProgress progress, int rank, string colour, bool ticked)
    {
        Progress = progress;
        Rank = rank;
        Colour = colour;
        Ticked = ticked;
    }

    public CompGuide Guide => Progress.Guide;

    /// <summary>Where the player stands on the guide: core cards, enablers and add-ons held, kept apart (never one mixed list).</summary>
    public CompGuideProgress Progress { get; }

    /// <summary>
    /// 1 for the first target, then 2, 3, 4: the ticked guides first, in the order they were ticked, then the most
    /// probable ones (<see cref="CompGuideMatch"/>). Rank 1 is the guide being played (a trinket's full adjustment).
    /// </summary>
    public int Rank { get; }

    /// <summary>"#RRGGBB", one of <see cref="CompTargetTracker.Palette"/>; kept while the guide stays a target.</summary>
    public string Colour { get; }

    /// <summary>Ticked by the player: a target whatever its score.</summary>
    public bool Ticked { get; }
}

/// <summary>Which guides are targets, before any colour: the pure part of <see cref="CompTargetTracker.Next"/>.</summary>
public static class CompTargets
{
    /// <summary>
    /// The ticked guides first (those the board knows, in the order of <paramref name="ticked"/>, at most
    /// <see cref="CompTargetTracker.MaxTicked"/>), then the most probable guides that are not ticked and score above 0
    /// (<see cref="CompGuideBoard.Ranked"/>), until there are <paramref name="count"/> targets in all. Ticked guides are
    /// targets even beyond <paramref name="count"/>: four ticked guides and a count of 2 give four targets.
    /// </summary>
    /// <param name="ticked">Guide ids (<see cref="CompGuide.Id"/>), in the order they were ticked; an id the board does not know is skipped.</param>
    /// <param name="count">Targets wanted in all, 1 to <see cref="HudSettings.MaxSuggested"/> (brought inside).</param>
    public static IReadOnlyList<(CompGuideProgress Progress, bool Ticked)> Choose(CompGuideBoard board, IReadOnlyList<string> ticked, int count)
    {
        count = Math.Max(HudSettings.MinSuggested, Math.Min(HudSettings.MaxSuggested, count));
        var byId = new Dictionary<string, CompGuideProgress>(StringComparer.Ordinal);
        foreach (var progress in board.All)
        {
            if (!byId.ContainsKey(progress.Guide.Id))
            {
                byId[progress.Guide.Id] = progress;
            }
        }

        var chosen = ticked
            .Distinct(StringComparer.Ordinal)
            .Where(byId.ContainsKey)
            .Take(CompTargetTracker.MaxTicked)
            .Select(id => (Progress: byId[id], Ticked: true))
            .ToList();
        var taken = new HashSet<string>(chosen.Select(c => c.Progress.Guide.Id), StringComparer.Ordinal);
        foreach (var progress in board.Ranked)
        {
            if (chosen.Count >= count)
            {
                break;
            }

            if (progress.Score > 0 && taken.Add(progress.Guide.Id))
            {
                chosen.Add((progress, false));
            }
        }

        return chosen;
    }

    /// <summary>
    /// The panel's tiers with the targets first in their tier (by <see cref="CompTarget.Rank"/>), then the other guides in
    /// HDT's order: a guide ticked but unlikely comes up like an automatic one, and a guide the automatic ranking would
    /// highlight but the ticks pushed out keeps its HDT place.
    /// </summary>
    public static IReadOnlyList<CompGuideBoardTier> Tiers(CompGuideBoard board, IReadOnlyList<CompTarget> targets)
    {
        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            if (!rank.ContainsKey(target.Guide.Id))
            {
                rank[target.Guide.Id] = target.Rank;
            }
        }

        var order = new Dictionary<CompGuideProgress, int>();
        foreach (var progress in board.All)
        {
            order[progress] = order.Count;
        }

        return board.Tiers
            .Select(t => new CompGuideBoardTier(t.Tier, t.Rows
                .OrderBy(p => rank.TryGetValue(p.Guide.Id, out var r) ? r : int.MaxValue)
                .ThenBy(p => order.TryGetValue(p, out var o) ? o : int.MaxValue)
                .ToList()))
            .ToList();
    }

    /// <summary>The target for a guide, or null when it is not one.</summary>
    public static CompTarget? Find(IReadOnlyList<CompTarget> targets, CompGuide guide) =>
        targets.FirstOrDefault(t => t.Guide.Id == guide.Id);

    /// <summary>"[Undead Butcher #FF2BD6; Beast Lobster #B8FF1F]", for the log lines; "none" when there is no target.</summary>
    public static string Summary(IReadOnlyList<CompTarget> targets) =>
        targets.Count == 0 ? "none" : "[" + string.Join("; ", targets.Select(t => $"{t.Guide.Name} {t.Colour}")) + "]";
}

/// <summary>
/// The targets of one game, and their colours. The player ticks guides in the panel (four at most, one colour each);
/// <see cref="Next"/> then picks the targets — the ticked guides, then the most probable ones up to the number chosen
/// in the settings — and gives each a colour of <see cref="Palette"/>: a guide keeps its colour as long as it stays a
/// target from one call to the next, a colour is freed when its guide stops being a target, and a new target takes the
/// first free colour. Ticks and colours are forgotten at the next game (<see cref="BeginGame"/>) or on
/// <see cref="Reset"/>. Not thread-safe: the plugin calls it from HDT's update loop only.
/// </summary>
public sealed class CompTargetTracker
{
    /// <summary>
    /// Four colours that read on the game's dark board and differ from each other and from the orange of the plugin's
    /// borders (#D9480F): magenta, lime, sky blue, white. A violet was tried and was too close to the magenta.
    /// </summary>
    public static readonly IReadOnlyList<string> Palette = new[] { "#FF2BD6", "#B8FF1F", "#2EA8FF", "#FFFFFF" };

    /// <summary>Guides ticked at once, at most: one colour each.</summary>
    public const int MaxTicked = 4;

    private readonly List<string> _ticked = new();
    private Dictionary<string, int> _slots = new(StringComparer.Ordinal);

    /// <summary>The game the ticks and colours belong to; -1 before the first one.</summary>
    public int Game { get; private set; } = -1;

    /// <summary>Ticked guide ids (<see cref="CompGuide.Id"/>), in the order they were ticked.</summary>
    public IReadOnlyList<string> Ticked => _ticked;

    /// <summary>The targets of the last <see cref="Next"/>; empty before it and after a reset.</summary>
    public IReadOnlyList<CompTarget> Targets { get; private set; } = Array.Empty<CompTarget>();

    public bool IsTicked(string guideId) => _ticked.Contains(guideId, StringComparer.Ordinal);

    /// <summary>
    /// Ticks or unticks a guide; false when refused because <see cref="MaxTicked"/> guides already are. The targets change
    /// at the next <see cref="Next"/>: call it again to redraw.
    /// </summary>
    public bool Toggle(string guideId)
    {
        if (_ticked.Remove(guideId))
        {
            return true;
        }

        if (_ticked.Count >= MaxTicked)
        {
            return false;
        }

        _ticked.Add(guideId);
        return true;
    }

    /// <summary>
    /// The line for HDT's log after <see cref="Toggle"/>: "Bronzebeard HUD: ticked guides=[A/11,B/20]", or, refused,
    /// "Bronzebeard HUD: C/17 not ticked, four guides already are".
    /// </summary>
    public string ToggleLine(string guideId, bool accepted) => accepted
        ? $"Bronzebeard HUD: ticked guides=[{string.Join(",", _ticked)}]"
        : $"Bronzebeard HUD: {guideId} not ticked, four guides already are";

    /// <summary>
    /// The targets for this board (<see cref="CompTargets.Choose"/>, with the ticks held here), coloured: a guide that was
    /// a target at the previous call keeps its colour; the others take the free colours, first free first, in target
    /// order. Colours of guides that are no longer targets are freed. Calling it again with the same board changes nothing.
    /// </summary>
    /// <param name="board">The guides ranked against the player's board and hand (<see cref="CompGuideMatch.Rank"/>).</param>
    /// <param name="count">Targets wanted (HudSettings.SuggestedCompositions, 1 to 4); ticked guides count among them.</param>
    public IReadOnlyList<CompTarget> Next(CompGuideBoard board, int count)
    {
        var chosen = CompTargets.Choose(board, _ticked, count);
        var slots = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (progress, _) in chosen)
        {
            if (_slots.TryGetValue(progress.Guide.Id, out var kept))
            {
                slots[progress.Guide.Id] = kept;
            }
        }

        foreach (var (progress, _) in chosen)
        {
            if (!slots.ContainsKey(progress.Guide.Id))
            {
                slots[progress.Guide.Id] = Enumerable.Range(0, Palette.Count).First(slot => !slots.ContainsValue(slot));
            }
        }

        _slots = slots;
        Targets = chosen
            .Select((c, i) => new CompTarget(c.Progress, i + 1, Palette[slots[c.Progress.Guide.Id]], c.Ticked))
            .ToList();
        return Targets;
    }

    /// <summary>Forgets the ticks, the colours and the targets.</summary>
    public void Reset()
    {
        _ticked.Clear();
        _slots = new Dictionary<string, int>(StringComparer.Ordinal);
        Targets = Array.Empty<CompTarget>();
    }

    /// <summary>A new game forgets everything (<see cref="Reset"/>); the same game keeps it.</summary>
    public void BeginGame(int game)
    {
        if (game == Game)
        {
            return;
        }

        Game = game;
        Reset();
    }
}
