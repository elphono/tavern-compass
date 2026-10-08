namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// The best cards of the player's tavern tier and of the next one, at this turn (Ali, 2026-10-08: "which cards we aim at
/// when we level up early"): card-stats against every card played at this turn, the noise rule of CardTurnValue, the
/// lobby's pool only.
/// </summary>
public class EarlyCardsTests
{
    private static CardStatsFile File(params (string Id, int Turn, int Played, double Placement)[] cells) =>
        new(new StatProvenance(StatsSources.Firestone, null, null, null, "last-patch", 25, null),
            cells.GroupBy(c => c.Id)
                .Select(g => new CardStat(g.Key, g.Select(c => new CardTurnStat(c.Turn, c.Played, c.Placement)).ToList()))
                .ToList());

    // Turn 3: 5,300 plays, average (400×3.4 + 400×3.5 + 400×3.9 + 300×3.5 + 400×3.0 + 400×3.0 + 3000×4.1) / 5300 = 3.787.
    // Noise: 0.23 for 400 plays, 0.27 for 300. T2_A −0.39 and T2_B −0.29 are better; T2_C +0.11 says nothing; T3_A −0.29
    // is better; T4_X is tier 4; T2_GONE is good but out of the lobby's pool.
    private static readonly CardStatsFile Turn3 = File(("T2_A", 3, 400, 3.4), ("T2_B", 3, 400, 3.5), ("T2_C", 3, 400, 3.9),
        ("T3_A", 3, 300, 3.5), ("T4_X", 3, 400, 3.0), ("T2_GONE", 3, 400, 3.0), ("REST", 3, 3000, 4.1));

    private static readonly IReadOnlyDictionary<string, int> Pool = new Dictionary<string, int>
    {
        ["T2_A"] = 2,
        ["T2_B"] = 2,
        ["T2_C"] = 2,
        ["T3_A"] = 3,
        ["T4_X"] = 4,
        ["REST"] = 1,
    };

    private static string Describe(IReadOnlyList<EarlyCardsRow> rows) =>
        string.Join(" | ", rows.Select(r => $"{r.Tier}{(r.IsNext ? " next" : string.Empty)}: {string.Join(",", r.Cards.Select(c => c.CardId))}"));

    [Fact]
    public void TheTierAndTheNext_BestFirst_OnlyWhatBeatsTheNoise_OnlyTheLobbysPool()
    {
        Assert.Equal("2: T2_A,T2_B | 3 next: T3_A", Describe(EarlyCards.For(Turn3, turn: 3, tavernTier: 2, Pool)));
    }

    [Fact]
    public void AtMostFourPerTier_TheBestOnes()
    {
        var cells = Enumerable.Range(1, 6).Select(i => ($"T1_{i}", 2, 900, 3.0 + 0.05 * i)).Append(("REST", 2, 9000, 4.4)).ToArray();
        var pool = cells.ToDictionary(c => c.Item1, _ => 1);

        Assert.Equal("1: T1_1,T1_2,T1_3,T1_4", Describe(EarlyCards.For(File(cells), turn: 2, tavernTier: 1, pool)));
    }

    /// <summary>A tier with nothing in the pool (no tier 7 without an anomaly) or nothing above the noise gives no row.</summary>
    [Fact]
    public void NoEmptyRow_ATierSevenOnlyWhenThePoolHasOne()
    {
        var file = File(("T6_A", 5, 400, 3.0), ("T5_A", 5, 400, 4.6), ("REST", 5, 3000, 4.0));
        var pool = new Dictionary<string, int> { ["T6_A"] = 6, ["T5_A"] = 5, ["REST"] = 1 };

        Assert.Equal("6: T6_A", Describe(EarlyCards.For(file, turn: 5, tavernTier: 6, pool)));
        Assert.Equal("6 next: T6_A", Describe(EarlyCards.For(file, turn: 5, tavernTier: 5, pool)));

        var anomaly = File(("T7_A", 5, 400, 3.0), ("REST", 5, 3000, 4.0));
        Assert.Equal("7 next: T7_A", Describe(EarlyCards.For(anomaly, turn: 5, tavernTier: 6, new Dictionary<string, int> { ["T7_A"] = 7, ["REST"] = 1 })));
    }

    /// <summary>Early only: past turn 6 the section gives way to the comps. Without card stats or a known tier, nothing.</summary>
    [Theory]
    [InlineData(6, 1)]
    [InlineData(7, 0)]
    public void OnlyUpToTurnSix(int turn, int rows)
    {
        var file = File(("T2_A", turn, 400, 3.0), ("REST", turn, 3000, 4.0));

        Assert.Equal(rows, EarlyCards.For(file, turn, tavernTier: 2, new Dictionary<string, int> { ["T2_A"] = 2, ["REST"] = 1 }).Count);
        Assert.Empty(EarlyCards.For(null, 3, 2, Pool));
        Assert.Empty(EarlyCards.For(Turn3, 3, 0, Pool));
    }

    /// <summary>The lobby's pool: a minion shows up when it has no tribe, is an amalgam, or has one of its tribes in the lobby.</summary>
    [Fact]
    public void Pool_FollowsTheLobbysTribes_EveryMinionWhileTheyAreUnknown()
    {
        var minions = new (string Id, int Tier, IReadOnlyCollection<string>? Tribes)[]
        {
            ("NEUTRAL", 1, Array.Empty<string>()), ("BEAST", 2, new[] { "BEAST" }), ("MURLOC", 2, new[] { "MURLOC" }),
            ("DUAL", 3, new[] { "MURLOC", "DRAGON" }), ("AMALGAM", 3, new[] { Tribes.Any }), ("UNKNOWN", 4, null), ("NO_TIER", 0, Array.Empty<string>()),
        };

        Assert.Equal(new[] { "AMALGAM:3", "BEAST:2", "DUAL:3", "NEUTRAL:1", "UNKNOWN:4" },
            EarlyCards.Pool(minions, new[] { "BEAST", "DRAGON" }).Select(p => $"{p.Key}:{p.Value}").OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(6, EarlyCards.Pool(minions, Array.Empty<string>()).Count); // lobby unknown: every minion with a tier
    }

    [Fact]
    public void LogLine_NamesTheTurnTheTiersAndEachCardsFigures()
    {
        var rows = EarlyCards.For(Turn3, turn: 3, tavernTier: 2, Pool);

        Assert.Equal("early cards turn=3 tier=2 [2: T2_A 3.4 vs 3.8 (400), T2_B 3.5 vs 3.8 (400); 3 next: T3_A 3.5 vs 3.8 (300)]",
            EarlyCards.LogLine(3, 2, rows));
        Assert.Equal("early cards turn=8 tier=4 [none]", EarlyCards.LogLine(8, 4, Array.Empty<EarlyCardsRow>()));
    }
}
