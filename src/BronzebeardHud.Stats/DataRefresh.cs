using System;
using System.Globalization;

namespace BronzebeardHud.Stats;

/// <summary>
/// The line HDT's log gets when a Firestone file has been loaded, so that "are my stats fresh?" is answered
/// by reading the log (CLAUDE.md: read the line before supposing a cause).
/// </summary>
public static class DataRefresh
{
    /// <summary>"Bronzebeard HUD: data comp-stats last-patch: unchanged (304), fetched 2026-09-28 12:56 UTC".</summary>
    public static string Line(string what, bool downloaded, bool unchanged, string? error, DateTimeOffset? fetchedAt)
    {
        var outcome = error != null ? "FAILED, " + error
            : downloaded ? "downloaded"
            : unchanged ? "unchanged (304)"
            : "cached";
        var date = fetchedAt is { } at
            ? ", fetched " + at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
            : ", no data";
        return $"Bronzebeard HUD: data {what}: {outcome}{date}";
    }
}
