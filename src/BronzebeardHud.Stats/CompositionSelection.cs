using System;
using System.Collections.Generic;
using System.Linq;

namespace BronzebeardHud.Stats;

/// <summary>
/// The compositions Ali ticks in the target panel. Nothing ticked: the advisor picks its targets itself, as
/// before. Something ticked: only those compositions are aimed at, in the tavern and in choices, even when
/// they fall out of the automatic ranking. Each ticked composition keeps one colour of <see cref="Palette"/>
/// for the whole game, on its panel line and on the markers of its minions; the choices are forgotten when a
/// new game starts.
/// </summary>
public sealed class CompositionSelection
{
    /// <summary>
    /// Four colours that read on the game's dark board and differ from each other and from the orange of the
    /// plugin's borders (#D9480F): magenta, lime, sky blue, white. A violet was tried and was too close to
    /// the magenta (palette test).
    /// </summary>
    public static readonly IReadOnlyList<string> Palette = new[] { "#FF2BD6", "#B8FF1F", "#2EA8FF", "#FFFFFF" };

    /// <summary>
    /// Markers when nothing is ticked: white. It never shares the screen with the palette, since ticking a
    /// composition replaces the automatic markers by those of the ticked ones.
    /// </summary>
    public const string AutoColour = "#FFFFFF";

    private readonly List<string> _checked = new();
    private readonly Dictionary<string, int> _slots = new(StringComparer.Ordinal);

    /// <summary>The game the choices belong to; -1 before the first one.</summary>
    public int Game { get; private set; } = -1;

    /// <summary>Ticked compositions, in the order they were ticked.</summary>
    public IReadOnlyList<string> Checked => _checked;

    public bool Any => _checked.Count > 0;

    public bool IsChecked(string compositionId) => _slots.ContainsKey(compositionId);

    /// <summary>Ticks or unticks; false when refused because the four colours are taken.</summary>
    public bool Toggle(string compositionId)
    {
        if (_slots.Remove(compositionId))
        {
            _checked.Remove(compositionId);
            return true;
        }

        if (_checked.Count >= Palette.Count)
        {
            return false;
        }

        var free = Enumerable.Range(0, Palette.Count).First(slot => !_slots.ContainsValue(slot));
        _slots[compositionId] = free;
        _checked.Add(compositionId);
        return true;
    }

    /// <summary>The colour of a ticked composition; null when it is not ticked.</summary>
    public string? ColourOf(string compositionId) => _slots.TryGetValue(compositionId, out var slot) ? Palette[slot] : null;

    /// <summary>
    /// The colour of a marker: that of the first ticked composition among the ones the card advances (they
    /// come in target order), or <see cref="AutoColour"/> when nothing is ticked.
    /// </summary>
    public string MarkerColour(IEnumerable<string> compositionIds) =>
        compositionIds.Select(ColourOf).FirstOrDefault(c => c != null) ?? AutoColour;

    /// <summary>Forgets every choice (the feature was switched off): back to the automatic targets.</summary>
    public void Clear()
    {
        _checked.Clear();
        _slots.Clear();
    }

    /// <summary>A new game forgets the choices; the same game keeps them.</summary>
    public void BeginGame(int game)
    {
        if (game == Game)
        {
            return;
        }

        Game = game;
        Clear();
    }
}
