namespace BronzebeardHud.Stats.Tests;

public class FinalBoardsTests
{
    private static string Board(int mmr, int turn, params string[] ids) =>
        $"{{\"mmr\":{mmr},\"finalComp\":{{\"turn\":{turn},\"board\":[" + string.Join(",", ids.Select(id => $"{{\"cardID\":\"{id}\"}}")) + "]}}";

    private static string Payload()
    {
        var boards = new List<string>
        {
            Board(9900, 14, "BG32_324_G", "BG25_010", "BG28_309"),
            Board(9500, 13, "BG32_324", "BG25_010", "BG28_309"), // the 9900 board once the golden copy counts as the card: dropped
            Board(9400, 15, "BG32_324", "BG32_880", "BG25_010", "BG31_835"),
            Board(9450, 12, "BG32_324", "BG25_010", "BG28_309"), // the same again: dropped
            Board(8800, 11, "BG32_324", "BG25_009", "BG25_010"),
        };
        boards.AddRange(Enumerable.Range(0, 16).Select(i => Board(6000 + i, 10, "BG32_324", $"TOKEN_{i}")));
        return "{\"timePeriod\":\"last-patch\",\"compStats\":[{\"archetype\":\"undead_butcher\",\"dataPoints\":6461,\"averagePlacement\":3.8," +
               "\"heroStats\":[{\"finalBoards\":[" + string.Join(",", boards) + "]}]}]}";
    }

    [Fact]
    public void Import_KeepsTheFiveHighestMmrBoards_WithoutDuplicates_WithMmrAndTurn()
    {
        var comp = Assert.Single(FirestoneCompImporter.Import(Payload(), "https://example.invalid/", DateTimeOffset.UnixEpoch).Compositions);

        Assert.Equal(new[] { (9900, 14), (9400, 15), (8800, 11), (6015, 10), (6014, 10) }, comp.FinalBoards.Select(b => (b.Mmr, b.Turn ?? 0)));
        Assert.Equal(new[] { "BG32_324", "BG25_010", "BG28_309" }, comp.FinalBoards[0].Cards);
        Assert.Equal(new[] { "BG32_324", "TOKEN_14" }, comp.FinalBoards[4].Cards);
    }

    [Fact]
    public void Loader_RoundTripsBoards_AndRejectsAnEightCardBoard()
    {
        var file = FirestoneCompImporter.Import(Payload(), "https://example.invalid/", DateTimeOffset.UnixEpoch);
        var copy = CompositionLoader.Parse(CompositionLoader.Serialize(file));
        Assert.Equal(file.Compositions[0].FinalBoards.Select(b => $"{b.Mmr}:{b.Turn}:{string.Join(",", b.Cards)}"),
            copy.Compositions[0].FinalBoards.Select(b => $"{b.Mmr}:{b.Turn}:{string.Join(",", b.Cards)}"));

        var json = CompositionLoader.Serialize(file).Replace("\"BG32_880\",", "\"A_1\",\"B_2\",\"C_3\",\"D_4\",\"E_5\",\"BG32_880\",");
        Assert.StartsWith("compositions[0].finalBoards", Assert.Throws<StatsFormatException>(() => CompositionLoader.Parse(json)).Message);
    }
}
