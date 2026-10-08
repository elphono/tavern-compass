using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Where a loaded stats file comes from, written in its header: the common format every source shares
/// (docs/plans/2026-10-08-stats-multi-sources.html § 6.1). A figure is only compared with another one whose
/// provenance says it measures the same players, over the same period.
/// </summary>
public sealed class StatProvenance
{
    public StatProvenance(string source, string? sourceUrl, DateTimeOffset? generatedAt, DateTimeOffset? fetchedAt,
        string? timePeriod, int? mmrPercentile, string? patch)
    {
        Source = string.IsNullOrEmpty(source) ? throw new ArgumentException("a provenance names its source", nameof(source)) : source;
        SourceUrl = sourceUrl;
        GeneratedAt = generatedAt;
        FetchedAt = fetchedAt;
        TimePeriod = timePeriod;
        MmrPercentile = mmrPercentile;
        Patch = patch;
    }

    /// <summary>An open list ("firestone", "nomi.gg", "hsreplay-manual"…): an unknown source is shown under its name.</summary>
    public string Source { get; }

    public string? SourceUrl { get; }

    /// <summary>When the source computed the figures; null when it does not say.</summary>
    public DateTimeOffset? GeneratedAt { get; }

    /// <summary>When the plugin downloaded them.</summary>
    public DateTimeOffset? FetchedAt { get; }

    /// <summary>The source's window, e.g. "last-patch".</summary>
    public string? TimePeriod { get; }

    /// <summary>The MMR percentile measured (100 = every player); null for a source without brackets.</summary>
    public int? MmrPercentile { get; }

    /// <summary>The game patch measured; null until a source gives one (Firestone does not).</summary>
    public string? Patch { get; }

    /// <summary>Writes the header fields into a local file's root object.</summary>
    public void WriteTo(JObject root)
    {
        root["source"] = Source;
        root["sourceUrl"] = SourceUrl;
        root["generatedAt"] = Format(GeneratedAt);
        root["fetchedAt"] = Format(FetchedAt);
        root["timePeriod"] = TimePeriod;
        root["mmrPercentile"] = MmrPercentile.HasValue ? new JValue(MmrPercentile.Value) : JValue.CreateNull();
        root["patch"] = Patch;
    }

    /// <summary>Reads the header of a local file; <paramref name="what"/> names the file in the error.</summary>
    public static StatProvenance ReadFrom(JObject root, string what)
    {
        if (root.Value<string>("source") is not { Length: > 0 } source)
        {
            throw new StatsFormatException($"{what}: source: expected a non-empty string");
        }

        int? percentile = root["mmrPercentile"]?.Type == JTokenType.Integer ? root.Value<int>("mmrPercentile") : null;
        return new StatProvenance(source, root.Value<string>("sourceUrl"), Date(root, "generatedAt"), Date(root, "fetchedAt"),
            root.Value<string>("timePeriod"), percentile, root.Value<string>("patch"));
    }

    /// <summary>A date as the sources write it ("2026-10-08T00:10:31.956Z"); null when absent or unreadable.</summary>
    public static DateTimeOffset? Date(JObject obj, string name) =>
        obj.Value<string>(name) is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
            ? date
            : null;

    private static JToken Format(DateTimeOffset? date) =>
        date.HasValue
            ? new JValue(date.Value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
            : JValue.CreateNull();
}
