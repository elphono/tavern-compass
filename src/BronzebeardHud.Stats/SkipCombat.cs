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

/// <summary>How the client is started again after the kill; <see cref="Refusal"/> set when it cannot be, and then nothing is killed.</summary>
public sealed class RelaunchPlan
{
    private RelaunchPlan(string? file, string arguments, string source, string? refusal)
    {
        File = file;
        Arguments = arguments;
        Source = source;
        Refusal = refusal;
    }

    public string? File { get; }
    public string Arguments { get; }

    /// <summary>Where the Battle.net path came from, for the log line.</summary>
    public string Source { get; }

    public string? Refusal { get; }

    public static RelaunchPlan Through(string file, string source) => new(file, SkipCombatPlan.LaunchArguments, source, null);
    public static RelaunchPlan Refused(string reason) => new(null, string.Empty, "nowhere", reason);
}

public static class SkipCombatPlan
{
    public const string BattleNetExecutable = "Battle.net.exe";

    /// <summary>What HDT itself hands Battle.net to start Hearthstone (Utility/HearthstoneRunner.cs:54).</summary>
    public const string LaunchArguments = "--exec=\"launch WTCG\"";

    /// <summary>
    /// The client is always started again THROUGH Battle.net, never by its own executable. Measured on
    /// 2026-09-27 on Ali's machine: a client started directly — with or without its original "-launch -uid
    /// hs_beta" — reuses the one-time login token of the killed client and is refused ("A repeated token was
    /// retrieved when disallowed", "Failed to get token to respond to login challenge", Login.log), which shows
    /// the "could not connect to Blizzard services" screen: worse than no skip at all. Battle.net is the killed
    /// client's parent when it launched it (read before the kill), else a running Battle.net.exe; with neither,
    /// the skip is refused and Hearthstone is left running.
    /// </summary>
    /// <param name="parentName">The running client's parent process name (WMI ParentProcessId).</param>
    /// <param name="parentPath">That parent's executable path.</param>
    /// <param name="runningBattleNet">The executable of a running Battle.net.exe, when one could be read.</param>
    public static RelaunchPlan Relaunch(string? parentName, string? parentPath, string? runningBattleNet)
    {
        if (string.Equals(parentName, BattleNetExecutable, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(parentPath))
        {
            return RelaunchPlan.Through(parentPath!, "Battle.net, the client's parent");
        }

        if (!string.IsNullOrWhiteSpace(runningBattleNet))
        {
            return RelaunchPlan.Through(runningBattleNet!, "a running Battle.net");
        }

        return RelaunchPlan.Refused("no Battle.net to start Hearthstone again (a direct start cannot log in); Hearthstone left running");
    }
}

/// <summary>
/// The "Skip combat" button's default place: in combat, at the right end of the player's board row, inside
/// Hearthstone's 4:3 frame and right of seven minions (a board card every 138 design units, TavernLayout),
/// over the board's decoration; above the target composition panel (TavernLayout.TargetPanel). It can be moved
/// (PanelLayout, id "skip-combat").
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
