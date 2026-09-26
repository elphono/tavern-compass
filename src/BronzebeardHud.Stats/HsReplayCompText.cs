using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace BronzebeardHud.Stats;

/// <summary>
/// Semi-manual import of HSReplay compositions. hsreplay.net answers automated requests with a
/// Cloudflare JavaScript challenge (measured 2026-09-26: HTTP 403 "Just a moment..."), which we do
/// not try to get past; Ali copies what he reads on the page, in his own browser, into a text file:
/// <code>
/// # comment
/// comp: Undead Butcher
/// tribes: undead
/// tier: A
/// avg: 3.95
/// core: Drustfallen Butcher, Handless Forsaken, BG28_309
/// addon: Friendly Geist
/// </code>
/// One block per composition, starting with <c>comp:</c>; <c>core:</c> is required. A card is
/// written either as its id (e.g. <c>BG28_309</c>) or as its name, resolved by the caller
/// (HearthDb in the plugin). Any error rejects the whole file, naming the line.
/// </summary>
public static class HsReplayCompText
{
    private static readonly Regex CardIdPattern = new(@"^[A-Za-z0-9]+(_[A-Za-z0-9]+)+$", RegexOptions.CultureInvariant);
    private static readonly string[] AllowedTiers = { "S", "A", "B", "C", "D", "E", "F" };

    /// <param name="resolveCardName">Card name → card id, or null when unknown.</param>
    public static CompositionFile Parse(string text, Func<string, string?> resolveCardName, DateTimeOffset? fetchedAt = null)
    {
        var compositions = new List<Composition>();
        Block? current = null;
        var lineNumber = 0;
        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                throw new StatsFormatException($"line {lineNumber}: expected \"key: value\"");
            }

            var key = line.Substring(0, colon).Trim().ToLowerInvariant();
            var value = line.Substring(colon + 1).Trim();
            if (key == "comp")
            {
                if (current != null)
                {
                    compositions.Add(current.Build());
                }

                if (value.Length == 0)
                {
                    throw new StatsFormatException($"line {lineNumber}: comp needs a name");
                }

                current = new Block(value, lineNumber);
                continue;
            }

            if (current == null)
            {
                throw new StatsFormatException($"line {lineNumber}: \"{key}\" before any \"comp:\" line");
            }

            switch (key)
            {
                case "tribes":
                    current.Tribes = Items(value).Select(t => t.ToUpperInvariant()).Select(t => t == "MECH" ? "MECHANICAL" : t).ToList();
                    var unknown = current.Tribes.FirstOrDefault(t => !Tribes.All.Contains(t));
                    if (unknown != null)
                    {
                        throw new StatsFormatException($"line {lineNumber}: unknown tribe \"{unknown}\"");
                    }

                    break;
                case "tier":
                    var tier = value.ToUpperInvariant();
                    if (!AllowedTiers.Contains(tier))
                    {
                        throw new StatsFormatException($"line {lineNumber}: tier must be one of {string.Join(", ", AllowedTiers)}");
                    }

                    current.Tier = tier;
                    break;
                case "avg":
                    if (!double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var avg) || avg < 1 || avg > 8)
                    {
                        throw new StatsFormatException($"line {lineNumber}: avg must be a number between 1 and 8");
                    }

                    current.AveragePlacement = avg;
                    break;
                case "core":
                    current.Core = Cards(value, resolveCardName, lineNumber);
                    break;
                case "addon":
                    current.Addon = Cards(value, resolveCardName, lineNumber);
                    break;
                default:
                    throw new StatsFormatException($"line {lineNumber}: unknown key \"{key}\"");
            }
        }

        if (current != null)
        {
            compositions.Add(current.Build());
        }

        var file = new CompositionFile(StatsSources.HsReplayManual, compositions, "https://hsreplay.net/battlegrounds/comps/", fetchedAt: fetchedAt);
        // Same validation as any local composition file (non-empty, no duplicate, …).
        return CompositionLoader.Parse(CompositionLoader.Serialize(file));
    }

    private static IEnumerable<string> Items(string value) =>
        value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0);

    private static List<string> Cards(string value, Func<string, string?> resolveCardName, int lineNumber) =>
        Items(value).Select(item =>
        {
            if (CardIdPattern.IsMatch(item) && item.Any(char.IsDigit))
            {
                return CardIds.Normalize(item);
            }

            return resolveCardName(item) is { } id
                ? CardIds.Normalize(id)
                : throw new StatsFormatException($"line {lineNumber}: unknown card \"{item}\"");
        }).Distinct(StringComparer.Ordinal).ToList();

    private sealed class Block
    {
        private readonly string _name;
        private readonly int _line;

        public Block(string name, int line)
        {
            _name = name;
            _line = line;
        }

        public List<string> Tribes { get; set; } = new();
        public List<string>? Core { get; set; }
        public List<string> Addon { get; set; } = new();
        public string? Tier { get; set; }
        public double? AveragePlacement { get; set; }

        public Composition Build()
        {
            if (Core == null || Core.Count == 0)
            {
                throw new StatsFormatException($"line {_line}: comp \"{_name}\" has no \"core:\" cards");
            }

            var id = "hsr-" + Regex.Replace(_name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            return new Composition(id, _name, Tribes, Core, Addon.Except(Core, StringComparer.Ordinal).ToList(), AveragePlacement, tier: Tier);
        }
    }
}
