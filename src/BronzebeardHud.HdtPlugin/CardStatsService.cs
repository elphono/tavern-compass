using System;
using System.Threading;
using System.Threading.Tasks;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// Firestone's card stats for the plugin: the bracket's file through the shared cache's daily rule, loaded off the UI
/// thread (CardStatsRefresh: never another bracket's file on screen), and the value of a card at a turn (CardTurnValue).
/// Polled from HDT's UI thread, like StatsService.
/// </summary>
internal sealed class CardStatsService
{
    private const string TimePeriod = "last-patch";

    private readonly CardStatsRefresh _refresh;

    /// <param name="cache">The plugin's one cache, shared by every service.</param>
    public CardStatsService(StatsCache cache)
    {
        _refresh = new CardStatsRefresh(bracket =>
            Task.Run(() => cache.GetCardStatsAsync(bracket, TimePeriod, RefreshPolicy.HeroStats, CancellationToken.None)));
    }

    /// <summary>Changes when the bracket changes or its file arrives: the values on screen are computed again.</summary>
    public int Version => _refresh.Version;

    public int? Bracket => _refresh.Bracket;

    /// <summary>The bracket's card stats; null until its load brings them.</summary>
    public CardStatsFile? File => _refresh.File;

    /// <summary>The diagnostic line of the finished load, until the plugin logs it; see <see cref="DataRefresh"/>.</summary>
    public string? PendingLogLine { get; set; }

    public void SetBracket(int bracket) => _refresh.SetBracket(bracket);

    /// <summary>A new game: the cache is asked again, once, and applies its daily rule.</summary>
    public void BeginGame(int gameNumber) => _refresh.BeginGame(gameNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public void Poll()
    {
        if (_refresh.Poll())
        {
            PendingLogLine = _refresh.LastLine;
        }
    }

    /// <summary>What card-stats says of the card at this turn; null when nothing above the noise, or no file yet.</summary>
    public CardTurnNote? Note(string cardId, int turn) => CardTurnValue.For(_refresh.File, cardId, turn);
}
