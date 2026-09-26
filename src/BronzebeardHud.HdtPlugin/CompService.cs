using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// The compositions the tavern advisor aims at: hand-typed HSReplay files first
/// (<c>stats\manual\*.comps.txt</c>, format in the spec), then Firestone's composition stats
/// from the 7-day cache. Loaded once per game, off the UI thread.
/// </summary>
internal sealed class CompService : IDisposable
{
    private const string TimePeriod = "last-patch";

    private readonly HttpStatsFetcher _fetcher = new();
    private readonly StatsCache _cache;
    private readonly string _manualDirectory;
    private Task<CompositionCacheResult>? _refresh;
    private CompositionCacheResult? _firestone;
    private IReadOnlyList<Composition> _manual = Array.Empty<Composition>();
    private readonly List<string> _manualErrors = new();

    /// <summary>Minions to flag in the tavern, from stats\manual\pins.txt.</summary>
    public TavernPins Pins { get; private set; } = TavernPins.Empty;

    public CompService(string statsDirectory)
    {
        _cache = new StatsCache(statsDirectory, _fetcher, () => DateTimeOffset.UtcNow);
        _manualDirectory = Path.Combine(statsDirectory, "manual");
    }

    public int Version { get; private set; }

    /// <summary>Called when a game's first shopping phase starts.</summary>
    public void BeginGame()
    {
        var manual = new List<Composition>();
        _manualErrors.Clear();
        if (Directory.Exists(_manualDirectory))
        {
            foreach (var path in Directory.GetFiles(_manualDirectory, "*.comps.txt").OrderBy(p => p, StringComparer.Ordinal))
            {
                try
                {
                    manual.AddRange(HsReplayCompText.Parse(File.ReadAllText(path), HdtEntityAdapter.ResolveCardName, File.GetLastWriteTimeUtc(path)).Compositions);
                }
                catch (Exception e) when (e is StatsFormatException or IOException)
                {
                    _manualErrors.Add($"{Path.GetFileName(path)}: {e.Message}");
                }
            }
        }

        _manual = manual;
        Pins = TavernPins.Empty;
        var pinsPath = Path.Combine(_manualDirectory, "pins.txt");
        if (File.Exists(pinsPath))
        {
            try
            {
                Pins = TavernPins.Parse(File.ReadAllText(pinsPath), HdtEntityAdapter.ResolveCardName);
            }
            catch (Exception e) when (e is StatsFormatException or IOException)
            {
                _manualErrors.Add($"pins.txt: {e.Message}");
            }
        }

        if (_refresh == null || _refresh.IsCompleted)
        {
            _refresh = Task.Run(() => _cache.GetCompositionsAsync(TimePeriod, RefreshPolicy.CompStats, CancellationToken.None));
        }

        Version++;
    }

    public void Poll()
    {
        if (_refresh is not { IsCompleted: true } done)
        {
            return;
        }

        _firestone = done.Status == TaskStatus.RanToCompletion
            ? done.Result
            : new CompositionCacheResult(_firestone?.File, downloaded: false, error: done.Exception?.GetBaseException().Message);
        _refresh = null;
        Version++;
    }

    public IReadOnlyList<Composition> Compositions() =>
        _firestone?.File is { } firestone ? _manual.Concat(firestone.Compositions).ToList() : _manual;

    public string? Status
    {
        get
        {
            var problems = new List<string>();
            if (_refresh != null)
            {
                problems.Add("loading Firestone compositions…");
            }

            if (_firestone?.Error is { } error)
            {
                problems.Add(error);
            }

            problems.AddRange(_manualErrors);
            return problems.Count == 0 ? null : string.Join(" · ", problems);
        }
    }

    public void Dispose() => _fetcher.Dispose();
}
