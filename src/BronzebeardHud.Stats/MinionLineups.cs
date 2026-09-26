using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>One composition whose top final boards hold the minion.</summary>
public sealed class MinionLineup
{
    public MinionLineup(Composition composition, IReadOnlyList<FinalBoard> boards, int boardsKept)
    {
        Composition = composition;
        Boards = boards;
        BoardsKept = boardsKept;
    }

    public Composition Composition { get; }

    /// <summary>The composition's final boards that hold the minion, highest MMR first.</summary>
    public IReadOnlyList<FinalBoard> Boards { get; }

    /// <summary>How many final boards the composition has in the cache (the denominator of "in 4 of 5").</summary>
    public int BoardsKept { get; }

    /// <summary>"Undead Butcher 3,8 · in 4 of 5 top boards".</summary>
    public string Label =>
        $"{Composition.Name}{(Composition.AveragePlacement is { } p ? " " + p.ToString("0.0", CultureInfo.GetCultureInfo("fr-FR")) : string.Empty)} · in {Boards.Count} of {BoardsKept} top boards";
}

/// <summary>How the best players field one minion: the compositions and real final boards that hold it.</summary>
public sealed class MinionLineups
{
    public MinionLineups(string cardId, IReadOnlyList<MinionLineup> compositions, int? usualPosition)
    {
        CardId = cardId;
        Compositions = compositions;
        UsualPosition = usualPosition;
    }

    public string CardId { get; }
    public IReadOnlyList<MinionLineup> Compositions { get; }

    /// <summary>The position (1 to 7, left to right) it holds most often on those boards; null when on none.</summary>
    public int? UsualPosition { get; }

    public bool Found => Compositions.Count > 0;

    /// <summary>The panel's first line: where it usually stands, or that no top board holds it.</summary>
    public string Headline => UsualPosition is { } position
        ? $"Usually in position {position} of 7 · in {Compositions.Count} composition{(Compositions.Count > 1 ? "s" : string.Empty)}"
        : "In no top final board of the compositions in this lobby";

    /// <summary>
    /// From each playable composition's kept final boards (Firestone finalBoards: the highest MMR ones, since
    /// the data carries no placement), those that hold the minion, a golden copy counting as the card.
    /// Compositions holding it in the most boards first, then better placement; at most
    /// <paramref name="maxCompositions"/>. The usual position is the most frequent one over those boards,
    /// the leftmost on a tie.
    /// </summary>
    public static MinionLineups For(string cardId, IReadOnlyList<Composition> playable, int maxCompositions = 3)
    {
        var id = CardIds.Normalize(cardId);
        var lineups = playable
            .Select(c => new MinionLineup(c,
                c.FinalBoards.Where(b => b.Cards.Any(x => CardIds.Normalize(x) == id)).OrderByDescending(b => b.Mmr).ToList(),
                c.FinalBoards.Count))
            .Where(l => l.Boards.Count > 0)
            .OrderByDescending(l => l.Boards.Count)
            .ThenBy(l => l.Composition.AveragePlacement ?? double.MaxValue)
            .ThenBy(l => l.Composition.Id, StringComparer.Ordinal)
            .Take(maxCompositions)
            .ToList();
        var positions = lineups
            .SelectMany(l => l.Boards)
            .SelectMany(b => b.Cards.Select((x, i) => (Card: CardIds.Normalize(x), Position: i + 1)))
            .Where(x => x.Card == id)
            .GroupBy(x => x.Position)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => (int?)g.Key)
            .FirstOrDefault();
        return new MinionLineups(id, lineups, positions);
    }
}
