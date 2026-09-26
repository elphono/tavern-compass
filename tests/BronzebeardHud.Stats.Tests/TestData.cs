using System.Globalization;

namespace BronzebeardHud.Stats.Tests;

/// <summary>Synthetic stats. No real Firestone or HSReplay data is committed to this repository.</summary>
internal static class TestData
{
    /// <summary>
    /// Twenty heroes with distinct placements 3.0, 3.1, …, 4.9 (hero ids GRID_00 … GRID_19).
    /// Hand-computed: mean 3.95, population σ = sqrt(0.01 × 399 / 12) ≈ 0.5766, so the tier
    /// bounds are A &lt; 3.085 ≤ B &lt; 3.95 ≤ C &lt; 4.527 ≤ D &lt; 5.103 ≤ E, and no hero is S.
    /// </summary>
    public static HeroStatsFile GridPool(string source = StatsSources.Firestone)
    {
        var heroes = Enumerable.Range(0, 20)
            .Select(i => new HeroStat($"GRID_{i:00}", Math.Round(3.0 + 0.1 * i, 2), dataPoints: 100 + i))
            .ToList();
        return new HeroStatsFile(source, heroes);
    }

    public static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A Firestone hero-stats payload with the given hero objects (raw JSON fragments).</summary>
    public static string FirestonePayload(params string[] heroObjects) =>
        "{\"lastUpdateDate\":\"2026-09-26T12:10:35.950Z\",\"dataPoints\":322049,\"mmrPercentiles\":[]," +
        "\"heroStats\":[" + string.Join(",", heroObjects) + "]}";

    public static string FirestoneHero(string id, double averagePosition, int dataPoints, long offered, long picked,
        string? distribution = null, int mmrPercentile = 25, string timePeriod = "past-seven") =>
        $"{{\"heroCardId\":\"{id}\",\"dataPoints\":{dataPoints},\"totalOffered\":{offered},\"totalPicked\":{picked}," +
        $"\"averagePosition\":{Num(averagePosition)},\"conservativePositionEstimate\":{Num(averagePosition + 0.2)}," +
        $"\"placementDistribution\":{distribution ?? "[]"},\"mmrPercentile\":{mmrPercentile},\"timePeriod\":\"{timePeriod}\"}}";

    public static string Rank(int rank, double percentage) =>
        $"{{\"rank\":{rank},\"percentage\":{Num(percentage)},\"totalMatches\":{rank * 7}}}";
}
