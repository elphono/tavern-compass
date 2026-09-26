using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>One row of Blizzard's public Battlegrounds leaderboard.</summary>
public sealed class LeaderboardRow
{
    public LeaderboardRow(int rank, string name, int rating)
    {
        Rank = rank;
        Name = name;
        Rating = rating;
    }

    public int Rank { get; }
    public string Name { get; }
    public int Rating { get; }
}

/// <summary>Parses one page of <c>api/community/leaderboardsData</c> (format read by bg_treehudder's leaderboard.rs).</summary>
public static class LeaderboardPage
{
    public static (IReadOnlyList<LeaderboardRow> Rows, int TotalPages) Parse(string json)
    {
        JObject root;
        try
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
            root = JToken.ReadFrom(reader) as JObject ?? throw new StatsFormatException("leaderboard page: the root must be an object");
        }
        catch (JsonException e)
        {
            throw new StatsFormatException($"leaderboard page: invalid JSON: {e.Message}", e);
        }

        if (root["leaderboard"] is not JObject leaderboard
            || leaderboard["rows"] is not JArray rows
            || leaderboard["pagination"]?["totalPages"] is not { Type: JTokenType.Integer } totalPages)
        {
            throw new StatsFormatException("leaderboard page: leaderboard.rows or leaderboard.pagination.totalPages is missing");
        }

        var parsed = rows.OfType<JObject>()
            .Where(r => r["rank"]?.Type == JTokenType.Integer && r["rating"]?.Type == JTokenType.Integer
                        && r["accountid"]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace(r.Value<string>("accountid")))
            .Select(r => new LeaderboardRow(r.Value<int>("rank"), r.Value<string>("accountid")!, r.Value<int>("rating")))
            .ToList();
        return (parsed, totalPages.Value<int>());
    }
}

/// <summary>Player name → best leaderboard row. Names compare case-insensitively, without the "#1234" tag.</summary>
public sealed class LeaderboardIndex
{
    private readonly Dictionary<string, LeaderboardRow> _rows = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _rows.Count;

    /// <summary>Homonyms keep the higher rating, as leaderboard.rs did.</summary>
    public void Add(LeaderboardRow row)
    {
        var key = Key(row.Name);
        if (!_rows.TryGetValue(key, out var existing) || row.Rating > existing.Rating)
        {
            _rows[key] = row;
        }
    }

    public LeaderboardRow? Lookup(string nameOrBattleTag) =>
        _rows.TryGetValue(Key(nameOrBattleTag), out var row) ? row : null;

    private static string Key(string name) => name.Split('#')[0].Trim();
}

public sealed class LeaderboardFetchResult
{
    public LeaderboardFetchResult(LeaderboardIndex index, IReadOnlyList<int> pagesFetched, string? error)
    {
        Index = index;
        PagesFetched = pagesFetched;
        Error = error;
    }

    public LeaderboardIndex Index { get; }
    public IReadOnlyList<int> PagesFetched { get; }

    /// <summary>Set when a page after the first failed: the index holds what was read before it.</summary>
    public string? Error { get; }
}

/// <summary>
/// Downloads the slice of the leaderboard given by a <see cref="LeaderboardRange"/>, politely: page 1
/// (it gives the page count), then from the last page upwards, since ratings fall with the page
/// number, until a page lies entirely above the range; one pause between two requests, and never
/// more than <see cref="MaxPages"/> requests. A failed first page throws; a failed later page stops
/// the scan and keeps what was read. The caller downloads it at most once per session.
/// </summary>
public sealed class LeaderboardClient
{
    public const int MaxPages = 60;
    public static readonly TimeSpan PauseBetweenPages = TimeSpan.FromSeconds(1);

    private readonly IStatsFetcher _fetcher;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public LeaderboardClient(IStatsFetcher fetcher, Func<TimeSpan, CancellationToken, Task> delay)
    {
        _fetcher = fetcher;
        _delay = delay;
    }

    public static string PageUrl(string region, int page) =>
        $"https://hearthstone.blizzard.com/en-us/api/community/leaderboardsData?region={region}&leaderboardId=battlegrounds&page={page}";

    public async Task<LeaderboardFetchResult> FetchRangeAsync(LeaderboardRange range, CancellationToken cancellationToken)
    {
        var index = new LeaderboardIndex();
        var fetched = new List<int>();

        var (firstRows, totalPages) = LeaderboardPage.Parse(await FetchAsync(range.Region, 1, fetched, cancellationToken).ConfigureAwait(false));
        AddInRange(index, firstRows, range);

        for (var page = totalPages; page >= 2 && fetched.Count < MaxPages; page--)
        {
            IReadOnlyList<LeaderboardRow> rows;
            try
            {
                await _delay(PauseBetweenPages, cancellationToken).ConfigureAwait(false);
                (rows, _) = LeaderboardPage.Parse(await FetchAsync(range.Region, page, fetched, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception e) when (!(e is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                return new LeaderboardFetchResult(index, fetched, $"leaderboard page {page} failed: {e.Message}");
            }

            AddInRange(index, rows, range);
            if (rows.Count > 0 && rows.Min(r => r.Rating) > range.MaxRating)
            {
                break;
            }
        }

        return new LeaderboardFetchResult(index, fetched, null);
    }

    private Task<string> FetchAsync(string region, int page, List<int> fetched, CancellationToken cancellationToken)
    {
        fetched.Add(page);
        return _fetcher.FetchAsync(PageUrl(region, page), cancellationToken);
    }

    private static void AddInRange(LeaderboardIndex index, IEnumerable<LeaderboardRow> rows, LeaderboardRange range)
    {
        foreach (var row in rows.Where(r => r.Rating >= range.MinRating && r.Rating <= range.MaxRating))
        {
            index.Add(row);
        }
    }
}

/// <summary>A lobby opponent, as HDT reports the lobby (name and hero), with its leaderboard place in the game.</summary>
public sealed class OpponentMmr
{
    public OpponentMmr(int leaderboardPlace, string name, LeaderboardRow? row)
    {
        LeaderboardPlace = leaderboardPlace;
        Name = name;
        Row = row;
    }

    /// <summary>1 to 8: which tile of the in-game leaderboard the opponent sits on.</summary>
    public int LeaderboardPlace { get; }

    public string Name { get; }

    /// <summary>Null when the name is not in the downloaded slice.</summary>
    public LeaderboardRow? Row { get; }

    /// <summary>
    /// Matches each lobby player to its in-game leaderboard place through its hero (skins mapped to
    /// the base hero), then looks the name up. Players whose hero is not on the leaderboard yet are left out.
    /// </summary>
    public static IReadOnlyList<OpponentMmr> Build(
        IEnumerable<(string Name, string HeroCardId)> lobby,
        IReadOnlyDictionary<string, int> placeByHeroCardId,
        LeaderboardIndex index)
    {
        var places = placeByHeroCardId
            .GroupBy(kv => HeroIdNormalizer.Normalize(kv.Key), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);
        return lobby
            .Where(p => !string.IsNullOrWhiteSpace(p.Name) && !string.IsNullOrWhiteSpace(p.HeroCardId))
            .Select(p => (Player: p, Found: places.TryGetValue(HeroIdNormalizer.Normalize(p.HeroCardId), out var place), Place: place))
            .Where(x => x.Found)
            .Select(x => new OpponentMmr(x.Place, x.Player.Name, index.Lookup(x.Player.Name)))
            .OrderBy(o => o.LeaderboardPlace)
            .ToList();
    }
}

/// <summary>
/// Where to write an opponent's MMR: right of its tile on the in-game leaderboard, with HDT's
/// constants (HearthSim/Hearthstone-Deck-Tracker, master 509bb0b): first tile top 0.15 × H
/// (Windows/OverlayWindow.MouseOverDetection.cs:48), one square tile of 0.69 × H / 8 per place
/// (Windows/OverlayWindow.xaml.cs:509, 513), tile left at GetScaledXPos(0.001 × (8 − place))
/// (OverlayWindow.MouseOverDetection.cs:114).
/// </summary>
public static class LeaderboardLayout
{
    public const double Top = 0.15;
    public const double TileSize = 0.69 / 8;

    public static LayoutRect MmrLabel(double width, double height, int place)
    {
        var frameWidth = height * 4 / 3;
        var frameLeft = (width - frameWidth) / 2;
        var tile = TileSize * height;
        var tileLeft = frameLeft + frameWidth * 0.001 * (8 - place);
        var tileTop = Top * height + tile * (place - 1);
        var labelWidth = 0.09 * height;
        var labelHeight = 0.028 * height;
        return new LayoutRect(tileLeft + tile + 0.004 * height + labelWidth / 2, tileTop + tile / 2, labelWidth, labelHeight);
    }
}
