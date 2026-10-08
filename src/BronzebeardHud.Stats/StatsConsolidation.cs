using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>One figure of one source, in the common format: what it measures, its value, and the sample behind it.</summary>
public sealed class StatRecord
{
    public StatRecord(string kind, string subject, string measure, double value, int count, string countUnit)
    {
        Kind = kind;
        Subject = subject;
        Measure = measure;
        Value = value;
        Count = count;
        CountUnit = countUnit;
    }

    /// <summary>"hero", "trinket", "card".</summary>
    public string Kind { get; }

    /// <summary>The card id of the hero, trinket or card: the same key in every source.</summary>
    public string Subject { get; }

    /// <summary>"placement", "placement at turn 6": only the same measure is combined.</summary>
    public string Measure { get; }

    public double Value { get; }

    /// <summary>The sample size; it weighs the figure.</summary>
    public int Count { get; }

    /// <summary>"games" or "plays" (cards played): two units are never combined.</summary>
    public string CountUnit { get; }
}

/// <summary>The figures of one loaded file, with its provenance.</summary>
public sealed class SourceSnapshot
{
    public SourceSnapshot(StatProvenance provenance, IReadOnlyList<StatRecord> records)
    {
        Provenance = provenance;
        Records = records;
    }

    public StatProvenance Provenance { get; }

    public IReadOnlyList<StatRecord> Records { get; }

    /// <summary>
    /// The heroes' average placement, sample in games. The local format is the same for every source (Firestone, a
    /// hand-typed HSReplay file, any other): what is proper to a source is its importer, not this translation.
    /// </summary>
    public static SourceSnapshot Of(HeroStatsFile file) => new(file.Provenance,
        file.Heroes.Where(h => h.DataPoints > 0).Select(h => new StatRecord("hero", h.HeroCardId, "placement", h.AveragePlacement, h.DataPoints, "games")).ToList());

    /// <summary>The trinkets' average placement over every player (the file names no bracket), sample in games picked.</summary>
    public static SourceSnapshot Of(TrinketStatsFile file) => new(file.Provenance,
        file.Trinkets.Where(t => t.DataPoints > 0).Select(t => new StatRecord("trinket", t.TrinketCardId, "placement", t.AveragePlacement, t.DataPoints, "games")).ToList());

    /// <summary>nomi.gg's heroes and its winning and losing trinkets: average placement in games, every bracket mixed.</summary>
    public static SourceSnapshot Of(NomiAnalysisFile file) => new(file.Provenance,
        file.Heroes.Where(h => h.Games > 0).Select(h => new StatRecord("hero", h.HeroCardId, "placement", h.AveragePlacement, h.Games, "games"))
            .Concat(file.Trinkets.Where(t => t.Games > 0).Select(t => new StatRecord("trinket", t.TrinketCardId, "placement", t.AveragePlacement, t.Games, "games")))
            .ToList());

    /// <summary>The cards' average placement at each turn, sample in plays.</summary>
    public static SourceSnapshot Of(CardStatsFile file) => new(file.Provenance,
        file.Cards.SelectMany(c => c.Turns.Where(t => t.Played > 0).Select(t =>
            new StatRecord("card", c.CardId, "placement at turn " + t.Turn.ToString(CultureInfo.InvariantCulture), t.AveragePlacement, t.Played, "plays"))).ToList());
}

public enum StatVerdict
{
    /// <summary>One source judged: its figure, pulled towards the mean.</summary>
    Single,

    /// <summary>Several sources, their intervals meet: the weighted figure.</summary>
    Consensus,

    /// <summary>Several sources, two intervals apart: the weighted figure exists, but the screen shows both.</summary>
    Contested,

    /// <summary>No source with enough games, or none with a window: nothing judged, the figures shown apart if at all.</summary>
    Apart,
}

/// <summary>What one source brought to a consolidated figure, and why it counted or not.</summary>
public sealed class StatContribution
{
    public StatContribution(StatProvenance provenance, StatRecord record, double discount, bool included, string? reason)
    {
        Provenance = provenance;
        Record = record;
        Discount = discount;
        Included = included;
        Reason = reason;
    }

    public StatProvenance Provenance { get; }

    public StatRecord Record { get; }

    /// <summary>1 in the player's bracket, <see cref="StatsConsolidation.OtherBracketDiscount"/> otherwise.</summary>
    public double Discount { get; }

    public bool Included { get; }

    /// <summary>Why it was left out: "older patch", "under 10 games", "no window"; null when included.</summary>
    public string? Reason { get; }
}

public sealed class ConsolidatedStat
{
    public ConsolidatedStat(string kind, string subject, string measure, string countUnit, double? value, int count, StatVerdict verdict,
        IReadOnlyList<StatContribution> contributions)
    {
        Kind = kind;
        Subject = subject;
        Measure = measure;
        CountUnit = countUnit;
        Value = value;
        Count = count;
        Verdict = verdict;
        Contributions = contributions;
    }

    public string Kind { get; }
    public string Subject { get; }
    public string Measure { get; }
    public string CountUnit { get; }

    /// <summary>The weighted figure, pulled towards the mean; null when <see cref="Verdict"/> is Apart.</summary>
    public double? Value { get; }

    /// <summary>The sample of the sources that counted, undiscounted.</summary>
    public int Count { get; }

    public StatVerdict Verdict { get; }

    /// <summary>Every source's figure, counted or not: the "why" of the figure.</summary>
    public IReadOnlyList<StatContribution> Contributions { get; }
}

public sealed class ConsolidatedView
{
    private readonly Dictionary<(string, string, string, string), ConsolidatedStat> _byKey;

    public ConsolidatedView(IReadOnlyList<ConsolidatedStat> stats)
    {
        Stats = stats;
        _byKey = stats.ToDictionary(s => (s.Kind, s.Subject, s.Measure, s.CountUnit));
    }

    public IReadOnlyList<ConsolidatedStat> Stats { get; }

    public ConsolidatedStat? Find(string kind, string subject, string measure, string countUnit) =>
        _byKey.TryGetValue((kind, subject, measure, countUnit), out var stat) ? stat : null;

    /// <summary>
    /// For HDT's log: "heroes 116 (116 single, 0 consensus, 0 contested, 0 apart) contested=[A 3.0 ↔ 4.0]" — subjects
    /// of a kind and the verdicts of their figures ("cards 806 in 9120 figures": one per turn), the contested ones named
    /// with their sources' lowest and highest figures (five at most).
    /// </summary>
    public string Summary(string kind)
    {
        var stats = Stats.Where(s => s.Kind == kind).ToList();
        var subjects = stats.Select(s => s.Subject).Distinct().Count();
        var label = (kind == "hero" ? "heroes" : kind + "s") + " " + subjects.ToString(CultureInfo.InvariantCulture);
        if (stats.Count == 0)
        {
            return label;
        }

        int Count(StatVerdict verdict) => stats.Count(s => s.Verdict == verdict);
        if (stats.Count != subjects)
        {
            label += $" in {stats.Count.ToString(CultureInfo.InvariantCulture)} figures"; // a card has one figure per turn
        }

        var line = $"{label} ({Count(StatVerdict.Single)} single, {Count(StatVerdict.Consensus)} consensus, {Count(StatVerdict.Contested)} contested, {Count(StatVerdict.Apart)} apart)";
        var contested = stats.Where(s => s.Verdict == StatVerdict.Contested).Take(5).Select(s =>
        {
            var values = s.Contributions.Where(c => c.Included).Select(c => c.Record.Value).ToList();
            return string.Format(CultureInfo.InvariantCulture, "{0} {1:0.0} ↔ {2:0.0}", s.Subject, values.Min(), values.Max());
        }).ToList();
        return contested.Count == 0 ? line : $"{line} contested=[{string.Join(", ", contested)}]";
    }
}

/// <summary>
/// The combination rule of docs/plans/2026-10-08-stats-multi-sources.html § 6.2, as one pure function: no network, no
/// disk, no state. Align (same kind, subject, measure and unit; a known older patch is left out), discount a source
/// outside the player's bracket, pull towards the mean, then judge: contested when two sources' intervals do not meet.
/// The thresholds are starting points (decision 8), to be recalibrated on what the inspection tool measures.
/// </summary>
public static class StatsConsolidation
{
    /// <summary>Fictitious games at the mean (HeroCompAffinity's: 2.3² / 0.4² ≈ 30).</summary>
    public const double PriorGames = 30;

    /// <summary>The mean placement of eight players.</summary>
    public const double PriorPlacement = 4.5;

    /// <summary>A source outside the player's bracket weighs half its sample.</summary>
    public const double OtherBracketDiscount = 0.5;

    /// <summary>Under this many games, a source judges nothing.</summary>
    public const int MinimumCount = 10;

    /// <summary>One game's placement varies by about 2.3 places.</summary>
    public const double PlacementSpread = 2.3;

    public static ConsolidatedView Consolidate(IEnumerable<SourceSnapshot> snapshots, int playerPercentile)
    {
        var stats = snapshots
            .SelectMany(s => s.Records.Select(r => (s.Provenance, Record: r)))
            .GroupBy(x => (x.Record.Kind, x.Record.Subject, x.Record.Measure, x.Record.CountUnit))
            .Select(g => Combine(g.Key, g.ToList(), playerPercentile))
            .ToList();
        return new ConsolidatedView(stats);
    }

    private static ConsolidatedStat Combine((string Kind, string Subject, string Measure, string Unit) key,
        List<(StatProvenance Provenance, StatRecord Record)> figures, int playerPercentile)
    {
        var newest = figures.Select(f => f.Provenance.Patch).Where(p => p != null).OrderBy(p => p!, PatchOrder.Instance).LastOrDefault();
        var contributions = figures.Select(f =>
        {
            var discount = f.Provenance.MmrPercentile == playerPercentile ? 1 : OtherBracketDiscount;
            var reason = f.Provenance.Patch != null && newest != null && PatchOrder.Instance.Compare(f.Provenance.Patch, newest) < 0 ? "older patch"
                : f.Provenance.TimePeriod == null && f.Provenance.GeneratedAt == null ? "no window"
                : f.Record.Count < MinimumCount ? $"under {MinimumCount} games"
                : null;
            return new StatContribution(f.Provenance, f.Record, discount, reason == null, reason);
        }).ToList();

        var counted = contributions.Where(c => c.Included).ToList();
        if (counted.Count == 0)
        {
            return new ConsolidatedStat(key.Kind, key.Subject, key.Measure, key.Unit, null, 0, StatVerdict.Apart, contributions);
        }

        var weight = counted.Sum(c => c.Discount * c.Record.Count) + PriorGames;
        var value = (counted.Sum(c => c.Discount * c.Record.Count * c.Record.Value) + PriorGames * PriorPlacement) / weight;
        var verdict = counted.Count == 1 ? StatVerdict.Single : AnyApart(counted) ? StatVerdict.Contested : StatVerdict.Consensus;
        return new ConsolidatedStat(key.Kind, key.Subject, key.Measure, key.Unit, value, counted.Sum(c => c.Record.Count), verdict, contributions);
    }

    /// <summary>Two intervals x ± 2σ/√n that do not meet.</summary>
    private static bool AnyApart(List<StatContribution> counted)
    {
        var intervals = counted.Select(c =>
        {
            var half = 2 * PlacementSpread / Math.Sqrt(c.Record.Count);
            return (Low: c.Record.Value - half, High: c.Record.Value + half);
        }).ToList();
        return intervals.Max(i => i.Low) > intervals.Min(i => i.High);
    }

    /// <summary>"36.6.10" after "36.6.3": compared number by number; a part that is not a number compares as text.</summary>
    private sealed class PatchOrder : IComparer<string>
    {
        public static readonly PatchOrder Instance = new();

        public int Compare(string? x, string? y)
        {
            var a = (x ?? string.Empty).Split('.');
            var b = (y ?? string.Empty).Split('.');
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                var p = i < a.Length ? a[i] : "0";
                var q = i < b.Length ? b[i] : "0";
                var order = int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var m) && int.TryParse(q, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                    ? m.CompareTo(n)
                    : string.CompareOrdinal(p, q);
                if (order != 0)
                {
                    return order;
                }
            }

            return 0;
        }
    }
}
