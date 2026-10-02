using System;
using System.Threading.Tasks;

namespace BronzebeardHud.Stats;

/// <summary>
/// The trinket stats the plugin shows during an HDT session.
/// - The first poll loads them, from the plugin's start on (the cache's first use always asks Firestone's server).
/// - Each new trinket choice asks the cache again, once per choice: the cache then applies its age rule
///   (<see cref="RefreshPolicy.HeroStats"/> in the plugin), so a session longer than a day gets fresh stats
///   instead of keeping the ones loaded at start. Asking once per choice, not once per HDT update, keeps the
///   file from being read from disk several times a second.
/// - The file shown is kept while a load runs, and when a load brings none or fails, so that a choice on screen
///   never loses its stats.
/// </summary>
public sealed class TrinketStatsRefresh
{
    private readonly string _what;
    private readonly Func<Task<(TrinketStatsFile? File, bool Downloaded, string? Error, bool Unchanged)>> _load;
    private Task<(TrinketStatsFile? File, bool Downloaded, string? Error, bool Unchanged)>? _running;
    private string? _lastChoice;

    /// <param name="what">Names the file in the log line, e.g. "trinket-stats last-patch".</param>
    /// <param name="load">One load through the cache, started off the UI thread by the caller.</param>
    public TrinketStatsRefresh(string what, Func<Task<(TrinketStatsFile? File, bool Downloaded, string? Error, bool Unchanged)>> load)
    {
        _what = what;
        _load = load;
    }

    /// <summary>The stats to show; null until a load brings a file.</summary>
    public TrinketStatsFile? File { get; private set; }

    /// <summary>True once a load has finished, whatever it brought.</summary>
    public bool Loaded => Version > 0;

    /// <summary>Incremented each time a load finishes, so that the advice on screen is rebuilt with what it brought.</summary>
    public int Version { get; private set; }

    /// <summary>Why the last load brought nothing new; null when it succeeded.</summary>
    public string? Error { get; private set; }

    /// <summary>The line for HDT's log about the last finished load (see <see cref="DataRefresh"/>); null before.</summary>
    public string? LastLine { get; private set; }

    public bool IsLoading => _running != null;

    /// <summary>A trinket choice is on screen, named by an id unique to it: the first call for it asks the cache again.</summary>
    public void BeginChoice(string choiceId)
    {
        if (string.Equals(choiceId, _lastChoice, StringComparison.Ordinal))
        {
            return;
        }

        _lastChoice = choiceId;
        _running ??= Start();
    }

    /// <summary>Starts the first load when none ran yet; true when a load finished since the last call.</summary>
    public bool Poll()
    {
        if (_running == null && !Loaded)
        {
            _running = Start();
        }

        if (_running is not { IsCompleted: true } done)
        {
            return false;
        }

        _running = null;
        Version++;
        var (downloaded, unchanged) = (false, false);
        if (done.Status == TaskStatus.RanToCompletion)
        {
            var result = done.Result;
            File = result.File ?? File;
            Error = result.Error;
            (downloaded, unchanged) = (result.Downloaded, result.Unchanged);
        }
        else
        {
            Error = "loading failed: " + (done.Exception?.GetBaseException().Message ?? "cancelled");
        }

        LastLine = DataRefresh.Line(_what, downloaded, unchanged, Error, File?.FetchedAt);
        return true;
    }

    private Task<(TrinketStatsFile? File, bool Downloaded, string? Error, bool Unchanged)> Start()
    {
        try
        {
            return _load();
        }
        catch (Exception e)
        {
            return Task.FromException<(TrinketStatsFile? File, bool Downloaded, string? Error, bool Unchanged)>(e);
        }
    }
}
