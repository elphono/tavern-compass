using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BronzebeardHud.Stats;

/// <summary>
/// Minions the player wants flagged whenever Bob offers them, kept from one game to the next in a
/// text file (<c>stats\manual\pins.txt</c>): one card per line, by name (resolved by the caller,
/// HearthDb in the plugin) or by id; <c>#</c> starts a comment. An unknown card rejects the file,
/// naming the line, like the other hand-written files.
/// </summary>
public sealed class TavernPins
{
    private static readonly Regex CardIdPattern = new(@"^[A-Za-z0-9]+(_[A-Za-z0-9]+)+$", RegexOptions.CultureInvariant);
    private readonly List<string> _ids;

    private TavernPins(IEnumerable<string> ids) => _ids = ids.Distinct(StringComparer.Ordinal).ToList();

    public static TavernPins Empty => new(Array.Empty<string>());

    public IReadOnlyList<string> CardIds => _ids;

    public bool IsPinned(string cardId) => _ids.Contains(Stats.CardIds.Normalize(cardId));

    /// <summary>Pin the card if it is not pinned, unpin it otherwise. Returns true when it ends up pinned.</summary>
    public bool Toggle(string cardId)
    {
        var id = Stats.CardIds.Normalize(cardId);
        if (_ids.Remove(id))
        {
            return false;
        }

        _ids.Add(id);
        return true;
    }

    public static TavernPins Parse(string text, Func<string, string?> resolveCardName)
    {
        var ids = new List<string>();
        var lineNumber = 0;
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            lineNumber++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (CardIdPattern.IsMatch(line) && line.Any(char.IsDigit))
            {
                ids.Add(Stats.CardIds.Normalize(line));
            }
            else if (resolveCardName(line) is { } id)
            {
                ids.Add(Stats.CardIds.Normalize(id));
            }
            else
            {
                throw new StatsFormatException($"line {lineNumber}: unknown card \"{line}\"");
            }
        }

        return new TavernPins(ids);
    }

    /// <summary>One id per line, the format <see cref="Parse"/> reads back.</summary>
    public string Serialize() =>
        "# Tavern pins: one card per line, by name or id\n" + string.Join("\n", _ids) + (_ids.Count > 0 ? "\n" : string.Empty);
}
