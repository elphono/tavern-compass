using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// One opponent's board as HDT keeps it: the hero it fought with (base hero id, skins mapped), the turn, and the attack and
/// health of its minions, snapshotted by HDT when a combat begins (GameV2.GetBattlegroundsBoardStateFor, the "last known
/// board" HDT shows when a tile of the leaderboard is hovered).
/// </summary>
public sealed class BoardSeen
{
    public BoardSeen(string heroCardId, int turn, IReadOnlyList<(int Attack, int Health)> minions)
    {
        HeroCardId = heroCardId;
        Turn = turn;
        Minions = minions;
    }

    public string HeroCardId { get; }
    public int Turn { get; }
    public IReadOnlyList<(int Attack, int Health)> Minions { get; }

    /// <summary>Attack plus health of every minion, Firestone's measure (<see cref="WarbandCurve.BoardStats"/>).</summary>
    public int Stats => WarbandCurve.BoardStats(Minions);
}

/// <summary>What the plugin reads of the game for the opponent's gauge (HdtEntityAdapter.OpponentFacts).</summary>
public sealed class OpponentFacts
{
    /// <param name="playerHeroCardId">The player's own hero (base id): never the opponent's reference, only said in the log.</param>
    /// <param name="combatOpponentId">PLAYER_ID of the player fought in this combat; 0 out of combat or when unknown.</param>
    /// <param name="nextOpponentId">The player's NEXT_OPPONENT_PLAYER_ID; 0 when the game has not said it yet.</param>
    /// <param name="lastBoards">HDT's last known board of each opponent, by PLAYER_ID.</param>
    /// <param name="heroes">Each player's hero as the leaderboard shows it (base id), by PLAYER_ID: the name of an opponent never fought.</param>
    public OpponentFacts(OverlayPhase phase, int turn, string? playerHeroCardId, int combatOpponentId, int nextOpponentId,
        IReadOnlyDictionary<int, BoardSeen> lastBoards, IReadOnlyDictionary<int, string> heroes)
    {
        Phase = phase;
        Turn = turn;
        PlayerHeroCardId = playerHeroCardId;
        CombatOpponentId = combatOpponentId;
        NextOpponentId = nextOpponentId;
        LastBoards = lastBoards;
        Heroes = heroes;
    }

    public OverlayPhase Phase { get; }
    public int Turn { get; }
    public string? PlayerHeroCardId { get; }
    public int CombatOpponentId { get; }
    public int NextOpponentId { get; }
    public IReadOnlyDictionary<int, BoardSeen> LastBoards { get; }
    public IReadOnlyDictionary<int, string> Heroes { get; }
}

/// <summary>
/// The opponent's board against the average of THEIR hero at the same turn (Ali, 2026-10-06: "another indicator, the same,
/// for the enemy's composition when it shows"), on the levels of the player's own gauge (<see cref="BoardPowerLevels"/>,
/// <see cref="WarbandCurve"/>: the same "too early", "few games" and "curve falls" rules). Which board:
/// <list type="bullet">
/// <item>in combat, the opponent being fought, as HDT snapshotted it when the combat began (minions die during it): "Opp. 160 ·
/// their hero avg 143 at turn 8";</item>
/// <item>in the shop, the next opponent (NEXT_OPPONENT_PLAYER_ID), on the last board HDT saw of them, against their hero's
/// average at the turn it was seen, which the line says: "Next opp. 95 at turn 5 · their hero avg 35". A board of five turns
/// ago against today's average would read "behind" whatever the player did since.</item>
/// </list>
/// Without data the gauge is grey and says why ("not known yet", "not fought yet", "no curve for Rakanishu"): never the
/// player's own curve in place of theirs.
/// </summary>
public static class OpponentPower
{
    /// <param name="heroName">A hero's name, for "no curve for Rakanishu" and "Next opp. Rakanishu – not fought yet".</param>
    public static WarbandComparison Compare(OpponentFacts facts, IReadOnlyList<HeroStatsFile> sources, Func<string, string> heroName)
    {
        switch (facts.Phase)
        {
            case OverlayPhase.Combat:
                if (facts.CombatOpponentId <= 0)
                {
                    return Grey(facts.Turn, "Opp. – not known yet");
                }

                // HDT snapshots the board when the combat begins (TagChangeActions.OnBattlegroundsCombatSetupChange): one of an
                // earlier turn is not this combat's.
                if (!facts.LastBoards.TryGetValue(facts.CombatOpponentId, out var fought) || fought.Turn != facts.Turn)
                {
                    return Grey(facts.Turn, "Opp. – board not read yet");
                }

                return WarbandCurve.Compare(facts.Turn, fought.Stats, fought.HeroCardId, sources,
                    new WarbandWords($"Opp. {fought.Stats.ToString(CultureInfo.InvariantCulture)}", heroName(fought.HeroCardId), "their hero avg {0} at turn {1}"));

            case OverlayPhase.Shop:
                if (facts.NextOpponentId <= 0)
                {
                    return Grey(facts.Turn, "Next opp. – not known yet");
                }

                if (!facts.LastBoards.TryGetValue(facts.NextOpponentId, out var seen))
                {
                    var name = facts.Heroes.TryGetValue(facts.NextOpponentId, out var hero) ? heroName(hero) + " " : string.Empty;
                    return Grey(facts.Turn, $"Next opp. {name}– not fought yet");
                }

                // Against their hero's average at the turn it was seen, not today's.
                return WarbandCurve.Compare(seen.Turn, seen.Stats, seen.HeroCardId, sources,
                    new WarbandWords($"Next opp. {seen.Stats.ToString(CultureInfo.InvariantCulture)} at turn {seen.Turn.ToString(CultureInfo.InvariantCulture)}",
                        heroName(seen.HeroCardId), "their hero avg {0}"));

            default:
                return Grey(facts.Turn, "Opp. – not in a shop or a combat");
        }
    }

    private static WarbandComparison Grey(int turn, string text) => new(turn, 0, null, text);

    /// <summary>
    /// The line written in HDT's log each time the gauge changes, with what it rests on (the board, its hero, the turn it was
    /// seen, the average): "Bronzebeard HUD: opponent power scope=combat id=5 hero=TB_X (yours TB_Y) turn=8 seen=8 board=160
    /// (3 minions) · Opp. 160 · their hero avg 220 at turn 8 · −27% power=behind".
    /// </summary>
    public static string LogLine(OpponentFacts facts, WarbandComparison comparison)
    {
        var inv = CultureInfo.InvariantCulture;
        var (scope, id) = facts.Phase switch
        {
            OverlayPhase.Combat => ("combat", facts.CombatOpponentId),
            OverlayPhase.Shop => ("next", facts.NextOpponentId),
            _ => ("none", 0),
        };
        var seen = id > 0 && facts.LastBoards.TryGetValue(id, out var board) ? board : null;
        var hero = seen?.HeroCardId ?? (facts.Heroes.TryGetValue(id, out var listed) ? listed : "none");
        var measure = seen != null
            ? $"seen={seen.Turn.ToString(inv)} board={seen.Stats.ToString(inv)} ({seen.Minions.Count.ToString(inv)} minions)"
            : "seen=none";
        return $"Bronzebeard HUD: opponent power scope={scope} id={id.ToString(inv)} hero={hero} (yours {facts.PlayerHeroCardId ?? "none"}) "
               + $"turn={facts.Turn.ToString(inv)} {measure} · {comparison.Line} {comparison.PowerText}";
    }
}
