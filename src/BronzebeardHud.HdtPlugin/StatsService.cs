using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// Feeds the hero-pick panel: hand-typed HSReplay files first (<see cref="ReloadManual"/>, under the plugin's
/// "data-manual" guard), then the cached Firestone file (under "data-firestone"). Polled from HDT's UI thread; the download
/// runs on the thread pool and is picked up by <see cref="Poll"/> once finished, so the UI thread never waits on the network.
/// </summary>
internal sealed class StatsService
{
    private const string TimePeriod = "last-patch";

    private readonly StatsCache _cache;
    private readonly string _manualDirectory;
    private Task<CacheResult>? _refresh;
    private CacheResult? _firestone;
    private IReadOnlyList<HeroStatsFile> _manual = Array.Empty<HeroStatsFile>();
    private IReadOnlyList<StatsLoadError> _manualErrors = Array.Empty<StatsLoadError>();
    private int? _chosenBracket;

    /// <param name="cache">The plugin's one cache, shared by every service.</param>
    public StatsService(StatsCache cache, string statsDirectory)
    {
        _cache = cache;
        _manualDirectory = Path.Combine(statsDirectory, "manual");
    }

    /// <summary>Changes whenever the sources change, so the panel knows when to rebuild.</summary>
    public int Version { get; private set; }

    /// <summary>
    /// Called once when a hero selection starts: fetch (or reuse) the "every player" file, read its bracket table, and
    /// switch to the player's own bracket.
    /// </summary>
    /// <param name="rating">The player's MMR as HDT exposes it; null when unknown.</param>
    public void BeginHeroSelection(int? rating)
    {
        _chosenBracket = null; // a new game: the player's own bracket again (BracketChoice)
        if (_refresh == null || _refresh.IsCompleted)
        {
            _refresh = Task.Run(() => LoadForBracketAsync(rating));
        }

        Version++;
    }

    /// <summary>The hand-typed hero files (stats\manual\*.json), read again: at the plugin's start and at each game.</summary>
    public void ReloadManual()
    {
        (_manual, _manualErrors) = HeroStatsLoader.LoadDirectory(_manualDirectory);
        Version++;
    }

    /// <summary>
    /// A bracket picked in the overlay (BracketChoice) for the rest of the game: hero stats, the power gauge, trinkets and
    /// card stats follow it, since they all read <see cref="Bracket"/>. A load still running is left to finish unseen.
    /// </summary>
    public void ChooseBracket(int bracket, int? rating)
    {
        _chosenBracket = bracket;
        _refresh = Task.Run(() => LoadForBracketAsync(rating));
        Version++;
    }

    private async Task<CacheResult> LoadForBracketAsync(int? rating)
    {
        var chosen = _chosenBracket;
        var all = await _cache.GetHeroStatsAsync(MmrBracket.EveryPlayer, TimePeriod, RefreshPolicy.HeroStats, CancellationToken.None).ConfigureAwait(false);
        var bracket = chosen ?? MmrBracket.Select(rating, all.File?.MmrThresholds ?? Array.Empty<MmrThreshold>());
        if (bracket == MmrBracket.EveryPlayer)
        {
            return all;
        }

        var mine = await _cache.GetHeroStatsAsync(bracket, TimePeriod, RefreshPolicy.HeroStats, CancellationToken.None).ConfigureAwait(false);
        return mine.File != null ? mine : new CacheResult(all.File, all.Downloaded, mine.Error, all.Unchanged);
    }

    /// <summary>
    /// Loads the stats if no hero selection was seen (plugin re-enabled, or HDT started mid-game): the
    /// warband curve needs them in the shop too.
    /// </summary>
    public void EnsureStarted(int? rating)
    {
        if (_refresh == null && _firestone == null)
        {
            BeginHeroSelection(rating);
        }
    }

    public void Poll()
    {
        if (_refresh is not { IsCompleted: true } done)
        {
            return;
        }

        _firestone = done.Status == TaskStatus.RanToCompletion
            ? done.Result
            : new CacheResult(_firestone?.File, downloaded: false, error: done.Exception?.GetBaseException().Message);
        _refresh = null;
        Version++;
        PendingLogLine = DataRefresh.Line($"hero-stats mmr-{Bracket} {TimePeriod}", _firestone.Downloaded, _firestone.Unchanged,
            _firestone.Error, _firestone.File?.FetchedAt);
    }

    /// <summary>The diagnostic line of the last finished load, until the plugin logs it; see <see cref="DataRefresh"/>.</summary>
    public string? PendingLogLine { get; set; }

    /// <summary>True once a load finished, whatever it brought: <see cref="Bracket"/> is then the player's, or 100 for good.</summary>
    public bool BracketKnown => _firestone != null;

    /// <summary>The player's MMR bracket as last resolved (100 = every player until known).</summary>
    public int Bracket => _firestone?.File?.MmrPercentile ?? MmrBracket.EveryPlayer;

    public IReadOnlyList<HeroStatsFile> Sources() =>
        _firestone?.File is { } firestone ? _manual.Concat(new[] { firestone }).ToList() : _manual;

    /// <summary>One line for the panel footer, or null when everything loaded.</summary>
    public string? Status
    {
        get
        {
            var problems = new List<string>();
            if (_refresh != null)
            {
                problems.Add("loading Firestone stats…");
            }

            if (_firestone?.Error is { } error)
            {
                problems.Add(error);
            }

            problems.AddRange(_manualErrors.Select(e => $"{Path.GetFileName(e.Path)}: {e.Message}"));
            return problems.Count == 0 ? null : string.Join(" · ", problems);
        }
    }
}
