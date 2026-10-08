namespace BronzebeardHud.Stats.Tests;

/// <summary>Firestone's card-stats → the local card stats format (synthetic data: the repository holds no real file).</summary>
public class CardStatsTests
{
    private static readonly DateTimeOffset Fetched = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);

    // Two made-up cards. A turn without a number, one never played and one with an impossible placement are dropped;
    // averagePlacementOther is never read (it favours every card played, see CardTurnValue).
    private const string Firestone = """
        {
          "lastUpdateDate": "2026-10-08T00:10:31.956Z",
          "timePeriod": "last-patch",
          "dataPoints": 1000,
          "cardStats": [
            { "cardId": "BG_TEST_A", "totalPlayed": 400, "averagePlacement": 3.5, "averagePlacementOther": 4.1,
              "turnStats": [
                { "turn": 6, "totalPlayed": 100, "averagePlacement": 3.0, "totalOther": 9, "averagePlacementOther": null },
                { "turn": null, "totalPlayed": 61, "averagePlacement": 3.3 },
                { "turn": 7, "totalPlayed": 0, "averagePlacement": 3.3 },
                { "turn": 8, "totalPlayed": 50, "averagePlacement": 9.5 }
              ] },
            { "cardId": "BG_TEST_B", "totalPlayed": 300, "averagePlacement": 5.0,
              "turnStats": [ { "turn": 6, "totalPlayed": 300, "averagePlacement": 5.0 } ] }
          ]
        }
        """;

    private static CardStatsFile Imported() =>
        CardStatsLoader.ImportFirestone(Firestone, "https://example.test/card-stats", Fetched, mmrPercentile: 25);

    [Fact]
    public void ImportFirestone_KeepsProvenance_AndOnlyUsableTurns()
    {
        var file = Imported();

        Assert.Equal(StatsSources.Firestone, file.Provenance.Source);
        Assert.Equal("https://example.test/card-stats", file.Provenance.SourceUrl);
        Assert.Equal(new DateTimeOffset(2026, 10, 8, 0, 10, 31, 956, TimeSpan.Zero), file.Provenance.GeneratedAt);
        Assert.Equal(Fetched, file.Provenance.FetchedAt);
        Assert.Equal("last-patch", file.Provenance.TimePeriod);
        Assert.Equal(25, file.Provenance.MmrPercentile);
        Assert.Null(file.Provenance.Patch);

        var a = Assert.Single(file.Cards, c => c.CardId == "BG_TEST_A");
        var turn = Assert.Single(a.Turns);
        Assert.Equal((6, 100, 3.0), (turn.Turn, turn.Played, turn.AveragePlacement));
        Assert.Equal(2, file.Cards.Count);
    }

    [Fact]
    public void SerializeThenParse_GivesTheSameFile()
    {
        var file = Imported();

        var again = CardStatsLoader.Parse(CardStatsLoader.Serialize(file));

        Assert.Equal(file.Provenance.GeneratedAt, again.Provenance.GeneratedAt);
        Assert.Equal(file.Provenance.MmrPercentile, again.Provenance.MmrPercentile);
        Assert.Equal(file.Provenance.SourceUrl, again.Provenance.SourceUrl);
        Assert.Equal(
            file.Cards.SelectMany(c => c.Turns.Select(t => (c.CardId, t.Turn, t.Played, t.AveragePlacement))),
            again.Cards.SelectMany(c => c.Turns.Select(t => (c.CardId, t.Turn, t.Played, t.AveragePlacement))));
    }

    [Theory]
    [InlineData("""{"schema": 2, "source": "firestone", "cards": [{"cardId": "X", "turns": []}]}""", "schema")]
    [InlineData("""{"schema": 1, "source": "firestone", "cards": []}""", "cards")]
    [InlineData("""{"schema": 1, "source": "firestone", "cards": [{"cardId": "X", "turns": []}, {"cardId": "X", "turns": []}]}""", "cardId")]
    [InlineData("""{"schema": 1, "source": "firestone", "cards": [{"cardId": "X", "turns": [{"turn": 6, "played": 0, "averagePlacement": 3}]}]}""", "played")]
    [InlineData("""{"schema": 1, "source": "firestone", "cards": [{"cardId": "X", "turns": [{"turn": 6, "played": 5, "averagePlacement": 9}]}]}""", "averagePlacement")]
    [InlineData("""{"schema": 1, "cards": [{"cardId": "X", "turns": []}]}""", "source")]
    public void Parse_BrokenRule_RejectsTheFile(string json, string field)
    {
        var e = Assert.Throws<StatsFormatException>(() => CardStatsLoader.Parse(json));
        Assert.Contains(field, e.Message);
    }

    [Fact]
    public void TurnAverage_IsWeightedByTimesPlayed()
    {
        // 100 × 3.0 and 300 × 5.0: 4.5 weighted, 4.0 if each card counted once.
        Assert.Equal(4.5, Imported().TurnAverage(6)!.Value, 6);
        Assert.Null(Imported().TurnAverage(9));
    }

    [Fact]
    public void Find_TakesTheGoldenCardForItsBase()
    {
        var file = Imported();

        Assert.Equal("BG_TEST_A", file.Find("BG_TEST_A_G")?.CardId);
        Assert.Equal("BG_TEST_B", file.Find("BG_TEST_B")?.CardId);
        Assert.Null(file.Find("BG_TEST_C"));
    }
}
