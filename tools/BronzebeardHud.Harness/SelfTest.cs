using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace BronzebeardHud.Harness;

/// <summary>
/// What must hold for the harness to be worth debugging with, checked on the window as built: the four movable panels
/// are on the overlay and inside it, move mode shows a handle and a frame on the three resizable ones, nothing was
/// logged as a warning or an error, and the layout file is the harness's own, never the real plugin's.
/// </summary>
internal static class SelfTest
{
    public static (bool Passed, string Report) Run(HarnessWindow window)
    {
        var checks = new List<(string Name, bool Ok, string Detail)>();
        void Check(string name, bool ok, string detail) => checks.Add((name, ok, detail));

        var canvas = window.Overlay;
        var handles = canvas.Children.OfType<Border>().Where(b => Equals(b.Cursor, Cursors.SizeNWSE)).ToList();
        var panels = canvas.Children.OfType<Border>().Where(b => !handles.Contains(b) && b.Visibility == Visibility.Visible && b.ActualWidth > 0 && b.ActualHeight > 0).ToList();
        var frames = canvas.Children.OfType<Rectangle>().Where(r => r.StrokeDashArray.Count > 0 && r.Visibility == Visibility.Visible).ToList();

        Check("four movable panels are drawn", panels.Count == 4, $"{panels.Count} visible panels: " + string.Join(" | ", panels.Select(Describe)));

        var outside = panels.Where(p => Canvas.GetLeft(p) < -0.5 || Canvas.GetTop(p) < -0.5
                                         || Canvas.GetLeft(p) + p.ActualWidth > canvas.ActualWidth + 0.5
                                         || Canvas.GetTop(p) + p.ActualHeight > canvas.ActualHeight + 0.5).ToList();
        Check("every panel is inside the overlay", outside.Count == 0, outside.Count == 0 ? "ok" : string.Join(" | ", outside.Select(Describe)));

        Check("move mode is on", window.MoveMode, $"MoveMode={window.MoveMode}");
        Check("three handles and three frames in move mode", handles.Count(h => h.Visibility == Visibility.Visible) == 3 && frames.Count == 3,
            $"{handles.Count(h => h.Visibility == Visibility.Visible)} visible handles, {frames.Count} frames");

        var bad = window.LogLines.Where(l => l.Contains("|Warning|") || l.Contains("|Error|")).ToList();
        Check("nothing logged as a warning or an error", bad.Count == 0, bad.Count == 0 ? $"{window.LogLines.Count} log lines" : string.Join(" | ", bad));

        Check("the layout file is the harness's own", window.LayoutPath.Contains("BronzebeardHarness") && !window.LayoutPath.Contains(@"AppData\Local\BronzebeardHud"), window.LayoutPath);

        var passed = checks.All(c => c.Ok);
        var report = string.Join(Environment.NewLine, checks.Select(c => $"{(c.Ok ? "PASS" : "FAIL")}  {c.Name}: {c.Detail}"))
                     + Environment.NewLine + (passed ? "ALL PASSED" : "FAILED") + Environment.NewLine;
        return (passed, report);
    }

    private static string Describe(Border b) =>
        $"({Canvas.GetLeft(b):0},{Canvas.GetTop(b):0} {b.ActualWidth:0}x{b.ActualHeight:0})";
}
