using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// The player's settings, kept in <c>%LocalAppData%\BronzebeardHud\settings.json</c>, next to layout.json:
/// <code>{ "schema": 1, "suggestedCompositions": 3 }</code>
/// A missing or unreadable file gives the defaults (and an error to report); a value out of range is
/// brought back inside it.
/// </summary>
public sealed class HudSettings
{
    public const int CurrentSchema = 1;
    public const int DefaultSuggested = 3;
    public const int MinSuggested = 1;
    public const int MaxSuggested = 8;

    public HudSettings(int suggestedCompositions = DefaultSuggested) =>
        SuggestedCompositions = Math.Max(MinSuggested, Math.Min(MaxSuggested, suggestedCompositions));

    public static HudSettings Default { get; } = new();

    /// <summary>How many reachable compositions the target panel suggests, besides the ticked ones: 1 to 8.</summary>
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
                return (Default, "settings.json: expected {\"schema\": 1, \"suggestedCompositions\": 1 to 8}; defaults used");
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
