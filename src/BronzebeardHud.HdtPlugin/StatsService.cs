using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// Feeds the hero-pick panel: hand-typed HSReplay files first, then the cached Firestone file.
/// Polled from HDT's UI thread; the download runs on the thread pool and is picked up by
/// <see cref="Poll"/> once finished, so the UI thread never waits on the network.
/// </summary>
internal sealed class StatsService : IDisposable
{
    private const string TimePeriod = "last-patch";

    private readonly HttpStatsFetcher _fetcher = new();
    private readonly StatsCache _cache;
    private readonly string _manualDirectory;
    private Task<CacheResult>? _refresh;
    private CacheResult? _firestone;
    private IReadOnlyList<HeroStatsFile> _manual = Array.Empty<HeroStatsFile>();
    private IReadOnlyList<StatsLoadError> _manualErrors = Array.Empty<StatsLoadError>();

    public StatsService(string statsDirectory)
    {
        _cache = new StatsCache(statsDirectory, _fetcher, () => DateTimeOffset.UtcNow);
        _manualDirectory = Path.Combine(statsDirectory, "manual");
    }

    /// <summary>Changes whenever the sources change, so the panel knows when to rebuild.</summary>
    public int Version { get; private set; }

    /// <summary>
    /// Called once when a hero selection starts: reload hand-typed files, then fetch (or reuse) the
    /// "every player" file, read its bracket table, and switch to the player's own bracket.
    /// </summary>
    /// <param name="rating">The player's MMR as HDT exposes it; null when unknown.</param>
    public void BeginHeroSelection(int? rating)
    {
        (_manual, _manualErrors) = HeroStatsLoader.LoadDirectory(_manualDirectory);
        if (_refresh == null || _refresh.IsCompleted)
        {
            _refresh = Task.Run(() => LoadForBracketAsync(rating));
        }

        Version++;
    }

    private async Task<CacheResult> LoadForBracketAsync(int? rating)
    {
        var all = await _cache.GetHeroStatsAsync(MmrBracket.EveryPlayer, TimePeriod, RefreshPolicy.HeroStats, CancellationToken.None).ConfigureAwait(false);
        var bracket = MmrBracket.Select(rating, all.File?.MmrThresholds ?? Array.Empty<MmrThreshold>());
        if (bracket == MmrBracket.EveryPlayer)
        {
            return all;
        }

        var mine = await _cache.GetHeroStatsAsync(bracket, TimePeriod, RefreshPolicy.HeroStats, CancellationToken.None).ConfigureAwait(false);
        return mine.File != null ? mine : new CacheResult(all.File, all.Downloaded, mine.Error);
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
    }

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

    public void Dispose() => _fetcher.Dispose();
}
