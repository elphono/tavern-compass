namespace BronzebeardHud.Stats;

/// <summary>
/// What a choice open in the shop takes off the screen: the markers on Bob's cards for any choice, the "Compositions"
/// panel for any but trinkets (Ali, 2026-10-08: the panel must not disappear while choosing a trinket).
/// </summary>
public sealed class ChoiceCover
{
    public const string HiddenLine = "Bronzebeard HUD: choice open: markers and panel hidden";
    public const string PanelKeptLine = "Bronzebeard HUD: choice open (trinkets): markers hidden, panel kept";
    public const string RestoredLine = "Bronzebeard HUD: choice closed: restored";

    /// <summary>True while a choice is open: the markers on Bob's cards are off the screen.</summary>
    public bool Hidden { get; private set; }

    /// <summary>True while a choice other than trinkets is open: the panel and its popup are off the screen too.</summary>
    public bool PanelHidden { get; private set; }

    public static bool IsOpen(OverlayPhase phase, ChoiceKind kind) => phase == OverlayPhase.Shop && kind != ChoiceKind.None;

    /// <summary>One line when what is hidden changes, none for the next updates of the same state.</summary>
    public string? Observe(OverlayPhase phase, ChoiceKind kind)
    {
        var open = IsOpen(phase, kind);
        var panelHidden = open && kind != ChoiceKind.Trinket;
        if (open == Hidden && panelHidden == PanelHidden)
        {
            return null;
        }

        Hidden = open;
        PanelHidden = panelHidden;
        return !open ? RestoredLine : panelHidden ? HiddenLine : PanelKeptLine;
    }

    public void Reset()
    {
        Hidden = false;
        PanelHidden = false;
    }
}
