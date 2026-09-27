using System;

namespace BronzebeardHud.Stats;

/// <summary>
/// When the "Skip combat" button shows and may act. The trick (known in Battlegrounds): close Hearthstone during
/// the combat and start it again at once; the reconnection lands after the combat animation. The button shows
/// in combat only, and acts once per combat: after a click it stays hidden until the phase has left combat
/// (HDT resets its game when Hearthstone's window goes away, or the next shop starts), so that a second click
/// can never kill the client that was just started.
/// </summary>
public sealed class SkipCombatState
{
    private bool _armed = true;
    private OverlayPhase _phase = OverlayPhase.OutOfGame;

    /// <summary>Called on every update with the current phase; true while the button should show.</summary>
    public bool Observe(OverlayPhase phase)
    {
        _phase = phase;
        if (phase != OverlayPhase.Combat)
        {
            _armed = true;
        }

        return phase == OverlayPhase.Combat && _armed;
    }

    /// <summary>A click: true once per combat, false outside combat or after a skip in this combat.</summary>
    public bool TryBegin()
    {
        if (_phase != OverlayPhase.Combat || !_armed)
        {
            return false;
        }

        _armed = false;
        return true;
    }
}

public static class SkipCombatPlan
{
    public const string ExecutableName = "Hearthstone.exe";

    /// <summary>
    /// Which executable to start again, chosen BEFORE the process is killed: the running process's own file
    /// (Process.MainModule.FileName) when it could be read, else HDT's configured Hearthstone folder
    /// (Config.HearthstoneDirectory) plus Hearthstone.exe; null when neither is known, and then nothing is killed.
    /// Windows paths, joined with a backslash whatever the platform the tests run on.
    /// </summary>
    public static (string? Path, string Source) Executable(string? fromProcess, string? hdtHearthstoneDirectory)
    {
        if (!string.IsNullOrWhiteSpace(fromProcess))
        {
            return (fromProcess, "process");
        }

        if (!string.IsNullOrWhiteSpace(hdtHearthstoneDirectory))
        {
            return (hdtHearthstoneDirectory!.TrimEnd('\\', '/') + "\\" + ExecutableName, "HDT config");
        }

        return (null, "nowhere");
    }
}

/// <summary>
/// The "Skip combat" button's default place: in combat, at the right end of the player's board row, inside
/// Hearthstone's 4:3 frame and right of seven minions (a board card every 138 design units, TavernLayout),
/// over the board's decoration; above the target composition panel (TavernLayout.TargetPanel) and well below
/// the combats panel (HistoryLayout). It can be moved (PanelLayout, id "skip-combat").
/// </summary>
public static class SkipCombatLayout
{
    public const double Width = 0.12;
    public const double Height = 0.036;

    public static LayoutRect Button(double width, double height)
    {
        var right = width / 2 + height * 2 / 3 - 0.01 * height;
        var rowTop = height / 2 - 0.03 * height;
        var centerY = (rowTop + TavernLayout.PlayerRowBottom(height)) / 2;
        return new LayoutRect(right - Width * height / 2, centerY, Width * height, Height * height);
    }
}
