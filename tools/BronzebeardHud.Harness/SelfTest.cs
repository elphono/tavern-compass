using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BronzebeardHud.HdtPlugin;
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
/// its popup where it covers nothing and hides it as it should (HoverChecks), the bridge to the synthetic Firestone comps
/// is logged and shows in the labels of a discover, on Bob's cards and in a guide's context line (BridgeLine,
/// TopBoardFrame, ChoiceChecks, ContextChecks), an open choice takes the markers and the panel off the screen and its
/// closing puts them back as they were (CoverChecks), nothing was logged as a warning or an error (the log read once the
/// dispatcher has delivered it), and the layout file is the harness's own, never the real plugin's.
/// </summary>
internal static class SelfTest
{
    /// <summary>PanelMover's colour in move mode: its frames are told apart from the dotted frames on Bob's cards by it.</summary>
    private static readonly Color MoverColour = Color.FromRgb(0x00, 0xE5, 0xFF);

    /// <summary>ChoiceAdvicePanel's background for a label that names no target (a pivot, another guide, "—").</summary>
    private static readonly Color NeutralLabel = Color.FromArgb(0xE6, 0x3A, 0x3A, 0x44);

    /// <summary>GuideView.MutedBrush: the context line's colour.</summary>
    private static readonly Color Muted = Color.FromRgb(0xC8, 0xCD, 0xD8);

    public static (bool Passed, string Report) Run(HarnessWindow window)
    {
        var checks = new List<(string Name, bool Ok, string Detail)>();
        void Check(string name, bool ok, string detail) => checks.Add((name, ok, detail));

        // A group of checks that throws (a line it looks for is not drawn, say) is one failed check, and the others still
        // run: a self-test that stops on an exception writes no report, and its "detection" says nothing about what broke.
        void Group(string name, Action<HarnessWindow, Action<string, bool, string>> group)
        {
            try
            {
                group(window, Check);
            }
            catch (Exception e)
            {
                Check($"{name}: ran to the end", false, $"{e.GetType().Name}: {e.Message} at {e.StackTrace?.Split('\n').FirstOrDefault(l => l.Contains("SelfTest"))?.Trim()}");
                window.SetMoveMode(false);
                window.CursorInside = null;
                window.ShowChoice(ChoiceKind.None);
                window.SetScenario(HarnessData.DefaultScenario);
                window.UpdateLayout();
            }
        }

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

        var counts = Enumerable.Range(0, HarnessData.Scenarios.Count).Select(s => window.TargetsOf(s).Count).ToList();
        Check($"the scenarios give {string.Join(", ", HarnessData.ExpectedTargets)} targets", counts.SequenceEqual(HarnessData.ExpectedTargets),
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

        BridgeLine(window, Check);
        TopBoardFrame(window, Check);

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

        Group("ticks", TickChecks);
        Group("lobby", LobbyChecks);
        Group("board power", PowerChecks);

        Group("choices", ChoiceChecks);
        Group("hover", HoverChecks);
        Group("context line", ContextChecks);
        Group("choice cover", CoverChecks);

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
    /// (Second bug of 2026-10-06, "when I click a checkbox it removes other compositions, I think the ones I was already
    /// playing") A tick names where the player wants to go, and keeps what he is building. In the scenario "Ticks: in progress
    /// and guesses" the automatic targets are Elemental Cycle (two key cards held: in progress), Beast Deathrattle and Pirate
    /// Discover (one each: guesses). Ticking Pirate Discover makes it the first target, keeps Elemental Cycle in its colour
    /// with "in progress" under its name, and silences Beast Deathrattle; Bob's frames follow; a second tick adds a chosen
    /// guide; − and + are dim and do nothing; unticking everything gives back the automatic targets in the colours they had.
    /// The boxes are clicked as the mouse would (their Click event), found in the line of the guide named, never by place.
    /// </summary>
    private static void TickChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        var comps = window.Comps.Element;
        window.SetScenario(5);
        window.UpdateLayout();
        static string Kinds(IEnumerable<CompTarget> targets) => string.Join("; ", targets.Select(t => $"{t.Rank}. {t.Guide.Name} {t.Colour} {t.Kind}"));
        var auto = window.Targets.ToList();
        var colourOf = auto.ToDictionary(t => t.Guide.Name, t => t.Colour);
        var autoNames = new[] { "Elemental Cycle", "Beast Deathrattle", "Pirate Discover" };
        check("ticks scene: the automatic targets are a guide in progress and two guesses",
            auto.Select(t => t.Guide.Name).SequenceEqual(autoNames) && auto.All(t => t.Kind == TargetKind.Probable), Kinds(auto));

        var clicked = ClickBoxOf(window, "Pirate Discover");
        window.UpdateLayout();
        var one = window.Targets.ToList();
        var frames = window.Highlights.Where(h => h.Kind != HighlightKind.None).Select(h => h.Target?.Guide.Name).Distinct().ToList();
        var caption = window.Comps.ShownLines.TryGetValue(window.GuideOf("Elemental Cycle")!.Id, out var inProgressLine)
                      && Texts(inProgressLine).Any(t => t.IsVisible && Content(t) == "in progress");
        var captions = Texts(comps).Count(t => t.IsVisible && Content(t) == "in progress");
        check("ticking a guide: it comes first, the guide in progress stays in its colour and says so, the guess leaves; Bob's frames follow",
            clicked && one.Select(t => (t.Guide.Name, t.Kind)).SequenceEqual(new[] { ("Pirate Discover", TargetKind.Chosen), ("Elemental Cycle", TargetKind.InProgress) })
            && one.All(t => colourOf.TryGetValue(t.Guide.Name, out var c) && c == t.Colour) && caption && captions == 1
            && frames.Count >= 2 && frames.All(n => n is "Pirate Discover" or "Elemental Cycle") && FindText(comps, "1 chosen") != null,
            $"{Kinds(one)}; \"in progress\" under Elemental Cycle: {caption} ({captions} drawn); frames for [{string.Join(", ", frames)}]; "
            + $"title: {(FindText(comps, "1 chosen") != null ? "1 chosen" : "not 1 chosen")}");

        ClickBoxOf(window, "Undead Butcher");
        window.UpdateLayout();
        var two = window.Targets.ToList();
        check("ticking a second one adds it after the first, the guide in progress still after them",
            two.Select(t => (t.Guide.Name, t.Kind)).SequenceEqual(new[] { ("Pirate Discover", TargetKind.Chosen), ("Undead Butcher", TargetKind.Chosen), ("Elemental Cycle", TargetKind.InProgress) })
            && FindText(comps, "2 chosen") != null, Kinds(two));

        // − and + are dim, and a click on either changes nothing. One at a time and on the count itself: the log lines of
        // a click reach the window's list later, through the dispatcher, and − then + would cancel out.
        var minus = FindText(comps, "−");
        var plus = FindText(comps, "+");
        var wanted = window.Count;
        var clickedMinus = Click(comps, "−");
        var afterMinus = window.Count;
        var clickedPlus = Click(comps, "+");
        window.UpdateLayout();
        check("− and + are dim and do nothing while a guide is ticked", clickedMinus && clickedPlus && afterMinus == wanted && window.Count == wanted
            && minus?.Parent is Border { Opacity: < 1 } && plus?.Parent is Border { Opacity: < 1 } && window.Targets.Count == 3,
            $"clicked: {clickedMinus}/{clickedPlus}, count {wanted} -> {afterMinus} -> {window.Count}, opacity {(minus?.Parent as Border)?.Opacity}/{(plus?.Parent as Border)?.Opacity}, {window.Targets.Count} targets");

        ClickBoxOf(window, "Undead Butcher");
        window.UpdateLayout();
        ClickBoxOf(window, "Pirate Discover");
        window.UpdateLayout();
        var back = window.Targets.ToList();
        var minusAgain = FindText(comps, "−");
        // The same three in the same colours; Pirate Discover, a target all along, may keep its place ahead of Beast
        // Deathrattle, tied with it (CompTargets.Choose: a target keeps its place at an equal score).
        check("unticking everything gives the automatic targets back, in the colours they had",
            back.Select(t => t.Guide.Name).OrderBy(n => n).SequenceEqual(autoNames.OrderBy(n => n))
            && back.All(t => t.Kind == TargetKind.Probable && colourOf.TryGetValue(t.Guide.Name, out var c) && c == t.Colour)
            && FindText(comps, "3 targets") != null && minusAgain?.Parent is Border { Opacity: 1 },
            Kinds(back));

        window.SetScenario(HarnessData.DefaultScenario);
        window.UpdateLayout();
    }

    /// <summary>
    /// Clicks the tick box of a guide's line, as the mouse would (its Click event); false when the line is not shown or has no
    /// box: the check that asked fails and says so, rather than the whole self-test stopping on an exception.
    /// </summary>
    private static bool ClickBoxOf(HarnessWindow window, string guideName)
    {
        var line = window.GuideOf(guideName) is { } guide && window.Comps.ShownLines.TryGetValue(guide.Id, out var shown) ? shown : null;
        var box = line == null ? null : Boxes(line).FirstOrDefault(b => b.IsVisible);
        box?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        return box != null;
    }

    /// <summary>
    /// (First bug of 2026-10-06, "do not advise compositions with tribes absent from the game") (a) In a lobby without undead
    /// or dragons, holding the neutral card that is a key card of Undead Butcher, Undead Attack and Dragon Shields: none of
    /// them is a target, a line of the list or a frame on Bob's cards — the guides of absent tribes are worked out here from
    /// the harness's data, not taken from LobbyGuides —, and the log line names them with why. (b) The same cards, the lobby
    /// not known yet: nothing is left out (Undead Attack is a target: what (a) would have caught) and the panel says so.
    /// (c) A guide ticked while the lobby was not known, which the lobby then turns out not to play: unticked, said once in
    /// the log, never a target.
    /// </summary>
    private static void LobbyChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        var comps = window.Comps.Element;
        var guides = HarnessData.Guides(id => id).All;
        FlushLog(window);
        var logStart = window.LogLines.Count;

        window.SetScenario(3);
        window.UpdateLayout();
        var lobby = HarnessData.Scenarios[3].Lobby;
        bool Absent(CompGuide g) => GuideTribes.NameOf(g.PrimaryTribe) is { } tribe && !lobby.Contains(tribe);
        var off = guides.Where(Absent).Select(g => g.Name).ToList();
        var targets = window.Targets.ToList();
        var drawn = Texts(comps).Where(t => t.IsVisible).Select(Content).ToList();
        var offDrawn = drawn.Where(off.Contains).ToList();
        var onDrawn = drawn.Count(text => guides.Any(g => g.Name == text && !Absent(g)));
        var framed = window.Highlights.Where(h => h.Target != null).Select(h => h.Target!.Guide).Where(Absent).Select(g => g.Name).Distinct().ToList();
        FlushLog(window);
        var line = window.LogLines.Skip(logStart).FirstOrDefault(l => l.Contains("Bronzebeard HUD: lobby tribes=[BEAST,ELEMENTAL,MECHANICAL,MURLOC,PIRATE]")) ?? string.Empty;
        check("lobby without undead or dragons: the neutral key card makes none of their guides a target, a line or a frame; the log says which and why",
            off.Count >= 3 && targets.Select(t => t.Guide.Name).SequenceEqual(new[] { "Mech Magnet", "Mech Divine Shield" }) && targets.All(t => !Absent(t.Guide))
            && offDrawn.Count == 0 && onDrawn >= 2 && framed.Count == 0 && line.Contains("Undead Attack (no UNDEAD)") && line.Contains("Dragon Shields (no DRAGON)"),
            $"targets {CompTargets.Summary(targets)}; {off.Count} guides of absent tribes, {offDrawn.Count} drawn [{string.Join(", ", offDrawn)}], {onDrawn} playable names drawn; "
            + $"frames for absent tribes [{string.Join(", ", framed)}]; log: {(line.IndexOf(">> ", StringComparison.Ordinal) is var at and >= 0 ? line.Substring(at + 3) : "no lobby line with these tribes")}");

        // (b) Three targets in two tiers: at the default size the note would push the third out, so it gives way
        // (CompGuideLayout.KeepsOptionalLine); with one target wanted (− twice) it has the room, and shows.
        const string unknownNote = "Lobby tribes unknown: every guide listed";
        window.SetScenario(4);
        window.UpdateLayout();
        var unknownTargets = window.Targets.ToList();
        var targetLines = unknownTargets.Count(t => window.Comps.ShownLines.ContainsKey(t.Guide.Id));
        var noteThree = FindText(comps, unknownNote) != null;
        var fewer = Click(comps, "−") & Click(comps, "−");
        window.UpdateLayout();
        var noteOne = FindText(comps, unknownNote) != null;
        var oneTarget = window.Targets.Count;
        Click(comps, "+");
        Click(comps, "+");
        window.UpdateLayout();
        check("lobby not known yet: nothing left out (Undead Attack is a target); the note says so, giving way to a target's line",
            unknownTargets.Any(t => t.Guide.Name == "Undead Attack") && unknownTargets.Count == 3 && targetLines == 3 && !noteThree
            && fewer && oneTarget == 1 && noteOne && window.Count == 3,
            $"targets {CompTargets.Summary(unknownTargets)}, {targetLines} of their lines drawn, note {(noteThree ? "drawn" : "given way")}; "
            + $"one target wanted: {oneTarget} target(s), note {(noteOne ? "drawn" : "missing")}; count back to {window.Count}");

        window.Tick("Undead Attack");
        window.UpdateLayout();
        var tickedBefore = window.Targets.Any(t => t.Guide.Name == "Undead Attack" && t.Ticked);
        FlushLog(window);
        var before = window.LogLines.Count;
        window.SetScenario(3, sameGame: true); // the same game: its lobby now known
        window.UpdateLayout();
        FlushLog(window);
        var unticked = window.LogLines.Skip(before).Count(l => l.Contains("Bronzebeard HUD: unticked Undead Attack/11: no UNDEAD"));
        check("a tick on a guide the lobby turns out not to play: dropped, said once, never a target",
            tickedBefore && unticked == 1 && window.Targets.All(t => t.Guide.Name != "Undead Attack") && FindText(comps, "1 chosen") == null,
            $"ticked while unknown: {tickedBefore}; {unticked} log line(s); targets {CompTargets.Summary(window.Targets)}");

        window.SetScenario(HarnessData.DefaultScenario);
        window.UpdateLayout();
    }

    /// <summary>
    /// The board's power under the list (BoardPowerView), read from what is drawn: for each level, the badge in the level's
    /// colour with its sign and percentage, the lit segment of the gauge at the level's place (red, yellow, green, gold),
    /// the figures beside it; without data ("none": a hero without curve; "early": turn 2), a grey badge "–", no segment lit,
    /// no colour, and the reason written. Texts at least 12 px and uncut, the indicator inside the panel, at the panel's
    /// default width, which is also its minimum.
    /// </summary>
    private static void PowerChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        var comps = window.Comps.Element;
        var scale = TavernLayout.Scale(window.Overlay.ActualHeight);
        var scenes = new (string Scene, int Lit, string Badge, string Text)[]
        {
            ("behind", 0, "▼ −33%", "Board 80 · hero avg 120 at turn 8"),
            ("even", 1, "≈ +18%", "Board 142 · hero avg 120 at turn 8"),
            ("ahead", 2, "▲ +58%", "Board 190 · hero avg 120 at turn 8"),
            ("shiny", 3, "★ +117%", "Board 260 · hero avg 120 at turn 8"),
            ("none", -1, "–", "Board 142 · no curve for this hero"),
            ("early", -1, "–", "Board 3 · hero avg 6 at turn 2 · too early"),
        };
        var gaugeColours = new List<Color?>();
        foreach (var (scene, lit, badgeText, text) in scenes)
        {
            window.SetPower(scene);
            window.UpdateLayout();
            var badge = Tagged(comps, BoardPowerView.BadgeTag).OfType<Border>().FirstOrDefault();
            var segments = Enumerable.Range(0, 4).Select(i => Tagged(comps, BoardPowerView.GaugeTag + i).OfType<Border>().FirstOrDefault()).ToList();
            var badgeColour = (badge?.Background as SolidColorBrush)?.Color;
            var said = badge == null ? "none" : string.Join(" ", Texts(badge).Select(Content));
            var litNow = segments.Select((s, i) => (s, i)).Where(x => x.s != null && Math.Abs(x.s.Opacity - 1) < 1e-9).Select(x => x.i).ToList();
            var colours = segments.Select(s => (s?.Background as SolidColorBrush)?.Color).ToList();
            var expectedColour = lit >= 0 ? colours[lit] : (Color)ColorConverter.ConvertFromString(BoardPowerLevels.Colour(BoardPower.None));
            if (scene == "even")
            {
                gaugeColours = colours;
            }

            var details = FindText(comps, text) != null;
            var inside = badge != null && RectIn(badge, comps) is var r && r.Left >= -0.5 && r.Right <= comps.ActualWidth + 0.5 && r.Bottom <= comps.ActualHeight + 0.5;
            var texts = TextProblems(comps, scale);
            var ok = badge != null && segments.All(s => s != null) && said == badgeText && details && inside && texts.Problems.Count == 0
                     && (lit >= 0
                         ? litNow.SequenceEqual(new[] { lit }) && badgeColour == expectedColour && badgeColour == (Color)ColorConverter.ConvertFromString(BoardPowerLevels.Colour(BoardPowerLevels.Gauge[lit]))
                         : litNow.Count == 0 && badgeColour == expectedColour && colours.All(c => c == expectedColour));
            check($"board power \"{scene}\": badge \"{badgeText}\" in its colour, gauge lit at {(lit >= 0 ? lit.ToString() : "nothing")}, the figures beside it, nothing cut",
                ok, $"badge \"{said}\" {badgeColour}, lit [{string.Join(",", litNow)}], segments [{string.Join(" ", colours)}], figures {(details ? "drawn" : "missing")}, inside {inside}, "
                    + $"{texts.Checked} texts checked" + Problems(texts.Problems));
        }

        check("board power: the gauge's four segments are four colours, red to gold, whatever the level",
            gaugeColours.Count == 4 && gaugeColours.Distinct().Count() == 4
            && gaugeColours.SequenceEqual(BoardPowerLevels.Gauge.Select(l => (Color?)(Color)ColorConverter.ConvertFromString(BoardPowerLevels.Colour(l)))),
            string.Join(" ", gaugeColours));

        window.SetPower("even");
        window.UpdateLayout();
    }

    /// <summary>The elements under <paramref name="root"/> whose Tag is <paramref name="tag"/>.</summary>
    private static IEnumerable<FrameworkElement> Tagged(DependencyObject root, string tag)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement element && Equals(element.Tag, tag))
            {
                yield return element;
            }

            foreach (var inner in Tagged(child, tag))
            {
                yield return inner;
            }
        }
    }

    /// <summary>An element's box in an ancestor's coordinates.</summary>
    private static Rect RectIn(FrameworkElement element, FrameworkElement ancestor) =>
        element.TransformToAncestor(ancestor).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

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

            if (kind == ChoiceKind.Discover)
            {
                BridgeLabels(labels, said, targets, check);
            }

            // Serves nothing: no target lists it, no other guide has it as a core card, and it stands on fewer than two final
            // boards of every synthetic Firestone comp (the harness's data, not the advice).
            var guides = HarnessData.Guides(id => id).All;
            for (var i = 0; i < options.Count && i < said.Count; i++)
            {
                var id = options[i].CardId;
                if (kind != ChoiceKind.Trinket && targets.All(t => GuideCardEffects.RoleIn(t.Guide, id) == null) && !guides.Any(g => g.CoreCards.Contains(id))
                    && !HarnessData.FirestoneComps.Any(c => BoardsWith(c, id) >= CardEvidence.MinimumBoards))
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

    /// <summary>Final boards of a composition that hold a card (a board holding it twice counts once).</summary>
    private static int BoardsWith(Composition comp, string cardId) => comp.FinalBoards.Count(b => b.Cards.Contains(cardId));

    /// <summary>
    /// (a) The bridge's log line, once (the guides do not change in the self-test): every guide named, Pirate Discover and
    /// Mech Magnet bridged to their synthetic comps, and the counter-example — Mech Divine Shield, which shares at least two
    /// cards with mech_fs but under half of its core cards (measured here on the data, not taken from GuideBridge) — "no match".
    /// </summary>
    private static void BridgeLine(HarnessWindow window, Action<string, bool, string> check)
    {
        FlushLog(window);
        var lines = window.LogLines.Where(l => l.Contains("Bronzebeard HUD: bridge: ")).ToList();
        var line = lines.FirstOrDefault() ?? string.Empty;
        var guides = HarnessData.Guides(id => id).All;
        var mds = guides.First(g => g.Name == "Mech Divine Shield");
        var mech = HarnessData.FirestoneComps.First(c => c.Id == "mech_fs");
        var mechCards = new HashSet<string>(mech.CoreCards.Concat(mech.AddonCards).Concat(mech.FinalBoards.SelectMany(b => b.Cards)));
        var shared = mds.CoreCards.Concat(mds.AddonCards).Distinct().Where(mechCards.Contains).ToList();
        var sharedKeys = mds.CoreCards.Count(mechCards.Contains);
        var nearMiss = shared.Count >= GuideBridge.MinimumShared && 2 * sharedKeys < mds.CoreCards.Count;
        check("bridge: one log line naming every guide; Pirate Discover and Mech Magnet bridged, the counter-example Mech Divine Shield \"no match\"",
            lines.Count == 1 && guides.All(g => line.Contains(g.Name + " → ")) && line.Contains("Pirate Discover → pirate_fs (") && line.Contains("Mech Magnet → mech_fs (")
            && line.Contains("Mech Divine Shield → no match") && nearMiss,
            $"{lines.Count} line(s); counter-example: Mech Divine Shield shares {shared.Count} cards with mech_fs, {sharedKeys} of its {mds.CoreCards.Count} core cards; "
            + line.Substring(Math.Max(0, line.IndexOf(">> ", StringComparison.Ordinal) + 3)));
    }

    /// <summary>
    /// (c) A card of Bob's no target's guide lists but that stands on at least two final boards of a synthetic comp (57, on 3
    /// of mech_fs's 5, Mech Magnet's comp: HarnessData.FirestoneComps): a dotted frame in Mech Magnet's colour on its slot,
    /// a "+ … 3/5" label in that colour, and the plugin's log line says "card:boards 3/5:guide" (TavernHighlights.Summary).
    /// </summary>
    private static void TopBoardFrame(HarnessWindow window, Action<string, bool, string> check)
    {
        var canvas = window.Overlay;
        var shop = HarnessData.Shop;
        var top = shop.Select((card, i) => (Card: card, Index: i, Boards: HarnessData.FirestoneComps.Select(c => (Comp: c, Count: BoardsWith(c, card))).OrderByDescending(x => x.Count).First()))
            .Where(x => window.Targets.All(t => GuideCardEffects.RoleIn(t.Guide, x.Card) == null) && x.Boards.Count >= CardEvidence.MinimumBoards)
            .ToList();
        var mm = window.Targets.FirstOrDefault(t => t.Guide.Name == "Mech Magnet");
        if (top.Count != 1 || mm == null || top[0].Boards.Comp.Id != "mech_fs")
        {
            check("Bob's card on the top boards of a bridged target: dotted frame and \"+ T k/n\" in T's colour, in the log", false,
                $"{top.Count} such cards in Bob's row, Mech Magnet {(mm == null ? "no target" : "a target")}");
            return;
        }

        var (card, index, boards) = top[0];
        var count = $"{boards.Count}/{boards.Comp.FinalBoards.Count}";
        var colour = (Color)ColorConverter.ConvertFromString(mm.Colour);
        var slot = TavernLayout.CardSlots(canvas.ActualWidth, canvas.ActualHeight, shop.Count)[index];
        var frame = canvas.Children.OfType<Rectangle>().FirstOrDefault(r => r.IsVisible && r.StrokeDashArray.Count > 0
            && Math.Abs(Canvas.GetLeft(r) - slot.Left) < 0.5 && Math.Abs(Canvas.GetTop(r) - slot.Top) < 0.5);
        var frameColour = (frame?.Stroke as SolidColorBrush)?.Color;
        var label = canvas.Children.OfType<Border>().FirstOrDefault(b => b.IsVisible && b.Child is StackPanel && !Equals(b.Tag, "choice")
            && Canvas.GetLeft(b) + b.ActualWidth / 2 > slot.Left && Canvas.GetLeft(b) + b.ActualWidth / 2 < slot.Left + slot.Width
            && Texts(b).Any(t => Content(t).StartsWith("+ ", StringComparison.Ordinal) && Content(t).EndsWith(" " + count, StringComparison.Ordinal)));
        var labelText = label == null ? "none" : string.Join(" / ", Texts(label).Select(Content));
        FlushLog(window);
        var summary = $"{card}:boards {count}:{mm.Guide.Id}";
        var logged = window.LogLines.Where(l => l.Contains("tavern highlights=[") && l.Contains(summary)).ToList();
        check("Bob's card on the top boards of a bridged target: dotted frame and \"+ T k/n\" in T's colour, in the log",
            frameColour == colour && (label?.Background as SolidColorBrush)?.Color == colour && logged.Count >= 1,
            $"{card} (Bob's #{index + 1}) on {count} boards of {boards.Comp.Id}: frame {(frame == null ? "none" : $"dotted {frameColour}")}, label \"{labelText}\" "
            + $"{(label?.Background as SolidColorBrush)?.Color}, Mech Magnet is {colour}; log: {(logged.Count > 0 ? "\"" + summary + "\"" : "no line with " + summary)}");
    }

    /// <summary>
    /// (b) In a discover, the three labels the bridge opens, read from what is drawn (text and background), never from the
    /// advice: "+ T k/n boards" (a card no list of T names, on T's comp's boards) in the colour of T, a role followed by
    /// "· k/n boards", and "pivot → G (X)" on the neutral background. Their sizes and cuts: the check before, on every label.
    /// </summary>
    private static void BridgeLabels(IReadOnlyList<Border?> labels, IReadOnlyList<List<string>> said, IReadOnlyList<CompTarget> targets, Action<string, bool, string> check)
    {
        var topBoards = new List<string>();
        var suffixed = new List<string>();
        var pivots = new List<string>();
        var wrong = new List<string>();
        for (var i = 0; i < labels.Count && i < said.Count; i++)
        {
            var drawn = (labels[i]?.Background as SolidColorBrush)?.Color;
            foreach (var line in said[i])
            {
                var what = $"#{i} \"{line}\" on {drawn}";
                if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\+ .+ \d+/\d+ boards$") && !line.Contains("·"))
                {
                    var named = targets.Where(t => line.StartsWith("+ " + t.Guide.Name + " ", StringComparison.Ordinal)).ToList();
                    var ok = named.Count == 1 && line == said[i][0] && drawn == (Color)ColorConverter.ConvertFromString(named[0].Colour);
                    (ok ? topBoards : wrong).Add(what + (named.Count == 1 ? $", {named[0].Guide.Name} is {named[0].Colour}" : $", names {named.Count} targets"));
                }
                else if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^(★ core|\+) .+ · \d+/\d+ boards$"))
                {
                    suffixed.Add(what);
                }
                else if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^pivot → .+ \([SABCD]\)$"))
                {
                    (drawn == NeutralLabel && line == said[i][0] ? pivots : wrong).Add(what);
                }
            }
        }

        check("choice discover: the bridge's labels: \"+ T k/n boards\" in T's colour, a role \"· k/n boards\", \"pivot → G (X)\" neutral",
            topBoards.Count >= 1 && suffixed.Count >= 1 && pivots.Count >= 1 && wrong.Count == 0,
            $"top boards: {string.Join("; ", topBoards)} | suffix: {string.Join("; ", suffixed)} | pivot: {string.Join("; ", pivots)}"
            + (wrong.Count > 0 ? " | WRONG: " + string.Join("; ", wrong) : string.Empty));
    }

    /// <summary>
    /// (e) A guide's line of Firestone context (TargetContext), under its header in the detail and in the popup: drawn for a
    /// bridged guide (Pirate Discover: "≈ 3,5 with your hero (23) · final turn ≈ 13 · 5 top boards", computed by hand from
    /// HarnessData — 23 games at 2,9 pulled towards 3,9 by 30: 3,47; boards at turns 11, 12, 13, 13, 14), at
    /// PanelTypography.Small in the muted colour, between the name and the first section; not drawn for the guide bridged to
    /// nothing (Mech Divine Shield).
    /// </summary>
    private static void ContextChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        const string expected = "≈ 3,5 with your hero (23) · final turn ≈ 13 · 5 top boards";
        var scale = TavernLayout.Scale(window.Overlay.ActualHeight);
        var comps = window.Comps;
        var popup = comps.Popup;
        window.SetMoveMode(false);
        window.ShowSkipCombat(false);
        window.CursorInside = _ => false;
        window.UpdateLayout();

        static TextBlock? ContextIn(DependencyObject root) => Texts(root).FirstOrDefault(t => t.IsVisible && Content(t).Contains(" top board"));

        string LookAt(DependencyObject root, TextBlock? context)
        {
            if (context == null)
            {
                return "none";
            }

            var y = context.TransformToAncestor((Visual)root).Transform(new Point(0, 0)).Y;
            var firstSection = Texts(root).Where(t => t.IsVisible && SectionOrder.Any(s => Content(t).StartsWith(s, StringComparison.Ordinal)))
                .Select(t => t.TransformToAncestor((Visual)root).Transform(new Point(0, 0)).Y).DefaultIfEmpty(double.MaxValue).Min();
            var colour = (context.Foreground as SolidColorBrush)?.Color;
            var ok = Content(context) == expected && Math.Abs(context.FontSize - PanelTypography.Small * scale) < 1e-6 && colour == Muted && y < firstSection;
            return $"{(ok ? "ok" : "WRONG")} \"{Content(context)}\" {context.FontSize:0.#} px {colour} at y {y:0}, first section at {firstSection:0}";
        }

        (string Detail, string Popup) Look(string name)
        {
            var guide = window.GuideOf(name)!;
            Click(comps.Element, guide.Name);
            window.UpdateLayout();
            var detail = comps.ShowsDetail ? LookAt(comps.Element, ContextIn(comps.Element)) : "detail not opened";
            Click(comps.Element, "← All comp guides");
            window.UpdateLayout();
            var line = comps.ShownLines[guide.Id];
            Raise(line, UIElement.MouseEnterEvent);
            Headless.Pump(400);
            window.UpdateLayout();
            var shown = popup.IsVisible ? LookAt(popup.Element, ContextIn(popup.Element)) : "popup not shown";
            Raise(line, UIElement.MouseLeaveEvent);
            return (detail, shown);
        }

        var bridged = Look("Pirate Discover");
        var unbridged = Look("Mech Divine Shield");
        check("context line: under the header of a bridged guide's detail and popup (small, muted), absent for a guide bridged to nothing",
            bridged.Detail.StartsWith("ok ", StringComparison.Ordinal) && bridged.Popup.StartsWith("ok ", StringComparison.Ordinal)
            && unbridged.Detail == "none" && unbridged.Popup == "none",
            $"Pirate Discover: detail {bridged.Detail}; popup {bridged.Popup} | Mech Divine Shield: detail {unbridged.Detail}; popup {unbridged.Popup}");
        window.CursorInside = null;
    }

    /// <summary>
    /// What TavernMarkers has on the canvas, found without asking it: every visible element that is neither the scene's
    /// (a string Tag: zones, Bob's and the options' boxes, HDT's tooltip slot), nor the panel or its popup, nor a label of
    /// the choice open (at ChoiceLayout's places); each described by its kind, place, colours and texts, sorted.
    /// </summary>
    private static List<string> MarkerSignatures(HarnessWindow window)
    {
        var canvas = window.Overlay;
        var choiceLabels = window.Choice is { HasMarkers: true } advice
            ? ChoiceLayout.Labels(advice.Kind, window.ChoiceOptions.Count, canvas.ActualWidth, canvas.ActualHeight,
                window.Choices.LastLines.Select(l => l.Count).DefaultIfEmpty(1).Max()).Select(RectOf).ToList()
            : new List<Rect>();
        return canvas.Children.OfType<FrameworkElement>()
            .Where(e => e.Visibility == Visibility.Visible && e.Tag is not string && !ReferenceEquals(e, window.Comps.Element) && !ReferenceEquals(e, window.Comps.Popup.Element))
            .Where(e => !choiceLabels.Any(r => Math.Abs(Canvas.GetLeft(e) - r.Left) < 0.5 && Math.Abs(Canvas.GetTop(e) - r.Top) < 0.5))
            .Select(e =>
            {
                var look = e switch
                {
                    Rectangle r => $"stroke {(r.Stroke as SolidColorBrush)?.Color} dash {r.StrokeDashArray.Count}",
                    Border b => $"border {(b.BorderBrush as SolidColorBrush)?.Color} {b.BorderThickness.Left:0.##} background {(b.Background as SolidColorBrush)?.Color}",
                    _ => string.Empty,
                };
                return $"{e.GetType().Name} ({Canvas.GetLeft(e):0.#},{Canvas.GetTop(e):0.#} {e.ActualWidth:0.#}x{e.ActualHeight:0.#}) {look} [{string.Join(" / ", Texts(e).Select(Content))}]";
            })
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// (d) A choice opened in the shop takes off the screen Bob's frames, labels and ◇ buttons, the panel and a guide popup on
    /// show, and no popup shows meanwhile; one log line when it opens, none for the next updates of the same choice or of
    /// another; closing it puts back the same targets and the same markers (same places, colours and texts), the panel as
    /// it was — the same elements, not rebuilt —, one log line, and a hovered line shows its popup again. A Dark Gift first:
    /// its ◇ fell inside the options. Also measured, for the record: how much of each option the panel covers at its place.
    /// </summary>
    private static void CoverChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        var canvas = window.Overlay;
        var comps = window.Comps;
        var popup = comps.Popup;
        window.SetMoveMode(false);
        window.ShowSkipCombat(false);
        window.CursorInside = _ => false;
        window.ShowChoice(ChoiceKind.None);
        window.UpdateLayout();
        FlushLog(window);
        var logStart = window.LogLines.Count;

        var markersBefore = MarkerSignatures(window);
        var pinsBefore = markersBefore.Count(s => s.EndsWith("[◇]", StringComparison.Ordinal) || s.EndsWith("[◆]", StringComparison.Ordinal));
        var targetsBefore = CompTargets.Summary(window.Targets);
        var highlightsBefore = window.Highlights;
        var panelBefore = RectOf(comps.Element);
        var contentBefore = comps.Element.Child;
        var guide = window.Targets[0].Guide;
        var line = comps.ShownLines[guide.Id];
        Raise(line, UIElement.MouseEnterEvent);
        Headless.Pump(400);
        var popupBefore = popup.IsVisible;

        string Covered(ChoiceKind kind) => string.Join(", ", ChoiceLayout.Cards(kind, 3, canvas.ActualWidth, canvas.ActualHeight).Select((c, i) =>
        {
            var overlap = Rect.Intersect(panelBefore, RectOf(c));
            return overlap.IsEmpty ? $"option {i + 1}: 0" : $"option {i + 1}: {overlap.Width:0}x{overlap.Height:0} px";
        }));
        var measure = $"panel at {Describe(panelBefore)} covers, discover: {Covered(ChoiceKind.Discover)}; Dark Gift: {Covered(ChoiceKind.DarkGift)}";

        window.ShowChoice(ChoiceKind.DarkGift);
        window.UpdateLayout();
        var markersDuring = MarkerSignatures(window);
        var panelDuring = comps.Element.IsVisible;
        var popupDuring = popup.IsVisible;
        Raise(line, UIElement.MouseLeaveEvent);
        Raise(line, UIElement.MouseEnterEvent); // the line "entered" again while the choice is open: no popup
        Headless.Pump(400);
        var popupOnHover = popup.IsVisible;
        Raise(line, UIElement.MouseLeaveEvent);
        window.ShowChoice(ChoiceKind.DarkGift); // the next updates of the same choice
        window.ShowChoice(ChoiceKind.Discover); // then another kind, still open
        window.UpdateLayout();
        var markersStill = MarkerSignatures(window);
        var panelStill = comps.Element.IsVisible;
        check("choice open: Bob's frames, labels and ◇, the panel and a popup on show leave the screen; no popup on hover meanwhile",
            popupBefore && pinsBefore > 0 && markersBefore.Count > pinsBefore && markersDuring.Count == 0 && markersStill.Count == 0
            && !panelDuring && !panelStill && !popupDuring && !popupOnHover,
            $"before: {markersBefore.Count} marker elements ({pinsBefore} ◇), panel shown, popup {popupBefore}; Dark Gift open: {markersDuring.Count} marker elements"
            + $"{(markersDuring.Count > 0 ? " (" + string.Join(" | ", markersDuring.Take(3)) + ")" : string.Empty)}, panel {panelDuring}, popup {popupDuring}, "
            + $"popup on hover {popupOnHover}; discover after it: {markersStill.Count}, panel {panelStill}; {measure}");

        window.ShowChoice(ChoiceKind.None);
        window.UpdateLayout();
        var markersAfter = MarkerSignatures(window);
        var lost = markersBefore.Except(markersAfter).ToList();
        var added = markersAfter.Except(markersBefore).ToList();
        var sameContent = ReferenceEquals(comps.Element.Child, contentBefore);
        check("choice closed: the same targets, frames, labels and ◇ as before, the panel back as it was (the same elements)",
            markersAfter.SequenceEqual(markersBefore) && CompTargets.Summary(window.Targets) == targetsBefore && ReferenceEquals(window.Highlights, highlightsBefore)
            && comps.Element.IsVisible && RectOf(comps.Element) == panelBefore && sameContent,
            $"{markersAfter.Count} marker elements, identical {markersAfter.SequenceEqual(markersBefore)}"
            + (lost.Count + added.Count > 0 ? $" (gone: {string.Join(" | ", lost)}; new: {string.Join(" | ", added)})" : string.Empty)
            + $"; targets {CompTargets.Summary(window.Targets)}; panel {Describe(RectOf(comps.Element))} visible {comps.Element.IsVisible}, same content {sameContent}");

        FlushLog(window);
        var transitions = window.LogLines.Skip(logStart)
            .Where(l => l.Contains("Bronzebeard HUD: choice open") || l.Contains("Bronzebeard HUD: choice closed"))
            .Select(l => l.Substring(l.IndexOf(">> ", StringComparison.Ordinal) + 3))
            .ToList();
        check("one log line per transition, none per update", transitions.SequenceEqual(new[] { ChoiceCover.HiddenLine, ChoiceCover.RestoredLine }),
            string.Join(" | ", transitions));

        Raise(comps.ShownLines[guide.Id], UIElement.MouseEnterEvent);
        Headless.Pump(400);
        var popupAgain = popup.IsVisible;
        Raise(comps.ShownLines[guide.Id], UIElement.MouseLeaveEvent);
        check("after the choice, a hovered line shows its popup again", popupAgain, $"popup visible {popupAgain}");
        window.CursorInside = null;
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
