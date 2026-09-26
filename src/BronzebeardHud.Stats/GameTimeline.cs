using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>One player's hero at one moment: effective health = HEALTH − DAMAGE + ARMOR.</summary>
public sealed class HeroHealth
{
    public HeroHealth(int playerId, string heroCardId, int health)
    {
        PlayerId = playerId;
        HeroCardId = heroCardId;
        Health = health;
    }

    public int PlayerId { get; }
    public string HeroCardId { get; }
    public int Health { get; }

    public static HeroHealth? From(EntitySnapshot hero) =>
        hero.IsHero && hero.GetTag("PLAYER_ID") > 0 && !string.IsNullOrEmpty(hero.CardId)
            ? new HeroHealth(hero.GetTag("PLAYER_ID"), hero.CardId!, hero.GetTag("HEALTH") - hero.GetTag("DAMAGE") + hero.GetTag("ARMOR"))
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
/// The history panel (combats and health curves), shown during combat on the right of Hearthstone's
/// 4:3 frame: right of a full seven-minion row (<see cref="TavernLayout.MinionPitch"/>), left of the
/// frame's right edge, level with the boards.
/// </summary>
public static class HistoryLayout
{
    public static LayoutRect Panel(double width, double height)
    {
        var left = width / 2 + 3.5 * TavernLayout.MinionPitch(height) + 0.012 * height;
        var right = width / 2 + height * 2 / 3 - 0.005 * height;
        var top = 0.30 * height;
        var panelHeight = 0.40 * height;
        return new LayoutRect((left + right) / 2, top + panelHeight / 2, Math.Max(0, right - left), panelHeight);
    }
}
