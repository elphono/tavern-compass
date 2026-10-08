namespace BronzebeardHud.Stats;

/// <summary>
/// While a choice is open in the shop, the markers on Bob's cards leave the screen: the options cover Bob's row, and the ◇
/// buttons fell inside them (a click pinned instead of choosing). The "Compositions" panel stays (Ali, 2026-10-08: "the
/// panel simply always visible").
/// </summary>
public sealed class ChoiceCover
{
    public const string HiddenLine = "Bronzebeard HUD: choice open: markers hidden, panel kept";
    public const string RestoredLine = "Bronzebeard HUD: choice closed: markers restored";

    public bool Hidden { get; private set; }

    public static bool IsOpen(OverlayPhase phase, ChoiceKind kind) => phase == OverlayPhase.Shop && kind != ChoiceKind.None;

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

    public void Reset() => Hidden = false;
}
