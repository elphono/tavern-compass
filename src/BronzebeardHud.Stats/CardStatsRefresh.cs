using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace BronzebeardHud.Stats;

/// <summary>
/// The card stats of the chosen MMR bracket (the player's own, or the one picked in the overlay): one
/// <see cref="StatsFileRefresh{T}"/> per bracket, so that a file is only ever shown for the bracket it was loaded for —
/// a load still running for a bracket left meanwhile finishes in its own refresh, never on screen — and going back to a
/// bracket already loaded costs no download.
/// </summary>
public sealed class CardStatsRefresh
{
    private const string TimePeriod = "last-patch";
    private readonly Func<int, Task<(CardStatsFile? File, bool Downloaded, string? Error, bool Unchanged)>> _load;
    private readonly Dictionary<int, StatsFileRefresh<CardStatsFile>> _byBracket = new();
    private StatsFileRefresh<CardStatsFile>? _current;

    /// <param name="load">One load of a bracket through the cache, started off the UI thread by the caller.</param>
    public CardStatsRefresh(Func<int, Task<(CardStatsFile? File, bool Downloaded, string? Error, bool Unchanged)>> load) => _load = load;

    /// <summary>The bracket shown; null before the first <see cref="SetBracket"/>.</summary>
    public int? Bracket { get; private set; }

    /// <summary>The card stats of <see cref="Bracket"/>; null until its load brings a file.</summary>
    public CardStatsFile? File => _current?.File;

    /// <summary>Changes when the bracket changes or its load finishes, so that the values on screen are computed again.</summary>
    public int Version { get; private set; }

    /// <summary>The line for HDT's log about the bracket's last finished load; null before.</summary>
    public string? LastLine => _current?.LastLine;

    public string? Error => _current?.Error;

    public void SetBracket(int bracket)
    {
        if (Bracket == bracket)
        {
            return;
        }

        Bracket = bracket;
        Version++;
        if (!_byBracket.TryGetValue(bracket, out var refresh))
        {
            refresh = new StatsFileRefresh<CardStatsFile>(
                $"card-stats mmr-{bracket.ToString(CultureInfo.InvariantCulture)} {TimePeriod}", () => _load(bracket), f => f.FetchedAt);
            _byBracket[bracket] = refresh;
        }

        _current = refresh;
    }

    /// <summary>A new game: the cache is asked again, once, and applies its age rule.</summary>
    public void BeginGame(string gameId) => _current?.BeginChoice(gameId);

    /// <summary>Starts the bracket's first load on first call; true when a load of the bracket shown finished since the last call.</summary>
    public bool Poll()
    {
        if (_current?.Poll() != true)
        {
            return false;
        }

        Version++;
        return true;
    }
}
