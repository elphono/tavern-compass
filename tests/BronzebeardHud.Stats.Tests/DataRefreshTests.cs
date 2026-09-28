namespace BronzebeardHud.Stats.Tests;

public sealed class DataRefreshTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 28, 14, 56, 10, TimeSpan.FromHours(2));

    [Theory]
    [InlineData(true, false, null, "downloaded")]
    [InlineData(false, true, null, "unchanged (304)")]
    [InlineData(false, false, null, "cached")]
    [InlineData(false, false, "download of x failed: 503", "FAILED, download of x failed: 503")]
    public void Line_SaysWhereTheDataCameFrom_AndHowOldItIs(bool downloaded, bool unchanged, string? error, string outcome)
    {
        Assert.Equal($"Bronzebeard HUD: data comp-stats last-patch: {outcome}, fetched 2026-09-28 12:56 UTC",
            DataRefresh.Line("comp-stats last-patch", downloaded, unchanged, error, At));
    }

    [Fact]
    public void Line_WithNothingLoaded_SaysSo()
    {
        Assert.Equal("Bronzebeard HUD: data trinket-stats last-patch: FAILED, boom, no data",
            DataRefresh.Line("trinket-stats last-patch", false, false, "boom", null));
    }
}
