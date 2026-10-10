using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Polygon = System.Windows.Shapes.Polygon;

namespace BronzebeardHud.Harness;

/// <summary>
/// HDT's OverlayWindow.CustomMouseEventArgs: the MouseEnter and MouseLeave its probe raises. WPF's own are plain
/// MouseEventArgs, and HDT's tooltip tells the two apart (OverlayExtensions.ShowTooltip).
/// </summary>
internal sealed class ProbeMouseEventArgs : MouseEventArgs
{
    public ProbeMouseEventArgs(RoutedEvent routed)
        : base(Mouse.PrimaryDevice, 0)
    {
        RoutedEvent = routed;
    }
}

/// <summary>Who raised a mouse event on an element of the overlay.</summary>
internal enum OverlaySource
{
    /// <summary>HDT's probe (UpdateHoverable): a ProbeMouseEventArgs.</summary>
    Probe,

    /// <summary>WPF, while the overlay window catches the mouse: a plain MouseEventArgs.</summary>
    Wpf,
}

/// <summary>One MouseEnter or MouseLeave the layer raised, and where the cursor was.</summary>
internal sealed class OverlayEvent
{
    public OverlayEvent(int tick, OverlaySource source, bool enter, FrameworkElement element, Point cursor, bool cursorInside)
    {
        Tick = tick;
        Source = source;
        Enter = enter;
        Element = element;
        Cursor = cursor;
        CursorInside = cursorInside;
    }

    /// <summary>The probe's run count when it was raised.</summary>
    public int Tick { get; }

    public OverlaySource Source { get; }

    /// <summary>True: MouseEnter; false: MouseLeave.</summary>
    public bool Enter { get; }

    public FrameworkElement Element { get; }

    public Point Cursor { get; }

    /// <summary>Whether the cursor was within the element's rectangle, as the plugin tests it (GuidePopup.IsCursorOver).</summary>
    public bool CursorInside { get; }

    public override string ToString() =>
        $"{(Source == OverlaySource.Probe ? "probe" : "WPF")} {(Enter ? "enter" : "leave")} {HdtOverlay.Describe(Element)} at ({Cursor.X:0},{Cursor.Y:0}){(CursorInside ? ", cursor inside" : string.Empty)}";
}

/// <summary>Where an injected click went: to the game (the window was click-through there) or to an element of the overlay.</summary>
internal sealed class OverlayClick
{
    public OverlayClick(Point at, bool toGame, FrameworkElement? target)
    {
        At = at;
        ToGame = toGame;
        Target = target;
    }

    public Point At { get; }

    /// <summary>True: the overlay window was click-through at that point, the game had the click.</summary>
    public bool ToGame { get; }

    /// <summary>The element WPF pressed and released (the deepest one hit); null when the click went to the game, or hit nothing.</summary>
    public FrameworkElement? Target { get; }

    public override string ToString() =>
        $"click at ({At.X:0},{At.Y:0}) → " + (ToGame ? "the game (the window is click-through there)" : Target != null ? $"the overlay: {HdtOverlay.Describe(Target)}" : "the overlay, on no element (swallowed)");
}

/// <summary>
/// HDT's overlay window around the plugin's panels, reproduced without HDT, on an injected cursor. Two parts, of two kinds:
///
/// Read in HDT's code (Windows/OverlayWindow.xaml.cs, HDT 1.58.9 decompiled with ilspycmd; the same methods, character for
/// character, in 1.58.10). The window starts click-through (Window_SourceInitialized_1: WS_EX_NOACTIVATE | WS_EX_TRANSPARENT,
/// lines 4692-4696). A probe runs for as long as the overlay lives (StartInteractivityUpdates, lines 2418-2429:
/// UpdateHoverable, then await Task.Delay(16), so about 60 Hz at best). Each run (UpdateHoverable, lines 2436-2546) reads the
/// cursor on the overlay canvas; the window catches the mouse while the cursor is within an element declared clickable
/// (IsOverlayHitTestVisible) and is click-through everywhere else (SetClickthrough, lines 4698-4716: WS_EX_TRANSPARENT off
/// or on); the elements declared hoverable (IsOverlayHoverVisible) that contain the cursor are grouped by the child of the
/// canvas they belong to, the group of the last such child wins, and its elements get a MouseEnter (CustomMouseEventArgs)
/// when they were not entered, the elements entered before and no longer in it a MouseLeave. "Contains" is purely geometric
/// (ElementContains, lines 2596-2642: visible, loaded, a size, a FrameworkElement parent; its origin on the canvas and
/// its size times the scale transforms of it and its ancestors, strict bounds), with origins kept 200 ms and scales 1 s
/// (lines 178-180). This class ports those methods; only the cursor is the injected one, not GetMousePos.
///
/// Deduced from WPF and Windows, NOT read in HDT, never seen in a game. The window catches the mouse only over a declared
/// clickable; there WPF gets the OS's mouse moves and raises its own MouseEnter and MouseLeave (plain MouseEventArgs) on
/// the element it hit-tests and its ancestors, so a hoverable line gets a second "enter" when the cursor reaches its
/// clickable name. Once the probe makes the window click-through again, the next move goes to the game and WPF loses the
/// mouse (WM_MOUSELEAVE): a MouseLeave on everything it held, the line included, although the cursor is still on it. Here
/// WPF learns of the cursor only through <see cref="Move"/> (one OS move each), and only while the window catches the mouse;
/// a click lands where the window is at that moment. A press and release WPF would give a ButtonBase (the tick boxes) is
/// handed to it as ButtonBase does (OnClick), since a synthetic press carries the real button's state, released.
///
/// Not reproduced: the screen-to-canvas conversion of the cursor (DPI), HDT's own elements and its "behind" state, a
/// stationary cursor (Windows may send a move when a window under it changes; here only <see cref="Move"/> does).
/// </summary>
internal sealed class HdtOverlay
{
    /// <summary>StartInteractivityUpdates: one UpdateHoverable, then await Task.Delay(16).</summary>
    public const int ProbeDelayMilliseconds = 16;

    private static readonly MethodInfo ButtonOnClick = typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly Canvas _canvas;

    // HDT's _clickableElements is a List, its _hoverableElements a HashSet, its _mouseOverElements a HashSet.
    private readonly List<FrameworkElement> _clickables = new();
    private readonly HashSet<FrameworkElement> _hoverables = new();
    private HashSet<FrameworkElement> _mouseOver = new();
    private readonly ElementCache<Vector> _scaleCache = new(TimeSpan.FromSeconds(1));
    private readonly ElementCache<Point> _transformCache = new(TimeSpan.FromMilliseconds(200));

    // What WPF holds the mouse to be over (IsMouseOver): the element hit, then its ancestors up to the canvas.
    private List<FrameworkElement> _wpfOver = new();
    private readonly List<OverlayEvent> _events = new();
    private readonly Stopwatch _clock = new();

    // The window's WS_EX_TRANSPARENT as SetClickthrough last left it: click-through until the cursor is over a clickable.
    private bool _clickthrough = true;
    private int _generation;

    public HdtOverlay(Canvas canvas)
    {
        _canvas = canvas;
        OverlayExtensions.OnRegisterHitTestVisible += (element, clickable) =>
        {
            if (clickable)
            {
                _clickables.Add(element);
            }
            else
            {
                _clickables.Remove(element);
            }
        };
        OverlayExtensions.OnRegisterHoverVisible += (element, hoverable) =>
        {
            if (hoverable)
            {
                _hoverables.Add(element);
            }
            else
            {
                _hoverables.Remove(element);
            }
        };

        // While the injected mouse runs it is the only mouse: any other mouse input is swallowed at the canvas. A capture
        // (PanelMover takes one on a press in move mode) makes WPF process a move from the real cursor, far from the parked
        // window, at once and in the middle of the injected press: the dragged panel would follow it. Told apart by identity:
        // only the arguments the layer is raising pass.
        foreach (var routed in new[] { UIElement.PreviewMouseMoveEvent, UIElement.PreviewMouseDownEvent, UIElement.PreviewMouseUpEvent, UIElement.PreviewMouseWheelEvent })
        {
            canvas.AddHandler(routed, new RoutedEventHandler((_, e) =>
            {
                if (Running && !ReferenceEquals(e, _raising))
                {
                    e.Handled = true;
                }
            }));
        }
    }

    // The press or release the layer is raising, the only mouse input let through while it runs.
    private RoutedEventArgs? _raising;

    /// <summary>HDT's list of the elements declared clickable: how many entries it holds (one element may be in it more than once).</summary>
    public int ClickableEntries => _clickables.Count;

    /// <summary>How many different elements of that list are still loaded: the others are entries nothing will remove.</summary>
    public int LiveClickables => _clickables.Where(e => e.IsLoaded).Distinct().Count();

    /// <summary>True while the injected mouse drives the overlay (the probe runs).</summary>
    public bool Running { get; private set; }

    /// <summary>The injected cursor on the canvas; null before the first <see cref="Move"/>.</summary>
    public Point? Cursor { get; private set; }

    /// <summary>Whether the window is click-through (the game has the mouse) at the cursor, as the probe last set it.</summary>
    public bool ClickThrough => _clickthrough;

    /// <summary>How many times the probe ran since <see cref="Start"/>.</summary>
    public int Ticks { get; private set; }

    /// <summary>Milliseconds since <see cref="Start"/>.</summary>
    public long Elapsed => _clock.ElapsedMilliseconds;

    /// <summary>Every MouseEnter and MouseLeave raised since <see cref="Start"/>, in order.</summary>
    public IReadOnlyList<OverlayEvent> Events => _events;

    /// <summary>
    /// Starts the probe, on the dispatcher at its normal priority as HDT starts it from its window's constructor, and makes
    /// HDT's tooltips answer its events only on hoverable elements (OverlayExtensions). No cursor until <see cref="Move"/>.
    /// </summary>
    public void Start()
    {
        if (Running)
        {
            return;
        }

        Running = true;
        OverlayExtensions.ProbeOnlyOnHoverables = true;
        Ticks = 0;
        _events.Clear();
        _clock.Restart();
        var generation = ++_generation;
        _canvas.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => Loop(generation)));
        Log.Info($"simulated HDT overlay: injected mouse on (probe every {ProbeDelayMilliseconds} ms, click-through outside declared clickables)");
    }

    /// <summary>Stops the probe and forgets the cursor, without raising anything: the plain window again.</summary>
    public void Stop()
    {
        if (!Running)
        {
            return;
        }

        Running = false;
        _generation++;
        OverlayExtensions.ProbeOnlyOnHoverables = false;
        Cursor = null;
        _mouseOver.Clear();
        _wpfOver.Clear();
        _clickthrough = true;
        Log.Info($"simulated HDT overlay: injected mouse off after {Ticks} probe runs in {_clock.ElapsedMilliseconds} ms");
    }

    private async void Loop(int generation)
    {
        while (Running && generation == _generation)
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                Log.Error($"simulated HDT overlay: the probe threw {e.GetType().Name}: {e.Message}");
            }

            await Task.Delay(ProbeDelayMilliseconds);
        }
    }

    /// <summary>
    /// The OS moves the cursor to <paramref name="to"/> (one WM_MOUSEMOVE, nothing when it does not move). The window gets it
    /// only if it catches the mouse (the probe's last verdict): WPF then hit-tests there and raises its MouseLeave and
    /// MouseEnter. Click-through, the move goes to the game, and WPF, if it held the mouse, loses it: a MouseLeave on
    /// everything it held. The probe sees the new place at its next run.
    /// </summary>
    public void Move(Point to)
    {
        if (Cursor == to)
        {
            return;
        }

        Cursor = to;
        if (!_clickthrough)
        {
            WpfSees(to);
        }
        else if (_wpfOver.Count > 0)
        {
            WpfLoses(to);
        }
    }

    /// <summary>
    /// A left press and release where the cursor is. Click-through there: the game has it, nothing of the overlay runs.
    /// Otherwise WPF presses and releases the element it hit-tests (PreviewMouseDown, MouseDown, PreviewMouseUp, MouseUp,
    /// which each UIElement on the way turns into its own MouseLeftButtonDown and MouseLeftButtonUp), and the nearest
    /// ButtonBase on the way is clicked.
    /// </summary>
    public OverlayClick Click()
    {
        if (Cursor is not { } at)
        {
            throw new InvalidOperationException("the injected mouse has no cursor yet: move it first");
        }

        OverlayClick click;
        if (_clickthrough)
        {
            click = new OverlayClick(at, toGame: true, target: null);
        }
        else
        {
            var chain = HitChain(at);
            var target = chain.FirstOrDefault();
            if (target != null)
            {
                try
                {
                    foreach (var routed in new[] { UIElement.PreviewMouseDownEvent, UIElement.MouseDownEvent, UIElement.PreviewMouseUpEvent, UIElement.MouseUpEvent })
                    {
                        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = routed };
                        _raising = args;
                        target.RaiseEvent(args);
                    }
                }
                finally
                {
                    _raising = null;
                }

                if (chain.OfType<ButtonBase>().FirstOrDefault() is { IsEnabled: true } button)
                {
                    ButtonOnClick.Invoke(button, null);
                }
            }

            click = new OverlayClick(at, toGame: false, target);
        }

        Log.Info("simulated HDT overlay: " + click);
        return click;
    }

    /// <summary>One run of HDT's UpdateHoverable (lines 2436-2546), on the injected cursor.</summary>
    internal void Tick()
    {
        Ticks++;
        if (Cursor is not { } cursor)
        {
            return; // GetCursorPos: null while the overlay's content is hidden
        }

        if (_scaleCache.IsInvalid)
        {
            _scaleCache.Clear();
        }

        if (_transformCache.IsInvalid)
        {
            _transformCache.Clear();
        }

        var clickables = _clickables.Where(e => ElementContains(e, cursor, cached: true)).ToList();
        if (SetClickthrough(clickables.Count == 0))
        {
            Log.Info($"simulated HDT overlay: at ({cursor.X:0},{cursor.Y:0}) the window {(_clickthrough ? "is click-through" : $"catches the mouse ({Describe(clickables[0])})")}");
        }

        var hoverables = _hoverables.Where(e => ElementContains(e, cursor, cached: true)).ToList();
        if (hoverables.Count == 0)
        {
            EmitLeave(hoverables, cursor);
            _mouseOver.Clear();
            return;
        }

        var groups = new Dictionary<DependencyObject, Group>();
        foreach (var element in hoverables)
        {
            if (RootOf(element) is { } root)
            {
                GroupOf(groups, root).Hoverables.Add(element);
            }
        }

        foreach (var element in clickables)
        {
            if (RootOf(element) is { } root)
            {
                GroupOf(groups, root).Clickables.Add(element);
            }
        }

        if (groups.Count == 0)
        {
            EmitLeave(hoverables, cursor);
            _mouseOver.Clear();
            return;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(_canvas); i++)
        {
            if (groups.TryGetValue(VisualTreeHelper.GetChild(_canvas, i), out var group))
            {
                group.Index = i;
            }
        }

        var top = groups.Values.OrderByDescending(g => g.Index).First().Hoverables;
        EmitLeave(top, cursor);
        foreach (var element in top)
        {
            if (!_mouseOver.Contains(element))
            {
                Raise(element, enter: true, OverlaySource.Probe, cursor);
            }
        }

        _mouseOver = new HashSet<FrameworkElement>(top);
    }

    /// <summary>
    /// Whether the cursor is within <paramref name="element"/>, as the plugin's GuidePopup.IsCursorOver tests the real one
    /// (the point brought into the element, half-open bounds): what the plugin would read in a game at this place.
    /// </summary>
    public bool IsCursorOver(FrameworkElement element)
    {
        if (Cursor is not { } at || !element.IsVisible || PresentationSource.FromVisual(element) == null)
        {
            return false;
        }

        try
        {
            var local = _canvas.TranslatePoint(at, element);
            return local.X >= 0 && local.Y >= 0 && local.X < element.ActualWidth && local.Y < element.ActualHeight;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>The elements declared clickable that contain <paramref name="at"/>, as the probe would find them now.</summary>
    public IReadOnlyList<FrameworkElement> ClickablesAt(Point at) => _clickables.Where(e => ElementContains(e, at, cached: false)).Distinct().ToList();

    /// <summary>The elements declared hoverable that contain <paramref name="at"/>, as the probe would find them now.</summary>
    public IReadOnlyList<FrameworkElement> HoverablesAt(Point at) => _hoverables.Where(e => ElementContains(e, at, cached: false)).ToList();

    /// <summary>
    /// A point of <paramref name="element"/> (and the one a pixel to its right) on no element declared clickable, where
    /// <paramref name="element"/> is hoverable for the probe: its background. Null when it has none.
    /// </summary>
    public Point? BackgroundOf(FrameworkElement element)
    {
        var rect = element.TransformToAncestor(_canvas).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        for (var y = rect.Top + 2; y < rect.Bottom - 2; y += 2)
        {
            for (var x = rect.Left + 2; x < rect.Right - 3; x += 2)
            {
                var here = new Point(Math.Round(x), Math.Round(y));
                var next = new Point(here.X + 1, here.Y);
                if (ClickablesAt(here).Count == 0 && ClickablesAt(next).Count == 0 && HoverablesAt(here).Contains(element) && HoverablesAt(next).Contains(element))
                {
                    return here;
                }
            }
        }

        return null;
    }

    /// <summary>The centre of <paramref name="element"/> on the canvas, rounded to the pixel.</summary>
    public Point CentreOf(FrameworkElement element)
    {
        var rect = element.TransformToAncestor(_canvas).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        return new Point(Math.Round(rect.Left + rect.Width / 2), Math.Round(rect.Top + rect.Height / 2));
    }

    /// <summary>
    /// An element as the log names it: its type, the first text it shows (of three characters or more when it has one:
    /// a guide's name rather than its rank), and what it is declared to HDT.
    /// </summary>
    internal static string Describe(FrameworkElement element)
    {
        var text = FirstText(element, 3) ?? FirstText(element, 1);
        if (text is { Length: > 28 })
        {
            text = text.Substring(0, 27) + "…";
        }

        var roles = (OverlayExtensions.GetIsOverlayHitTestVisible(element) ? " clickable" : string.Empty)
                    + (OverlayExtensions.GetIsOverlayHoverVisible(element) ? " hoverable" : string.Empty);
        return element.GetType().Name + (text != null ? $" \"{text}\"" : string.Empty) + roles;
    }

    /// <summary>The shallowest text of at least <paramref name="least"/> characters (breadth first: a panel's title before its labels).</summary>
    private static string? FirstText(DependencyObject root, int least)
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node is TextBlock block)
            {
                var content = !string.IsNullOrEmpty(block.Text) ? block.Text : string.Concat(block.Inlines.OfType<System.Windows.Documents.Run>().Select(r => r.Text));
                if (content.Trim().Length >= least)
                {
                    return content;
                }
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                queue.Enqueue(VisualTreeHelper.GetChild(node, i));
            }
        }

        return null;
    }

    /// <summary>SetClickthrough (lines 4698-4716): changes the window only when the verdict changes; true when it did.</summary>
    private bool SetClickthrough(bool clickthrough)
    {
        if (_clickthrough == clickthrough)
        {
            return false;
        }

        _clickthrough = clickthrough;
        return true;
    }

    /// <summary>UpdateHoverable's EmitMouseLeave: every element entered before and not in <paramref name="hovered"/> gets a MouseLeave.</summary>
    private void EmitLeave(IList<FrameworkElement> hovered, Point cursor)
    {
        foreach (var element in _mouseOver.ToList())
        {
            if (!hovered.Contains(element) && element != null)
            {
                Raise(element, enter: false, OverlaySource.Probe, cursor);
            }
        }
    }

    /// <summary>UpdateHoverable's GetCanvasInfoParentRoot: the child of the canvas that holds the element; null when the canvas does not.</summary>
    private DependencyObject? RootOf(FrameworkElement element)
    {
        DependencyObject result = element;
        var parent = VisualTreeHelper.GetParent(element);
        while (parent != null && !ReferenceEquals(parent, _canvas))
        {
            result = parent;
            parent = VisualTreeHelper.GetParent(parent);
        }

        return parent != null ? result : null;
    }

    /// <summary>
    /// ElementContains (lines 2596-2642), the origin and scale cached as UpdateHoverable caches them when
    /// <paramref name="cached"/>, computed afresh otherwise (the helpers that pick a point to move to).
    /// </summary>
    private bool ElementContains(FrameworkElement element, Point location, bool cached)
    {
        if (!element.IsVisible || !element.IsLoaded || element.ActualWidth <= 0.0 || element.ActualHeight <= 0.0)
        {
            return false;
        }

        if (VisualTreeHelper.GetParent(element) is not FrameworkElement)
        {
            return false;
        }

        var scale = TotalScale(element, cached ? _scaleCache.Dict : new Dictionary<FrameworkElement, Vector>());
        try
        {
            Point origin;
            if (!cached)
            {
                origin = element.TransformToAncestor(_canvas).Transform(new Point(0.0, 0.0));
            }
            else if (!_transformCache.Dict.TryGetValue(element, out origin))
            {
                origin = _transformCache.Dict[element] = element.TransformToAncestor(_canvas).Transform(new Point(0.0, 0.0));
            }

            if (element is Polygon polygon)
            {
                return polygon.RenderedGeometry.FillContains(new Point((location.X - origin.X) / scale.X, (location.Y - origin.Y) / scale.Y));
            }

            return location.X > origin.X && location.X < origin.X + element.ActualWidth * scale.X
                   && location.Y > origin.Y && location.Y < origin.Y + element.ActualHeight * scale.Y;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Helper.GetTotalScaleTransform with its cache (HDT's Helper, lines 1892-1930): the ScaleTransforms (render and layout)
    /// of the element and of every FrameworkElement above it. Here the walk stops at the harness's Viewbox, whose scale is
    /// held by a ContainerVisual, not a FrameworkElement: the canvas's own pixels, as HDT's canvas, which nothing scales.
    /// </summary>
    private static Vector TotalScale(FrameworkElement? element, Dictionary<FrameworkElement, Vector> cache)
    {
        var result = new Vector(1.0, 1.0);
        if (element == null)
        {
            return result;
        }

        if (cache.TryGetValue(element, out var known))
        {
            return known;
        }

        if (element.RenderTransform is ScaleTransform render)
        {
            result.X *= render.ScaleX;
            result.Y *= render.ScaleY;
        }

        if (element.LayoutTransform is ScaleTransform layout)
        {
            result.X *= layout.ScaleX;
            result.Y *= layout.ScaleY;
        }

        var above = TotalScale(VisualTreeHelper.GetParent(element) as FrameworkElement, cache);
        return cache[element] = new Vector(above.X * result.X, above.Y * result.Y);
    }

    /// <summary>WPF gets a mouse move at <paramref name="at"/>: what it held and no longer hits gets a MouseLeave, what it hits anew a MouseEnter.</summary>
    private void WpfSees(Point at)
    {
        var chain = HitChain(at);
        foreach (var element in _wpfOver.Where(e => !chain.Contains(e)).ToList())
        {
            Raise(element, enter: false, OverlaySource.Wpf, at);
        }

        var entered = chain.Where(e => !_wpfOver.Contains(e)).Reverse().ToList();
        _wpfOver = chain;
        foreach (var element in entered)
        {
            Raise(element, enter: true, OverlaySource.Wpf, at);
        }
    }

    /// <summary>WPF loses the mouse (WM_MOUSELEAVE, the window click-through): a MouseLeave on everything it held.</summary>
    private void WpfLoses(Point at)
    {
        var held = _wpfOver;
        _wpfOver = new List<FrameworkElement>();
        foreach (var element in held)
        {
            Raise(element, enter: false, OverlaySource.Wpf, at);
        }
    }

    /// <summary>What WPF hit-tests at <paramref name="at"/> (InputHitTest, so IsHitTestVisible counts), then its ancestors below the canvas.</summary>
    private List<FrameworkElement> HitChain(Point at)
    {
        var chain = new List<FrameworkElement>();
        for (var node = _canvas.InputHitTest(at) as DependencyObject; node != null && !ReferenceEquals(node, _canvas); node = Parent(node))
        {
            if (node is FrameworkElement element)
            {
                chain.Add(element);
            }
        }

        return chain;
    }

    private static DependencyObject? Parent(DependencyObject node) =>
        node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);

    private void Raise(FrameworkElement element, bool enter, OverlaySource source, Point cursor)
    {
        var routed = enter ? UIElement.MouseEnterEvent : UIElement.MouseLeaveEvent;
        _events.Add(new OverlayEvent(Ticks, source, enter, element, cursor, IsCursorOver(element)));
        element.RaiseEvent(source == OverlaySource.Probe ? new ProbeMouseEventArgs(routed) : new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = routed });
    }

    private static Group GroupOf(Dictionary<DependencyObject, Group> groups, DependencyObject root)
    {
        if (!groups.TryGetValue(root, out var group))
        {
            group = groups[root] = new Group();
        }

        return group;
    }

    /// <summary>UpdateHoverable's OverlayElement: the hoverables and clickables under the cursor in one child of the canvas, and its index.</summary>
    private sealed class Group
    {
        public List<FrameworkElement> Hoverables { get; } = new();

        public List<FrameworkElement> Clickables { get; } = new();

        public int Index { get; set; } = -1;
    }

    /// <summary>OverlayWindow.ElementCache: a dictionary emptied when older than its age.</summary>
    private sealed class ElementCache<T>
    {
        private readonly TimeSpan _maxAge;
        private DateTime _cacheDate = DateTime.MinValue;

        public ElementCache(TimeSpan maxAge) => _maxAge = maxAge;

        public Dictionary<FrameworkElement, T> Dict { get; } = new();

        public bool IsInvalid => DateTime.Now.Subtract(_cacheDate) > _maxAge;

        public void Clear()
        {
            Dict.Clear();
            _cacheDate = DateTime.Now;
        }
    }
}
