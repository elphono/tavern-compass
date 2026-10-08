namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// The combination rule of docs/plans/2026-10-08-stats-multi-sources.html § 6.2 (StatsConsolidation.Consolidate), on
/// invented figures: align, discount a source outside the player's bracket, pull towards the mean, then judge. The
/// worked example of the note (§ 6.2, "Les preuves") is checked to the hundredth.
/// </summary>
public class StatsConsolidationTests
{
    private const int Player = 25;

    private static SourceSnapshot Snap(string source, int? mmr, string? patch, params StatRecord[] records) =>
        new(new StatProvenance(source, null, null, null, "last-patch", mmr, patch), records);

    private static StatRecord Hero(string id, double placement, int games) => new("hero", id, "placement", placement, games, "games");

    private static ConsolidatedStat Only(ConsolidatedView view) => Assert.Single(view.Stats);

    [Fact]
    public void TheNotesExample_CaseA_Consensus_CaseB_Contested()
    {
        // § 6.2: Firestone 4,000 games in the player's bracket; nomi.gg 51 games over every bracket (discount 0.5).
        var nomi = Snap("nomi.gg", null, null, Hero("H", 2.86, 51));

        var a = Only(StatsConsolidation.Consolidate(new[] { Snap("firestone", Player, null, Hero("H", 3.40, 4000)), nomi }, Player));
        Assert.Equal(StatVerdict.Consensus, a.Verdict);
        Assert.Equal(3.40, a.Value!.Value, precision: 2);
        Assert.Equal(4051, a.Count);

        var b = Only(StatsConsolidation.Consolidate(new[] { Snap("firestone", Player, null, Hero("H", 3.80, 4000)), nomi }, Player));
        Assert.Equal(StatVerdict.Contested, b.Verdict);
        Assert.Equal(3.80, b.Value!.Value, precision: 2);
    }

    [Fact]
    public void TheWeights_FollowTheNote_ADiscountedSourceWeighsHalf()
    {
        // x = (4000 × 3.40 + 0.5 × 51 × 2.86 + 30 × 4.5) / (4000 + 25.5 + 30): 13 807.93 / 4055.5.
        var view = StatsConsolidation.Consolidate(new[]
        {
            Snap("firestone", Player, null, Hero("H", 3.40, 4000)),
            Snap("nomi.gg", null, null, Hero("H", 2.86, 51)),
        }, Player);

        var stat = Only(view);
        Assert.Equal(13807.93 / 4055.5, stat.Value!.Value, precision: 6);
        Assert.Equal(new[] { 1.0, 0.5 }, stat.Contributions.Select(c => c.Discount));
    }

    /// <summary>A small sample is pulled towards 4.5: 3 games at 1.0 are not a first place.</summary>
    [Fact]
    public void ASingleSource_IsPulledTowardsTheMean_AndSaysSingle()
    {
        var stat = Only(StatsConsolidation.Consolidate(new[] { Snap("firestone", Player, null, Hero("H", 1.0, 12)) }, Player));

        Assert.Equal(StatVerdict.Single, stat.Verdict);
        Assert.Equal((12 * 1.0 + 30 * 4.5) / 42, stat.Value!.Value, precision: 9);
    }

    [Fact]
    public void UnderTenGames_NothingIsJudged_TheFiguresStandApart()
    {
        var stat = Only(StatsConsolidation.Consolidate(new[]
        {
            Snap("firestone", Player, null, Hero("H", 3.0, 9)),
            Snap("nomi.gg", null, null, Hero("H", 6.0, 9)),
        }, Player));

        Assert.Equal(StatVerdict.Apart, stat.Verdict);
        Assert.Null(stat.Value);
        Assert.Equal(2, stat.Contributions.Count);
        Assert.All(stat.Contributions, c => Assert.False(c.Included));
    }

    /// <summary>Ten games is enough: the threshold is "under 10", not "10 or under".</summary>
    [Fact]
    public void TenGames_AreJudged()
    {
        var stat = Only(StatsConsolidation.Consolidate(new[] { Snap("firestone", Player, null, Hero("H", 3.0, 10)) }, Player));

        Assert.Equal(StatVerdict.Single, stat.Verdict);
    }

    [Fact]
    public void AnOlderPatch_IsLeftOut_NotMixed()
    {
        var stat = Only(StatsConsolidation.Consolidate(new[]
        {
            Snap("nomi.gg", null, "36.6.3", Hero("H", 3.0, 500)),
            Snap("other", null, "36.6.10", Hero("H", 5.0, 500)),
        }, Player));

        Assert.Equal(StatVerdict.Single, stat.Verdict);
        Assert.Equal((0.5 * 500 * 5.0 + 30 * 4.5) / (0.5 * 500 + 30), stat.Value!.Value, precision: 9);
        Assert.Equal(new[] { false, true }, stat.Contributions.Select(c => c.Included)); // 36.6.10 is after 36.6.3
    }

    /// <summary>A source without a patch is not left out: none has one today.</summary>
    [Fact]
    public void NoPatchKnown_EverySourceCounts()
    {
        var stat = Only(StatsConsolidation.Consolidate(new[]
        {
            Snap("firestone", Player, null, Hero("H", 3.0, 500)),
            Snap("nomi.gg", null, "36.6.3", Hero("H", 3.1, 500)),
        }, Player));

        Assert.Equal(StatVerdict.Consensus, stat.Verdict);
        Assert.All(stat.Contributions, c => Assert.True(c.Included));
    }

    /// <summary>Same subject, different measure or unit: two figures that are not compared.</summary>
    [Fact]
    public void OnlyTheSameKindSubjectMeasureAndUnit_AreCombined()
    {
        var view = StatsConsolidation.Consolidate(new[]
        {
            Snap("firestone", Player, null,
                Hero("H", 3.0, 500),
                Hero("G", 6.0, 500),
                new StatRecord("trinket", "H", "placement", 6.0, 500, "games"),
                new StatRecord("card", "H", "placement at turn 6", 6.0, 500, "plays")),
            Snap("nomi.gg", null, null, new StatRecord("hero", "H", "placement", 6.0, 500, "plays")),
        }, Player);

        Assert.Equal(5, view.Stats.Count);
        Assert.All(view.Stats, s => Assert.Equal(StatVerdict.Single, s.Verdict));
        Assert.Equal(StatVerdict.Single, view.Find("hero", "H", "placement", "games")!.Verdict);
        Assert.Null(view.Find("hero", "X", "placement", "games"));
    }

    /// <summary>
    /// The overlap rule on its edge: intervals x ± 2 × 2.3 / √n. 400 games at 3.0 reach 3.23; 400 at 3.46 reach down to
    /// 3.23 — they touch, consensus; at 3.47 they part, contested.
    /// </summary>
    [Theory]
    [InlineData(3.45, StatVerdict.Consensus)]
    [InlineData(3.47, StatVerdict.Contested)]
    public void Contested_IsWhenTheIntervalsDoNotMeet(double other, StatVerdict verdict)
    {
        var stat = Only(StatsConsolidation.Consolidate(new[]
        {
            Snap("firestone", Player, null, Hero("H", 3.0, 400)),
            Snap("other", Player, null, Hero("H", other, 400)),
        }, Player));

        Assert.Equal(verdict, stat.Verdict);
    }

    /// <summary>
    /// Two intervals that touch meet: the rule is "do not meet", and touching is meeting. The other figure is computed the
    /// way the code computes the bounds, so that the two meet exactly in floating point.
    /// </summary>
    [Fact]
    public void IntervalsThatTouch_Meet()
    {
        var half = 2 * StatsConsolidation.PlacementSpread / Math.Sqrt(400);
        var other = 3.0 + 2 * half;
        Assert.Equal(3.0 + half, other - half); // they touch, to the last bit

        var stat = Only(StatsConsolidation.Consolidate(new[]
        {
            Snap("firestone", Player, null, Hero("H", 3.0, 400)),
            Snap("other", Player, null, Hero("H", other, 400)),
        }, Player));

        Assert.Equal(StatVerdict.Consensus, stat.Verdict);
    }

    /// <summary>Three sources: one pair apart is enough.</summary>
    [Fact]
    public void ThreeSources_OnePairApart_IsContested()
    {
        var stat = Only(StatsConsolidation.Consolidate(new[]
        {
            Snap("a", Player, null, Hero("H", 3.0, 400)),
            Snap("b", Player, null, Hero("H", 3.1, 400)),
            Snap("c", Player, null, Hero("H", 4.0, 400)),
        }, Player));

        Assert.Equal(StatVerdict.Contested, stat.Verdict);
    }

    /// <summary>§ 4: a figure whose period is unknown is not combined; a generation date is enough of a window.</summary>
    [Fact]
    public void NoWindow_IsNotCombined_AGenerationDateIsEnough()
    {
        var bare = new SourceSnapshot(new StatProvenance("typed", null, null, null, null, Player, null), new[] { Hero("H", 3.0, 500) });
        var dated = new SourceSnapshot(new StatProvenance("dated", null, new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero), null, null, Player, null),
            new[] { Hero("H", 3.0, 500) });

        var none = Only(StatsConsolidation.Consolidate(new[] { bare }, Player));
        Assert.Equal(StatVerdict.Apart, none.Verdict);
        Assert.Equal("no window", Assert.Single(none.Contributions).Reason);

        Assert.Equal(StatVerdict.Single, Only(StatsConsolidation.Consolidate(new[] { dated }, Player)).Verdict);
    }

    /// <summary>HDT's log line: per kind, how many subjects and their verdicts, and the contested ones named (five at most).</summary>
    [Fact]
    public void Summary_CountsVerdictsPerKind_AndNamesTheContested()
    {
        var view = StatsConsolidation.Consolidate(new[]
        {
            Snap("firestone", Player, null, Hero("A", 3.0, 400), Hero("B", 4.0, 400), Hero("C", 5.0, 5),
                new StatRecord("card", "K", "placement at turn 6", 4.0, 300, "plays"),
                new StatRecord("card", "K", "placement at turn 7", 4.0, 5, "plays")),
            Snap("other", Player, null, Hero("A", 4.0, 400), Hero("B", 4.05, 400)),
        }, Player);

        Assert.Equal("heroes 3 (0 single, 1 consensus, 1 contested, 1 apart) contested=[A 3.0 ↔ 4.0]", view.Summary("hero"));
        Assert.Equal("cards 1 in 2 figures (1 single, 0 consensus, 0 contested, 1 apart)", view.Summary("card"));
        Assert.Equal("trinkets 0", view.Summary("trinket"));
    }

    /// <summary>No data, no line: the view is empty, never a made-up figure.</summary>
    [Fact]
    public void NoSnapshot_GivesAnEmptyView()
    {
        Assert.Empty(StatsConsolidation.Consolidate(Array.Empty<SourceSnapshot>(), Player).Stats);
    }
}
