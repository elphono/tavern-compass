using System.Globalization;
using BronzebeardHud.Stats;

// Usage: dotnet run --project tools/BronzebeardHud.Inspect -- <stats folder> [--bracket 25] [--nomi-raw <analysis.json>]
// The stats folder is the plugin's (%LocalAppData%\BronzebeardHud\stats). Nothing is downloaded, nothing is written.
// --nomi-raw reads a nomi.gg analysis as the site serves it (saved from a browser) instead of the plugin's cached one.
var inv = CultureInfo.InvariantCulture;
if (args.Length < 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("usage: BronzebeardHud.Inspect <stats folder> [--bracket 25] [--nomi-raw <analysis.json>]");
    return 2;
}

var folder = args[0];
string? Option(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();
var player = Option("--bracket") is { } bracketText ? int.Parse(bracketText, inv) : 25;
var nomiRaw = Option("--nomi-raw");
var brackets = new[] { 100, 50, 25, 10, 1 };
const string period = "last-patch";
var cache = new StatsCache(folder, new NoNetwork(), () => DateTimeOffset.UtcNow); // only for its file names

T? Read<T>(string path, Func<string, T> load) where T : class
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"  missing: {Path.GetFileName(path)}");
        return null;
    }

    try
    {
        return load(path);
    }
    catch (StatsFormatException e)
    {
        Console.WriteLine($"  unreadable: {Path.GetFileName(path)}: {e.Message}");
        return null;
    }
}

var heroes = brackets.ToDictionary(b => b, b => Read(cache.HeroStatsPath(b, period), HeroStatsLoader.Load));
var cards = brackets.ToDictionary(b => b, b => Read(cache.CardStatsPath(b, period), CardStatsLoader.Load));
var trinkets = Read(cache.TrinketStatsPath(period), TrinketStatsLoader.Load);
var manual = HeroStatsLoader.LoadDirectory(Path.Combine(folder, "manual"));
var nomiPath = Directory.GetFiles(folder, "nomi-patch-analysis-*.json").OrderBy(p => p, StringComparer.Ordinal).LastOrDefault();
var nomi = nomiRaw != null ? Read(nomiRaw, path => NomiAnalysis.Import(File.ReadAllText(path), path, File.GetLastWriteTimeUtc(path)))
    : nomiPath == null ? null : Read(nomiPath, path => NomiAnalysis.Parse(File.ReadAllText(path)));

static string Q(IReadOnlyList<double> sorted, double q) =>
    sorted.Count == 0 ? "–" : sorted[(int)Math.Floor(q * (sorted.Count - 1))].ToString("0.00", CultureInfo.InvariantCulture);

// 1. What the plugin logs for the player's bracket ("stats view" line).
Console.WriteLine($"# 1. The view the plugin consolidates, bracket mmr-{player}");
var snapshots = manual.Files.Select(SourceSnapshot.Of).ToList();
if (heroes[player] is { } mine)
{
    snapshots.Add(SourceSnapshot.Of(mine));
}

if (trinkets != null)
{
    snapshots.Add(SourceSnapshot.Of(trinkets));
}

if (cards[player] is { } myCards)
{
    snapshots.Add(SourceSnapshot.Of(myCards));
}

if (nomi != null)
{
    snapshots.Add(SourceSnapshot.Of(nomi));
}

var view = StatsConsolidation.Consolidate(snapshots, player);
Console.WriteLine($"  sources: {string.Join(", ", snapshots.Select(s => $"{s.Provenance.Source} mmr-{s.Provenance.MmrPercentile?.ToString(inv) ?? "all"} ({s.Records.Count})"))}");
Console.WriteLine($"  hand-typed hero files: {manual.Files.Count} ({manual.Errors.Count} unreadable)");
foreach (var kind in new[] { "hero", "trinket", "card" })
{
    Console.WriteLine("  " + view.Summary(kind));
}

// 2. Each bracket against every player's figures, as if they were two sources: what the discount d and the overlap rule
// make of a real disagreement. The two are not independent (every player includes the bracket): a lower bound.
Console.WriteLine();
Console.WriteLine("# 2. Heroes: each bracket against every player (as two sources)");
Console.WriteLine("  bracket | heroes | contested | |Δ| median, p90 (places) | z median, p90 | Δ mean (bracket − all)");
foreach (var b in brackets.Where(b => b != 100))
{
    if (heroes[b] is not { } h || heroes[100] is not { } all)
    {
        continue;
    }

    var pairs = h.Heroes.Join(all.Heroes, x => x.HeroCardId, y => y.HeroCardId, (x, y) => (x, y))
        .Where(p => p.x.DataPoints >= StatsConsolidation.MinimumCount && p.y.DataPoints >= StatsConsolidation.MinimumCount).ToList();
    var deltas = pairs.Select(p => Math.Abs(p.x.AveragePlacement - p.y.AveragePlacement)).OrderBy(d => d).ToList();
    var z = pairs.Select(p => Math.Abs(p.x.AveragePlacement - p.y.AveragePlacement)
        / (StatsConsolidation.PlacementSpread * Math.Sqrt(1.0 / p.x.DataPoints + 1.0 / p.y.DataPoints))).OrderBy(v => v).ToList();
    var pairView = StatsConsolidation.Consolidate(new[] { SourceSnapshot.Of(h), SourceSnapshot.Of(all) }, b);
    var contested = pairView.Stats.Count(s => s.Verdict == StatVerdict.Contested);
    var mean = pairs.Count == 0 ? 0 : pairs.Average(p => p.x.AveragePlacement - p.y.AveragePlacement);
    Console.WriteLine($"  mmr-{b,-4} | {pairs.Count,6} | {contested,9} | {Q(deltas, 0.5)}, {Q(deltas, 0.9)} | {Q(z, 0.5)}, {Q(z, 0.9)} | {mean.ToString("+0.00;-0.00", inv)}");
}

// 3. Sample sizes: what the thresholds of 10 games (heroes) and 200 plays (a card at a turn) leave out.
Console.WriteLine();
Console.WriteLine($"# 3. Sample sizes (heroes: under {StatsConsolidation.MinimumCount} games judge nothing; cards: under {CardTurnValue.MinimumPlayed} plays say nothing)");
Console.WriteLine("  bracket | heroes | games median, p10, min | under 10 | card figures t3–t10 | plays median, p10 | under 200");
foreach (var b in brackets)
{
    var games = heroes[b]?.Heroes.Select(x => (double)x.DataPoints).OrderBy(v => v).ToList() ?? new List<double>();
    var plays = cards[b]?.Cards.SelectMany(c => c.Turns).Where(t => t.Turn is >= 3 and <= 10).Select(t => (double)t.Played).OrderBy(v => v).ToList()
        ?? new List<double>();
    Console.WriteLine($"  mmr-{b,-4} | {games.Count,6} | {Q(games, 0.5)}, {Q(games, 0.1)}, {(games.Count == 0 ? "–" : games[0].ToString("0", inv))} | {games.Count(g => g < StatsConsolidation.MinimumCount),8} | {plays.Count,19} | {Q(plays, 0.5)}, {Q(plays, 0.1)} | {plays.Count(p => p < CardTurnValue.MinimumPlayed),9}");
}

// 4. Firestone against nomi.gg: the first real disagreement between two sources, and what the rule makes of it.
Console.WriteLine();
Console.WriteLine($"# 4. Heroes: Firestone against nomi.gg (patch {nomi?.Provenance.Patch ?? "–"}, every bracket, discount {StatsConsolidation.OtherBracketDiscount})");
Console.WriteLine("  Firestone | heroes | contested | |Δ| median, p90 (places) | Δ mean (nomi − Firestone) | nomi games median");
foreach (var b in brackets)
{
    if (heroes[b] is not { } h || nomi == null)
    {
        continue;
    }

    var pairView = StatsConsolidation.Consolidate(new[] { SourceSnapshot.Of(h), SourceSnapshot.Of(nomi) }, b);
    var both = pairView.Stats.Where(s => s.Kind == "hero" && s.Contributions.Count(c => c.Included) == 2).ToList();
    double Value(ConsolidatedStat s, string source) => s.Contributions.First(c => c.Provenance.Source == source).Record.Value;
    var deltas = both.Select(s => Math.Abs(Value(s, StatsSources.NomiGg) - Value(s, StatsSources.Firestone))).OrderBy(d => d).ToList();
    var mean = both.Count == 0 ? 0 : both.Average(s => Value(s, StatsSources.NomiGg) - Value(s, StatsSources.Firestone));
    var games = both.Select(s => (double)s.Contributions.First(c => c.Provenance.Source == StatsSources.NomiGg).Record.Count).OrderBy(g => g).ToList();
    Console.WriteLine($"  mmr-{b,-5} | {both.Count,6} | {both.Count(s => s.Verdict == StatVerdict.Contested),9} | {Q(deltas, 0.5)}, {Q(deltas, 0.9)} | {mean.ToString("+0.00;-0.00", inv),24} | {Q(games, 0.5)}");
}

return 0;

/// <summary>The inspection never downloads: StatsCache is only asked for its file names.</summary>
internal sealed class NoNetwork : IConditionalFetcher
{
    public Task<FetchedText> FetchAsync(string url, string? ifNoneMatch, CancellationToken cancellationToken) =>
        throw new InvalidOperationException($"the inspection does not download ({url})");
}
