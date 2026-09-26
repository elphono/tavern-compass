using System;
using System.Threading.Tasks;

namespace BronzebeardHud.Stats;

/// <summary>
/// Loading the compositions without depending on who asks first. HDT calls a plugin's OnLoad again on
/// the same instance when the player disables then re-enables it (Plugins/PluginWrapper.cs:58-94), and
/// HDT itself may start in the middle of a game: a load started only "at the beginning of a game" then
/// never happens. So the first <see cref="Poll"/> starts a load by itself when none was ever requested.
/// </summary>
public sealed class CompositionRefresh
{
    private readonly Func<Task<CompositionCacheResult>> _load;
    private Task<CompositionCacheResult>? _running;

    public CompositionRefresh(Func<Task<CompositionCacheResult>> load) => _load = load;

    /// <summary>The last finished load; null until one has finished.</summary>
    public CompositionCacheResult? Last { get; private set; }

    public bool IsLoading => _running != null;

    /// <summary>A new game: load again (the cache decides whether to download). Ignored while a load runs.</summary>
    public void Request() => _running ??= Start();

    /// <summary>
    /// Called on every overlay update: starts a first load if nothing was ever loaded, and collects a
    /// finished one. True when a new result has just arrived.
    /// </summary>
    public bool Poll()
    {
        if (_running == null && Last == null)
        {
            _running = Start();
        }

        if (_running is not { IsCompleted: true } done)
        {
            return false;
        }

        Last = done.Status == TaskStatus.RanToCompletion
            ? done.Result
            : new CompositionCacheResult(Last?.File, downloaded: false,
                error: "loading failed: " + (done.Exception?.GetBaseException().Message ?? "cancelled"));
        _running = null;
        return true;
    }

    /// <summary>For the diagnostic line: "loading", "ok", or the reason there is nothing (never a bare "none").</summary>
    public string State =>
        _running != null ? "loading"
        : Last == null ? "not started"
        : Last.Error ?? (Last.File == null ? "no file and no error" : "ok");

    private Task<CompositionCacheResult> Start()
    {
        try
        {
            return _load();
        }
        catch (Exception e)
        {
            return Task.FromException<CompositionCacheResult>(e);
        }
    }
}
