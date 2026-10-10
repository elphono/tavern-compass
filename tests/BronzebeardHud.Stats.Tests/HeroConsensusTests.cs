namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// Component 3 of docs/plans/2026-10-08-stats-multi-sources.html § 11: the hero badge shows one consolidated line, with
/// its sample, instead of one line per source; a contest shows both figures. Invented figures.
/// </summary>
public class HeroConsensusTests
{
    private const int Player = 25;

    private static SourceSnapshot Snap(string source, int? mmr, params (string Id, double Placement, int Games)[] heroes) =>
        new(new StatProvenance(source, null, null, null, "last-patch", mmr, null),
            heroes.Select(h => new StatRecord("hero", h.Id, "placement", h.Placement, h.Games, "games")).ToList());

    private static ConsolidatedStat? Stat(params SourceSnapshot[] snapshots) =>
        StatsConsolidation.Consolidate(snapshots, Player).Find("hero", "H", "placement", "games");

    [Fact]
    public void Consensus_OneFigure_ItsGames_AndEverySourceNamed()
    {
        var line = HeroConsensus.For(Stat(Snap("firestone", Player, ("H", 3.40, 4000)), Snap("nomi.gg", null, ("H", 3.35, 51))))!;

        Assert.Equal(("3.41 · 4,051 games", "FS 25% + nomi.gg", false), (line.Figure, line.Sources, line.Contested));
    }

    [Fact]
    public void Single_ItsSourceAlone_EveryPlayerWithoutABracket()
    {
        var line = HeroConsensus.For(Stat(Snap("firestone", 100, ("H", 4.10, 1200))))!;

        Assert.Equal(("4.12 · 1,200 games", "FS", false), (line.Figure, line.Sources, line.Contested)); // 600 games out of the bracket + 30 at 4.5
    }

    /// <summary>Contested: the lowest and highest figures, each with its source, never their mean.</summary>
    [Fact]
    public void Contested_BothFigures_EachWithItsSource()
    {
        var line = HeroConsensus.For(Stat(Snap("firestone", Player, ("H", 3.80, 4000)), Snap("nomi.gg", null, ("H", 2.86, 51))))!;

        Assert.Equal(("2.9 ↔ 3.8 contested", "nomi.gg ↔ FS 25%", true), (line.Figure, line.Sources, line.Contested));
    }

    /// <summary>Under ten games nothing is judged: no consolidated line, the badge keeps its per-source lines.</summary>
    [Fact]
    public void Apart_OrUnknown_NoLine()
    {
        Assert.Null(HeroConsensus.For(Stat(Snap("firestone", Player, ("H", 3.0, 9)))));
        Assert.Null(HeroConsensus.For(null));
    }

    /// <summary>Component 8, the "why", in HDT's log: every source, its figure, its shift, its weight, and the pick rate the badge no longer shows.</summary>
    [Fact]
    public void Why_NamesEverySourceItsShiftWeightAndThePickRate()
    {
        var firestone = new SourceSnapshot(new StatProvenance("firestone", null, null, null, "last-patch", Player, null),
            new[] { new StatRecord("hero", "H", "placement", 3.40, 4000, "games") }, new Dictionary<string, double> { ["hero"] = 4.1 });
        var nomi = new SourceSnapshot(new StatProvenance("nomi.gg", null, null, null, "last-patch", null, null),
            new[] { new StatRecord("hero", "H", "placement", 3.10, 51, "games"), new StatRecord("hero", "J", "placement", 3.9, 9, "games") },
            new Dictionary<string, double> { ["hero"] = 3.8 });
        var view = StatsConsolidation.Consolidate(new[] { firestone, nomi }, Player);

        Assert.Equal("H consensus 3.41 (4051 games) ← FS 25% 3.40 (4000) · nomi.gg 3.10+0.30 (51, ×0.5) · pick 12%",
            HeroConsensus.Why("H", view.Find("hero", "H", "placement", "games"), 0.12));
        Assert.Equal("J apart ← nomi.gg 3.90+0.30 (9, ×0.5, under 10 games)", HeroConsensus.Why("J", view.Find("hero", "J", "placement", "games"), null));
        Assert.Equal("K no figure", HeroConsensus.Why("K", null, null));
    }
}
