namespace BronzebeardHud.Stats;

/// <summary>
/// While a choice is open in the shop — a discover, a Dark Gift, a trinket, or a kind with no known layout — the game draws
/// its options over Bob's row and the middle of the screen. The markers on Bob's cards (frames, labels, ◇ pin buttons: in a
/// Dark Gift the buttons fell inside the cards, so a click would pin instead of choosing) and the "Compositions" panel (at
/// its default place it covered the bottom of the third option) are then hidden, and shown again as they were once the
/// choice closes; the labels of the choice carry the targets and their colours meanwhile. Seen in the simulation on
/// 2026-10-04, a decision to be confirmed in game by Ali. Only in the shop: in hero selection the panel and the markers are
/// not shown anyway, and the combat shows everything again even when a choice is still listed. One log line per
/// transition, never per update.
/// </summary>
public sealed class ChoiceCover
{
    public const string HiddenLine = "Bronzebeard HUD: choice open: markers and panel hidden";
    public const string RestoredLine = "Bronzebeard HUD: choice closed: restored";

    /// <summary>True while the markers and the panel are hidden.</summary>
    public bool Hidden { get; private set; }

    /// <summary>A choice is open in the shop: any kind but None (<see cref="ChoiceClassifier.Kind"/>).</summary>
    public static bool IsOpen(OverlayPhase phase, ChoiceKind kind) => phase == OverlayPhase.Shop && kind != ChoiceKind.None;

    /// <summary>Called at every update; the log line when the state changes (<see cref="HiddenLine"/>, <see cref="RestoredLine"/>), null otherwise.</summary>
    public string? Observe(OverlayPhase phase, ChoiceKind kind)
    {
        var open = IsOpen(phase, kind);
        if (open == Hidden)
        {
            return null;
        }

        Hidden = open;
        return open ? HiddenLine : RestoredLine;
    }

    /// <summary>Nothing hidden any more, without a line (the feature was switched off by its guard); the next choice hides again.</summary>
    public void Reset() => Hidden = false;
}
