using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// One opponent's board as HDT keeps it: their hero (the leaderboard's, base hero id, skins mapped: HDT's snapshot holds no
/// hero, <see cref="OpponentBoards"/>), the turn, and the attack and health of its minions, snapshotted by HDT when a combat
/// begins (GameV2.GetBattlegroundsBoardStateFor, the "last known board" HDT shows when a tile of the leaderboard is hovered).
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
    /// <param name="reads">How each board was asked of HDT (<see cref="BoardRead.Probe"/>), by PLAYER_ID: said in the log line.</param>
    /// <param name="kept">Facts of earlier in this combat, kept to its end (<see cref="CombatOpponentKeeper"/>): "id=3 (kept)" in the log.</param>
    public OpponentFacts(OverlayPhase phase, int turn, string? playerHeroCardId, int combatOpponentId, int nextOpponentId,
        IReadOnlyDictionary<int, BoardSeen> lastBoards, IReadOnlyDictionary<int, string> heroes, IReadOnlyDictionary<int, string>? reads = null,
        bool kept = false)
    {
        Phase = phase;
        Turn = turn;
        PlayerHeroCardId = playerHeroCardId;
        CombatOpponentId = combatOpponentId;
        NextOpponentId = nextOpponentId;
        LastBoards = lastBoards;
        Heroes = heroes;
        Reads = reads ?? new Dictionary<int, string>();
        Kept = kept;
    }

    public OverlayPhase Phase { get; }
    public int Turn { get; }
    public string? PlayerHeroCardId { get; }
    public int CombatOpponentId { get; }
    public int NextOpponentId { get; }
    public IReadOnlyDictionary<int, BoardSeen> LastBoards { get; }
    public IReadOnlyDictionary<int, string> Heroes { get; }
    public IReadOnlyDictionary<int, string> Reads { get; }
    public bool Kept { get; }

    /// <summary>The same facts, marked kept.</summary>
    public OpponentFacts AsKept() =>
        new(Phase, Turn, PlayerHeroCardId, CombatOpponentId, NextOpponentId, LastBoards, Heroes, Reads, kept: true);
}

/// <summary>
/// Keeps the opponent of a combat until the shop (Ali, 2026-10-07: keep the combat's opponent and their gauge until the
/// return to the shop). In HDT's log, at the end of every combat, no hero in play is controlled by game.Opponent any more
/// while the phase is still combat: 28 lines "scope=combat id=0" out of 28, each after a line of the same combat (same turn)
/// with an id found, 2 to 5 s before the shop's. In combat, facts without an opponent give back the last ones of the same
/// combat that had one, as they were (id, hero, board read, so the same gauge), marked kept; a combat with an opponent found
/// replaces them, a shop (or anything but a combat) forgets them, and so does a combat of another turn.
/// </summary>
public sealed class CombatOpponentKeeper
{
    private OpponentFacts? _kept;

    /// <summary>The facts the gauge shows: <paramref name="facts"/>, or the ones kept from earlier in this combat.</summary>
    public OpponentFacts Observe(OpponentFacts facts)
    {
        if (facts.Phase != OverlayPhase.Combat)
        {
            _kept = null;
            return facts;
        }

        if (facts.CombatOpponentId > 0)
        {
            _kept = facts;
            return facts;
        }

        return _kept != null && _kept.Turn == facts.Turn ? _kept.AsKept() : facts;
    }

    /// <summary>Out of a game: nothing kept.</summary>
    public void Forget() => _kept = null;
}

/// <summary>
/// A hero entity of HDT's game and the PLAYER_ID it carries. Several carry the same one: the leaderboard's tile, the hero in
/// play during a combat (Kel'Thuzad against a ghost, which carries the dead player's PLAYER_ID), a skin.
/// </summary>
public sealed class HeroEntity
{
    /// <param name="heroCardId">Base hero id, skins mapped to their parent (HeroIdNormalizer).</param>
    /// <param name="onLeaderboard">Carries PLAYER_LEADERBOARD_PLACE: the leaderboard's tile, the player's own hero.</param>
    public HeroEntity(int entityId, int playerId, string heroCardId, bool onLeaderboard)
    {
        EntityId = entityId;
        PlayerId = playerId;
        HeroCardId = heroCardId;
        OnLeaderboard = onLeaderboard;
    }

    public int EntityId { get; }
    public int PlayerId { get; }
    public string HeroCardId { get; }
    public bool OnLeaderboard { get; }
}

/// <summary>
/// HDT's snapshot of a board (Hearthstone_Deck_Tracker.Hearthstone.BoardSnapshot) as the plugin reads it: the turn it was
/// taken, how many entities it holds, and the attack and health of those that are minions. HDT 1.58.9 keeps the minions
/// only, never the hero (BattlegroundsBoardState.SnapshotCurrentBoard: <c>x.IsMinion &amp;&amp; x.IsInZone(Zone.PLAY) &amp;&amp;
/// x.IsControlledBy(_game.Opponent.Id)</c>).
/// </summary>
public sealed class SnapshotRead
{
    public SnapshotRead(int turn, int entities, IReadOnlyList<(int Attack, int Health)> minions)
    {
        Turn = turn;
        Entities = entities;
        Minions = minions;
    }

    public int Turn { get; }
    public int Entities { get; }
    public IReadOnlyList<(int Attack, int Health)> Minions { get; }
}

/// <summary>One opponent's board as read from HDT, and how it was read, for the log.</summary>
public sealed class BoardRead
{
    public BoardRead(BoardSeen? board, string probe)
    {
        Board = board;
        Probe = probe;
    }

    /// <summary>The board, against the player's own hero; null when HDT has none (never fought) or no hero carries the PLAYER_ID.</summary>
    public BoardSeen? Board { get; }

    /// <summary>"heroes 40,90 asked 40 → turn 8, 7 entities, 7 minions", "heroes 40 asked 40 → no snapshot", "no hero entity".</summary>
    public string Probe { get; }
}

/// <summary>
/// How an opponent's last board is asked of HDT (GameV2.GetBattlegroundsBoardStateFor), decompiled from HDT 1.58.9. When a
/// combat begins (TagChangeActions.OnBattlegroundsCombatSetupChange), BattlegroundsBoardState.SnapshotCurrentBoard files the
/// opponent's MINIONS under the PLAYER_ID of the hero in play that game.Opponent controls; GetSnapshot(entityId) finds them
/// again by the PLAYER_ID of the entity it is given. So any hero carrying that PLAYER_ID answers alike, and the hero is never
/// in the snapshot: it is taken from the entities instead (until 2026-10-07 the plugin looked for it in the snapshot, found
/// none, and never read a board).
/// </summary>
public static class OpponentBoards
{
    /// <summary>
    /// The hero of a PLAYER_ID: the leaderboard's (the player's own hero, the entity HDT's leaderboard hover hands
    /// GetBattlegroundsBoardStateFor), else the first by entity id; null when no hero carries it.
    /// </summary>
    public static HeroEntity? Pick(IReadOnlyList<HeroEntity> heroes, int playerId) =>
        heroes.Where(h => h.PlayerId == playerId).OrderByDescending(h => h.OnLeaderboard).ThenBy(h => h.EntityId).FirstOrDefault();

    /// <summary>Each player's hero as the leaderboard shows it, by PLAYER_ID: the leaderboard's tiles only, the first by entity id.</summary>
    public static IReadOnlyDictionary<int, string> Leaderboard(IReadOnlyList<HeroEntity> heroes) =>
        heroes.Where(h => h.OnLeaderboard && h.PlayerId > 0).OrderBy(h => h.EntityId)
            .GroupBy(h => h.PlayerId).ToDictionary(g => g.Key, g => g.First().HeroCardId);

    /// <summary>
    /// The last board of the player <paramref name="playerId"/>: <paramref name="snapshotFor"/> (GetBattlegroundsBoardStateFor,
    /// by entity id) asked for the hero <see cref="Pick"/> names, measured against that hero.
    /// </summary>
    public static BoardRead Read(int playerId, IReadOnlyList<HeroEntity> heroes, Func<int, SnapshotRead?> snapshotFor)
    {
        var hero = Pick(heroes, playerId);
        if (hero == null)
        {
            return new BoardRead(null, "no hero entity");
        }

        var inv = CultureInfo.InvariantCulture;
        var ids = string.Join(",", heroes.Where(h => h.PlayerId == playerId).Select(h => h.EntityId).OrderBy(id => id).Select(id => id.ToString(inv)));
        var asked = $"heroes {ids} asked {hero.EntityId.ToString(inv)}";
        var snapshot = snapshotFor(hero.EntityId);
        if (snapshot == null)
        {
            return new BoardRead(null, $"{asked} → no snapshot");
        }

        return new BoardRead(new BoardSeen(hero.HeroCardId, snapshot.Turn, snapshot.Minions),
            $"{asked} → turn {snapshot.Turn.ToString(inv)}, {snapshot.Entities.ToString(inv)} entities, {snapshot.Minions.Count.ToString(inv)} minions");
    }
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
    /// seen, how it was asked of HDT, the average): "Bronzebeard HUD: opponent power scope=combat id=5 hero=TB_X (yours TB_Y)
    /// turn=8 seen=8 board=160 (3 minions) read=[heroes 40,90 asked 40 → turn 8, 3 entities, 3 minions] · Opp. 160 · their
    /// hero avg 220 at turn 8 · −27% power=behind"; "id=5 (kept)" for an opponent kept to the end of the combat
    /// (<see cref="CombatOpponentKeeper"/>).
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
        var read = id > 0 && facts.Reads.TryGetValue(id, out var probe) ? $" read=[{probe}]" : string.Empty;
        var kept = facts.Kept ? " (kept)" : string.Empty;
        return $"Bronzebeard HUD: opponent power scope={scope} id={id.ToString(inv)}{kept} hero={hero} (yours {facts.PlayerHeroCardId ?? "none"}) "
               + $"turn={facts.Turn.ToString(inv)} {measure}{read} · {comparison.Line} {comparison.PowerText}";
    }
}
