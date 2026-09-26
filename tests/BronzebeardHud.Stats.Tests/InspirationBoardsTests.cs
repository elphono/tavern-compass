namespace BronzebeardHud.Stats.Tests;

public class InspirationBoardsTests
{
    private static string Board(int mmr, params string[] ids) =>
        $"{{\"mmr\":{mmr},\"finalComp\":{{\"turn\":13,\"board\":[" + string.Join(",", ids.Select(id => $"{{\"cardID\":\"{id}\"}}")) + "]}}";

    /// <summary>20 boards for one archetype at distinct MMRs; the three best are at 9900, 9400 and 8800,
    /// and a duplicate of the 9900 board sits at 9500 (it must not take a second slot).</summary>
    private static string Payload()
    {
        var boards = new List<string>
        {
            Board(9900, "BG32_324_G", "BG25_010", "BG28_309"),
            Board(9500, "BG32_324", "BG25_010", "BG28_309"),
            Board(9400, "BG32_324", "BG32_880", "BG25_010", "BG31_835"),
            Board(8800, "BG32_324", "BG25_009", "BG25_010"),
        };
        boards.AddRange(Enumerable.Range(0, 16).Select(i => Board(6000 + i, "BG32_324", $"TOKEN_{i}")));
        return "{\"timePeriod\":\"last-patch\",\"compStats\":[{\"archetype\":\"undead_butcher\",\"dataPoints\":6461,\"averagePlacement\":3.8," +
               "\"heroStats\":[{\"finalBoards\":[" + string.Join(",", boards) + "]}]}]}";
    }

    [Fact]
    public void Import_KeepsTheThreeHighestMmrBoards_WithoutDuplicates()
    {
        var comp = Assert.Single(FirestoneCompImporter.Import(Payload(), "https://example.invalid/", DateTimeOffset.UnixEpoch).Compositions);

        Assert.Equal(3, comp.InspirationBoards.Count);
        Assert.Equal(new[] { "BG32_324", "BG25_010", "BG28_309" }, comp.InspirationBoards[0]);
        Assert.Equal(new[] { "BG32_324", "BG32_880", "BG25_010", "BG31_835" }, comp.InspirationBoards[1]);
        Assert.Equal(new[] { "BG32_324", "BG25_009", "BG25_010" }, comp.InspirationBoards[2]);
    }

    [Fact]
    public void Loader_RoundTripsBoards_AndRejectsAnEightCardBoard()
    {
        var file = FirestoneCompImporter.Import(Payload(), "https://example.invalid/", DateTimeOffset.UnixEpoch);
        var copy = CompositionLoader.Parse(CompositionLoader.Serialize(file));
        Assert.Equal(file.Compositions[0].InspirationBoards.Select(b => string.Join(",", b)), copy.Compositions[0].InspirationBoards.Select(b => string.Join(",", b)));

        var json = CompositionLoader.Serialize(file).Replace("\"BG32_880\",", "\"A_1\",\"B_2\",\"C_3\",\"D_4\",\"E_5\",\"BG32_880\",");
        Assert.StartsWith("compositions[0].inspirationBoards", Assert.Throws<StatsFormatException>(() => CompositionLoader.Parse(json)).Message);
    }
}
