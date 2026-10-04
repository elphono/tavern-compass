using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// The player's settings, kept in <c>%LocalAppData%\BronzebeardHud\settings.json</c>, next to layout.json:
/// <code>{ "schema": 1, "suggestedCompositions": 3 }</code>
/// A missing or unreadable file gives the defaults (and an error to report); a value out of range is
/// brought back inside it: a file written when the bound was 8 (before the guides became the targets) and set to 5
/// to 8 reads as 4.
/// </summary>
public sealed class HudSettings
{
    public const int CurrentSchema = 1;
    public const int DefaultSuggested = 3;
    public const int MinSuggested = 1;

    /// <summary>One target per colour of CompTargetTracker.Palette.</summary>
    public const int MaxSuggested = 4;

    public HudSettings(int suggestedCompositions = DefaultSuggested) =>
        SuggestedCompositions = Math.Max(MinSuggested, Math.Min(MaxSuggested, suggestedCompositions));

    public static HudSettings Default { get; } = new();

    /// <summary>How many comp guides are targets (CompTargetTracker.Next), the ticked ones included: 1 to 4.</summary>
    public int SuggestedCompositions { get; }

    public HudSettings WithSuggested(int suggestedCompositions) => new(suggestedCompositions);

    public static (HudSettings Settings, string? Error) Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (Default, null);
        }

        try
        {
            using var reader = new JsonTextReader(new StringReader(json!));
            if (JToken.ReadFrom(reader) is not JObject root
                || root["schema"]?.Type != JTokenType.Integer || root.Value<int>("schema") != CurrentSchema
                || root["suggestedCompositions"]?.Type != JTokenType.Integer)
            {
                return (Default, "settings.json: expected {\"schema\": 1, \"suggestedCompositions\": 1 to 4}; defaults used");
            }

            return (new HudSettings(root.Value<int>("suggestedCompositions")), null);
        }
        catch (JsonException e)
        {
            return (Default, $"settings.json: invalid JSON ({e.Message}); defaults used");
        }
    }

    public string Serialize() => new JObject
    {
        ["schema"] = CurrentSchema,
        ["suggestedCompositions"] = SuggestedCompositions,
    }.ToString(Formatting.Indented);
}
