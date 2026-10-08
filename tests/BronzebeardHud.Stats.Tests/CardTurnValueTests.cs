namespace BronzebeardHud.Stats.Tests;

/// <summary>A card at a turn against every card played at that turn; nothing said under the noise.</summary>
public class CardTurnValueTests
{
    private static CardStatsFile File(params (string Id, int Turn, int Played, double Placement)[] cells) =>
        new(new StatProvenance(StatsSources.Firestone, null, null, null, "last-patch", 25, null),
            cells.GroupBy(c => c.Id)
                .Select(g => new CardStat(g.Key, g.Select(c => new CardTurnStat(c.Turn, c.Played, c.Placement)).ToList()))
                .ToList());

    // Turn 6: (400 × 3.6 + 1600 × 4.0) / 2000 = 3.92. The noise for 400 games: 2 × 2.3 / √400 = 0.23.
    private static readonly CardStatsFile Turn6 = File(("BG_GOOD", 6, 400, 3.6), ("BG_REST", 6, 1600, 4.0), ("BG_GOOD", 7, 400, 3.0));

    [Fact]
    public void ClearlyBetterThanTheTurn_IsBetter()
    {
        var note = CardTurnValue.For(Turn6, "BG_GOOD", 6)!;

        Assert.Equal(CardTurnVerdict.Better, note.Verdict);
        Assert.Equal((6, 3.6, 400), (note.Turn, note.Placement, note.Played));
        Assert.Equal(3.92, note.TurnAverage, 6);
    }

    [Fact]
    public void ClearlyWorseThanTheTurn_IsWorse()
    {
        // (400 × 4.3 + 1600 × 3.9) / 2000 = 3.98: 0.32 worse, above the 0.23 of noise.
        var file = File(("BG_BAD", 6, 400, 4.3), ("BG_REST", 6, 1600, 3.9));

        Assert.Equal(CardTurnVerdict.Worse, CardTurnValue.For(file, "BG_BAD", 6)!.Verdict);
    }

    [Fact]
    public void UnderTheNoise_SaysNothing()
    {
        // (400 × 3.8 + 1600 × 4.0) / 2000 = 3.96: 0.16 better, under the 0.23 of noise for 400 games.
        var file = File(("BG_MEH", 6, 400, 3.8), ("BG_REST", 6, 1600, 4.0));

        Assert.Null(CardTurnValue.For(file, "BG_MEH", 6));
    }

    [Fact]
    public void UnderTheMinimumGap_SaysNothing_HoweverManyGames()
    {
        // 90 000 games: a noise of 0.015, but 0.08 better is under the 0.1 place worth saying.
        var file = File(("BG_BIG", 6, 90_000, 3.92), ("BG_REST", 6, 90_000, 4.08));

        Assert.Null(CardTurnValue.For(file, "BG_BIG", 6));
    }

    [Fact]
    public void TooFewGames_SaysNothing()
    {
        var file = File(("BG_THIN", 6, CardTurnValue.MinimumPlayed - 1, 2.0), ("BG_REST", 6, 5000, 4.0));
        var enough = File(("BG_THIN", 6, CardTurnValue.MinimumPlayed, 2.0), ("BG_REST", 6, 5000, 4.0));

        Assert.Null(CardTurnValue.For(file, "BG_THIN", 6));
        Assert.NotNull(CardTurnValue.For(enough, "BG_THIN", 6));
    }

    [Fact]
    public void UnknownTurnCardOrFile_SaysNothing_NeverAnotherTurnsValue()
    {
        Assert.Null(CardTurnValue.For(Turn6, "BG_GOOD", 20));
        Assert.Null(CardTurnValue.For(Turn6, "BG_NONE", 6));
        Assert.Null(CardTurnValue.For(null, "BG_GOOD", 6));
    }

    [Fact]
    public void GoldenCopy_IsValuedAsItsBaseCard()
    {
        Assert.Equal(CardTurnVerdict.Better, CardTurnValue.For(Turn6, "BG_GOOD_G", 6)?.Verdict);
    }

    [Fact]
    public void Label_GivesTheComparison_ThenTheShortFormWhenItDoesNotFit()
    {
        var better = CardTurnValue.For(Turn6, "BG_GOOD", 6);
        var worse = CardTurnValue.For(File(("BG_BAD", 6, 400, 4.3), ("BG_REST", 6, 1600, 3.9)), "BG_BAD", 6);

        Assert.Equal("t6 ▲ 3.6 vs 3.9", CardTurnValue.Label(better, 30));
        Assert.Equal("t6 ▼ 4.3 vs 4.0", CardTurnValue.Label(worse, 30));
        Assert.Equal("t6 ▲ 3.6", CardTurnValue.Label(better, 10));
        Assert.Null(CardTurnValue.Label(better, 5));
        Assert.Null(CardTurnValue.Label(null, 30));
    }
}
