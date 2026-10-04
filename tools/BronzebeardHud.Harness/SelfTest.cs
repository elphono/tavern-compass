using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BronzebeardHud.Stats;
using BronzebeardHud.Stats.Tests;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace BronzebeardHud.Harness;

/// <summary>
/// What must hold for the harness to be worth debugging with, checked on the window as built: the two movable panels
/// (Compositions, Skip combat) are on the overlay and inside it, move mode shows one handle and one frame (the
/// Compositions panel is the only resizable one), a click on a guide's name opens its detail and "← All comp guides"
/// brings the list back, no text of the panel is under the 12 px floor or cut (list and detail), the scenarios give
/// the targets they are named for, Bob's cards carry the frames TavernHighlights asks for, hovering a guide line shows
/// its popup where it covers nothing and hides it as it should (HoverChecks), nothing was logged as a warning or an
/// error (the log read once the dispatcher has delivered it), and the layout file is the harness's own, never the real
/// plugin's.
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

        ChoiceChecks(window, Check);
        HoverChecks(window, Check);

        // The log lines reach the window through Dispatcher.BeginInvoke (HarnessWindow): read before they land, the list was
        // empty and the check passed on nothing. Let the dispatcher run what is queued first, then require lines.
        FlushLog(window);
        var bad = window.LogLines.Where(l => l.Contains("|Warning|") || l.Contains("|Error|")).ToList();
        Check("nothing logged as a warning or an error", window.LogLines.Count > 0 && bad.Count == 0,
            $"{window.LogLines.Count} log lines read" + (bad.Count == 0 ? string.Empty : ": " + string.Join(" | ", bad)));

        Check("the layout file is the harness's own", window.LayoutPath.Contains("BronzebeardHarness") && !window.LayoutPath.Contains(@"AppData\Local\BronzebeardHud"), window.LayoutPath);

        var passed = checks.All(c => c.Ok);
        var report = string.Join(Environment.NewLine, checks.Select(c => $"{(c.Ok ? "PASS" : "FAIL")}  {c.Name}: {c.Detail}"))
                     + Environment.NewLine + (passed ? "ALL PASSED" : "FAILED") + Environment.NewLine;
        return (passed, report);
    }

    /// <summary>
    /// The labels of ChoiceAdvicePanel above each choice (discover, Dark Gift, trinket), on the scene's automatic targets:
    /// one per option, where ChoiceLayout.Labels puts it; none of their texts under the floor or cut; a "★ core T" or
    /// "+ T" label in the colour of the target T it names (read from the text, not from the advice); every trinket with
    /// its placement, one adjusted for a target's tribe; an option that serves nothing says "—", never "no target comp";
    /// closing the choice takes its labels away.
    /// </summary>
    private static void ChoiceChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        var canvas = window.Overlay;
        var scale = TavernLayout.Scale(canvas.ActualHeight);
        var unrelated = new List<string>();
        var drawnRects = new List<Rect>();
        foreach (var kind in new[] { ChoiceKind.Discover, ChoiceKind.DarkGift, ChoiceKind.Trinket })
        {
            window.ShowChoice(kind);
            window.UpdateLayout();
            var targets = window.Targets;
            var name = "choice " + ChoiceClassifier.Name(kind);
            var options = window.ChoiceOptions;
            var lineCount = window.Choices.LastLines.Select(l => l.Count).DefaultIfEmpty(1).Max();
            var rects = ChoiceLayout.Labels(kind, options.Count, canvas.ActualWidth, canvas.ActualHeight, lineCount);
            var labels = rects.Select(r => canvas.Children.OfType<Border>().FirstOrDefault(b => !Equals(b.Tag, "choice") && b.IsVisible && b.Child is StackPanel
                && Math.Abs(Canvas.GetLeft(b) - r.Left) < 0.5 && Math.Abs(Canvas.GetTop(b) - r.Top) < 0.5
                && Math.Abs(b.ActualWidth - r.Width) < 0.5 && Math.Abs(b.ActualHeight - r.Height) < 0.5)).ToList();
            var said = labels.Select(l => l == null ? new List<string>() : Texts(l).Where(t => t.IsVisible).Select(Content).ToList()).ToList();
            drawnRects.AddRange(rects.Select(r => new Rect(r.Left, r.Top, r.Width, r.Height)));
            check($"{name}: one label per option, where ChoiceLayout puts it", options.Count > 0 && labels.All(l => l != null) && said.All(s => s.Count > 0),
                $"{labels.Count(l => l != null)} labels for {options.Count} options: " + string.Join("; ", said.Select((s, i) => $"#{i} {string.Join(" / ", s)}")));

            var problems = new List<string>();
            var checkedTexts = 0;
            foreach (var label in labels.Where(l => l != null))
            {
                var (count, found) = TextProblems(label!, scale);
                checkedTexts += count;
                problems.AddRange(found);
            }

            check($"{name}: no text under 12 px, none cut", checkedTexts > 0 && problems.Count == 0, $"{checkedTexts} texts checked" + Problems(problems));

            if (kind == ChoiceKind.Trinket)
            {
                var adjusted = said.Where(s => s.Any(line => line.StartsWith("≈ ", StringComparison.Ordinal) && targets.Any(t => line.Contains(t.Guide.Name)))).Count();
                check($"{name}: every trinket shows its placement, one adjusted for a target's tribe", said.Count > 0 && said.All(s => s.Count > 0 && s[0].StartsWith("avg ", StringComparison.Ordinal)) && adjusted >= 1,
                    $"{adjusted} adjusted: " + string.Join("; ", said.Select((s, i) => $"#{i} {string.Join(" / ", s)}")));
            }
            else
            {
                // The target is read from the label's text, the colour from what is drawn: neither from the advice.
                var coloured = new List<string>();
                var wrong = new List<string>();
                for (var i = 0; i < labels.Count; i++)
                {
                    var first = said[i].FirstOrDefault() ?? string.Empty;
                    if (!first.StartsWith("★ core ", StringComparison.Ordinal) && !first.StartsWith("+ ", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var named = targets.Where(t => first.Contains(t.Guide.Name)).ToList();
                    var drawn = (labels[i]?.Background as SolidColorBrush)?.Color;
                    var line = $"#{i} \"{first}\" drawn {drawn}";
                    if (named.Count != 1)
                    {
                        wrong.Add(line + $": names {named.Count} targets");
                        continue;
                    }

                    var expected = (Color)ColorConverter.ConvertFromString(named[0].Colour);
                    (drawn == expected ? coloured : wrong).Add(line + $", {named[0].Guide.Name} is {expected}");
                }

                check($"{name}: a \"★ core T\" or \"+ T\" label is in the colour of T", coloured.Count >= 1 && wrong.Count == 0,
                    string.Join("; ", wrong.Concat(coloured)));
            }

            // Serves nothing: no target lists it, and no other guide has it as a core card (the harness's guides, not the advice).
            var guides = HarnessData.Guides(id => id).All;
            for (var i = 0; i < options.Count && i < said.Count; i++)
            {
                var id = options[i].CardId;
                if (kind != ChoiceKind.Trinket && targets.All(t => GuideCardEffects.RoleIn(t.Guide, id) == null) && !guides.Any(g => g.CoreCards.Contains(id)))
                {
                    unrelated.Add($"{ChoiceClassifier.Name(kind)} #{i} {id}: {string.Join(" / ", said[i])}");
                }
            }

            if (said.Any(s => s.Any(line => line.IndexOf("no target comp", StringComparison.OrdinalIgnoreCase) >= 0)))
            {
                unrelated.Add($"{ChoiceClassifier.Name(kind)}: a label says \"no target comp\"");
            }
        }

        check("an option that serves nothing says \"—\", never \"no target comp\"",
            unrelated.Count >= 1 && unrelated.All(u => u.EndsWith(": —", StringComparison.Ordinal)), string.Join("; ", unrelated));

        window.ShowChoice(ChoiceKind.None);
        window.UpdateLayout();
        var left = canvas.Children.OfType<Border>().Count(b => b.Child is StackPanel && drawnRects.Any(r => Math.Abs(Canvas.GetLeft(b) - r.Left) < 0.5 && Math.Abs(Canvas.GetTop(b) - r.Top) < 0.5));
        var cards = canvas.Children.OfType<Border>().Count(b => Equals(b.Tag, "choice"));
        check("closing the choice takes its labels and options away", left == 0 && cards == 0 && window.Choice == null, $"{left} labels and {cards} options left");
    }

    private static readonly string[] SectionOrder = { "HOW TO PLAY", "CORE CARDS", "ADDON CARDS", "WHEN TO COMMIT", "COMMON ENABLERS", "PIVOTS" };

    /// <summary>Runs what the dispatcher has queued (the log lines, posted by Dispatcher.BeginInvoke) before coming back.</summary>
    private static void FlushLog(HarnessWindow window) =>
        window.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));

    private static void Raise(FrameworkElement element, RoutedEvent routed) =>
        element.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = routed });

    private static Rect RectOf(FrameworkElement element) =>
        new(Canvas.GetLeft(element), Canvas.GetTop(element), element.ActualWidth, element.ActualHeight);

    private static Rect RectOf(LayoutRect rect) => new(rect.Left, rect.Top, rect.Width, rect.Height);

    /// <summary>Overlap of more than half a pixel: two rectangles that touch do not overlap.</summary>
    private static bool Overlap(Rect a, Rect b) => a.Left < b.Right - 0.5 && b.Left < a.Right - 0.5 && a.Top < b.Bottom - 0.5 && b.Top < a.Bottom - 0.5;

    private static string Describe(Rect r) => $"({r.Left:0},{r.Top:0} {r.Width:0}x{r.Height:0})";

    /// <summary>The section titles drawn in an element, top to bottom, and its "k of n sections" line if any.</summary>
    private static (List<string> Drawn, string? More) Sections(DependencyObject root)
    {
        var texts = Texts(root).Where(t => t.IsVisible).Select(Content).ToList();
        var drawn = texts.Select(c => SectionOrder.FirstOrDefault(title => c.StartsWith(title, StringComparison.Ordinal))).Where(title => title != null).Select(t => t!).ToList();
        return (drawn, texts.FirstOrDefault(c => c.EndsWith(" sections", StringComparison.Ordinal)));
    }

    /// <summary>The element of the canvas (a panel) that holds a text, found by what it says, not by what the plugin calls it.</summary>
    private static FrameworkElement? CanvasChildSaying(Canvas canvas, string content) =>
        canvas.Children.OfType<FrameworkElement>().FirstOrDefault(e => e.IsVisible && Texts(e).Any(t => t.IsVisible && Content(t) == content));

    /// <summary>
    /// The popup of a hovered guide line (GuidePopup), driven as HDT's probe drives it: MouseEnter and MouseLeave raised on
    /// the line, the real 250 ms delay let run (the dispatcher pumped). Move mode off, as no popup shows in move mode. The
    /// cursor is said to be outside every line (CursorInside) unless a check says otherwise: the real one is never over a
    /// window parked off screen. In the tavern first (no Skip combat button), then in combat (the button shows).
    /// </summary>
    private static void HoverChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        var canvas = window.Overlay;
        var scale = TavernLayout.Scale(canvas.ActualHeight);
        var comps = window.Comps;
        var popup = comps.Popup;
        window.SetMoveMode(false);
        window.ShowSkipCombat(false);
        window.CursorInside = _ => false;
        window.UpdateLayout();
        FlushLog(window);
        var logStart = window.LogLines.Count;

        // A target whose guide has all six sections (the first such), else the first target.
        var guide = window.Targets.Select(t => t.Guide).FirstOrDefault(g => ExpectedSections(g, window) == SectionOrder.Length) ?? window.Targets.First().Guide;
        var line = comps.ShownLines[guide.Id];

        // (e) left before the delay: never shown.
        var shows = popup.Shows;
        Raise(line, UIElement.MouseEnterEvent);
        Headless.Pump(100);
        Raise(line, UIElement.MouseLeaveEvent);
        Headless.Pump(400);
        check("hover: a line left before the 250 ms delay never shows its popup", popup.Shows == shows && !popup.IsVisible,
            $"shown {popup.Shows - shows} times, visible: {popup.IsVisible}");

        // A second enter (HDT's probe, then WPF's) does not restart the delay: shown once, about 250 ms after the first.
        Raise(line, UIElement.MouseEnterEvent);
        Headless.Pump(150);
        Raise(line, UIElement.MouseEnterEvent);
        Headless.Pump(170);
        window.UpdateLayout();
        check("hover: after the delay the popup shows, once; a second MouseEnter on the line changes nothing",
            popup.IsVisible && popup.Shows == shows + 1 && popup.ShownGuide == guide.Id,
            $"\"{guide.Name}\": visible {popup.IsVisible} 320 ms after the first enter (170 after the second), shown {popup.Shows - shows} time(s)");

        // (a) where it is.
        var rect = RectOf(popup.Element);
        var panel = RectOf(comps.Element);
        var zones = NoGoZones.For(canvas.ActualWidth, canvas.ActualHeight).Where(z => Overlap(rect, RectOf(z.Rect))).Select(z => z.Name).ToList();
        var inside = rect.Left >= -0.5 && rect.Top >= -0.5 && rect.Right <= canvas.ActualWidth + 0.5 && rect.Bottom <= canvas.ActualHeight + 0.5;
        check("hover: the popup is inside the overlay, on no zone of the game (NoGoZones), off the panel",
            popup.IsVisible && rect.Width > 0 && inside && zones.Count == 0 && !Overlap(rect, panel),
            $"popup {Describe(rect)}, panel {Describe(panel)}, zones covered: [{string.Join(", ", zones)}]");

        // (b) its texts.
        var texts = TextProblems(popup.Element, scale);
        check("hover: popup: no text under 12 px, none cut", texts.Checked > 0 && texts.Problems.Count == 0, $"{texts.Checked} texts checked" + Problems(texts.Problems));

        // (c) its sections, all of them in the tavern.
        var expected = ExpectedSections(guide, window);
        var (drawn, more) = Sections(popup.Element);
        check("hover: the popup shows the guide's sections in HDT's order, all six for a guide that has them",
            expected == SectionOrder.Length && drawn.SequenceEqual(SectionOrder) && more == null,
            $"{string.Join(", ", drawn)} ({more ?? $"no \"k of n\" line"}; the guide has {expected})");

        FlushLog(window);
        var logged = window.LogLines.Skip(logStart).Where(l => l.Contains("guide popup")).ToList();
        check("hover: one log line per popup shown, saying where and how many sections",
            logged.Count == 1 && logged[0].Contains($"guide popup {guide.Name} shown at (") && logged[0].Contains($"sections={drawn.Count}/{expected}"),
            string.Join(" | ", logged));

        // (f) an oval of the same line hovered too: the line is still entered, the popup stays, the card shows elsewhere.
        var ovals = HarnessWindow.Ovals(line);
        if (ovals.Count > 0)
        {
            Raise(ovals[0], UIElement.MouseEnterEvent);
        }

        window.UpdateLayout();
        var preview = HdtTooltip.ShowingRect(canvas);
        check("hover: an oval of the line shows its card while the popup stays, neither covering the other nor the panel",
            ovals.Count > 0 && preview is { } p && p.Width > 0 && popup.IsVisible && !Overlap(p, rect) && !Overlap(p, panel),
            $"{ovals.Count} ovals; card preview {(preview is { } q ? Describe(q) : "none")}, popup {Describe(rect)}, panel {Describe(panel)}, popup visible {popup.IsVisible}");
        if (ovals.Count > 0)
        {
            Raise(ovals[0], UIElement.MouseLeaveEvent);
        }

        check("hover: leaving the oval takes its card away, the popup stays", HdtTooltip.Showing(canvas) == null && popup.IsVisible,
            $"card still shown: {HdtTooltip.Showing(canvas) != null}, popup visible: {popup.IsVisible}");

        // WPF's MouseLeave as HDT's window turns click-through again, the cursor still on the line: ignored.
        window.CursorInside = element => ReferenceEquals(element, line);
        Raise(line, UIElement.MouseLeaveEvent);
        check("hover: a MouseLeave with the cursor still on the line keeps the popup", popup.IsVisible, $"visible {popup.IsVisible}");

        // (d) the cursor really left.
        window.CursorInside = _ => false;
        Raise(line, UIElement.MouseLeaveEvent);
        check("hover: MouseLeave hides the popup", !popup.IsVisible, $"visible {popup.IsVisible}");

        // In combat the Skip combat button shows: the popup goes around it.
        window.ShowSkipCombat(true);
        window.UpdateLayout();
        Raise(line, UIElement.MouseEnterEvent);
        Headless.Pump(400);
        window.UpdateLayout();
        var skip = CanvasChildSaying(canvas, "Skip combat");
        var inCombat = RectOf(popup.Element);
        var combatZones = NoGoZones.For(canvas.ActualWidth, canvas.ActualHeight).Where(z => Overlap(inCombat, RectOf(z.Rect))).Select(z => z.Name).ToList();
        var (combatDrawn, combatMore) = Sections(popup.Element);
        var combatCounted = combatMore == null ? combatDrawn.Count == expected : combatMore == $"{combatDrawn.Count} of {expected} sections";
        check("hover, in combat: the popup covers neither the Skip combat button nor a zone; its sections in order, counted when some are left out",
            popup.IsVisible && skip != null && !Overlap(inCombat, RectOf(skip)) && combatZones.Count == 0 && !Overlap(inCombat, panel)
            && combatDrawn.Count >= 1 && combatDrawn.SequenceEqual(SectionOrder.Where(combatDrawn.Contains)) && combatCounted,
            $"popup {Describe(inCombat)}, Skip combat {(skip != null ? Describe(RectOf(skip)) : "not found")}, zones [{string.Join(", ", combatZones)}]; "
            + $"{string.Join(", ", combatDrawn)} ({combatMore ?? $"all {expected}"})");

        // (g) a click on the line's name opens the detail and hides the popup.
        var clicked = Click(comps.Element, guide.Name);
        window.UpdateLayout();
        check("hover: a click on the line's name opens the detail and hides the popup", clicked && comps.ShowsDetail && !popup.IsVisible,
            $"clicked {clicked}, detail {comps.ShowsDetail}, popup visible {popup.IsVisible}");
        Click(comps.Element, "← All comp guides");
        window.UpdateLayout();

        // Move mode hides it, and no line shows one in move mode.
        line = comps.ShownLines[guide.Id];
        Raise(line, UIElement.MouseEnterEvent);
        Headless.Pump(400);
        var shownBefore = popup.IsVisible;
        window.SetMoveMode(true);
        var hiddenByMove = !popup.IsVisible;
        line = comps.ShownLines[guide.Id];
        Raise(line, UIElement.MouseLeaveEvent);
        Raise(line, UIElement.MouseEnterEvent);
        Headless.Pump(400);
        check("hover: move mode hides the popup, and a line hovered in move mode shows none", shownBefore && hiddenByMove && !popup.IsVisible,
            $"shown before {shownBefore}, hidden by move mode {hiddenByMove}, shown in move mode {popup.IsVisible}");

        window.CursorInside = null;
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
