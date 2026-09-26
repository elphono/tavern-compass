namespace BronzebeardHud.Stats.Tests;

public class HeroStatsLoaderTests
{
    private const string ValidFile = """
        {
          "schema": 1,
          "source": "hsreplay-manual",
          "sourceUrl": "https://hsreplay.net/battlegrounds/heroes/",
          "generatedAt": "2026-09-25T08:30:00Z",
          "fetchedAt": "2026-09-26T19:45:12Z",
          "mmrPercentile": 25,
          "timePeriod": "past-seven",
          "heroes": [
            { "heroCardId": "BG22_HERO_004", "averagePlacement": 3.25, "dataPoints": 1200, "pickRate": 0.21,
              "tier": null, "placementDistribution": [21.5, 17.25, 14, 12.5, 10.75, 9.5, 8, 6.5] },
            { "heroCardId": "TB_BaconShop_HERO_39", "averagePlacement": 4.5, "dataPoints": 800, "pickRate": 0.12, "tier": "A" },
            { "heroCardId": "BG31_HERO_802", "averagePlacement": 5.75, "dataPoints": 0 }
          ]
        }
        """;

    [Fact]
    public void Parse_ValidFile_ReadsEveryField()
    {
        var file = HeroStatsLoader.Parse(ValidFile);

        Assert.Equal(StatsSources.HsReplayManual, file.Source);
        Assert.Equal("https://hsreplay.net/battlegrounds/heroes/", file.SourceUrl);
        Assert.Equal(new DateTimeOffset(2026, 9, 25, 8, 30, 0, TimeSpan.Zero), file.GeneratedAt);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 19, 45, 12, TimeSpan.Zero), file.FetchedAt);
        Assert.Equal(25, file.MmrPercentile);
        Assert.Equal("past-seven", file.TimePeriod);
        Assert.Equal(new[] { "BG22_HERO_004", "TB_BaconShop_HERO_39", "BG31_HERO_802" }, file.Heroes.Select(h => h.HeroCardId));

        var first = file.Find("BG22_HERO_004")!;
        Assert.Equal(3.25, first.AveragePlacement);
        Assert.Equal(1200, first.DataPoints);
        Assert.Equal(0.21, first.PickRate);
        Assert.Null(first.Tier);
        Assert.Equal(new[] { 21.5, 17.25, 14, 12.5, 10.75, 9.5, 8, 6.5 }, first.PlacementDistribution);

        var second = file.Find("TB_BaconShop_HERO_39")!;
        Assert.Equal(4.5, second.AveragePlacement);
        Assert.Equal(800, second.DataPoints);
        Assert.Equal(0.12, second.PickRate);
        Assert.Equal("A", second.Tier);

        var third = file.Find("BG31_HERO_802")!;
        Assert.Equal(5.75, third.AveragePlacement);
        Assert.Equal(0, third.DataPoints);
        Assert.Null(third.PickRate);
        Assert.Null(third.PlacementDistribution);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(8.0)]
    public void Parse_PlacementOnTheBounds_IsAccepted(double placement)
    {
        var json = ValidFile.Replace("\"averagePlacement\": 5.75", $"\"averagePlacement\": {TestData.Num(placement)}");
        Assert.Equal(placement, HeroStatsLoader.Parse(json).Find("BG31_HERO_802")!.AveragePlacement);
    }

    [Fact]
    public void Parse_BrokenJson_IsRejected()
    {
        var e = Assert.Throws<StatsFormatException>(() => HeroStatsLoader.Parse(ValidFile.Substring(0, ValidFile.Length / 2)));
        Assert.StartsWith("invalid JSON", e.Message);
    }

    [Fact]
    public void Parse_RootNotAnObject_IsRejected()
    {
        var e = Assert.Throws<StatsFormatException>(() => HeroStatsLoader.Parse("[1, 2, 3]"));
        Assert.Contains("root", e.Message);
    }

    [Fact]
    public void Parse_UnknownSchema_IsRejected()
    {
        var e = Assert.Throws<StatsFormatException>(() => HeroStatsLoader.Parse(ValidFile.Replace("\"schema\": 1", "\"schema\": 2")));
        Assert.StartsWith("schema:", e.Message);
    }

    [Fact]
    public void Parse_UnknownSource_IsRejected()
    {
        var e = Assert.Throws<StatsFormatException>(() => HeroStatsLoader.Parse(ValidFile.Replace("hsreplay-manual", "hsreplay")));
        Assert.StartsWith("source:", e.Message);
    }

    [Fact]
    public void Parse_MissingPlacement_NamesTheField()
    {
        var json = ValidFile.Replace("\"averagePlacement\": 4.5, ", "");
        var e = Assert.Throws<StatsFormatException>(() => HeroStatsLoader.Parse(json));
        Assert.StartsWith("heroes[1].averagePlacement:", e.Message);
    }

    [Theory]
    [InlineData(0.99)]
    [InlineData(8.01)]
    public void Parse_PlacementOutsideOneToEight_IsRejected(double placement)
    {
        var json = ValidFile.Replace("\"averagePlacement\": 5.75", $"\"averagePlacement\": {TestData.Num(placement)}");
        var e = Assert.Throws<StatsFormatException>(() => HeroStatsLoader.Parse(json));
        Assert.StartsWith("heroes[2].averagePlacement:", e.Message);
        Assert.Contains("outside [1, 8]", e.Message);
    }

    [Fact]
    public void Parse_DuplicateHero_IsRejected()
    {
        var json = ValidFile.Replace("\"BG31_HERO_802\"", "\"BG22_HERO_004\"");
        var e = Assert.Throws<StatsFormatException>(() => HeroStatsLoader.Parse(json));
        Assert.Contains("heroes[2].heroCardId", e.Message);
        Assert.Contains("appears twice", e.Message);
    }

    [Fact]
    public void Parse_SkinInsteadOfBaseHero_IsRejected()
    {
        var json = ValidFile.Replace("\"BG31_HERO_802\"", "\"BG31_HERO_802_SKIN_A\"");
        var e = Assert.Throws<StatsFormatException>(() => HeroStatsLoader.Parse(json));
        Assert.Contains("is a skin", e.Message);
    }

    [Fact]
    public void Parse_PickRateAboveOne_IsRejected()
    {
        var e = Assert.Throws<StatsFormatException>(() => HeroStatsLoader.Parse(ValidFile.Replace("0.12", "1.2")));
        Assert.StartsWith("heroes[1].pickRate:", e.Message);
    }

    [Fact]
    public void Parse_UnknownTierLetter_IsRejected()
    {
        var e = Assert.Throws<StatsFormatException>(() => HeroStatsLoader.Parse(ValidFile.Replace("\"tier\": \"A\"", "\"tier\": \"Z\"")));
        Assert.StartsWith("heroes[1].tier:", e.Message);
    }

    [Fact]
    public void Parse_DistributionOfSevenPlacements_IsRejected()
    {
        var e = Assert.Throws<StatsFormatException>(() => HeroStatsLoader.Parse(ValidFile.Replace(", 6.5]", "]")));
        Assert.StartsWith("heroes[0].placementDistribution:", e.Message);
    }

    [Fact]
    public void Parse_EmptyHeroList_IsRejected()
    {
        var json = """{ "schema": 1, "source": "firestone", "heroes": [] }""";
        var e = Assert.Throws<StatsFormatException>(() => HeroStatsLoader.Parse(json));
        Assert.StartsWith("heroes:", e.Message);
    }

    [Fact]
    public void Serialize_ThenParse_KeepsEveryField()
    {
        var original = HeroStatsLoader.Parse(ValidFile);
        var copy = HeroStatsLoader.Parse(HeroStatsLoader.Serialize(original));

        Assert.Equal(original.Source, copy.Source);
        Assert.Equal(original.SourceUrl, copy.SourceUrl);
        Assert.Equal(original.GeneratedAt, copy.GeneratedAt);
        Assert.Equal(original.FetchedAt, copy.FetchedAt);
        Assert.Equal(original.MmrPercentile, copy.MmrPercentile);
        Assert.Equal(original.TimePeriod, copy.TimePeriod);
        Assert.Equal(
            original.Heroes.Select(h => (h.HeroCardId, h.AveragePlacement, h.DataPoints, h.PickRate, h.Tier)),
            copy.Heroes.Select(h => (h.HeroCardId, h.AveragePlacement, h.DataPoints, h.PickRate, h.Tier)));
        Assert.Equal(original.Heroes[0].PlacementDistribution, copy.Heroes[0].PlacementDistribution);
    }

    [Fact]
    public void LoadDirectory_BadFile_IsReportedWithoutBlockingTheGoodOne()
    {
        var directory = Directory.CreateTempSubdirectory("bbhud-loader-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "a-good.json"), ValidFile);
            File.WriteAllText(Path.Combine(directory, "b-bad.json"), ValidFile.Replace("\"schema\": 1", "\"schema\": 7"));
            File.WriteAllText(Path.Combine(directory, "notes.txt"), "not a stats file");

            var (files, errors) = HeroStatsLoader.LoadDirectory(directory);

            var loaded = Assert.Single(files);
            Assert.Equal(StatsSources.HsReplayManual, loaded.Source);
            var error = Assert.Single(errors);
            Assert.EndsWith("b-bad.json", error.Path);
            Assert.StartsWith("schema:", error.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
