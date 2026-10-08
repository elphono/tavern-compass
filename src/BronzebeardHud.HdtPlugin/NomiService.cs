using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// nomi.gg's patch analysis for the plugin (chantier c), loaded off the UI thread under the "data-nomi" guard. Asked at
/// the plugin's start and at each game; NomiCache alone decides whether that reaches the network (one attempt a day at
/// most, as agreed with the site's owner). Nothing shows it yet: it enters the consolidated view (chantier d displays it,
/// credited "data: nomi.gg").
/// </summary>
internal sealed class NomiService
{
    private readonly StatsFileRefresh<NomiAnalysisFile> _refresh;

    public NomiService(string statsDirectory, IConditionalFetcher fetcher)
    {
        var cache = new NomiCache(statsDirectory, fetcher, () => DateTimeOffset.UtcNow);
        _refresh = new StatsFileRefresh<NomiAnalysisFile>("nomi.gg patch analysis",
            () => Task.Run(() => cache.GetAsync(CancellationToken.None)), f => f.FetchedAt);
    }

    /// <summary>Changes when a load finishes: the consolidated view is computed again.</summary>
    public int Version => _refresh.Version;

    /// <summary>The analysis kept on disk or just downloaded; null until one exists.</summary>
    public NomiAnalysisFile? File => _refresh.File;

    /// <summary>The diagnostic line of the finished load, until the plugin logs it; see <see cref="DataRefresh"/>.</summary>
    public string? PendingLogLine { get; set; }

    /// <summary>A new game: the cache is asked again, and applies its daily rule.</summary>
    public void BeginGame(int gameNumber) => _refresh.BeginChoice(gameNumber.ToString(CultureInfo.InvariantCulture));

    public void Poll()
    {
        if (_refresh.Poll())
        {
            PendingLogLine = _refresh.LastLine + (File is { } file ? $" (patch {file.Provenance.Patch})" : string.Empty);
        }
    }
}
