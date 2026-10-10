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
/// closing puts them back as they were (CoverChecks), the power inset sits under the frame between − and + (InsetChecks),
/// each of its rows lights and makes glow the lamp of its level only, grey without data (PowerChecks, OpponentChecks), + and
/// − size the panel to N lines, the best targets first, on no zone of the game, against the bottom upwards, a handle's box
/// kept until a press, back at the next game and at "Reset" (ResizeChecks), a row that throws is left out alone
/// (GuardChecks), HDT's overlay layer reproduced on an injected mouse lets clicks through outside the elements declared
/// clickable and drives the popup and the card previews from its probe, its second "enter" and its stray "leave" changing
/// nothing (MouseChecks), nothing was logged as a warning or an error (the log read once the dispatcher has delivered it),
/// and the layout file is the harness's own, never the real plugin's.
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
                window.Hdt.Stop();
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
        Group("inset", InsetChecks);
        Group("board power", PowerChecks);
        Group("opponent power", OpponentChecks);
        Group("resize", ResizeChecks);

        Group("choices", ChoiceChecks);
        Group("hover", HoverChecks);
        Group("context line", ContextChecks);
        Group("choice cover", CoverChecks);
        Group("guard", GuardChecks);
        Group("mouse", MouseChecks);

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
    /// with "in progress" under its name, and silences Beast Deathrattle; Bob's frames are then Pirate Discover's alone
    /// (2026-10-07: a guide in progress frames nothing once a guide is ticked); a second tick adds a chosen
    /// guide; − lowers the number to the two ticked guides, which the panel then lists alone, and is dim there, + shows the
    /// others again (Ali, 2026-10-10); unticking everything gives back the automatic targets in the colours they had.
    /// The boxes are clicked as the mouse would (their Click event), found in the line of the guide named, never by place.
    /// </summary>
    private static void TickChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        var comps = window.Comps.Element;
        window.SetScenario(5);

        // A tall box (the handle, in move mode), so that every guide's line is there to be ticked: sized to N lines, the list
        // shows the targets and the next ones only, and this check is about what a tick does, not about sizes.
        var scale = TavernLayout.Scale(window.Overlay.ActualHeight);
        var place = TavernLayout.TargetPanel(window.Overlay.ActualWidth, window.Overlay.ActualHeight);
        window.SetMoveMode(true);
        window.DropPanel(place.Left, 40 * scale);
        window.ResizePanel(place.Right, (40 + 900) * scale);
        window.UpdateLayout();
        static string Kinds(IEnumerable<CompTarget> targets) => string.Join("; ", targets.Select(t => $"{t.Rank}. {t.Guide.Name} {t.Colour} {t.Kind}"));
        var auto = window.Targets.ToList();
        var colourOf = auto.ToDictionary(t => t.Guide.Name, t => t.Colour);
        var autoNames = new[] { "Elemental Cycle", "Beast Deathrattle", "Pirate Discover" };
        var autoFrames = window.Highlights.Where(h => h.Kind != HighlightKind.None).Select(h => h.Target?.Guide.Name).Distinct().ToList();
        check("ticks scene: the automatic targets are a guide in progress and two guesses, Bob's cards framed for both of the first and the last",
            auto.Select(t => t.Guide.Name).SequenceEqual(autoNames) && auto.All(t => t.Kind == TargetKind.Probable)
            && autoFrames.Contains("Elemental Cycle") && autoFrames.Contains("Pirate Discover"),
            $"{Kinds(auto)}; frames for [{string.Join(", ", autoFrames)}]");

        // (Ali, 2026-10-07) With a guide ticked, Bob's frames come from the ticked guides alone: Elemental Cycle, in progress,
        // stays in the panel but frames nothing.
        var clicked = ClickBoxOf(window, "Pirate Discover");
        window.UpdateLayout();
        var one = window.Targets.ToList();
        var frames = window.Highlights.Where(h => h.Kind != HighlightKind.None).Select(h => h.Target?.Guide.Name).Distinct().ToList();
        var caption = window.Comps.ShownLines.TryGetValue(window.GuideOf("Elemental Cycle")!.Id, out var inProgressLine)
                      && Texts(inProgressLine).Any(t => t.IsVisible && Content(t) == "in progress");
        var captions = Texts(comps).Count(t => t.IsVisible && Content(t) == "in progress");
        check("ticking a guide: it comes first, the guide in progress stays in its colour and says so, the guess leaves; Bob's frames are the ticked guide's alone",
            clicked && one.Select(t => (t.Guide.Name, t.Kind)).SequenceEqual(new[] { ("Pirate Discover", TargetKind.Chosen), ("Elemental Cycle", TargetKind.InProgress) })
            && one.All(t => colourOf.TryGetValue(t.Guide.Name, out var c) && c == t.Colour) && caption && captions == 1
            && frames.SequenceEqual(new[] { "Pirate Discover" }) && FindText(comps, "1/3 chosen") != null,
            $"{Kinds(one)}; \"in progress\" under Elemental Cycle: {caption} ({captions} drawn); frames for [{string.Join(", ", frames)}]; "
            + $"title: {(FindText(comps, "1/3 chosen") != null ? "1/3 chosen" : "not 1/3 chosen")}");

        ClickBoxOf(window, "Undead Butcher");
        window.UpdateLayout();
        var two = window.Targets.ToList();
        check("ticking a second one adds it after the first, the guide in progress still after them",
            two.Select(t => (t.Guide.Name, t.Kind)).SequenceEqual(new[] { ("Pirate Discover", TargetKind.Chosen), ("Undead Butcher", TargetKind.Chosen), ("Elemental Cycle", TargetKind.InProgress) })
            && FindText(comps, "2/3 chosen") != null, Kinds(two));

        // − with two guides ticked of three wanted: two wanted, the two ticked guides alone in the list (the guide in progress
        // leaves), − dim there; a second − changes nothing; + gives three again and the whole list. One press at a time, on
        // the count itself: the log lines of a click reach the window's list later, through the dispatcher.
        var wanted = window.Count;
        var clickedMinus = Click(comps, "−");
        window.UpdateLayout();
        var afterMinus = window.Count;
        var listedAlone = window.Comps.ShownLines.Keys.OrderBy(k => k).SequenceEqual(window.Targets.Where(t => t.Ticked).Select(t => t.Guide.Id).OrderBy(k => k));
        var dim = FindText(comps, "−")?.Parent is Border { Opacity: < 1 } && FindText(comps, "2/2 chosen") != null;
        Click(comps, "−");
        window.UpdateLayout();
        var afterSecond = window.Count;
        var clickedPlus = Click(comps, "+");
        window.UpdateLayout();
        var whole = window.Comps.ShownLines.Count > 2;
        check("− and + with two guides ticked: − to two lists them alone and is dim there, + shows the others again",
            clickedMinus && clickedPlus && afterMinus == wanted - 1 && afterSecond == afterMinus && listedAlone && dim && window.Count == wanted && whole
            && window.Targets.Count == 3,
            $"count {wanted} -> {afterMinus} -> {afterSecond} -> {window.Count}, listed alone {listedAlone}, − dim and 2/2 {dim}, whole list again {whole}, {window.Targets.Count} targets");

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

        window.ResetLayout();
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
        window.SetMoveMode(false); // as in a game: the panel sized to N lines
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
        window.SetMoveMode(true);
        window.UpdateLayout();
    }

    /// <summary>
    /// One row of the power inset (BoardPowerView), read from what is drawn: for each level, the badge in the level's colour
    /// with its sign and percentage, the lit lamp at the level's place (red, yellow, green, gold) and only it glowing, in the
    /// level's halo colour (BoardPowerLevels.Halo: gold for shiny), the others dimmed without glow; the figures beside it;
    /// without data, a grey badge "–", no lamp lit, nothing glowing, no colour, and the reason written. Texts at least 12 px
    /// and uncut in the inset.
    /// </summary>
    private static void RowChecks(HarnessWindow window, Action<string, bool, string> check, string row, string what,
        IReadOnlyList<(string Scene, int Lit, string Badge, string Text)> scenes, Action<string> show)
    {
        var inset = window.Comps.Inset;
        var scale = TavernLayout.Scale(window.Overlay.ActualHeight);
        var grey = (Color)ColorConverter.ConvertFromString(BoardPowerLevels.Colour(BoardPower.None));
        var lampColours = new List<Color?>();
        foreach (var (scene, lit, badgeText, text) in scenes)
        {
            show(scene);
            window.UpdateLayout();
            var badge = Tagged(inset, BoardPowerView.BadgeTag(row)).OfType<Border>().FirstOrDefault();
            var lamps = Enumerable.Range(0, 4).Select(i => Tagged(inset, BoardPowerView.LampTag(row, i)).OfType<System.Windows.Shapes.Ellipse>().FirstOrDefault()).ToList();
            var badgeColour = (badge?.Background as SolidColorBrush)?.Color;
            var said = badge == null ? "none" : string.Join(" ", Texts(badge).Select(Content));
            var litNow = lamps.Select((l, i) => (l, i)).Where(x => x.l != null && Math.Abs(x.l.Opacity - 1) < 1e-9).Select(x => x.i).ToList();
            var glowing = lamps.Select((l, i) => (l, i)).Where(x => x.l?.Effect != null).Select(x => x.i).ToList();
            var glow = lit >= 0 ? (lamps[lit]?.Effect as System.Windows.Media.Effects.DropShadowEffect)?.Color : null;
            var colours = lamps.Select(l => (l?.Fill as SolidColorBrush)?.Color).ToList();
            if (scene == "even")
            {
                lampColours = colours;
            }

            var details = FindText(inset, text) != null;
            var texts = TextProblems(inset, scale);
            var dim = lamps.Where((l, i) => i != lit).All(l => l != null && Math.Abs(l.Opacity - BoardPowerView.OffOpacity) < 1e-9);
            var ok = badge != null && lamps.All(l => l != null) && said == badgeText && details && texts.Problems.Count == 0 && dim
                     && (lit >= 0
                         ? litNow.SequenceEqual(new[] { lit }) && glowing.SequenceEqual(new[] { lit })
                           && glow == (Color)ColorConverter.ConvertFromString(BoardPowerLevels.Halo(BoardPowerLevels.Gauge[lit])!)
                           && badgeColour == (Color)ColorConverter.ConvertFromString(BoardPowerLevels.Colour(BoardPowerLevels.Gauge[lit]))
                         : litNow.Count == 0 && glowing.Count == 0 && badgeColour == grey && colours.All(c => c == grey));
            check($"{what} \"{scene}\": badge \"{badgeText}\", lamp {(lit >= 0 ? lit + " lit and glowing in its level's colour, the others dim" : "none lit, none glowing, all grey")}, the figures beside it, nothing cut",
                ok, $"badge \"{said}\" {badgeColour}, lit [{string.Join(",", litNow)}], glowing [{string.Join(",", glowing)}] {glow}, lamps [{string.Join(" ", colours)}], "
                    + $"figures \"{text}\" {(details ? "drawn" : "missing")}, {texts.Checked} texts checked" + Problems(texts.Problems));
        }

        check($"{what}: the four lamps are four colours, red to gold, whatever the level",
            lampColours.Count == 4 && lampColours.Distinct().Count() == 4
            && lampColours.SequenceEqual(BoardPowerLevels.Gauge.Select(l => (Color?)(Color)ColorConverter.ConvertFromString(BoardPowerLevels.Colour(l)))),
            string.Join(" ", lampColours));
    }

    /// <summary>The player's row: their board against their hero's invented curve (120 at turn 8), HarnessData.Power.</summary>
    private static void PowerChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        RowChecks(window, check, BoardPowerView.Own, "board power", new (string, int, string, string)[]
        {
            ("behind", 0, "▼ −33%", "Board 80 · hero avg 120 at turn 8"),
            ("even", 1, "≈ +18%", "Board 142 · hero avg 120 at turn 8"),
            ("ahead", 2, "▲ +58%", "Board 190 · hero avg 120 at turn 8"),
            ("shiny", 3, "★ +117%", "Board 260 · hero avg 120 at turn 8"),
            ("none", -1, "–", "Board 142 · no curve for this hero"),
            ("early", -1, "–", "Board 3 · hero avg 6 at turn 2 · too early"),
        }, window.SetPower);
        window.SetPower("even");
        window.UpdateLayout();
    }

    /// <summary>
    /// (2026-10-06, "the same indicator for the enemy's composition when it shows") The opponent's row: their board against
    /// THEIR hero's invented curve (143 at turn 8, 50 at turn 5; the player's is 120 at turn 8, so a gauge on the wrong curve
    /// reads other figures), in combat; the next opponent's last board in the shop, against their average at the turn it was
    /// seen; grey and saying why without a curve or a board. One log line each time it changes, with the measure.
    /// </summary>
    private static void OpponentChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        static string Name(string id) => Hearthstone_Deck_Tracker.Hearthstone.Database.GetCardFromId(id)?.LocalizedName ?? id;
        FlushLog(window);
        var logStart = window.LogLines.Count;
        RowChecks(window, check, BoardPowerView.Opponent, "opponent power", new (string, int, string, string)[]
        {
            ("behind", 0, "▼ −37%", "Opp. 90 · their hero avg 143 at turn 8"),
            ("even", 1, "≈ +12%", "Opp. 160 · their hero avg 143 at turn 8"),
            ("ahead", 2, "▲ +47%", "Opp. 210 · their hero avg 143 at turn 8"),
            ("shiny", 3, "★ +110%", "Opp. 300 · their hero avg 143 at turn 8"),
            ("none", -1, "–", $"Opp. 160 · no curve for {Name(HarnessData.UnchartedHero)}"),
            ("next", 2, "▲ +40%", "Next opp. 70 at turn 5 · their hero avg 50"),
            ("unseen", -1, "–", $"Next opp. {Name(HarnessData.OpponentHero)} – not fought yet"),
        }, window.SetOpponentPower);

        FlushLog(window);
        var lines = window.LogLines.Skip(logStart).Where(l => l.Contains("Bronzebeard HUD: opponent power ")).ToList();
        var fight = lines.FirstOrDefault(l => l.Contains("scope=combat") && l.Contains("board=210"));
        check("opponent power: one log line per change, naming their hero and yours, the turn seen, the board and the average it rests on",
            lines.Count == 7 && fight != null && fight.Contains($"hero={HarnessData.OpponentHero} (yours {HarnessData.Hero}) turn=8 seen=8 board=210 (3 minions)")
            && fight.Contains("their hero avg 143 at turn 8 · +47% power=ahead") && lines.Any(l => l.Contains("scope=next") && l.Contains("seen=5 board=70")),
            $"{lines.Count} lines; " + string.Join(" | ", lines.Take(3).Select(l => l.Substring(l.IndexOf("opponent power", StringComparison.Ordinal)))));

        window.SetOpponentPower("even");
        window.UpdateLayout();
    }

    /// <summary>
    /// (2026-10-06, "the indicator out into a small inset under the main frame, framed by the + and −") The inset: out of the
    /// frame (no lamp of either row inside it), under it, as wide as the panel; − at its left and + at its right, centred on
    /// it; the player's row above the opponent's between them; its texts at least 12 px and uncut.
    /// </summary>
    private static void InsetChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        var comps = window.Comps;
        var panel = comps.Element;
        var scale = TavernLayout.Scale(window.Overlay.ActualHeight);
        window.UpdateLayout();
        var frame = RectIn(comps.Frame, panel);
        var inset = RectIn(comps.Inset, panel);
        var inFrame = Tagged(comps.Frame, BoardPowerView.LampTag(BoardPowerView.Own, 0)).Count() + Tagged(comps.Frame, BoardPowerView.LampTag(BoardPowerView.Opponent, 0)).Count();
        check("inset: out of the frame, under it, as wide as the panel",
            comps.Inset.IsVisible && inFrame == 0 && inset.Top >= frame.Bottom + 0.5 && inset.Top - frame.Bottom <= 6 * scale
            && Math.Abs(inset.Width - panel.ActualWidth) < 0.5 && Math.Abs(inset.Bottom - panel.ActualHeight) < 0.5,
            $"frame {Describe(frame)}, inset {Describe(inset)}, panel {panel.ActualWidth:0}x{panel.ActualHeight:0}, lamps inside the frame: {inFrame}");

        var minus = Tagged(comps.Inset, CompsPanel.MinusTag).FirstOrDefault();
        var plus = Tagged(comps.Inset, CompsPanel.PlusTag).FirstOrDefault();
        var own = Tagged(comps.Inset, BoardPowerView.LampTag(BoardPowerView.Own, 0)).FirstOrDefault();
        var theirs = Tagged(comps.Inset, BoardPowerView.LampTag(BoardPowerView.Opponent, 3)).FirstOrDefault();
        var ownBadge = Tagged(comps.Inset, BoardPowerView.BadgeTag(BoardPowerView.Own)).FirstOrDefault();
        var theirBadge = Tagged(comps.Inset, BoardPowerView.BadgeTag(BoardPowerView.Opponent)).FirstOrDefault();
        var ok = minus != null && plus != null && own != null && theirs != null && ownBadge != null && theirBadge != null;
        if (ok)
        {
            var m = RectIn(minus!, comps.Inset);
            var p = RectIn(plus!, comps.Inset);
            var first = RectIn(own!, comps.Inset);
            var last = RectIn(theirs!, comps.Inset);
            var centre = comps.Inset.ActualHeight / 2;
            ok = m.Right <= first.Left && p.Left >= last.Right && m.Left < 12 * scale && comps.Inset.ActualWidth - p.Right < 12 * scale
                 && Math.Abs((m.Top + m.Bottom) / 2 - centre) < 1 && Math.Abs((p.Top + p.Bottom) / 2 - centre) < 1
                 && RectIn(ownBadge!, comps.Inset).Bottom <= RectIn(theirBadge!, comps.Inset).Top + 0.5;
            check("inset: − at its left, + at its right, centred on it; the player's row above the opponent's between them", ok,
                $"− {Describe(m)}, + {Describe(p)}, first lamp {Describe(first)}, last lamp of the opponent {Describe(last)}, inset {comps.Inset.ActualWidth:0}x{comps.Inset.ActualHeight:0}");
        }
        else
        {
            check("inset: − at its left, + at its right, centred on it; the player's row above the opponent's between them", false,
                $"found: − {minus != null}, + {plus != null}, own lamp {own != null}, opponent lamp {theirs != null}, badges {ownBadge != null}/{theirBadge != null}");
        }

        var texts = TextProblems(comps.Inset, scale);
        check("inset: no text under 12 px, none cut", texts.Checked >= 6 && texts.Problems.Count == 0, $"{texts.Checked} texts checked" + Problems(texts.Problems));
    }

    /// <summary>
    /// (2026-10-06, "a press on + or − resizes the window to show the N best compositions") Move mode off, in the default
    /// place: N from 1 to 4 and back, three times, by clicking − and + as the mouse would. Each N shows N lines — the targets
    /// first — or, when N lines do not fit between the boards and the gold, as many as fit in all that room; never on a zone
    /// of the game, never off the overlay; the same N lands on the same rectangle every time (no creeping); one log line per
    /// press, its figures those drawn; + at 4 still logs its resize. Then against the bottom of the screen it keeps its bottom
    /// and grows upwards; a box given by the handle stays until + is pressed, a new game returns to it, "Reset" to the default.
    /// </summary>
    private static void ResizeChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        var canvas = window.Overlay;
        var comps = window.Comps;
        var panel = comps.Element;
        var height = canvas.ActualHeight;
        var boards = TavernLayout.PlayerRowBottom(height);
        var gold = PanelFit.BottomLimit * height;
        window.SetMoveMode(false);
        window.UpdateLayout();
        FlushLog(window);
        var logStart = window.LogLines.Count;

        var seen = new List<(int N, Rect Rect, int Lines)>();
        var problems = new List<string>();
        var clicks = 0;
        foreach (var n in new[] { 1, 2, 3, 4, 3, 2, 1, 2, 3, 4 })
        {
            clicks += Math.Abs(window.Count - n);
            window.ClickCountTo(n);
            window.UpdateLayout();
            var rect = RectOf(panel);
            var lines = comps.ShownLines.Count;
            var ranks = window.Targets.Where(t => comps.ShownLines.ContainsKey(t.Guide.Id)).Select(t => t.Rank).OrderBy(r => r).ToList();
            var targetsShown = ranks.SequenceEqual(Enumerable.Range(1, ranks.Count)) ? ranks.Count : -1; // the best ranks, none skipped
            seen.Add((n, rect, lines));
            var zones = NoGoZones.For(canvas.ActualWidth, height).Where(z => Overlap(rect, RectOf(z.Rect))).Select(z => z.Name).ToList();
            // Fewer lines than N: the next one did not fit between the boards and the gold, so the panel grew up to the
            // boards and kept its bottom on the gold, then fitted what it shows.
            var outOfRoom = Math.Abs(rect.Bottom - gold) < 1 && rect.Top >= boards - 0.5 && comps.Span is { Anchor: PanelAnchor.Bottom };
            if (!(lines == n || (lines < n && lines >= 1 && outOfRoom)) || targetsShown != Math.Min(window.Targets.Count, lines)
                || zones.Count > 0 || rect.Top < -0.5 || rect.Bottom > height + 0.5)
            {
                problems.Add($"N={n}: {lines} lines ({targetsShown} of the {window.Targets.Count} targets) at {Describe(rect)}, zones [{string.Join(",", zones)}]");
            }
        }

        var steady = seen.GroupBy(s => s.N).All(g => g.Select(s => s.Rect).Distinct().Count() == 1);
        var growing = Enumerable.Range(1, 3).All(n => seen.First(s => s.N == n + 1).Rect.Height > seen.First(s => s.N == n).Rect.Height
                                                      || seen.First(s => s.N == n + 1).Lines == seen.First(s => s.N == n).Lines);
        check("+ / −: N = 1 to 4 and back, three times: N lines, the best targets first (or, out of room, its bottom on the gold), on no zone, each N on the same rectangle",
            problems.Count == 0 && steady && growing,
            string.Join("; ", seen.Take(4).Select(s => $"N={s.N}: {s.Lines} lines {Describe(s.Rect)}")) + $"; steady {steady}, growing {growing}"
            + (problems.Count > 0 ? " | " + string.Join(" | ", problems) : string.Empty));

        // + at 4: the number stays, the panel is sized again all the same, and says so.
        window.ClickCount(+1);
        window.UpdateLayout();
        clicks++;
        FlushLog(window);
        var resizes = window.LogLines.Skip(logStart).Where(l => l.Contains("Bronzebeard HUD: targets n=")).ToList();
        var last = resizes.LastOrDefault() ?? string.Empty;
        var r = RectOf(panel);
        var said = $"targets n=4 panel resized to ({r.Left:0},{r.Top:0} {r.Width:0}x{r.Height:0}) anchor=";
        check("+ / −: one log line per press, its rectangle and lines those drawn (+ at 4 included)",
            resizes.Count == clicks && last.Contains(said) && last.Contains($"lines={comps.ShownLines.Count}/4 shown") && window.Count == 4,
            $"{resizes.Count} lines for {clicks} presses; last: {(last.Length > 0 ? last.Substring(last.IndexOf("targets", StringComparison.Ordinal)) : "none")}; drawn {Describe(r)}");

        // Against the bottom of the screen: it keeps its bottom, growing upwards.
        var defaultBox = TavernLayout.TargetPanel(canvas.ActualWidth, height);
        window.DropPanel(defaultBox.Left, height - defaultBox.Height);
        window.ClickCountTo(1);
        window.UpdateLayout();
        var low = RectOf(panel);
        window.ClickCountTo(3);
        window.UpdateLayout();
        var high = RectOf(panel);
        check("+ / −: a panel against the bottom of the screen keeps its bottom and grows upwards, never onto the boards",
            Math.Abs(low.Bottom - height) < 1 && Math.Abs(high.Bottom - height) < 1 && high.Top < low.Top - 1 && high.Top >= boards - 0.5
            && comps.Span is { Anchor: PanelAnchor.Bottom },
            $"N=1 {Describe(low)}, N=3 {Describe(high)}, anchor {comps.Span?.Anchor}");

        // A box given by the handle: shown as is until + or − is pressed; a new game goes back to it; "Reset" to the default.
        var chosen = 330 * TavernLayout.Scale(height);
        window.ResetLayout();
        window.SetMoveMode(true);
        window.ResizePanel(defaultBox.Right, defaultBox.Top + chosen);
        var wasResized = window.PanelResized;
        window.SetMoveMode(false);
        window.UpdateLayout();
        var box = RectOf(panel);
        window.ClickCount(-1);
        window.UpdateLayout();
        var fitted = RectOf(panel);
        window.SetScenario(HarnessData.DefaultScenario); // a new game
        window.UpdateLayout();
        var nextGame = RectOf(panel);
        var nextGameFits = comps.FitsContent;
        window.ResetLayout();
        window.UpdateLayout();
        var reset = RectOf(panel);
        check("+ / −: a box given by the handle stays until + or − is pressed; a new game goes back to it; \"Reset\" to the default place, sized to its content",
            // The box: its content up to the size chosen ("the box fits its content up to the chosen size", 2026-10-04), so
            // its height follows the targets; what tells it from the panel sized by − is that it is not sized to N lines.
            wasResized && !window.PanelResized && box.Height <= chosen + 0.5 && Math.Abs(box.Top - defaultBox.Top) < 0.5
            && Math.Abs(fitted.Height - box.Height) > 1 && Math.Abs(fitted.Top - box.Top) < 0.5
            && !nextGameFits && nextGame.Height <= chosen + 0.5 && Math.Abs(nextGame.Top - box.Top) < 0.5 && Math.Abs(nextGame.Height - fitted.Height) > 1
            && Math.Abs(reset.Left - defaultBox.Left) < 0.5 && Math.Abs(reset.Top - defaultBox.Top) < 0.5 && comps.FitsContent,
            $"box {Describe(box)} (resized {wasResized}), after − {Describe(fitted)}, next game {Describe(nextGame)} (sized to N lines {nextGameFits}), "
            + $"after Reset {Describe(reset)} (default top {defaultBox.Top:0}), resized {window.PanelResized}");

        window.ClickCountTo(HudSettings.DefaultSuggested);
        window.SetMoveMode(true);
        window.UpdateLayout();
    }

    /// <summary>
    /// The opponent's row switched off by its guard (drawing it throws): the player's row, the inset's buttons and the panel
    /// stay; the log says it, once. Its guard then back on, as a new session would have it.
    /// </summary>
    private static void GuardChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        var comps = window.Comps;
        FlushLog(window);
        var logStart = window.LogLines.Count;
        window.BreakOpponentRow = true;
        window.SetOpponentPower("ahead");
        window.UpdateLayout();
        var theirs = Tagged(comps.Inset, BoardPowerView.LampTag(BoardPowerView.Opponent, 0)).Count();
        var own = Tagged(comps.Inset, BoardPowerView.LampTag(BoardPowerView.Own, 0)).Count();
        var buttons = Tagged(comps.Inset, CompsPanel.MinusTag).Count() + Tagged(comps.Inset, CompsPanel.PlusTag).Count();
        FlushLog(window);
        var said = window.LogLines.Skip(logStart).Count(l => l.Contains("feature \"opponent-power\" disabled"));
        check("guard: the opponent's row that throws is left out alone; the player's row, − and + and the panel stay",
            theirs == 0 && own == 1 && buttons == 2 && comps.Element.IsVisible && said == 1,
            $"opponent lamps {theirs}, player's {own}, buttons {buttons}, panel visible {comps.Element.IsVisible}, {said} log line(s)");

        window.BreakOpponentRow = false;
        window.ResetGuards();
        window.SetOpponentPower("even");
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
                // A trinket nomi.gg contests shows both figures in place of its placement (component 9, HarnessData.TrinketView).
                var contested = said.Count(s => s.Count == 2 && s[0].EndsWith(" contested", StringComparison.Ordinal) && s[1] == "nomi.gg ↔ FS");
                check($"{name}: every trinket shows its placement or a contest, one adjusted for a target's tribe, one contested",
                    said.Count > 0 && said.All(s => s.Count > 0 && (s[0].StartsWith("avg ", StringComparison.Ordinal) || s[0].EndsWith(" contested", StringComparison.Ordinal)))
                    && adjusted >= 1 && contested == 1,
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
    /// nothing (Mech Divine Shield). Under it, on a line of its own, the guide's tribe since the patch (component 7, from
    /// HarnessData.Nomi: pirates 4.07 → 3.70 over 300 and 900 games, 2σ = 0.31; mechs 3.96 → 4.30 over 350 and 800, 2σ = 0.29),
    /// which the guide bridged to nothing shows alone.
    /// </summary>
    private static void ContextChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        const string expected = "≈ 3,5 with your hero (23) · final turn ≈ 13 · 5 top boards\nPirate ▲ 4.07 → 3.70 since 36.6.3 (nomi.gg, 900 games)";
        const string expectedAlone = "Mech ▼ 3.96 → 4.30 since 36.6.3 (nomi.gg, 800 games)";
        var scale = TavernLayout.Scale(window.Overlay.ActualHeight);
        var comps = window.Comps;
        var popup = comps.Popup;
        window.SetMoveMode(false);
        window.ShowSkipCombat(false);
        window.CursorInside = _ => false;
        window.UpdateLayout();

        static TextBlock? ContextIn(DependencyObject root) => Texts(root).FirstOrDefault(t => t.IsVisible && (Content(t).Contains(" top board") || Content(t).Contains(" since 36.6.3")));

        string LookAt(DependencyObject root, TextBlock? context, string expected)
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

        (string Detail, string Popup) Look(string name, string expected)
        {
            var guide = window.GuideOf(name)!;
            if (!comps.ShownLines.ContainsKey(guide.Id))
            {
                throw new InvalidOperationException($"{name} not in the list: lines [{string.Join(", ", comps.ShownLines.Keys)}], panel {Describe(RectOf(comps.Element))}, "
                                                    + $"fits {comps.FitsContent}, count {window.Count}, targets {CompTargets.Summary(window.Targets)}, detail {comps.ShowsDetail}");
            }

            Click(comps.Element, guide.Name);
            window.UpdateLayout();
            var detail = comps.ShowsDetail ? LookAt(comps.Element, ContextIn(comps.Element), expected) : "detail not opened";
            Click(comps.Element, "← All comp guides");
            window.UpdateLayout();
            var line = comps.ShownLines[guide.Id];
            Raise(line, UIElement.MouseEnterEvent);
            Headless.Pump(400);
            window.UpdateLayout();
            var shown = popup.IsVisible ? LookAt(popup.Element, ContextIn(popup.Element), expected) : "popup not shown";
            Raise(line, UIElement.MouseLeaveEvent);
            return (detail, shown);
        }

        var bridged = Look("Pirate Discover", expected);
        var unbridged = Look("Mech Divine Shield", expectedAlone);
        check("context line: under the header of a guide's detail and popup (small, muted), Firestone's for a bridged guide, then its tribe since the patch",
            bridged.Detail.StartsWith("ok ", StringComparison.Ordinal) && bridged.Popup.StartsWith("ok ", StringComparison.Ordinal)
            && unbridged.Detail.StartsWith("ok ", StringComparison.Ordinal) && unbridged.Popup.StartsWith("ok ", StringComparison.Ordinal),
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
    /// (d) A choice opened in the shop takes off the screen Bob's frames, labels and ◇ buttons; the panel and its inset stay
    /// where they are, the same elements (Ali, 2026-10-08: "the panel simply always visible"); one log line when it opens,
    /// none for the next updates of the same choice or of another; closing it puts back the same targets and the same markers
    /// (same places, colours and texts), one log line, and a hovered line shows its popup. A Dark Gift first: its ◇ fell
    /// inside the options. Also measured, for the record: how much of each option the panel covers at its place.
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
        var contentBefore = comps.Content; // the frame's content: a new element at every redraw (the panel's own child never changes)
        var insetBefore = comps.Inset.IsVisible;
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
        var panelDuring = comps.Element.IsVisible && RectOf(comps.Element) == panelBefore && ReferenceEquals(comps.Content, contentBefore);
        var insetDuring = comps.Inset.IsVisible;
        Raise(line, UIElement.MouseLeaveEvent);
        window.ShowChoice(ChoiceKind.DarkGift); // the next updates of the same choice
        window.ShowChoice(ChoiceKind.Discover); // then another kind, still open
        window.UpdateLayout();
        var markersStill = MarkerSignatures(window);
        var panelStill = comps.Element.IsVisible && RectOf(comps.Element) == panelBefore;
        check("choice open: Bob's frames, labels and ◇ leave the screen; the panel and its power inset stay, the same elements",
            popupBefore && pinsBefore > 0 && markersBefore.Count > pinsBefore && markersDuring.Count == 0 && markersStill.Count == 0
            && panelDuring && panelStill && insetBefore && insetDuring,
            $"before: {markersBefore.Count} marker elements ({pinsBefore} ◇), panel shown, inset {insetBefore}, popup {popupBefore}; Dark Gift open: {markersDuring.Count} marker elements"
            + $"{(markersDuring.Count > 0 ? " (" + string.Join(" | ", markersDuring.Take(3)) + ")" : string.Empty)}, panel kept {panelDuring}, inset {insetDuring}; "
            + $"discover after it: {markersStill.Count}, panel kept {panelStill}; {measure}");

        window.ShowChoice(ChoiceKind.None);
        window.UpdateLayout();
        var markersAfter = MarkerSignatures(window);
        var lost = markersBefore.Except(markersAfter).ToList();
        var added = markersAfter.Except(markersBefore).ToList();
        var sameContent = ReferenceEquals(comps.Content, contentBefore);
        check("choice closed: the same targets, frames, labels and ◇ as before, the panel and its inset back as they were (the same elements)",
            markersAfter.SequenceEqual(markersBefore) && CompTargets.Summary(window.Targets) == targetsBefore && ReferenceEquals(window.Highlights, highlightsBefore)
            && comps.Element.IsVisible && comps.Inset.IsVisible && RectOf(comps.Element) == panelBefore && sameContent,
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
        check("after the choice, a hovered line shows its popup", popupAgain, $"popup visible {popupAgain}");
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

    /// <summary>
    /// HDT's overlay layer as the harness reproduces it (HdtOverlay), driven by an injected mouse: the probe runs on its own
    /// timer and raises MouseEnter and MouseLeave from the cursor's place alone; the window catches the mouse only over the
    /// elements declared clickable (everywhere else a click is the game's), and WPF, there only, raises its own events. Move
    /// mode off unless said, the Skip combat button hidden, the cursor the injected one (CursorInside null). Each check reads
    /// what the layer raised (its list of events) as well as what the plugin did: "nothing changed" after a second "enter" or
    /// a stray "leave" proves something only if that event was raised. Last of the groups: a click in move mode drops the
    /// panel where it was, and "Reset" forgets that place again at the end.
    /// </summary>
    private static void MouseChecks(HarnessWindow window, Action<string, bool, string> check)
    {
        var canvas = window.Overlay;
        var hdt = window.Hdt;
        var comps = window.Comps;
        var popup = comps.Popup;
        window.SetMoveMode(false);
        window.ShowSkipCombat(false);
        window.CursorInside = null;
        window.UpdateLayout();
        hdt.Start();
        try
        {
            var outside = new Point(1, 1);
            hdt.Move(outside);
            Headless.Pump(500);
            check("mouse: HDT's probe runs on its own timer (UpdateHoverable, then Task.Delay(16))", hdt.Ticks >= 5,
                $"{hdt.Ticks} runs in {hdt.Elapsed} ms: one every {(hdt.Ticks > 0 ? hdt.Elapsed / (double)hdt.Ticks : 0):0} ms");

            // A click off move mode on the panel's title: no element declared clickable there, the window is click-through.
            var title = window.MousePoint("title");
            hdt.Move(title);
            Headless.Pump(60);
            hdt.Move(new Point(title.X + 1, title.Y));
            Headless.Pump(60);
            var declaredThere = hdt.ClickablesAt(hdt.Cursor!.Value);
            var declared = declaredThere.Count;
            FlushLog(window);
            var logStart = window.LogLines.Count;
            var offClick = window.MouseClick();
            FlushLog(window);
            var movedOff = window.LogLines.Skip(logStart).Count(l => l.Contains("panel moved"));
            check("mouse: off move mode, a click on the panel's title goes through to the game; nothing in the overlay takes it",
                declared == 0 && hdt.ClickThrough && offClick.ToGame && movedOff == 0,
                $"{offClick}; {declared} element(s) declared clickable there{(declared > 0 ? " (" + string.Join(", ", declaredThere.Select(HdtOverlay.Describe)) + ")" : string.Empty)}; {movedOff} \"panel moved\" line(s)");

            // In move mode PanelMover declares the whole panel clickable: the same click is the panel's (a drag, dropped in place).
            // Its place is read in move mode, where it shows its box rather than its content.
            window.SetMoveMode(true);
            window.UpdateLayout();
            Headless.Pump(60);
            var place = new Point(Canvas.GetLeft(comps.Element), Canvas.GetTop(comps.Element));
            hdt.Move(title);
            Headless.Pump(60);
            var declaredInMove = hdt.ClickablesAt(title);
            FlushLog(window);
            logStart = window.LogLines.Count;
            var onClick = window.MouseClick();
            FlushLog(window);
            var moved = window.LogLines.Skip(logStart).Where(l => l.Contains("panel moved")).ToList();
            var after = new Point(Canvas.GetLeft(comps.Element), Canvas.GetTop(comps.Element));
            check("mouse: in move mode, the same click reaches the panel, declared clickable as a whole: PanelMover takes it (a drag, dropped where it was)",
                declaredInMove.Count == 1 && ReferenceEquals(declaredInMove[0], comps.Element)
                && !hdt.ClickThrough && !onClick.ToGame && onClick.Target != null && IsWithin(onClick.Target, comps.Element)
                && moved.Count == 1 && moved[0].Contains($"panel moved {CompsPanel.PanelId} to (") && after == place,
                $"{onClick}; declared clickable there: [{string.Join(", ", declaredInMove.Select(HdtOverlay.Describe))}]; "
                + $"{string.Join(" | ", moved.Select(l => l.Substring(l.IndexOf(">>", StringComparison.Ordinal) + 3)))}; panel at ({after.X:0},{after.Y:0}), was ({place.X:0},{place.Y:0})");
            window.SetMoveMode(false);
            window.UpdateLayout();
            Headless.Pump(60);

            // A ◇ is declared clickable: the window catches the mouse over it alone; Bob's card under it stays the game's.
            var pins = window.PinButtons();
            var pin = window.MousePoint("pin:1");
            hdt.Move(pin);
            Headless.Pump(60);
            hdt.Move(new Point(pin.X + 1, pin.Y));
            Headless.Pump(60);
            FlushLog(window);
            logStart = window.LogLines.Count;
            var pinClick = window.MouseClick();
            var slot = TavernLayout.CardSlots(canvas.ActualWidth, canvas.ActualHeight, HarnessData.Shop.Count)[0];
            var card = new Point(Math.Round(slot.Left + slot.Width / 2), Math.Round(slot.Top + slot.Height / 2));
            hdt.Move(card);
            Headless.Pump(60);
            var cardClick = window.MouseClick();
            FlushLog(window);
            var pinned = window.LogLines.Skip(logStart).Where(l => l.Contains("pin toggled: ")).ToList();
            check("mouse: off move mode, a click on a ◇ (declared clickable) pins its card; a click on Bob's card under it goes to the game",
                pins.Count > 0 && !pinClick.ToGame && pinned.Count == 1 && pinned[0].EndsWith("pin toggled: " + HarnessData.Shop[0], StringComparison.Ordinal) && cardClick.ToGame,
                $"{pins.Count} ◇; {pinClick}; {cardClick}; {pinned.Count} \"pin toggled\" line(s)");

            // The popup of a target's line, as the probe and WPF drive it.
            var target = window.Targets.First();
            var guide = target.Guide;
            var rank = target.Rank.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var line = comps.ShownLines[guide.Id];
            var background = window.MousePoint("line:" + rank);
            var name = window.MousePoint("name:" + rank);
            hdt.Move(outside);
            Headless.Pump(60);

            var shows = popup.Shows;
            var mark = hdt.Events.Count;
            hdt.Move(background);
            Headless.Pump(100);
            var early = popup.IsVisible;
            Headless.Pump(300);
            var onLine = EventsOn(hdt, mark, line);
            check("mouse: the probe enters a line from its background (the window stays click-through): one probe MouseEnter, no WPF event; no popup 100 ms later, the popup by 400 ms, shown once",
                hdt.ClickThrough && onLine.Count == 1 && onLine[0].Source == OverlaySource.Probe && onLine[0].Enter && !early
                && popup.IsVisible && popup.ShownGuide == guide.Id && popup.Shows == shows + 1,
                $"\"{guide.Name}\" at ({background.X:0},{background.Y:0}): [{string.Join("; ", onLine)}]; visible after 100 ms {early}, after 400 ms {popup.IsVisible}; shown {popup.Shows - shows} time(s)");

            shows = popup.Shows;
            mark = hdt.Events.Count;
            hdt.Move(name);
            Headless.Pump(60); // the probe: the name is declared clickable, the window catches the mouse
            var catches = !hdt.ClickThrough;
            hdt.Move(new Point(name.X + 1, name.Y)); // the hand moves on: WPF sees it now, and enters the name and the line under it
            var keptAtOnce = popup.IsVisible;
            Headless.Pump(350);
            onLine = EventsOn(hdt, mark, line);
            check("mouse: onto the line's name (declared clickable), the window catches the mouse and WPF raises a second MouseEnter on the line: the popup stays, not shown again",
                catches && onLine.Count == 1 && onLine[0].Source == OverlaySource.Wpf && onLine[0].Enter && keptAtOnce && popup.IsVisible && popup.Shows == shows,
                $"name at ({name.X:0},{name.Y:0}), window catches the mouse {catches}: [{string.Join("; ", onLine)}]; popup visible at once {keptAtOnce}, 350 ms later {popup.IsVisible}; shown {popup.Shows - shows} more time(s)");

            mark = hdt.Events.Count;
            hdt.Move(background); // the window still catches the mouse: WPF hit-tests the background, leaves the name, keeps the line
            Headless.Pump(60); // the probe: nothing declared clickable here, the window is click-through again
            var through = hdt.ClickThrough;
            hdt.Move(new Point(background.X + 1, background.Y)); // that move goes to the game: WPF loses the mouse
            Headless.Pump(60);
            onLine = EventsOn(hdt, mark, line);
            check("mouse: back on the line's background, the window turns click-through and WPF's MouseLeave reaches the line with the cursor still in it: the popup stays",
                through && onLine.Count == 1 && onLine[0].Source == OverlaySource.Wpf && !onLine[0].Enter && onLine[0].CursorInside && popup.IsVisible && popup.ShownGuide == guide.Id,
                $"click-through {through}: [{string.Join("; ", onLine)}]; popup visible {popup.IsVisible}");

            var ovals = HarnessWindow.Ovals(line);
            var popupRect = RectOf(popup.Element);
            var oval = ovals.Count > 0 ? hdt.CentreOf(ovals[0]) : background;
            mark = hdt.Events.Count;
            hdt.Move(oval);
            Headless.Pump(60);
            hdt.Move(new Point(oval.X + 1, oval.Y));
            Headless.Pump(60);
            window.UpdateLayout();
            var preview = HdtTooltip.ShowingRect(canvas);
            var onOval = ovals.Count > 0 ? EventsOn(hdt, mark, ovals[0]) : new List<OverlayEvent>();
            check("mouse: on an oval of the line, HDT's tooltip shows its card (the probe enters the oval, then WPF once the window catches the mouse); the popup stays, neither covering the other",
                ovals.Count > 0 && preview is { Width: > 0 } shown && onOval.Count(e => e.Source == OverlaySource.Probe && e.Enter) == 1
                && onOval.Count(e => e.Source == OverlaySource.Wpf && e.Enter) == 1 && popup.IsVisible && !Overlap(shown, popupRect),
                $"{ovals.Count} ovals: [{string.Join("; ", onOval)}]; card {(preview is { } q ? Describe(q) : "none")}, popup {Describe(popupRect)} visible {popup.IsVisible}");

            mark = hdt.Events.Count;
            hdt.Move(background);
            Headless.Pump(60);
            hdt.Move(new Point(background.X + 1, background.Y));
            Headless.Pump(60);
            onOval = ovals.Count > 0 ? EventsOn(hdt, mark, ovals[0]) : new List<OverlayEvent>();
            check("mouse: off the oval, the probe's MouseLeave takes the card away; the popup stays",
                HdtTooltip.Showing(canvas) == null && onOval.Any(e => e.Source == OverlaySource.Probe && !e.Enter) && popup.IsVisible,
                $"[{string.Join("; ", onOval)}]; card still shown {HdtTooltip.Showing(canvas) != null}, popup visible {popup.IsVisible}");

            // A game update redraws the panel under a still cursor: new lines, the old ones gone.
            var before = comps.ShownLines[guide.Id];
            shows = popup.Shows;
            mark = hdt.Events.Count;
            var entries = hdt.ClickableEntries;
            window.SetPower(window.PowerScene);
            window.UpdateLayout();
            Headless.Pump(150);
            var rebuilt = comps.ShownLines[guide.Id];
            var leftOld = EventsOn(hdt, mark, before).Where(e => e.Source == OverlaySource.Probe && !e.Enter).ToList();
            var enteredNew = EventsOn(hdt, mark, rebuilt).Where(e => e.Source == OverlaySource.Probe && e.Enter).ToList();
            check("mouse: the panel redrawn under a still cursor, the probe leaves the old line and enters the new one: the popup stays, not shown again",
                !ReferenceEquals(before, rebuilt) && leftOld.Count == 1 && enteredNew.Count == 1 && popup.IsVisible && popup.Shows == shows,
                $"line rebuilt {!ReferenceEquals(before, rebuilt)}: old [{string.Join("; ", leftOld)}], new [{string.Join("; ", enteredNew)}]; popup visible {popup.IsVisible}, shown {popup.Shows - shows} more time(s); "
                + $"HDT's list of clickables: {entries} entries before the redraw, {hdt.ClickableEntries} after, for {hdt.LiveClickables} elements still loaded");

            // HDT puts a clickable in its List when it is declared and again at its Loaded, and takes it out once at its
            // Unloaded (issue #17): an element declared before it is loaded leaves a dead entry at every redraw (measured on
            // 2026-10-10 before the fix: 4146, 4184, 4222, 4260 entries for 29 loaded elements). Three more redraws of the
            // same scene (two check the change, the third that the state does not trap itself): every entry must be a
            // loaded element, listed once, and each redraw must really rebuild the line.
            var counts = new List<(int Entries, int Loaded, bool Rebuilt)> { (hdt.ClickableEntries, hdt.LiveClickables, true) };
            for (var redraw = 0; redraw < 3; redraw++)
            {
                var previous = comps.ShownLines[guide.Id];
                window.SetPower(window.PowerScene);
                window.UpdateLayout();
                Headless.Pump(150);
                counts.Add((hdt.ClickableEntries, hdt.LiveClickables, !ReferenceEquals(previous, comps.ShownLines[guide.Id])));
            }

            var dead = counts.Select(c => c.Entries - c.Loaded).ToList();
            check("mouse: before and after three more redraws of the panel, HDT's list of clickables holds each loaded clickable once and no dead entry (an element is declared clickable once loaded)",
                counts.All(c => c.Rebuilt && c.Loaded > 0) && dead.All(d => d == 0),
                $"entries / loaded elements / dead entries, before and after each redraw: {string.Join(" → ", counts.Select((c, i) => $"{c.Entries}/{c.Loaded}/{dead[i]}"))}; "
                + $"line rebuilt at each redraw {counts.All(c => c.Rebuilt)}");

            line = comps.ShownLines[guide.Id];
            mark = hdt.Events.Count;
            hdt.Move(outside);
            Headless.Pump(80);
            onLine = EventsOn(hdt, mark, line);
            check("mouse: out of the line, the probe's MouseLeave hides the popup",
                onLine.Count == 1 && onLine[0].Source == OverlaySource.Probe && !onLine[0].Enter && !onLine[0].CursorInside && !popup.IsVisible,
                $"[{string.Join("; ", onLine)}]; popup visible {popup.IsVisible}");

            // The same element taken off the overlay and put back three times (issue #17): declared by OverlayClickable, HDT
            // lists it once while it is on the overlay and drops it when it leaves; one taken away before it was ever loaded
            // (added and removed within one dispatcher turn) is never listed. Away from the cursor, which is at (1,1).
            var entriesBefore = hdt.ClickableEntries;
            var kept = new Border { Width = 10, Height = 10, Background = Brushes.Transparent };
            Canvas.SetLeft(kept, 300);
            Canvas.SetTop(kept, 5);
            OverlayClickable.Declare(kept);
            var listed = new List<(int On, int Off)>();
            for (var round = 0; round < 3; round++)
            {
                canvas.Children.Add(kept);
                Headless.Pump(50);
                var on = hdt.ClickableEntries - entriesBefore;
                canvas.Children.Remove(kept);
                Headless.Pump(50);
                listed.Add((on, hdt.ClickableEntries - entriesBefore));
            }

            var fleeting = new Border { Width = 10, Height = 10, Background = Brushes.Transparent };
            OverlayClickable.Declare(fleeting);
            canvas.Children.Add(fleeting);
            canvas.Children.Remove(fleeting);
            Headless.Pump(50);
            var fleetingListed = hdt.ClickableEntries - entriesBefore;
            check("mouse: an element declared clickable by OverlayClickable is in HDT's list once while on the overlay and not after, through three removals and re-adds; one removed before it was loaded is never in it",
                listed.All(l => l == (1, 0)) && fleetingListed == 0 && Hearthstone_Deck_Tracker.Utility.Extensions.OverlayExtensions.GetIsOverlayHitTestVisible(kept),
                $"entries it adds on the overlay / after its removal: {string.Join(", ", listed.Select(l => $"{l.On}/{l.Off}"))}; removed before it was loaded: {fleetingListed} entries");
        }
        finally
        {
            hdt.Stop();
            window.ResetLayout();
            window.UpdateLayout();
        }
    }

    /// <summary>The events the layer raised on <paramref name="element"/> since the <paramref name="from"/>-th.</summary>
    private static List<OverlayEvent> EventsOn(HdtOverlay hdt, int from, FrameworkElement element) =>
        hdt.Events.Skip(from).Where(e => ReferenceEquals(e.Element, element)).ToList();

    private static bool IsWithin(DependencyObject element, DependencyObject ancestor)
    {
        for (DependencyObject? node = element; node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }
        }

        return false;
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
