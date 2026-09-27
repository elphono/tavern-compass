using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Where the player has moved each panel, kept between sessions in <c>%LocalAppData%\BronzebeardHud\layout.json</c>:
/// <code>{ "schema": 1, "panels": { "target-compositions": { "left": 0.12, "top": 0.64 } } }</code>
/// Positions are fractions of the overlay size, so they survive a change of resolution; a panel that
/// would end up partly off-screen is pulled back inside. Panels never moved keep their default place.
/// </summary>
public sealed class PanelLayout
{
    public const int CurrentSchema = 1;

    /// <summary>
    /// The movable panels that exist. An entry for any other name in the file is ignored without a word: the
    /// combats panel, removed on 2026-09-27 (Ali: "l'onglet combat est inutile"), may still sit in an existing
    /// layout.json, and it must neither break the others nor raise a warning. It disappears at the next save.
    /// </summary>
    public static readonly IReadOnlyCollection<string> KnownPanels = new[] { "target-compositions", "lineups", "skip-combat" };

    private readonly Dictionary<string, (double Left, double Top)> _positions;

    private PanelLayout(Dictionary<string, (double Left, double Top)> positions) => _positions = positions;

    public static PanelLayout Empty => new(new Dictionary<string, (double, double)>(StringComparer.Ordinal));

    public IReadOnlyCollection<string> MovedPanels => _positions.Keys;

    /// <summary>
    /// Reads the file's content. Anything unreadable gives the default layout plus an error to report:
    /// a corrupt file must never leave panels in odd places, nor stop the plugin.
    /// </summary>
    public static (PanelLayout Layout, string? Error) Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (Empty, null);
        }

        try
        {
            using var reader = new JsonTextReader(new StringReader(json!));
            if (JToken.ReadFrom(reader) is not JObject root
                || root["schema"]?.Type != JTokenType.Integer || root.Value<int>("schema") != CurrentSchema
                || root["panels"] is not JObject panels)
            {
                return (Empty, "layout.json: expected {\"schema\": 1, \"panels\": {...}}; default layout used");
            }

            var positions = new Dictionary<string, (double, double)>(StringComparer.Ordinal);
            foreach (var panel in panels.Properties())
            {
                if (!KnownPanels.Contains(panel.Name))
                {
                    continue; // a panel that no longer exists (see KnownPanels): ignored, never reported
                }

                if (panel.Value is not JObject position
                    || position["left"]?.Type is not (JTokenType.Float or JTokenType.Integer)
                    || position["top"]?.Type is not (JTokenType.Float or JTokenType.Integer))
                {
                    return (Empty, $"layout.json: panel \"{panel.Name}\" needs numeric left and top; default layout used");
                }

                positions[panel.Name] = (position.Value<double>("left"), position.Value<double>("top"));
            }

            return (new PanelLayout(positions), null);
        }
        catch (JsonException e)
        {
            return (Empty, $"layout.json: invalid JSON ({e.Message}); default layout used");
        }
    }

    public string Serialize() => new JObject
    {
        ["schema"] = CurrentSchema,
        ["panels"] = new JObject(_positions.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new JProperty(p.Key, new JObject
        {
            ["left"] = Math.Round(p.Value.Left, 5),
            ["top"] = Math.Round(p.Value.Top, 5),
        }))),
    }.ToString(Formatting.Indented);

    /// <summary>Remembers where a panel was dropped, as fractions of the overlay size.</summary>
    public void Store(string panelId, double left, double top, double canvasWidth, double canvasHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            return;
        }

        _positions[panelId] = (left / canvasWidth, top / canvasHeight);
    }

    public void Forget(string panelId) => _positions.Remove(panelId);

    /// <summary>
    /// The panel's rectangle: its default place, or where it was moved, scaled to this overlay size,
    /// and always entirely inside it (a panel larger than the overlay sticks to the top left corner).
    /// </summary>
    public LayoutRect Resolve(string panelId, LayoutRect defaultRect, double canvasWidth, double canvasHeight)
    {
        var left = defaultRect.Left;
        var top = defaultRect.Top;
        if (_positions.TryGetValue(panelId, out var moved))
        {
            left = moved.Left * canvasWidth;
            top = moved.Top * canvasHeight;
        }

        left = Clamp(left, 0, canvasWidth - defaultRect.Width);
        top = Clamp(top, 0, canvasHeight - defaultRect.Height);
        return new LayoutRect(left + defaultRect.Width / 2, top + defaultRect.Height / 2, defaultRect.Width, defaultRect.Height);
    }

    private static double Clamp(double value, double min, double max) => max < min ? min : Math.Max(min, Math.Min(max, value));
}
