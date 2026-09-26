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

    public static TavernPins Of(IEnumerable<string> cardIds) => new(cardIds.Select(Stats.CardIds.Normalize));

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

/// <summary>
/// Pins made by clicking a tavern card during a game (the plugin's own Tavern Pinning, a Tier7 feature of
/// HDT otherwise), on top of pins.txt, which stays as it is: a click pins a card for the rest of the game,
/// a second click unpins it, a card of pins.txt included (for this game only). A new game forgets the clicks.
/// </summary>
public sealed class GamePins
{
    private readonly HashSet<string> _added = new(StringComparer.Ordinal);
    private readonly HashSet<string> _removed = new(StringComparer.Ordinal);

    /// <summary>The game the clicks belong to; -1 before the first one.</summary>
    public int Game { get; private set; } = -1;

    public void BeginGame(int game)
    {
        if (game == Game)
        {
            return;
        }

        Game = game;
        _added.Clear();
        _removed.Clear();
    }

    /// <summary>pins.txt plus the cards pinned by click, minus the ones unpinned by click.</summary>
    public TavernPins Merge(TavernPins file) =>
        TavernPins.Of(file.CardIds.Concat(_added).Where(id => !_removed.Contains(id)));

    /// <summary>Pins the card if it is not pinned (by file or click), unpins it otherwise; true when it ends up pinned.</summary>
    public bool Toggle(string cardId, TavernPins file)
    {
        var id = CardIds.Normalize(cardId);
        if (Merge(file).IsPinned(id))
        {
            _added.Remove(id);
            if (file.IsPinned(id))
            {
                _removed.Add(id);
            }

            return false;
        }

        _removed.Remove(id);
        if (!file.IsPinned(id))
        {
            _added.Add(id);
        }

        return true;
    }
}
