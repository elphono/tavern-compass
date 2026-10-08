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
/// (<c>stats\manual\*.comps.txt</c>, format in the spec, read by <see cref="LoadManual"/> under the plugin's
/// "data-manual" guard), then Firestone's composition stats from the 7-day cache, under "data-firestone". Loaded by the
/// first <see cref="Poll"/>, which the plugin makes right after its start, and that first load always asks Firestone's
/// server (StatsCache); reloaded at each game, off the UI thread.
/// </summary>
internal sealed class CompService
{
    private const string TimePeriod = "last-patch";

    private readonly StatsCache _cache;
    private readonly string _manualDirectory;
    private readonly CompositionRefresh _refresh;
    private IReadOnlyList<Composition> _manual = Array.Empty<Composition>();
    private readonly List<string> _manualErrors = new();

    /// <summary>Minions to flag in the tavern, from stats\manual\pins.txt.</summary>
    public TavernPins Pins { get; private set; } = TavernPins.Empty;

    /// <param name="cache">The plugin's one cache, shared by every service.</param>
    public CompService(StatsCache cache, string statsDirectory)
    {
        _cache = cache;
        _manualDirectory = Path.Combine(statsDirectory, "manual");
        _refresh = new CompositionRefresh(() => Task.Run(() => _cache.GetCompositionsAsync(TimePeriod, RefreshPolicy.CompStats, CancellationToken.None)));
    }

    public int Version { get; private set; }

    /// <summary>Called when a game's first shopping phase starts: reload Firestone's.</summary>
    public void BeginGame()
    {
        _refresh.Request();
        Version++;
    }

    /// <summary>Called on every update: loads Firestone's file if it never was, and collects a finished load.</summary>
    public void Poll()
    {
        if (_refresh.Poll())
        {
            Version++;
            var last = _refresh.Last!;
            PendingLogLine = DataRefresh.Line($"comp-stats {TimePeriod}", last.Downloaded, last.Unchanged, last.Error, last.File?.FetchedAt);
        }
    }

    /// <summary>The diagnostic line of the last finished load, until the plugin logs it; see <see cref="DataRefresh"/>.</summary>
    public string? PendingLogLine { get; set; }

    public IReadOnlyList<Composition> Compositions() =>
        _refresh.Last?.File is { } firestone ? _manual.Concat(firestone.Compositions).ToList() : _manual;

    /// <summary>Firestone side, for the diagnostic line: "loading", "ok", or the reason there is nothing.</summary>
    public string State => _refresh.State;

    public string? Status
    {
        get
        {
            var problems = new List<string>();
            if (_refresh.IsLoading)
            {
                problems.Add("loading Firestone compositions…");
            }

            if (_refresh.Last?.Error is { } error)
            {
                problems.Add(error);
            }

            problems.AddRange(_manualErrors);
            return problems.Count == 0 ? null : string.Join(" · ", problems);
        }
    }

    /// <summary>The hand-typed files (stats\manual\*.comps.txt and pins.txt), read again: at the plugin's start and at each game.</summary>
    public void LoadManual()
    {
        Version++;
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
    }
}
