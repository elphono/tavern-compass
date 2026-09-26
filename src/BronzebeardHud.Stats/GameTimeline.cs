using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>One player's hero at one moment: effective health = HEALTH − DAMAGE + ARMOR.</summary>
public sealed class HeroHealth
{
    public HeroHealth(int playerId, string heroCardId, int health, int leaderboardPlace = 0)
    {
        PlayerId = playerId;
        HeroCardId = heroCardId;
        Health = health;
        LeaderboardPlace = leaderboardPlace;
    }

    public int PlayerId { get; }
    public string HeroCardId { get; }
    public int Health { get; }

    /// <summary>PLAYER_LEADERBOARD_PLACE, the game's own ranking; 0 when unknown.</summary>
    public int LeaderboardPlace { get; }

    /// <summary>
    /// One hero per player in the game: heroes with a leaderboard place only (offered heroes that were not
    /// picked have none), and when a player's hero was replaced, the most recent entity.
    /// </summary>
    public static IReadOnlyList<HeroHealth> InGame(IEnumerable<EntitySnapshot> heroes) =>
        heroes
            .Where(h => h.IsHero && h.GetTag("PLAYER_ID") > 0 && h.GetTag("PLAYER_LEADERBOARD_PLACE") > 0 && !string.IsNullOrEmpty(h.CardId))
            .GroupBy(h => h.GetTag("PLAYER_ID"))
            .Select(g => From(g.OrderByDescending(h => h.Id).First())!)
            .OrderBy(h => h.PlayerId)
            .ToList();

    public static HeroHealth? From(EntitySnapshot hero) =>
        hero.IsHero && hero.GetTag("PLAYER_ID") > 0 && !string.IsNullOrEmpty(hero.CardId)
            ? new HeroHealth(hero.GetTag("PLAYER_ID"), hero.CardId!, hero.GetTag("HEALTH") - hero.GetTag("DAMAGE") + hero.GetTag("ARMOR"),
                hero.GetTag("PLAYER_LEADERBOARD_PLACE"))
            : null;
}

public enum CombatResult
{
    Win,
    Loss,
    Tie,
}

/// <summary>One combat of the game, closed when the next shopping phase starts.</summary>
public sealed class CombatRecord
{
    public CombatRecord(int turn, int opponentPlayerId, string opponentHeroCardId, int playerBefore, int playerAfter, int opponentBefore, int opponentAfter)
    {
        Turn = turn;
        OpponentPlayerId = opponentPlayerId;
        OpponentHeroCardId = opponentHeroCardId;
        PlayerHealthBefore = playerBefore;
        PlayerHealthAfter = playerAfter;
        OpponentHealthBefore = opponentBefore;
        OpponentHealthAfter = opponentAfter;
    }

    public int Turn { get; }
    public int OpponentPlayerId { get; }
    public string OpponentHeroCardId { get; }
    public int PlayerHealthBefore { get; }
    public int PlayerHealthAfter { get; }
    public int OpponentHealthBefore { get; }
    public int OpponentHealthAfter { get; }
    public int DamageTaken => Math.Max(0, PlayerHealthBefore - PlayerHealthAfter);
    public int DamageDealt => Math.Max(0, OpponentHealthBefore - OpponentHealthAfter);

    /// <summary>The loser's hero takes the damage at the end of the fight; nobody hit means a tie.</summary>
    public CombatResult Result => DamageDealt > 0 ? CombatResult.Win : DamageTaken > 0 ? CombatResult.Loss : CombatResult.Tie;
}

/// <summary>
/// Builds the game's combat history and health curves from repeated observations (the plugin calls
/// <see cref="Observe"/> on every HDT update, about ten times a second). Only transitions count:
/// shop → combat opens a combat against the opponent announced during the shop, with the health last read
/// in the shop; combat → shop closes it with the health read after the fight. Health curves keep one value per player and turn, the last seen.
/// </summary>
public sealed class GameTimeline
{
    private readonly List<CombatRecord> _combats = new();
    private readonly Dictionary<int, SortedDictionary<int, int>> _health = new();
    private readonly Dictionary<int, string> _heroByPlayer = new();
    private bool _inCombat;
    private int _announcedOpponent;
    private Dictionary<int, int> _lastShopHealth = new();
    private (int Turn, int Opponent, int PlayerBefore, int OpponentBefore)? _open;

    public IReadOnlyList<CombatRecord> Combats => _combats;

    /// <summary>Player id → (turn, health) points, in turn order.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<(int Turn, int Health)>> HealthByPlayer =>
        _health.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<(int, int)>)kv.Value.Select(p => (p.Key, p.Value)).ToList());

    public string? HeroOf(int playerId) => _heroByPlayer.TryGetValue(playerId, out var hero) ? hero : null;

    public void Reset()
    {
        _combats.Clear();
        _health.Clear();
        _heroByPlayer.Clear();
        _inCombat = false;
        _announcedOpponent = 0;
        _lastShopHealth = new Dictionary<int, int>();
        _open = null;
    }

    /// <param name="turn">The game turn (HDT's turn number).</param>
    /// <param name="inCombat">True during the combat phase.</param>
    /// <param name="playerId">The local player's PLAYER_ID.</param>
    /// <param name="nextOpponentPlayerId">NEXT_OPPONENT_PLAYER_ID as currently shown; only read during the shop.</param>
    /// <param name="heroes">Every player's hero health right now.</param>
    public void Observe(int turn, bool inCombat, int playerId, int nextOpponentPlayerId, IReadOnlyList<HeroHealth> heroes)
    {
        foreach (var hero in heroes)
        {
            _heroByPlayer[hero.PlayerId] = hero.HeroCardId;
            if (!inCombat && turn > 0)
            {
                if (!_health.TryGetValue(hero.PlayerId, out var curve))
                {
                    _health[hero.PlayerId] = curve = new SortedDictionary<int, int>();
                }

                curve[turn] = hero.Health;
            }
        }

        int? HealthOf(int id) => heroes.FirstOrDefault(h => h.PlayerId == id)?.Health;

        if (!inCombat)
        {
            // Health before a fight comes from the shop: during the fight the damage may already show.
            _lastShopHealth = heroes.GroupBy(h => h.PlayerId).ToDictionary(g => g.Key, g => g.Last().Health);
            if (nextOpponentPlayerId > 0)
            {
                _announcedOpponent = nextOpponentPlayerId;
            }
        }

        if (!_inCombat && inCombat)
        {
            if (_announcedOpponent > 0 && _lastShopHealth.TryGetValue(playerId, out var mine)
                && _lastShopHealth.TryGetValue(_announcedOpponent, out var theirs))
            {
                _open = (turn, _announcedOpponent, mine, theirs);
            }
        }
        else if (_inCombat && !inCombat && _open is { } open)
        {
            if (HealthOf(playerId) is { } mine && HealthOf(open.Opponent) is { } theirs)
            {
                _combats.Add(new CombatRecord(open.Turn, open.Opponent, HeroOf(open.Opponent) ?? string.Empty,
                    open.PlayerBefore, mine, open.OpponentBefore, theirs));
            }

            _open = null;
        }

        _inCombat = inCombat;
    }
}

/// <summary>Maps health curves to points of a small chart: x by turn, y by health, origin at the bottom left.</summary>
public static class HealthChart
{
    public static IReadOnlyList<(double X, double Y)> Points(IReadOnlyList<(int Turn, int Health)> curve, int maxTurn, int maxHealth, double width, double height)
    {
        if (curve.Count == 0 || maxTurn <= 0 || maxHealth <= 0)
        {
            return Array.Empty<(double, double)>();
        }

        return curve
            .Select(p => (X: (double)p.Turn / maxTurn * width,
                          Y: height - Math.Max(0, Math.Min(maxHealth, p.Health)) / (double)maxHealth * height))
            .ToList();
    }
}

/// <summary>
/// The combats panel's default place: the upper right part of Hearthstone's 4:3 frame, wider than tall
/// (a health chart reads left to right). HDT keeps that area free in Battlegrounds: Bob's Buddy and the
/// opponent info sit at the top centre (Windows/OverlayWindow.xaml.cs:279-280, 289-290), its minion
/// browser bar in the top right corner of the window (Windows/OverlayWindow.xaml:488, Canvas.Top/Right 0),
/// the session widget on the left (Config.cs:875-879, placed at OverlayWindow.Update.cs:645-646); below
/// starts the opponent row (H/2 − 0.158 H − 0.045 H, OverlayWindow.Update.cs:534-535). It can be moved.
/// </summary>
public static class HistoryLayout
{
    public const double Top = 0.06;
    public const double Bottom = 0.29;

    public static LayoutRect Panel(double width, double height)
    {
        var left = width / 2 + 0.25 * height;
        var right = width / 2 + height * 2 / 3 - 0.01 * height;
        var top = Top * height;
        var panelHeight = (Bottom - Top) * height;
        return new LayoutRect((left + right) / 2, top + panelHeight / 2, Math.Max(0, right - left), panelHeight);
    }
}

/// <summary>One line of the standings column: a player, its health now and since the last combat.</summary>
public sealed class StandingRow
{
    public StandingRow(int rank, HeroHealth hero, int delta, bool isLocal, bool isNextOpponent)
    {
        Rank = rank;
        Hero = hero;
        Delta = delta;
        IsLocal = isLocal;
        IsNextOpponent = isNextOpponent;
    }

    /// <summary>1 for the most health; tied players share a rank (1, 2, 2, 4).</summary>
    public int Rank { get; }

    public HeroHealth Hero { get; }

    /// <summary>Health change over the last combat (negative = damage taken).</summary>
    public int Delta { get; }

    public bool IsLocal { get; }
    public bool IsNextOpponent { get; }
    public bool IsDead => Hero.Health <= 0;
}

public static class Standings
{
    /// <summary>
    /// Living players by health, most first; ties by the game's leaderboard place, then player id;
    /// dead players last. The delta compares the last two points of the player's health curve.
    /// </summary>
    public static IReadOnlyList<StandingRow> Build(IReadOnlyList<HeroHealth> heroes, GameTimeline timeline, int localPlayerId, int nextOpponentPlayerId)
    {
        var curves = timeline.HealthByPlayer;
        int Delta(HeroHealth h) =>
            curves.TryGetValue(h.PlayerId, out var curve) && curve.Count >= 2 ? curve[curve.Count - 1].Health - curve[curve.Count - 2].Health : 0;

        var ordered = heroes
            .OrderBy(h => h.Health <= 0)
            .ThenByDescending(h => h.Health)
            .ThenBy(h => h.LeaderboardPlace <= 0 ? int.MaxValue : h.LeaderboardPlace)
            .ThenBy(h => h.PlayerId)
            .ToList();
        var rows = new List<StandingRow>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var rank = i > 0 && ordered[i].Health == ordered[i - 1].Health ? rows[i - 1].Rank : i + 1;
            rows.Add(new StandingRow(rank, ordered[i], Delta(ordered[i]), ordered[i].PlayerId == localPlayerId,
                nextOpponentPlayerId > 0 && ordered[i].PlayerId == nextOpponentPlayerId));
        }

        return rows;
    }
}

public static class CombatText
{
    /// <summary>"Turn 5 · vs Drek'Thar · Win, 7 damage dealt" / "Loss, 3 damage taken" / "Tie".</summary>
    public static string Label(CombatRecord combat, string opponentName)
    {
        var outcome = combat.Result switch
        {
            CombatResult.Win => $"Win, {combat.DamageDealt} damage dealt",
            CombatResult.Loss => $"Loss, {combat.DamageTaken} damage taken",
            _ => "Tie",
        };
        return $"Turn {combat.Turn} · vs {opponentName} · {outcome}";
    }
}

/// <summary>Axis ticks for the health chart.</summary>
public static class ChartAxes
{
    /// <summary>0 to the top of the scale: every 10 up to 50, every 20 above (heroes start at up to 60 health).</summary>
    public static IReadOnlyList<int> HealthTicks(int maxHealth)
    {
        var step = maxHealth <= 50 ? 10 : 20;
        var top = Math.Max(step, (int)Math.Ceiling(Math.Max(1, maxHealth) / (double)step) * step);
        return Enumerable.Range(0, top / step + 1).Select(i => i * step).ToList();
    }

    /// <summary>Every turn up to 8 turns, every other turn up to 16, every 5 beyond; always from turn 1.</summary>
    public static IReadOnlyList<int> TurnTicks(int turns)
    {
        if (turns <= 0)
        {
            return Array.Empty<int>();
        }

        var step = turns <= 8 ? 1 : turns <= 16 ? 2 : 5;
        return Enumerable.Range(0, (turns - 1) / step + 1).Select(i => 1 + i * step).ToList();
    }
}
