using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>A hero badge's consolidated line: the figure and its sample, or both figures of a contest; then its sources.</summary>
public sealed class HeroConsensusLine
{
    public HeroConsensusLine(string figure, string sources, bool contested)
    {
        Figure = figure;
        Sources = sources;
        Contested = contested;
    }

    /// <summary>"3.41 · 4,051 games", or "2.9 ↔ 3.8 contested".</summary>
    public string Figure { get; }

    /// <summary>"FS 25% + nomi.gg", or for a contest the two sources in the figure's order: "nomi.gg ↔ FS 25%".</summary>
    public string Sources { get; }

    public bool Contested { get; }
}

/// <summary>
/// Component 3 of docs/plans/2026-10-08-stats-multi-sources.html § 11 (decisions 6 and 7): one line per hero from the
/// consolidated view, its sample in place of the pick rate, "contested" with both figures rather than their mean; and
/// component 8, the "why" of that line, for HDT's log.
/// </summary>
public static class HeroConsensus
{
    /// <summary>Null when nothing is judged (no figure, or under ten games everywhere): the badge keeps its per-source lines.</summary>
    public static HeroConsensusLine? For(ConsolidatedStat? stat)
    {
        if (stat?.Value is not { } value || stat.Verdict == StatVerdict.Apart)
        {
            return null;
        }

        var counted = stat.Contributions.Where(c => c.Included).ToList();
        if (stat.Verdict == StatVerdict.Contested)
        {
            var low = counted.OrderBy(c => c.Value).First();
            var high = counted.OrderBy(c => c.Value).Last();
            return new HeroConsensusLine(F("{0:0.0} ↔ {1:0.0} contested", low.Value, high.Value), $"{Label(low)} ↔ {Label(high)}", true);
        }

        return new HeroConsensusLine(F("{0:0.00} · {1:N0} games", value, stat.Count), string.Join(" + ", counted.Select(Label).Distinct()), false);
    }

    /// <summary>"H consensus 3.41 (4051 games) ← FS 25% 3.40 (4000) · nomi.gg 3.10+0.30 (51, ×0.5) · pick 12%".</summary>
    public static string Why(string heroId, ConsolidatedStat? stat, double? pickRate)
    {
        if (stat == null)
        {
            return heroId + " no figure";
        }

        var head = stat.Value is { } value
            ? F("{0} {1} {2:0.00} ({3} games)", heroId, stat.Verdict.ToString().ToLowerInvariant(), value, stat.Count)
            : $"{heroId} {stat.Verdict.ToString().ToLowerInvariant()}";
        var parts = stat.Contributions.Select(c =>
        {
            var notes = new List<string> { c.Record.Count.ToString(CultureInfo.InvariantCulture) };
            if (c.Discount != 1)
            {
                notes.Add(F("×{0:0.##}", c.Discount));
            }

            if (c.Reason != null)
            {
                notes.Add(c.Reason);
            }

            var shift = c.Shift == 0 ? string.Empty : F("{0:+0.00;-0.00}", c.Shift);
            return F("{0} {1:0.00}{2} ({3})", Label(c), c.Record.Value, shift, string.Join(", ", notes));
        });
        var line = $"{head} ← {string.Join(" · ", parts)}";
        return pickRate is { } pick ? line + F(" · pick {0:0}%", pick * 100) : line;
    }

    private static string Label(StatContribution c) =>
        StatsSources.Label(c.Provenance.Source) + (c.Provenance.MmrPercentile is { } mmr && mmr < MmrBracket.EveryPlayer ? $" {mmr}%" : string.Empty);

    private static string F(string format, params object[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
