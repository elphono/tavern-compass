using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// Where the player has moved each panel, and the room he gave it, kept between sessions in
/// <c>%LocalAppData%\BronzebeardHud\layout.json</c>:
/// <code>{ "schema": 1, "panels": { "target-compositions": { "left": 0.12, "top": 0.64, "width": 0.3, "height": 0.2 } } }</code>
/// Positions and sizes are fractions of the overlay size, so they survive a change of resolution; a panel that
/// would end up partly off-screen is pulled back inside. Panels never moved keep their default place. The size
/// is optional (a panel never resized has none, and the file reads as it always did): it is the room the
/// content gets, never a zoom, so a panel shows more or fewer lines in it, never bigger or smaller text.
/// </summary>
public sealed class PanelLayout
{
    public const int CurrentSchema = 1;

    /// <summary>
    /// The movable panels that exist. An entry for any other name in the file is ignored without a word: the
    /// combats panel, removed on 2026-09-27 (Ali: "l'onglet combat est inutile"), may still sit in an existing
    /// layout.json, and it must neither break the others nor raise a warning. It disappears at the next save.
    /// </summary>
    public static readonly IReadOnlyCollection<string> KnownPanels = new[] { "target-compositions", "lineups", "skip-combat", CompGuideLayout.PanelId };

    private readonly struct Entry
    {
        public Entry(double left, double top, double? width = null, double? height = null)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public double Left { get; }
        public double Top { get; }
        public double? Width { get; }
        public double? Height { get; }
    }

    private readonly Dictionary<string, Entry> _positions;

    private PanelLayout(Dictionary<string, Entry> positions) => _positions = positions;

    public static PanelLayout Empty => new(new Dictionary<string, Entry>(StringComparer.Ordinal));

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

            var positions = new Dictionary<string, Entry>(StringComparer.Ordinal);
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

                double? width = null;
                double? height = null;
                if (position["width"] != null || position["height"] != null)
                {
                    if (!IsPositiveNumber(position["width"]) || !IsPositiveNumber(position["height"]))
                    {
                        return (Empty, $"layout.json: panel \"{panel.Name}\" needs a positive width and height together, or neither; default layout used");
                    }

                    width = position.Value<double>("width");
                    height = position.Value<double>("height");
                }

                positions[panel.Name] = new Entry(position.Value<double>("left"), position.Value<double>("top"), width, height);
            }

            return (new PanelLayout(positions), null);
        }
        catch (JsonException e)
        {
            return (Empty, $"layout.json: invalid JSON ({e.Message}); default layout used");
        }
    }

    private static bool IsPositiveNumber(JToken? token) =>
        token?.Type is JTokenType.Float or JTokenType.Integer && token.Value<double>() > 0;

    public string Serialize() => new JObject
    {
        ["schema"] = CurrentSchema,
        ["panels"] = new JObject(_positions.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new JProperty(p.Key, Written(p.Value)))),
    }.ToString(Formatting.Indented);

    private static JObject Written(Entry entry)
    {
        var written = new JObject
        {
            ["left"] = Math.Round(entry.Left, 5),
            ["top"] = Math.Round(entry.Top, 5),
        };
        if (entry.Width is { } width && entry.Height is { } height)
        {
            written["width"] = Math.Round(width, 5);
            written["height"] = Math.Round(height, 5);
        }

        return written;
    }

    /// <summary>Remembers where a panel was dropped, as fractions of the overlay size. A size it was given stays.</summary>
    public void Store(string panelId, double left, double top, double canvasWidth, double canvasHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            return;
        }

        _positions.TryGetValue(panelId, out var before);
        _positions[panelId] = new Entry(left / canvasWidth, top / canvasHeight, before.Width, before.Height);
    }

    /// <summary>
    /// Remembers a panel's place and the room it was given. A size that is not positive is never written (the file
    /// would be refused at the next start): only the place is kept.
    /// </summary>
    public void StoreRect(string panelId, double left, double top, double width, double height, double canvasWidth, double canvasHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            return;
        }

        if (width <= 0 || height <= 0)
        {
            Store(panelId, left, top, canvasWidth, canvasHeight);
            return;
        }

        _positions[panelId] = new Entry(left / canvasWidth, top / canvasHeight, width / canvasWidth, height / canvasHeight);
    }

    /// <summary>True once the player gave the panel a size (an unresized panel has only a place, or nothing).</summary>
    public bool IsResized(string panelId) => _positions.TryGetValue(panelId, out var entry) && entry.Width != null;

    /// <summary>
    /// Pulls the panel's bottom right corner to (<paramref name="cornerX"/>, <paramref name="cornerY"/>), overlay
    /// pixels, its top left corner staying where <paramref name="current"/> has it: the size stops at
    /// <paramref name="minimum"/> and at the overlay's edge.
    /// </summary>
    public void Resize(string panelId, LayoutRect current, double cornerX, double cornerY, (double Width, double Height) minimum,
        double canvasWidth, double canvasHeight)
    {
        var width = Clamp(cornerX - current.Left, minimum.Width, canvasWidth - current.Left);
        var height = Clamp(cornerY - current.Top, minimum.Height, canvasHeight - current.Top);
        StoreRect(panelId, current.Left, current.Top, width, height, canvasWidth, canvasHeight);
    }

    public void Forget(string panelId) => _positions.Remove(panelId);

    /// <summary>
    /// The panel's rectangle: its default place, or where it was moved, scaled to this overlay size,
    /// and always entirely inside it (a panel larger than the overlay sticks to the top left corner). A panel
    /// given a <paramref name="minimum"/> can be resized: it then has the size the player gave it, raised to that
    /// minimum and cut to the overlay. Without a minimum the panel cannot be resized, and a size found in the
    /// file for it is ignored.
    /// </summary>
    public LayoutRect Resolve(string panelId, LayoutRect defaultRect, double canvasWidth, double canvasHeight, (double Width, double Height)? minimum = null)
    {
        var left = defaultRect.Left;
        var top = defaultRect.Top;
        var width = defaultRect.Width;
        var height = defaultRect.Height;
        if (_positions.TryGetValue(panelId, out var moved))
        {
            left = moved.Left * canvasWidth;
            top = moved.Top * canvasHeight;
            if (minimum is { } least && moved.Width is { } chosenWidth && moved.Height is { } chosenHeight)
            {
                width = Clamp(chosenWidth * canvasWidth, least.Width, canvasWidth);
                height = Clamp(chosenHeight * canvasHeight, least.Height, canvasHeight);
            }
        }

        left = Clamp(left, 0, canvasWidth - width);
        top = Clamp(top, 0, canvasHeight - height);
        return new LayoutRect(left + width / 2, top + height / 2, width, height);
    }

    private static double Clamp(double value, double min, double max) => max < min ? min : Math.Max(min, Math.Min(max, value));
}
