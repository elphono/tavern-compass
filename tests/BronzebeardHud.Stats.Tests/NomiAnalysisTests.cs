namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// nomi.gg's patch analysis (chantier c): what the plugin keeps of it, in its local format, and how it enters the
/// consolidation. Invented figures, in the shape of the real file (read once on 2026-10-08, kept out of the repository).
/// </summary>
public class NomiAnalysisTests
{
    public const string Payload = """
        {
          "patch": "36.6.3", "build": 253216, "generated": "2026-10-08T03:03:35Z",
          "window": { "preEnd": "2026-10-01", "postStart": "2026-10-02" },
          "buffed": ["quilboar", "pirate"], "nerfed": ["aberration"],
          "tribes": {
            "pre": { "beast": { "lobby": 0.4, "share": 0.08, "games": 110, "avg": 3.8, "top4": 0.6, "win": 0.2 },
                     "naga": { "lobby": 0.0, "share": 0.0, "games": 0 } },
            "post": { "beast": { "lobby": 0.5, "share": 0.09, "games": 220, "avg": 4.1, "top4": 0.5, "win": 0.1 },
                      "naga": { "lobby": 0.0, "share": 0.0, "games": 0 },
                      "pirate": { "lobby": 0.5, "share": 0.1, "games": 300, "avg": 3.5, "top4": 0.7, "win": 0.3 } }
          },
          "heroes": [
            { "id": "HERO_A", "name": "A", "games": 51, "avg": 2.86, "top4": 0.7, "win": 0.4, "pick": 0.4 },
            { "id": "HERO_B", "name": "B", "games": 0, "avg": 4.5, "top4": 0.5, "win": 0.1, "pick": 0.1 }
          ],
          "boards": [], "cards": { "best": [], "common": [], "under": [] },
          "curve": {
            "all": { "games": 5000, "medians": {
              "first": { "5": { "median": 8, "reached": 0.99 } },
              "top4": { "5": { "median": 8.0, "reached": 0.99 }, "6": { "median": 10.0, "reached": 0.78 } } },
              "curves": [], "tier5": [] },
            "high": { "games": 1500, "medians": { "top4": { "5": { "median": 7, "reached": 1.0 } } }, "curves": [], "tier5": [] }
          },
          "trinkets": {
            "lesser": { "games": 5000, "avg": 3.8, "minPicks": 40,
                        "winners": [ { "id": "T_WIN", "name": "W", "games": 55, "avg": 3.09, "top4": 0.78, "win": 0.36 } ],
                        "losers": [ { "id": "T_LOSE", "name": "L", "games": 60, "avg": 5.2, "top4": 0.3, "win": 0.05 } ] },
            "greater": { "games": 4000, "avg": 3.6, "minPicks": 40, "winners": [], "losers": [] }
          }
        }
        """;

    private static readonly DateTimeOffset Fetched = new(2026, 10, 8, 14, 0, 0, TimeSpan.Zero);

    private static NomiAnalysisFile Imported() => NomiAnalysis.Import(Payload, "https://nomi.gg/patch/analysis/36.6.3.json", Fetched);

    [Fact]
    public void Import_KeepsItsProvenance_ThePatchAndTheWindow()
    {
        var p = Imported().Provenance;

        Assert.Equal(("nomi.gg", "36.6.3", (int?)null, "since 2026-10-02"), (p.Source, p.Patch, p.MmrPercentile, p.TimePeriod));
        Assert.Equal((new DateTimeOffset(2026, 10, 8, 3, 3, 35, TimeSpan.Zero), Fetched), (p.GeneratedAt!.Value, p.FetchedAt!.Value));
        Assert.Equal(253216, Imported().Build);
    }

    [Fact]
    public void Import_KeepsWhatTheHelpsRead()
    {
        var file = Imported();

        Assert.Equal(new[] { "quilboar", "pirate" }, file.Buffed);
        Assert.Equal(new[] { "aberration" }, file.Nerfed);
        Assert.Equal(new[] { ("HERO_A", 51, 2.86, 0.7, 0.4), ("HERO_B", 0, 4.5, 0.5, 0.1) },
            file.Heroes.Select(h => (h.HeroCardId, h.Games, h.AveragePlacement, h.Top4, h.Win)));
        // A tribe out of the patch has no "avg" at all, as in the real file (naga, 2026-10-08): no average, not an error.
        Assert.Equal(new[] { ("beast", 110, 3.8, 220, 4.1), ("naga", 0, 0.0, 0, 0.0), ("pirate", 0, 0.0, 300, 3.5) },
            file.Tribes.Select(t => (t.Tribe, t.PreGames, t.PreAverage ?? 0, t.PostGames, t.PostAverage ?? 0)));
        Assert.Null(file.Tribes.Single(t => t.Tribe == "pirate").PreAverage);
        Assert.Equal(((double?)null, (double?)null), (file.Tribes.Single(t => t.Tribe == "naga").PreAverage, file.Tribes.Single(t => t.Tribe == "naga").PostAverage));
        Assert.Equal(new[] { ("T_WIN", "lesser", true, 55, 3.09), ("T_LOSE", "lesser", false, 60, 5.2) },
            file.Trinkets.Select(t => (t.TrinketCardId, t.Kind, t.Winner, t.Games, t.AveragePlacement)));
        Assert.Equal(new[] { ("all", "first", 5, 8.0, 0.99), ("all", "top4", 5, 8.0, 0.99), ("all", "top4", 6, 10.0, 0.78), ("high", "top4", 5, 7.0, 1.0) },
            file.TierMedians.Select(m => (m.Scope, m.Group, m.Tier, m.MedianTurn, m.Reached)));
    }

    [Fact]
    public void LocalFormat_RoundTrips_AndCarriesNoServerOnlyField()
    {
        var file = Imported();
        var json = NomiAnalysis.Serialize(file);
        var copy = NomiAnalysis.Parse(json);

        Assert.Equal(file.Heroes.Select(h => (h.HeroCardId, h.Games, h.AveragePlacement)), copy.Heroes.Select(h => (h.HeroCardId, h.Games, h.AveragePlacement)));
        Assert.Equal(file.Trinkets.Select(t => (t.TrinketCardId, t.Winner)), copy.Trinkets.Select(t => (t.TrinketCardId, t.Winner)));
        Assert.Equal(file.TierMedians.Count, copy.TierMedians.Count);
        Assert.Equal((file.Provenance.Patch, file.Provenance.TimePeriod, file.Build), (copy.Provenance.Patch, copy.Provenance.TimePeriod, copy.Build));
        Assert.DoesNotContain("\"name\"", json); // display names are HearthDb's, not copied
        Assert.StartsWith("schema", Assert.Throws<StatsFormatException>(() => NomiAnalysis.Parse(json.Replace("\"schema\": 1", "\"schema\": 9"))).Message);
    }

    [Theory]
    [InlineData("{}", "patch")]
    [InlineData("""{ "patch": "36.6.3", "generated": "x", "window": {}, "heroes": "no" }""", "heroes")]
    public void Import_AMalformedPayload_IsRefusedNamingTheField(string payload, string field)
    {
        var e = Assert.Throws<StatsFormatException>(() => NomiAnalysis.Import(payload, "u", Fetched));

        Assert.StartsWith(field, e.Message);
    }

    /// <summary>Heroes and trinkets enter the consolidation, in games, with nomi.gg's patch and no bracket.</summary>
    [Fact]
    public void Snapshot_HeroesAndTrinkets_InGames_AHeroWithoutGamesLeftOut()
    {
        var snapshot = SourceSnapshot.Of(Imported());

        Assert.Equal(("nomi.gg", "36.6.3"), (snapshot.Provenance.Source, snapshot.Provenance.Patch));
        Assert.Equal(new[] { "hero|HERO_A|2.86|51", "trinket|T_WIN|3.09|55", "trinket|T_LOSE|5.2|60" },
            snapshot.Records.Select(r => string.Join("|", r.Kind, r.Subject, r.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), r.Count)));
        Assert.All(snapshot.Records, r => Assert.Equal(("placement", "games"), (r.Measure, r.CountUnit)));
    }
}
