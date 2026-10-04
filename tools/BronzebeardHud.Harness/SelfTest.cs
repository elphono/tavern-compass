using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BronzebeardHud.Stats;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace BronzebeardHud.Harness;

/// <summary>
/// What must hold for the harness to be worth debugging with, checked on the window as built: the two movable panels
/// (Compositions, Skip combat) are on the overlay and inside it, move mode shows one handle and one frame (the
/// Compositions panel is the only resizable one), a click on a guide's name opens its detail and "← All comp guides"
/// brings the list back, no text of the panel is under the 12 px floor or cut (list and detail), the scenarios give
/// the targets they are named for, Bob's cards carry the frames TavernHighlights asks for, nothing was logged as a
/// warning or an error, and the layout file is the harness's own, never the real plugin's.
/// </summary>
internal static class SelfTest
{
    /// <summary>PanelMover's colour in move mode: its frames are told apart from the dotted frames on Bob's cards by it.</summary>
    private static readonly Color MoverColour = Color.FromRgb(0x00, 0xE5, 0xFF);

    public static (bool Passed, string Report) Run(HarnessWindow window)
    {
        var checks = new List<(string Name, bool Ok, string Detail)>();
        void Check(string name, bool ok, string detail) => checks.Add((name, ok, detail));

        var canvas = window.Overlay;
        var scale = TavernLayout.Scale(canvas.ActualHeight);
        var handles = canvas.Children.OfType<Border>().Where(b => Equals(b.Cursor, Cursors.SizeNWSE)).ToList();
        // A panel is what PanelMover moves: in move mode it wears the four-way cursor (pin buttons wear the hand, markers none).
        var panels = canvas.Children.OfType<Border>().Where(b => Equals(b.Cursor, Cursors.SizeAll) && b.Visibility == Visibility.Visible && b.ActualWidth > 0 && b.ActualHeight > 0).ToList();
        var frames = canvas.Children.OfType<Rectangle>()
            .Where(r => r.StrokeDashArray.Count > 0 && r.Visibility == Visibility.Visible && r.Stroke is SolidColorBrush { Color: var c } && c == MoverColour)
            .ToList();

        Check("two movable panels are drawn", panels.Count == 2, $"{panels.Count} visible panels: " + string.Join(" | ", panels.Select(Describe)));

        var outside = panels.Where(p => Canvas.GetLeft(p) < -0.5 || Canvas.GetTop(p) < -0.5
                                         || Canvas.GetLeft(p) + p.ActualWidth > canvas.ActualWidth + 0.5
                                         || Canvas.GetTop(p) + p.ActualHeight > canvas.ActualHeight + 0.5).ToList();
        Check("every panel is inside the overlay", outside.Count == 0, outside.Count == 0 ? "ok" : string.Join(" | ", outside.Select(Describe)));

        Check("move mode is on", window.MoveMode, $"MoveMode={window.MoveMode}");
        Check("one handle and one frame in move mode", handles.Count(h => h.Visibility == Visibility.Visible) == 1 && frames.Count == 1,
            $"{handles.Count(h => h.Visibility == Visibility.Visible)} visible handles, {frames.Count} frames");

        var counts = Enumerable.Range(0, HarnessData.Scenarios.Count).Select(window.TargetCount).ToList();
        Check("the scenarios give 0, 1 and 3 targets", counts.SequenceEqual(HarnessData.ExpectedTargets),
            string.Join(", ", HarnessData.Scenarios.Select((s, i) => $"\"{s.Name}\": {counts[i]}")));
        var colours = window.Targets.Select(t => t.Colour).ToList();
        Check("the scene's targets have distinct colours", colours.Count == 3 && colours.Distinct().Count() == 3, CompTargets.Summary(window.Targets));

        // Bob's cards: one solid frame per core card of a target, one dotted frame per enabler or add-on, in its colour.
        var solid = window.Highlights.Count(h => h.Kind == HighlightKind.Commit);
        var dotted = window.Highlights.Count(h => h.Kind == HighlightKind.Enabler);
        var dottedDrawn = canvas.Children.OfType<Rectangle>().Count(r => r.StrokeDashArray.Count > 0 && r.Stroke is SolidColorBrush { Color: var c } && c != MoverColour);
        var solidDrawn = canvas.Children.OfType<Border>().Count(b => b.Child == null && b.BorderThickness.Left >= 6 * scale - 1e-6);
        Check("Bob's cards carry the frames of the targets", solid >= 2 && dotted >= 2 && solidDrawn == solid && dottedDrawn == dotted,
            $"{solid} core (solid) and {dotted} enabler or add-on (dotted) highlights; drawn: {solidDrawn} solid, {dottedDrawn} dotted");

        var comps = window.Comps.Element;
        var listTexts = TextProblems(comps, scale);
        Check("list: no text under 12 px, none cut", listTexts.Problems.Count == 0, $"{listTexts.Checked} texts checked" + Problems(listTexts.Problems));

        // A click on the first target's name opens its detail; "← All comp guides" brings the list back.
        var name = window.Targets.FirstOrDefault()?.Guide.Name ?? "?";
        var clicked = Click(comps, name);
        window.UpdateLayout();
        var back = FindText(comps, "← All comp guides");
        Check("a click on a guide's name opens its detail", clicked && window.Comps.ShowsDetail && back != null,
            $"clicked \"{name}\": {clicked}, detail shown: {window.Comps.ShowsDetail}, back button: {back != null}");
        var detailTexts = TextProblems(comps, scale);
        Check("detail: no text under 12 px, none cut", detailTexts.Problems.Count == 0, $"{detailTexts.Checked} texts checked" + Problems(detailTexts.Problems));
        // The sections drawn, top to bottom, against HDT's order; when some are left out, the panel says how many it shows.
        var order = new[] { "HOW TO PLAY", "CORE CARDS", "ADDON CARDS", "WHEN TO COMMIT", "COMMON ENABLERS", "PIVOTS" };
        var drawn = Texts(comps).Where(t => t.IsVisible).Select(Content).Select(c => order.FirstOrDefault(title => c.StartsWith(title, StringComparison.Ordinal))).Where(title => title != null).ToList();
        var more = Texts(comps).Where(t => t.IsVisible).Select(Content).FirstOrDefault(c => c.EndsWith(" sections", StringComparison.Ordinal));
        var expected = window.Targets.FirstOrDefault()?.Guide is { } first ? ExpectedSections(first, window) : 0;
        var inOrder = drawn.SequenceEqual(order.Where(drawn.Contains));
        var counted = more == null ? drawn.Count == expected : more == $"{drawn.Count} of {expected} sections";
        Check("the detail shows its sections in HDT's order, and how many when some are left out", drawn.Count >= 1 && inOrder && counted,
            $"{string.Join(", ", drawn)} ({more ?? $"all {expected}"})");

        var returned = Click(comps, "← All comp guides");
        window.UpdateLayout();
        Check("\"← All comp guides\" brings the list back", returned && !window.Comps.ShowsDetail && FindText(comps, name) != null && FindText(comps, "← All comp guides") == null,
            $"clicked: {returned}, detail shown: {window.Comps.ShowsDetail}");

        // Ticking restricts: the boxes are clicked as the mouse would (their Click event); ticked guides are the targets, alone.
        var autoTargets = window.Targets.Select(t => t.Guide.Id).ToList();
        ClickBox(comps, firstUnticked: true);
        window.UpdateLayout();
        var one = window.Targets;
        var oneGuide = one.FirstOrDefault()?.Guide.Id;
        var framesOfOne = window.Highlights.Where(h => h.Kind != HighlightKind.None).Select(h => h.Target?.Guide.Id).Distinct().ToList();
        Check("ticking a guide makes it the only target, and Bob's frames follow it", autoTargets.Count == 3 && one.Count == 1 && one[0].Ticked
            && framesOfOne.All(id => id == oneGuide) && FindText(comps, "1 chosen") != null,
            $"{autoTargets.Count} automatic targets, then {CompTargets.Summary(one)}; frames for {string.Join(",", framesOfOne)}; title: {(FindText(comps, "1 chosen") != null ? "1 chosen" : "not 1 chosen")}");

        ClickBox(comps, firstUnticked: true);
        window.UpdateLayout();
        var two = window.Targets;
        Check("ticking a second one adds it, in the order ticked", two.Count == 2 && two.All(t => t.Ticked) && two[0].Guide.Id == oneGuide && FindText(comps, "2 chosen") != null,
            CompTargets.Summary(two));

        // − and + are dim, and a click on either changes nothing. One at a time and on the count itself: the log lines of
        // a click reach the window's list later, through the dispatcher, and − then + would cancel out.
        var minus = FindText(comps, "−");
        var plus = FindText(comps, "+");
        var wanted = window.Count;
        var clickedMinus = Click(comps, "−");
        var afterMinus = window.Count;
        var clickedPlus = Click(comps, "+");
        window.UpdateLayout();
        Check("− and + are dim and do nothing while a guide is ticked", clickedMinus && clickedPlus && afterMinus == wanted && window.Count == wanted
            && minus?.Parent is Border { Opacity: < 1 } && plus?.Parent is Border { Opacity: < 1 } && window.Targets.Count == 2,
            $"clicked: {clickedMinus}/{clickedPlus}, count {wanted} -> {afterMinus} -> {window.Count}, opacity {(minus?.Parent as Border)?.Opacity}/{(plus?.Parent as Border)?.Opacity}, {window.Targets.Count} targets");

        ClickBox(comps, firstUnticked: false);
        window.UpdateLayout();
        ClickBox(comps, firstUnticked: false);
        window.UpdateLayout();
        var minusAgain = FindText(comps, "−");
        Check("unticking everything gives the automatic targets back", window.Targets.Select(t => t.Guide.Id).SequenceEqual(autoTargets) && window.Targets.All(t => !t.Ticked)
            && FindText(comps, "3 targets") != null && minusAgain?.Parent is Border { Opacity: 1 },
            CompTargets.Summary(window.Targets));

        var bad = window.LogLines.Where(l => l.Contains("|Warning|") || l.Contains("|Error|")).ToList();
        Check("nothing logged as a warning or an error", bad.Count == 0, bad.Count == 0 ? $"{window.LogLines.Count} log lines" : string.Join(" | ", bad));

        Check("the layout file is the harness's own", window.LayoutPath.Contains("BronzebeardHarness") && !window.LayoutPath.Contains(@"AppData\Local\BronzebeardHud"), window.LayoutPath);

        var passed = checks.All(c => c.Ok);
        var report = string.Join(Environment.NewLine, checks.Select(c => $"{(c.Ok ? "PASS" : "FAIL")}  {c.Name}: {c.Detail}"))
                     + Environment.NewLine + (passed ? "ALL PASSED" : "FAILED") + Environment.NewLine;
        return (passed, report);
    }

    /// <summary>The sections a guide's detail has: how to play when it has a text, core cards, then each list it has.</summary>
    private static int ExpectedSections(CompGuide guide, HarnessWindow window) =>
        (guide.HowToPlayFirstLine.Count > 0 ? 1 : 0) + 1 + (guide.AddonCards.Count > 0 ? 1 : 0) + (guide.WhenToCommitLines.Count > 0 ? 1 : 0)
        + (guide.Enablers.Count > 0 ? 1 : 0) + (window.PivotCount(guide) > 0 ? 1 : 0);

    private static string Describe(Border b) =>
        $"({Canvas.GetLeft(b):0},{Canvas.GetTop(b):0} {b.ActualWidth:0}x{b.ActualHeight:0})";

    private static string Problems(IReadOnlyList<string> problems) => problems.Count == 0 ? string.Empty : ": " + string.Join(" | ", problems.Take(8));

    private static IEnumerable<TextBlock> Texts(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock text)
            {
                yield return text;
            }

            foreach (var inner in Texts(child))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    /// Where a text block's glyphs are, in its own coordinates: its text laid out again at its size, weights and width
    /// (FormattedText), bold runs included; empty when it draws nothing.
    /// </summary>
    private static Rect Ink(TextBlock text)
    {
        var content = Content(text);
        if (string.IsNullOrWhiteSpace(content))
        {
            return Rect.Empty;
        }

        var typeface = new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch);
        var formatted = new FormattedText(content, System.Globalization.CultureInfo.InvariantCulture, text.FlowDirection, typeface, text.FontSize,
            Brushes.Black, VisualTreeHelper.GetDpi(text).PixelsPerDip);
        if (text.TextWrapping != TextWrapping.NoWrap)
        {
            formatted.MaxTextWidth = Math.Max(1, text.ActualWidth);
        }

        if (string.IsNullOrEmpty(text.Text))
        {
            var start = 0;
            foreach (var run in text.Inlines.OfType<System.Windows.Documents.Run>())
            {
                if (run.Text.Length > 0)
                {
                    formatted.SetFontWeight(run.FontWeight, start, run.Text.Length);
                    formatted.SetFontSize(run.FontSize, start, run.Text.Length);
                }

                start += run.Text.Length;
            }
        }

        return formatted.BuildGeometry(new Point(0, 0)).Bounds;
    }

    /// <summary>What a text block says, its runs included.</summary>
    private static string Content(TextBlock text) =>
        !string.IsNullOrEmpty(text.Text) ? text.Text : string.Concat(text.Inlines.OfType<System.Windows.Documents.Run>().Select(r => r.Text));

    private static IEnumerable<CheckBox> Boxes(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is CheckBox box)
            {
                yield return box;
            }

            foreach (var inner in Boxes(child))
            {
                yield return inner;
            }
        }
    }

    /// <summary>Clicks the first visible tick box that is unticked (or ticked), as the mouse would: its Click event.</summary>
    private static void ClickBox(Border panel, bool firstUnticked)
    {
        var box = Boxes(panel).First(b => b.IsVisible && (b.IsChecked == true) != firstUnticked);
        box.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }

    private static TextBlock? FindText(Border panel, string content) => Texts(panel).FirstOrDefault(t => t.IsVisible && Content(t) == content);

    /// <summary>
    /// Raises a left button-up on the text block that says <paramref name="content"/>, as the mouse would: MouseUp bubbles,
    /// and each element on its way turns it into its own MouseLeftButtonUp (a direct event: raised alone, it would reach
    /// the text block's handlers only), so the handler of its clickable parent runs.
    /// </summary>
    private static bool Click(Border panel, string content)
    {
        var text = FindText(panel, content);
        if (text == null)
        {
            return false;
        }

        text.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseUpEvent });
        return true;
    }

    /// <summary>
    /// Every visible text block of the panel: its size at least the floor (PanelTypography.Floor × scale), no ellipsis,
    /// as wide and as tall as its text needs where it is drawn (measured again at its own width), and inside the panel,
    /// which clips what goes beyond it.
    /// </summary>
    private static (int Checked, IReadOnlyList<string> Problems) TextProblems(Border panel, double scale)
    {
        var problems = new List<string>();
        var texts = Texts(panel).Where(t => t.IsVisible && t.ActualWidth > 0).ToList();

        // First what the layout did (WPF only answers GetLayoutClip while an element's measure is valid), then the
        // probes, which measure each text again and so invalidate it.
        foreach (var text in texts)
        {
            var what = $"\"{Content(text)}\"";
            if (text.FontSize < PanelTypography.Floor * scale - 1e-6)
            {
                problems.Add($"{what} at {text.FontSize:0.##} px");
            }

            if (text.TextTrimming != TextTrimming.None)
            {
                problems.Add($"{what} trimmed");
            }

            // What is drawn is the ink, not the line box: a ✓ in a 13 px badge has a 16 px line box and a 9 px glyph.
            var ink = Ink(text);
            if (ink.IsEmpty)
            {
                continue;
            }

            var box = text.TransformToAncestor(panel).TransformBounds(ink);
            if (box.Left < -0.5 || box.Top < -0.5 || box.Right > panel.ActualWidth + 0.5 || box.Bottom > panel.ActualHeight + 0.5)
            {
                problems.Add($"{what} at ({box.Left:0},{box.Top:0},{box.Right:0},{box.Bottom:0}) outside the panel {panel.ActualWidth:0}x{panel.ActualHeight:0}");
            }

            // An element larger than the room its parent gave it keeps its full size and is clipped to that room (its
            // layout clip): a name that does not wrap, in a narrow column, is cut that way. Every clip on the way up counts.
            for (DependencyObject? up = text; up != null && !ReferenceEquals(up, panel); up = VisualTreeHelper.GetParent(up))
            {
                if (up is FrameworkElement element && System.Windows.Controls.Primitives.LayoutInformation.GetLayoutClip(element) is { } clip)
                {
                    var local = ReferenceEquals(element, text) ? ink : text.TransformToAncestor(element).TransformBounds(ink);
                    var room = clip.Bounds;
                    if (local.Left < room.Left - 0.5 || local.Top < room.Top - 0.5 || local.Right > room.Right + 0.5 || local.Bottom > room.Bottom + 0.5)
                    {
                        problems.Add($"{what} cut by its {element.GetType().Name} to {room.Width:0.#}x{room.Height:0.#} (needs {local.Width:0.#}x{local.Height:0.#})");
                        break;
                    }
                }
            }
        }

        foreach (var text in texts)
        {
            var margin = text.Margin;
            var available = text.TextWrapping == TextWrapping.NoWrap ? double.PositiveInfinity : text.ActualWidth + margin.Left + margin.Right;
            text.Measure(new Size(available, double.PositiveInfinity));
            var needWidth = text.DesiredSize.Width - margin.Left - margin.Right;
            var needHeight = text.DesiredSize.Height - margin.Top - margin.Bottom;
            text.InvalidateMeasure();
            if (needWidth > text.ActualWidth + 0.5 || needHeight > text.ActualHeight + 0.5)
            {
                problems.Add($"\"{Content(text)}\" needs {needWidth:0.#}x{needHeight:0.#}, has {text.ActualWidth:0.#}x{text.ActualHeight:0.#}");
            }
        }

        panel.UpdateLayout();
        return (texts.Count, problems);
    }
}
