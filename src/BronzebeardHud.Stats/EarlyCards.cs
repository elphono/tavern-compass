using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>One card worth aiming at, with what card-stats says of it at this turn.</summary>
public sealed class EarlyCard
{
    public EarlyCard(string cardId, CardTurnNote note)
    {
        CardId = cardId;
        Note = note;
    }

    public string CardId { get; }

    public CardTurnNote Note { get; }
}

/// <summary>The best cards of one tavern tier: the player's (<see cref="IsNext"/> false) or the next one.</summary>
public sealed class EarlyCardsRow
{
    public EarlyCardsRow(int tier, bool isNext, IReadOnlyList<EarlyCard> cards)
    {
        Tier = tier;
        IsNext = isNext;
        Cards = cards;
    }

    public int Tier { get; }
    public bool IsNext { get; }
    public IReadOnlyList<EarlyCard> Cards { get; }
}

/// <summary>
/// The cards to aim at early (Ali, 2026-10-08: "the best cards at T1/T2/T3… to know which cards we aim at when we level
/// up early"; "ours and the one above"; "at the current turn"): among the lobby's minions of the player's tavern tier and
/// of the next one, those card-stats says do better than every card played at this turn (CardTurnValue: its noise rule,
/// so "less noise" holds), best first. A correlation, not a cause, like every card value.
/// </summary>
public static class EarlyCards
{
    /// <summary>Early only: after this turn the panel's room goes back to the comps.</summary>
    public const int LastTurn = 6;

    public const int PerTier = 4;

    /// <param name="pool">The minions the lobby can offer, card id → tavern tier (the lobby's tribes already applied).</param>
    public static IReadOnlyList<EarlyCardsRow> For(CardStatsFile? file, int turn, int tavernTier, IReadOnlyDictionary<string, int> pool)
    {
        if (file == null || turn < 1 || turn > LastTurn || tavernTier < 1)
        {
            return Array.Empty<EarlyCardsRow>();
        }

        // No cap on the tier: the pool says which exist (an anomaly can bring tier 7 minions).
        return new[] { (Tier: tavernTier, IsNext: false), (Tier: tavernTier + 1, IsNext: true) }
            .Select(t => new EarlyCardsRow(t.Tier, t.IsNext, pool
                .Where(p => p.Value == t.Tier)
                .Select(p => (Id: p.Key, Note: CardTurnValue.For(file, p.Key, turn)))
                .Where(c => c.Note is { Verdict: CardTurnVerdict.Better })
                .OrderBy(c => c.Note!.Placement - c.Note.TurnAverage)
                .ThenBy(c => c.Id, StringComparer.Ordinal)
                .Take(PerTier)
                .Select(c => new EarlyCard(c.Id, c.Note!))
                .ToList()))
            .Where(r => r.Cards.Count > 0)
            .ToList();
    }

    /// <summary>
    /// The minions the lobby can offer, card id → tavern tier: a minion shows up when it has no tribe, is an amalgam, or has
    /// one of its tribes in the lobby (LobbyGuides' rule); one whose tribes are unknown is kept; one without a tier is not a
    /// minion of the pool. A lobby not known yet keeps every minion.
    /// </summary>
    public static IReadOnlyDictionary<string, int> Pool(IEnumerable<(string Id, int Tier, IReadOnlyCollection<string>? Tribes)> minions,
        IReadOnlyCollection<string> lobbyTribes)
    {
        var pool = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (id, tier, tribes) in minions)
        {
            if (tier >= 1 && (lobbyTribes.Count == 0 || tribes == null || tribes.Count == 0 || tribes.Contains(Tribes.Any) || tribes.Any(lobbyTribes.Contains)))
            {
                pool[id] = tier;
            }
        }

        return pool;
    }

    /// <summary>"early cards turn=3 tier=2 [2: BG_A 3.4 vs 3.8 (400), …; 3 next: …]", for HDT's log.</summary>
    public static string LogLine(int turn, int tavernTier, IReadOnlyList<EarlyCardsRow> rows)
    {
        var inv = CultureInfo.InvariantCulture;
        var body = rows.Count == 0 ? "none" : string.Join("; ", rows.Select(r =>
            $"{r.Tier.ToString(inv)}{(r.IsNext ? " next" : string.Empty)}: " + string.Join(", ", r.Cards.Select(c =>
                $"{c.CardId} {c.Note.Placement.ToString("0.0", inv)} vs {c.Note.TurnAverage.ToString("0.0", inv)} ({c.Note.Played.ToString(inv)})"))));
        return $"early cards turn={turn.ToString(inv)} tier={tavernTier.ToString(inv)} [{body}]";
    }
}
