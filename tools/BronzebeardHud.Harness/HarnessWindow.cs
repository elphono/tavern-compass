using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;
using BronzebeardHud.HdtPlugin;
using BronzebeardHud.Stats;
using BronzebeardHud.Stats.Tests;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Assets;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace BronzebeardHud.Harness;

/// <summary>
/// The plugin's two movable panels (Compositions, Skip combat), the frames on Bob's cards and the labels above the
/// options of a choice, on a canvas the size of a Hearthstone window, fed with synthetic data as HDT's overlay would hold
/// them. Bob's seven cards are drawn as grey boxes where the game draws them (TavernLayout.CardSlots), so that the
/// frames and labels land on something; so are the options of a choice, when one is open (ChoiceLayout.Cards, above Bob's
/// row as the game draws them, under everything the plugin draws). Move mode is on at the start. The zones a panel must
/// not cover (the boards, the leaderboard, the hero) are drawn under the panels in red. The plugin's own log lines show
/// in the pane on the right. The layout is saved in the harness's own file, never in the plugin's layout.json.
/// </summary>
internal sealed class HarnessWindow : Window
{
    private static readonly (string Label, int Width, int Height)[] Sizes =
    {
        ("1920 × 1080", 1920, 1080),
        ("1600 × 900", 1600, 900),
        ("1440 × 1080 (4:3)", 1440, 1080),
        ("2560 × 1080 (21:9)", 2560, 1080),
        ("2291 × 1360", 2291, 1360),
    };

    /// <summary>Z-index of a choice's options: above Bob's cards (−5), as the game draws them, below the plugin's layer (OverlayLayer, −1).</summary>
    private const int ChoiceCardZIndex = -4;

    private readonly PanelMover _mover;
    private readonly SkipCombatPanel _skip;
    private readonly ChoiceAdvicePanel _choices;
    private readonly CompTargetTracker _tracker = new();
    private readonly ChoiceCover _cover = new();
    private ChoiceKind _choiceKind = ChoiceKind.None;
    private readonly TextBox _log = new() { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap };
    private readonly List<string> _lines = new();
    private readonly string _folder;
    private CompGuideSet _guides = CompGuideSet.Empty(CompGuideSources.HdtFree);

    // The guides the scenario's lobby can play (LobbyGuides), as Plugin.UpdateLobby narrows HDT's list.
    private LobbyGuides _lobby = LobbyGuides.Unknown(CompGuideSet.Empty(CompGuideSources.HdtFree));
    private string _lobbyKey = string.Empty;
    private string _power = "even";
    private string _opponentPower = "even";
    private string _opponentLine = string.Empty;

    // The two rows of the inset, each under its own guard, as in the plugin ("warband-curve", "opponent-power"). A guard
    // switched off says so in the log as information (the self-test switches one off on purpose: BreakOpponentRow).
    private FeatureGuard _powerGuard = null!;
    private FeatureGuard _opponentGuard = null!;

    /// <summary>When true, drawing the opponent's row throws, as a broken row would in the plugin (the self-test's guard check).</summary>
    public bool BreakOpponentRow { get; set; }

    /// <summary>The two guards back on (a new session in the plugin): the self-test's guard check leaves no row switched off.</summary>
    public void ResetGuards()
    {
        _powerGuard = new FeatureGuard("warband-curve", (n, e) => Log.Info($"simulated: feature \"{n}\" disabled after {e.GetType().Name}: {e.Message}"));
        _opponentGuard = new FeatureGuard("opponent-power", (n, e) =>
        {
            Log.Info($"simulated: feature \"{n}\" disabled after {e.GetType().Name}: {e.Message}");
            Comps.SetOpponentPower(null);
        });
    }

    // The bridge to the synthetic Firestone compositions and the hero's figures on them, as Plugin.UpdateBridge computes them.
    private IReadOnlyDictionary<string, GuideEvidence>? _bridge;
    private readonly IReadOnlyDictionary<string, HeroCompPick> _heroEffects = HeroCompAffinity.Effects(HarnessData.Hero, HarnessData.FirestoneComps);
    private string _bridgeKey = string.Empty;
    private string _loggedHighlights = string.Empty;
    private int _count = HudSettings.DefaultSuggested;
    private int _scenario;
    private bool _skipShown = true;

    public Canvas Overlay { get; }
    public CompsPanel Comps { get; }
    public TavernMarkers Markers { get; }
    public bool MoveMode => _mover.MoveMode;
    public string LayoutPath { get; }
    public IReadOnlyList<string> LogLines => _lines;

    /// <summary>The targets of the scene as it stands (the panel's colours).</summary>
    public IReadOnlyList<CompTarget> Targets => _tracker.Targets;

    /// <summary>The guides the scenario's lobby can play, and the ones it leaves out.</summary>
    public LobbyGuides Lobby => _lobby;

    /// <summary>The board held (HarnessData.Scenarios).</summary>
    public int Scenario => _scenario;

    /// <summary>The board's power scene in the inset (HarnessData.Power).</summary>
    public string PowerScene => _power;

    /// <summary>The opponent's power scene in the inset (HarnessData.OpponentPower).</summary>
    public string OpponentPowerScene => _opponentPower;

    /// <summary>
    /// Holds another scenario's cards in its lobby (the bar's list, the self-test). A scenario is a game of its own: ticks and
    /// colours are forgotten, as the plugin forgets them at the next game (CompTargetTracker.BeginGame), and the panel is
    /// hidden between the two games as the plugin hides it (CompsPanel.Hide: a size given by + / − is forgotten), unless
    /// <paramref name="sameGame"/> (the lobby becoming known in the same game, say).
    /// </summary>
    public void SetScenario(int scenario, bool sameGame = false)
    {
        _scenario = Math.Max(0, Math.Min(HarnessData.Scenarios.Count - 1, scenario));
        if (!sameGame)
        {
            _tracker.BeginGame(++_game);
            Comps.Hide();
        }

        Refresh();
    }

    /// <summary>Shows another opponent's board in the inset (HarnessData.OpponentPowerScenes).</summary>
    public void SetOpponentPower(string scene)
    {
        HarnessData.OpponentFacts(scene); // an unknown name throws here, before anything changes
        _opponentPower = scene;
        Refresh();
    }

    /// <summary>
    /// The panel dropped at (<paramref name="left"/>, <paramref name="top"/>) as a hand would drop it in move mode
    /// (PanelMover.Drop), then redrawn as the next update of the plugin would redraw it. A layout file that did not exist
    /// before is removed again (C:\temp\BronzebeardHarness-ci\layout.json stays unwritten).
    /// </summary>
    public void DropPanel(double left, double top) => KeepingNoLayoutFile(() =>
    {
        _mover.Drop(Comps.Element, left, top);
        Refresh();
    });

    /// <summary>The panel's corner pulled to (<paramref name="cornerX"/>, <paramref name="cornerY"/>) by its handle (PanelMover.ResizeTo).</summary>
    public void ResizePanel(double cornerX, double cornerY) => KeepingNoLayoutFile(() => _mover.ResizeTo(Comps.Element, cornerX, cornerY));

    /// <summary>
    /// Clicks − (<paramref name="step"/> &lt; 0) or + in the inset as the mouse would: a button-up raised on its text bubbles to
    /// the button's own handler. False when the button is not drawn.
    /// </summary>
    public bool ClickCount(int step)
    {
        var tag = step < 0 ? CompsPanel.MinusTag : CompsPanel.PlusTag;
        var button = Descendants(Comps.Inset).OfType<Border>().FirstOrDefault(b => Equals(b.Tag, tag));
        if (button?.Child is not TextBlock text)
        {
            return false;
        }

        text.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseUpEvent,
        });
        return true;
    }

    /// <summary>Clicks − or + until <paramref name="count"/> compositions are wanted (1 to 4); throws when the buttons cannot get there (a tick).</summary>
    public void ClickCountTo(int count)
    {
        for (var tries = 0; _count != count && tries < 8; tries++)
        {
            ClickCount(count < _count ? -1 : +1);
            UpdateLayout();
        }

        if (_count != count)
        {
            throw new ArgumentException($"--count {count}: − and + left it at {_count} (a guide ticked dims them)");
        }
    }

    /// <summary>"Reset panel positions" (the bar's button, PanelMover.Reset).</summary>
    public void ResetLayout() => KeepingNoLayoutFile(_mover.Reset);

    /// <summary>True once the panel was given a size by its handle (PanelLayout.IsResized).</summary>
    public bool PanelResized => _mover.IsResized(CompsPanel.PanelId);

    private void KeepingNoLayoutFile(Action action)
    {
        var existed = File.Exists(LayoutPath);
        action();
        if (!existed && File.Exists(LayoutPath))
        {
            File.Delete(LayoutPath);
        }
    }

    private int _game;

    /// <summary>Shows another board's power under the list (HarnessData.PowerScenes).</summary>
    public void SetPower(string scene)
    {
        HarnessData.Power(scene); // an unknown name throws here, before anything changes
        _power = scene;
        Refresh();
    }

    /// <summary>How many automatic targets are wanted (the panel's − n +): what a click on − or + would change.</summary>
    public int Count => _count;

    public IReadOnlyList<TavernHighlight> Highlights { get; private set; } = Array.Empty<TavernHighlight>();

    /// <summary>Guide id → its synthetic Firestone composition (GuideBridge), as the plugin passes it; null before the first refresh.</summary>
    public IReadOnlyDictionary<string, GuideEvidence>? Bridge => _bridge;

    /// <summary>The choice open above the scene; None when there is none.</summary>
    public ChoiceKind ChoiceKind => _choiceKind;

    /// <summary>Its options, left to right (HarnessData.Choice); empty when no choice is open.</summary>
    public IReadOnlyList<OfferedOption> ChoiceOptions => HarnessData.Choice(_choiceKind);

    /// <summary>What ChoiceAdvisor said of the choice on screen, as the plugin hands it to the panel; null when none.</summary>
    public ChoiceAdvice? Choice { get; private set; }

    public ChoiceAdvicePanel Choices => _choices;

    /// <summary>
    /// What the guide popup is told of the cursor when a line raises MouseLeave (is it still within the line?); null: the
    /// real cursor (GuidePopup.IsCursorOver). The self-test sets it: a parked window never has the real cursor over it, and
    /// "still over" is the case HDT's click-through window produces.
    /// </summary>
    public Func<FrameworkElement, bool>? CursorInside { get; set; }

    /// <summary>Whether the Skip combat button shows (in combat in the plugin; the bar's check box here).</summary>
    public bool SkipCombatShown => _skipShown;

    /// <summary>
    /// Switches move mode on or off (the bar's check box). Switching it off saves the layout (PanelMover); a layout file
    /// that did not exist before is removed again, so that a headless run leaves C:\temp\BronzebeardHarness-ci\layout.json
    /// unwritten, as its README says.
    /// </summary>
    public void SetMoveMode(bool on)
    {
        if (_mover.MoveMode == on)
        {
            return;
        }

        var existed = File.Exists(LayoutPath);
        _mover.ToggleMoveMode();
        if (!existed && File.Exists(LayoutPath))
        {
            File.Delete(LayoutPath);
        }
    }

    /// <summary>Shows or hides the Skip combat button, as the bar's check box does (it shows in combat only in the plugin).</summary>
    public void ShowSkipCombat(bool shown)
    {
        _skipShown = shown;
        if (shown)
        {
            _skip.Show();
        }
        else
        {
            _skip.Hide();
        }
    }

    /// <summary>A guide of the scene: a target's rank ("1") or a guide's name; null when there is none.</summary>
    public CompGuide? GuideOf(string which) =>
        int.TryParse(which, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank)
            ? _tracker.Targets.FirstOrDefault(t => t.Rank == rank)?.Guide
            : _guides.All.FirstOrDefault(g => string.Equals(g.Name, which, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Raises MouseEnter on the line of a guide of the list, as HDT's probe does when the cursor enters it: its popup
    /// shows after the delay. Throws when the line is not shown: a capture of the wrong view is worse than none.
    /// </summary>
    public FrameworkElement Hover(string which)
    {
        var line = LineOf(which);
        line.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = MouseEnterEvent });
        return line;
    }

    /// <summary>
    /// The line of a guide of the list as drawn now: the panel rebuilds its lines at every redraw (card names arriving
    /// redraw the scene), so a line found earlier may be gone.
    /// </summary>
    public FrameworkElement LineOf(string which)
    {
        var guide = GuideOf(which);
        if (guide == null || !Comps.ShownLines.TryGetValue(guide.Id, out var line))
        {
            throw new ArgumentException($"--hover {which}: no such line in the list (targets: {CompTargets.Summary(_tracker.Targets)})");
        }

        return line;
    }

    /// <summary>The card ovals of a line of the list, left to right: what shows a card's preview on hover.</summary>
    public static IReadOnlyList<FrameworkElement> Ovals(FrameworkElement line) =>
        Descendants(line).OfType<Grid>().Where(g => g.IsHitTestVisible && g.Background == Brushes.Transparent && g.Children.OfType<Image>().Any()).ToList();

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var inner in Descendants(child))
            {
                yield return inner;
            }
        }
    }

    private readonly bool _cardValues;
    private int _bracket = HarnessData.Bracket;

    private CardTurnNote? CardNote(string cardId) =>
        _cardValues ? CardTurnValue.For(HarnessData.CardStats, cardId, HarnessData.Turn) : null;

    public HarnessWindow(Options options)
    {
        _cardValues = options.CardValues;
        _folder = Path.Combine(Path.GetTempPath(), "BronzebeardHarness");
        Directory.CreateDirectory(_folder);
        LayoutPath = options.Layout ?? Path.Combine(_folder, "layout.json");
        _scenario = Math.Max(0, Math.Min(HarnessData.Scenarios.Count - 1, options.Scenario));
        if (options.Power != null)
        {
            if (HarnessData.PowerScenes.Contains(options.Power))
            {
                _power = options.Power;
            }
            else
            {
                Log.Warn($"--power {options.Power}: expected {string.Join(", ", HarnessData.PowerScenes)}"); // the self-test's log check reports it
            }
        }

        if (options.OpponentPower != null)
        {
            if (HarnessData.OpponentPowerScenes.Contains(options.OpponentPower))
            {
                _opponentPower = options.OpponentPower;
            }
            else
            {
                Log.Warn($"--opp-power {options.OpponentPower}: expected {string.Join(", ", HarnessData.OpponentPowerScenes)}");
            }
        }

        Log.Written += line => Dispatcher.BeginInvoke(new Action(() => AppendLog(line)));
        AssetDownloaders.Initialize(Path.Combine(_folder, "images"));
        HarnessCards.Install();

        Overlay = new Canvas { Width = options.Size.Width, Height = options.Size.Height, Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x22, 0x30)), ClipToBounds = true };
        Overlay.SizeChanged += (_, _) => DrawScene();

        _mover = new PanelMover(Overlay, LayoutPath);
        Comps = new CompsPanel(Overlay, _mover, ToggleGuide, () => _count, ChangeCount, () => Log.Info("Meta clicked"),
            guide => GuidePivots.For(guide, _lobby.Playable, Held()),
            (guide, fit) => Log.Info($"comp detail id={guide.Id} sections={fit.Shown.Count} of {fit.Total}"),
            guide => TargetContext.For(guide, _bridge, _heroEffects),
            action => action(),
            action => _powerGuard.Run(action),
            action => _opponentGuard.Run(() =>
            {
                if (BreakOpponentRow)
                {
                    throw new InvalidProgramException("simulated failure of the opponent's row");
                }

                action();
            }),
            line => Log.Info(line),
            element => CursorInside?.Invoke(element) ?? GuidePopup.IsCursorOver(element));
        Comps.Bracket = (BracketChoice.Label(_bracket), NextBracket);
        ResetGuards();
        Markers = new TavernMarkers(Overlay, id => Log.Info($"pin toggled: {id}"));
        _skip = new SkipCombatPanel(Overlay, _mover, () => Log.Info("Skip combat clicked (nothing is killed here)"));
        // Its trinket stats cache is never polled here: the harness hands ChoiceAdvisor synthetic stats
        // (HarnessData.TrinketStat) where the plugin hands it the panel's. The folder is the harness's, never the plugin's,
        // and its fetcher refuses the network, so that a poll added one day fails loudly instead of downloading.
        _choices = new ChoiceAdvicePanel(Overlay, new StatsCache(Path.Combine(_folder, "stats"), new NoNetwork(), () => DateTimeOffset.UtcNow));
        Overlay.SizeChanged += (_, _) => LogChoice(); // after the panel's own handler: the labels at the new size
        _mover.ToggleMoveMode(); // move mode on from the start: the harness is for moving and resizing
        if (options.Choice != null)
        {
            try
            {
                _choiceKind = HarnessData.ChoiceOf(options.Choice);
            }
            catch (ArgumentException e)
            {
                Log.Warn(e.Message); // a headless run reports it again, as its error (Headless.Run)
            }
        }

        Title = "Tavern Compass — simulation (not HDT)";
        Width = 1750;
        Height = 960;
        Content = BuildContent(options);
        Refresh();
        if (!options.SelfTest)
        {
            HarnessCards.Load(Path.Combine(_folder, "cache"), Refresh);
        }
    }

    /// <summary>How many pivots a guide's detail lists (GuidePivots, as the panel asks them).</summary>
    public int PivotCount(CompGuide guide) => GuidePivots.For(guide, _lobby.Playable, Held()).Count;

    private HashSet<string> Held() => new(HarnessData.Scenarios[_scenario].Cards.All.Select(c => c.CardId), StringComparer.Ordinal);

    /// <summary>The targets a scenario gives with the current ticks and count, without changing the scene (the self-test asks).</summary>
    public IReadOnlyList<CompTarget> TargetsOf(int scenario)
    {
        var probe = new CompTargetTracker();
        foreach (var id in _tracker.Ticked)
        {
            probe.Toggle(id);
        }

        var (_, cards, lobby) = HarnessData.Scenarios[scenario];
        return CompTargets.Round(LobbyGuides.Of(_guides, lobby, HarnessData.CardTribes), cards, probe, _count).Targets;
    }

    /// <summary>Shows the panel and the markers again from the current board, count and ticks: what a game update does in the plugin.</summary>
    private void Refresh()
    {
        _guides = HarnessData.Guides(id => Database.GetCardFromId(id)?.LocalizedName ?? id);
        UpdateBridge();
        var (_, cards, tribes) = HarnessData.Scenarios[_scenario];
        UpdateLobby(tribes);
        var round = CompTargets.Round(_lobby, cards, _tracker, _count); // as Plugin.UpdateComps: the lobby's guides only
        foreach (var unticked in round.Unticked)
        {
            Log.Info(unticked);
        }

        var targets = round.Targets;
        var note = !_lobby.Known && _lobby.All.Count > 0 ? "Lobby tribes unknown: every guide listed" : null;
        Comps.CannotShowUp = _lobby.CannotShowUp; // as Plugin.UpdateComps
        Comps.Show(round.Board, targets, cards.All.Select(c => c.CardId), CompGuideSources.HdtFree, null, note);
        Comps.SetPower(HarnessData.Power(_power));
        var (opponent, opponentLine) = HarnessData.OpponentPower(_opponentPower, id => Database.GetCardFromId(id)?.LocalizedName ?? id);
        if (opponentLine != _opponentLine)
        {
            _opponentLine = opponentLine; // as Plugin.UpdateOpponentPower: one line each time it changes
            Log.Info(opponentLine);
        }

        Comps.SetOpponentPower(opponent);
        DrawScene(); // Bob's cards by name, once the names are known
        Highlights = TavernHighlights.For(HarnessData.Shop, targets, _bridge);
        var line = TavernHighlights.Summary(HarnessData.Shop, Highlights);
        if (line != _loggedHighlights)
        {
            _loggedHighlights = line;
            Log.Info(TavernHighlights.LogLine(line, targets));
        }

        // Plugin.UpdateTavern: the value of each card at this turn (CardTurnValue), with --card-values.
        var notes = HarnessData.Shop.Select(id => CardNote(id)).ToList();
        Markers.Show(HarnessData.Shop, Highlights, HarnessData.Pins, HarnessData.Shop.Select(_ => true).ToList(), notes);
        if (_skipShown)
        {
            _skip.Show();
        }

        UpdateChoice(targets, cards);
    }

    /// <summary>
    /// The guides the lobby can play, as Plugin.UpdateLobby narrows them: again when the guides or the tribes change, with
    /// the plugin's log line.
    /// </summary>
    private void UpdateLobby(IReadOnlyList<string> tribes)
    {
        var key = string.Join(",", _guides.All.Select(g => g.Id)) + "|" + string.Join(",", tribes);
        if (key == _lobbyKey)
        {
            return;
        }

        _lobbyKey = key;
        _lobby = LobbyGuides.Of(_guides, tribes, HarnessData.CardTribes);
        Log.Info(_lobby.Line($"scenario {_scenario}"));
    }

    /// <summary>
    /// The bridge, as Plugin.UpdateBridge computes it: again when the guides change (their ids, here), with the plugin's log
    /// line, against the synthetic Firestone compositions (HarnessData.FirestoneComps).
    /// </summary>
    private void UpdateBridge()
    {
        var key = string.Join(",", _guides.All.Select(g => g.Id));
        if (key == _bridgeKey)
        {
            return;
        }

        _bridgeKey = key;
        var comps = HarnessData.FirestoneComps;
        _bridge = GuideBridge.For(_guides, comps);
        var bridged = _guides.All.Count(g => _bridge.ContainsKey(g.Id));
        Log.Info(GuideBridge.Line(_guides, _bridge) + $" ({bridged}/{_guides.Count} guides bridged, against {comps.Count} compositions; Firestone synthetic)");
    }

    /// <summary>
    /// The choice open above the scene, advised as Plugin.UpdateChoice advises it: ChoiceAdvisor on the board and hand, the
    /// targets, the guides the scenario's lobby can play and the bridge, then the panel, then the plugin's diagnostic line.
    /// The trinket stats are the harness's (HarnessData.TrinketStat, for HarnessData.Bracket).
    /// </summary>
    private void UpdateChoice(IReadOnlyList<CompTarget> targets, PlayerCards cards)
    {
        var options = HarnessData.Choice(_choiceKind);

        // Plugin.UpdateChoiceCover: the scene is a shop; while a choice is open, the markers are off the screen, the panel stays.
        if (_cover.Observe(OverlayPhase.Shop, ChoiceClassifier.Kind(options)) is { } line)
        {
            Log.Info(line);
        }

        Markers.Suspend(_cover.Hidden);

        if (options.Count == 0)
        {
            Choice = null;
            _choices.Hide();
            return;
        }

        var advice = ChoiceAdvisor.Advise(options, cards.All, targets, _lobby.Playable, _lobby.Tribes, HarnessData.TrinketStat, HarnessData.Bracket, _bridge,
            CardNote);
        Choice = advice;
        if (advice.HasMarkers)
        {
            _choices.Show(advice);
        }
        else
        {
            _choices.Hide();
        }

        LogChoice();
    }

    /// <summary>The plugin's line for the choice on screen, once the panel has drawn it (it draws nothing before the canvas has a size).</summary>
    private void LogChoice()
    {
        if (Choice != null && Overlay.ActualWidth > 0 && Overlay.ActualHeight > 0)
        {
            Log.Info(ChoiceAdvisor.DiagnosticLine(ChoiceOptions, Choice, _guides.Count, _guides.Source, _choices.LastLines, _choices.FirstLabel,
                Overlay.ActualWidth, Overlay.ActualHeight));
        }
    }

    /// <summary>
    /// Opens a choice above the scene (or closes it, with None), advised on the targets as they stand. As in the plugin, a
    /// choice that opens or closes runs the choice's code alone: the panel and the markers are neither given anything nor
    /// computed again, only taken off the screen and put back (ChoiceCover).
    /// </summary>
    public void ShowChoice(ChoiceKind kind)
    {
        _choiceKind = kind;
        DrawScene(); // the options' boxes
        UpdateChoice(_tracker.Targets, HarnessData.Scenarios[_scenario].Cards);
    }

    /// <summary>
    /// Opens a guide's detail: <paramref name="which"/> is a target's rank (1 for the first target) or a guide's name.
    /// Throws when there is no such guide: a capture of the wrong view is worse than none.
    /// </summary>
    public void OpenDetail(string which)
    {
        var guide = int.TryParse(which, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank)
            ? _tracker.Targets.FirstOrDefault(t => t.Rank == rank)?.Guide
            : _guides.All.FirstOrDefault(g => string.Equals(g.Name, which, StringComparison.OrdinalIgnoreCase));
        if (guide == null || !Comps.OpenDetail(guide.Id))
        {
            throw new ArgumentException($"--detail {which}: no such target or guide (targets: {CompTargets.Summary(_tracker.Targets)})");
        }
    }

    /// <summary>Ticks a guide as the player does: <paramref name="which"/> is a target's rank (1 for the first target) or a guide's name.</summary>
    public void Tick(string which)
    {
        var guide = int.TryParse(which, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank)
            ? _tracker.Targets.FirstOrDefault(t => t.Rank == rank)?.Guide
            : _guides.All.FirstOrDefault(g => string.Equals(g.Name, which, StringComparison.OrdinalIgnoreCase));
        if (guide == null)
        {
            throw new ArgumentException($"--tick {which}: no such target or guide (targets: {CompTargets.Summary(_tracker.Targets)})");
        }

        ToggleGuide(guide.Id);
    }

    private void ToggleGuide(string id)
    {
        Log.Info(_tracker.ToggleLine(id, _tracker.Toggle(id)));
        Refresh();
    }

    /// <summary>Plugin.NextBracket: the panel's bracket button, the next bracket; here only the label and the log line follow.</summary>
    private void NextBracket()
    {
        var next = BracketChoice.Next(_bracket);
        Log.Info($"bracket {BracketChoice.Label(next)} chosen in the overlay (was {BracketChoice.Label(_bracket)})");
        _bracket = next;
        Comps.Bracket = (BracketChoice.Label(_bracket), NextBracket);
        Refresh();
    }

    private void ChangeCount(int step)
    {
        _count = Math.Max(HudSettings.MinSuggested, Math.Min(HudSettings.MaxSuggested, _count + step));
        Log.Info($"suggested compositions={_count}");
        Refresh();
    }

    private UIElement BuildContent(Options options)
    {
        var move = new CheckBox { Content = "Move panels", IsChecked = true, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        move.Click += (_, _) => _mover.ToggleMoveMode();
        var reset = new Button { Content = "Reset panel positions", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 14, 0) };
        reset.Click += (_, _) => _mover.Reset();

        var size = new ComboBox { ItemsSource = Sizes.Select(s => s.Label).ToList(), Width = 150, Margin = new Thickness(0, 0, 14, 0) };
        size.SelectedIndex = Math.Max(0, Array.FindIndex(Sizes, s => s.Width == options.Size.Width && s.Height == options.Size.Height));
        size.SelectionChanged += (_, _) =>
        {
            Overlay.Width = Sizes[size.SelectedIndex].Width;
            Overlay.Height = Sizes[size.SelectedIndex].Height;
        };

        var board = new ComboBox { ItemsSource = HarnessData.Scenarios.Select(s => s.Name).ToList(), Width = 220, Margin = new Thickness(0, 0, 14, 0), SelectedIndex = _scenario };
        board.SelectionChanged += (_, _) =>
        {
            SetScenario(board.SelectedIndex);
        };

        var power = new ComboBox { ItemsSource = HarnessData.PowerScenes.Select(p => "power " + p).ToList(), Width = 120, Margin = new Thickness(0, 0, 14, 0) };
        power.SelectedIndex = Math.Max(0, HarnessData.PowerScenes.ToList().IndexOf(_power));
        power.SelectionChanged += (_, _) => SetPower(HarnessData.PowerScenes[power.SelectedIndex]);

        var opponentPower = new ComboBox { ItemsSource = HarnessData.OpponentPowerScenes.Select(p => "opp " + p).ToList(), Width = 110, Margin = new Thickness(0, 0, 14, 0) };
        opponentPower.SelectedIndex = Math.Max(0, HarnessData.OpponentPowerScenes.ToList().IndexOf(_opponentPower));
        opponentPower.SelectionChanged += (_, _) => SetOpponentPower(HarnessData.OpponentPowerScenes[opponentPower.SelectedIndex]);

        var choice = new ComboBox { ItemsSource = HarnessData.Choices.Select(c => c.Label).ToList(), Width = 110, Margin = new Thickness(0, 0, 14, 0) };
        choice.SelectedIndex = Math.Max(0, HarnessData.Choices.Select(c => c.Kind).ToList().IndexOf(_choiceKind));
        choice.SelectionChanged += (_, _) => ShowChoice(HarnessData.Choices[choice.SelectedIndex].Kind);

        var skip = new CheckBox { Content = "Skip combat button", IsChecked = true, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        skip.Click += (_, _) =>
        {
            _skipShown = skip.IsChecked == true;
            if (_skipShown)
            {
                _skip.Show();
            }
            else
            {
                _skip.Hide();
            }
        };

        var detail = new Button { Content = "Detail of target 1 / list", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 14, 0) };
        detail.Click += (_, _) =>
        {
            if (Comps.ShowsDetail)
            {
                Comps.CloseDetail();
            }
            else if (_tracker.Targets.Count > 0)
            {
                OpenDetail("1");
            }
            else
            {
                Log.Info("no target in this scenario: click a guide's name instead");
            }
        };

        var clear = new Button { Content = "Clear log", Padding = new Thickness(8, 2, 8, 2) };
        clear.Click += (_, _) =>
        {
            _lines.Clear();
            _log.Clear();
        };

        var bar = new WrapPanel { Margin = new Thickness(8) };
        foreach (var element in new UIElement[] { move, reset, size, board, power, opponentPower, choice, skip, detail, clear })
        {
            bar.Children.Add(element);
        }

        var path = new TextBlock { Text = "layout: " + LayoutPath, Margin = new Thickness(8, 0, 8, 6), Foreground = Brushes.Gray, TextTrimming = TextTrimming.CharacterEllipsis };
        var viewport = new Border { Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x11, 0x18)), Child = new Viewbox { Child = Overlay, Margin = new Thickness(6) } };

        var left = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(path, Dock.Bottom);
        left.Children.Add(bar);
        left.Children.Add(path);
        left.Children.Add(viewport);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(430) });
        Grid.SetColumn(_log, 1);
        root.Children.Add(left);
        root.Children.Add(_log);
        return root;
    }

    private void AppendLog(string line)
    {
        _lines.Add(line);
        try
        {
            File.AppendAllText(Path.Combine(_folder, "harness.log"), line + Environment.NewLine);
        }
        catch (IOException)
        {
            // The pane has it; the file is a convenience.
        }

        _log.AppendText(line + Environment.NewLine);
        _log.ScrollToEnd();
    }

    /// <summary>
    /// Under the panels, redrawn at every canvas size: the zones a default panel must not cover, in red, Bob's seven
    /// cards as grey boxes with their names, where TavernLayout puts the game's shop cards, and the options of the choice
    /// open, if any, where ChoiceLayout puts the game's.
    /// </summary>
    private void DrawScene()
    {
        foreach (var old in Overlay.Children.OfType<FrameworkElement>().Where(e => Equals(e.Tag, "zone") || Equals(e.Tag, "shop") || Equals(e.Tag, "choice")).ToList())
        {
            Overlay.Children.Remove(old);
        }

        DrawChoiceCards();

        foreach (var (name, zone) in NoGoZones.For(Overlay.ActualWidth, Overlay.ActualHeight))
        {
            var area = new Rectangle { Width = zone.Width, Height = zone.Height, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0x40, 0x40)), Stroke = new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0x40, 0x40)), StrokeThickness = 2, Tag = "zone", IsHitTestVisible = false };
            var label = new TextBlock { Text = name, Foreground = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0x90, 0x90)), FontSize = 16, Tag = "zone", IsHitTestVisible = false };
            Panel.SetZIndex(area, -10);
            Panel.SetZIndex(label, -10);
            Canvas.SetLeft(area, zone.Left);
            Canvas.SetTop(area, zone.Top);
            Canvas.SetLeft(label, zone.Left + 8);
            Canvas.SetTop(label, zone.Top + 6);
            Overlay.Children.Add(area);
            Overlay.Children.Add(label);
        }

        var slots = TavernLayout.CardSlots(Overlay.ActualWidth, Overlay.ActualHeight, HarnessData.Shop.Count);
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var card = new Border
            {
                Width = slot.Width * 0.9,
                Height = slot.Height * 0.9,
                Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x50, 0x5C)),
                CornerRadius = new CornerRadius(12),
                Tag = "shop",
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = Database.GetCardFromId(HarnessData.Shop[i])?.LocalizedName ?? HarnessData.Shop[i],
                    Foreground = Brushes.White,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6),
                },
            };
            Panel.SetZIndex(card, -5);
            Canvas.SetLeft(card, slot.Left + slot.Width * 0.05);
            Canvas.SetTop(card, slot.Top + slot.Height * 0.05);
            Overlay.Children.Add(card);
        }
    }

    /// <summary>
    /// The options of the choice open, as grey boxes at ChoiceLayout.Cards (HDT's constants, where the game draws them):
    /// the card's name, then what the harness's guides make of it (a minion) or its text (a trinket), so that a label can
    /// be read against the truth. Above Bob's row, under the plugin's labels.
    /// </summary>
    private void DrawChoiceCards()
    {
        var options = ChoiceOptions;
        var cards = ChoiceLayout.Cards(_choiceKind, options.Count, Overlay.ActualWidth, Overlay.ActualHeight);
        for (var i = 0; i < cards.Count; i++)
        {
            var id = options[i].CardId;
            var what = _choiceKind == ChoiceKind.Trinket
                ? System.Text.RegularExpressions.Regex.Replace(HarnessData.TrinketText(id) ?? string.Empty, @"<[^>]+>|\[x\]", string.Empty)
                : Roles(id);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10) };
            text.Children.Add(new TextBlock { Text = Database.GetCardFromId(id)?.LocalizedName ?? id, Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            text.Children.Add(new TextBlock { Text = $"option {i + 1} · {id}", Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xCC, 0xD4)), FontSize = 14, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 10) });
            text.Children.Add(new TextBlock { Text = what, Foreground = new SolidColorBrush(Color.FromRgb(0xE4, 0xE7, 0xEC)), FontSize = 15, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            var card = new Border
            {
                Width = cards[i].Width,
                Height = cards[i].Height,
                Background = new SolidColorBrush(Color.FromRgb(0x5E, 0x65, 0x73)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xCC, 0xD4)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(16),
                Tag = "choice",
                IsHitTestVisible = false,
                Child = text,
            };
            Panel.SetZIndex(card, ChoiceCardZIndex);
            Canvas.SetLeft(card, cards[i].Left);
            Canvas.SetTop(card, cards[i].Top);
            Overlay.Children.Add(card);
        }
    }

    /// <summary>What the harness's guides make of a card, one guide per line ("core of X (S)", "add-on of Y (A)"), or "in no guide".</summary>
    private string Roles(string cardId)
    {
        var roles = _guides.All
            .Select(g => (Guide: g, Role: GuideCardEffects.RoleIn(g, cardId)))
            .Where(x => x.Role != null)
            .Select(x => $"{(x.Role == GuideCardRole.Addon ? "add-on" : x.Role.ToString()!.ToLowerInvariant())} of {x.Guide.Name} ({x.Guide.TierLetter})")
            .ToList();
        return roles.Count == 0 ? "in no guide" : string.Join("\n", roles);
    }
}

/// <summary>The harness's fetcher: it never goes to the network (its data are synthetic).</summary>
internal sealed class NoNetwork : IConditionalFetcher
{
    public Task<FetchedText> FetchAsync(string url, string? ifNoneMatch, CancellationToken cancellationToken) =>
        throw new InvalidOperationException($"the harness does not download ({url})");
}
