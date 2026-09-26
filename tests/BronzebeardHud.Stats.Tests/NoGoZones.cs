namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// Parts of the Hearthstone window a default panel must not cover, from HDT's constants
/// (HearthSim/Hearthstone-Deck-Tracker, master 509bb0b):
/// - the boards: from the opponent (tavern) row top, H/2 − 0.158 H − 0.045 H (Windows/OverlayWindow.Update.cs:534-535,
///   MouseOverDetection.cs:38), to the player row bottom, H/2 − 0.03 H + 0.158 H (Update.cs:537-538), over seven
///   shop card slots (BattlegroundsMinionPinningShop.xaml, 138 units each);
/// - the native leaderboard column: tiles of 0.69 H / 8 from 0.15 H (MouseOverDetection.cs:48, OverlayWindow.xaml.cs:509)
///   at the left of the 4:3 frame (MouseOverDetection.cs:114), plus the plugin's MMR labels right of them;
/// - the player's hero and hero power, around the centre below the board.
/// </summary>
internal static class NoGoZones
{
    public static IEnumerable<(string Name, LayoutRect Rect)> For(double width, double height)
    {
        var s = height / 1080;
        var boardHalfWidth = 3.5 * 138 * s;
        var boardTop = height / 2 - 0.158 * height - 0.045 * height;
        var boardBottom = height / 2 - 0.03 * height + 0.158 * height;
        yield return ("boards", Rect(width / 2 - boardHalfWidth, boardTop, width / 2 + boardHalfWidth, boardBottom));

        var frameLeft = (width - height * 4 / 3) / 2;
        var tile = 0.69 / 8 * height;
        yield return ("leaderboard", Rect(frameLeft, 0.15 * height, frameLeft + tile + 0.1 * height, 0.15 * height + 8 * tile));

        yield return ("hero", Rect(width / 2 - 0.2 * height, boardBottom, width / 2 + 0.2 * height, height));
    }

    public static bool Overlaps(LayoutRect a, LayoutRect b) =>
        a.Left < b.Right && b.Left < a.Right && a.Top < b.Top + b.Height && b.Top < a.Top + a.Height;

    private static LayoutRect Rect(double left, double top, double right, double bottom) =>
        new((left + right) / 2, (top + bottom) / 2, right - left, bottom - top);
}
