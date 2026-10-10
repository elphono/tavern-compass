using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BronzebeardHud.Harness;

/// <summary>
/// Command line. Without argument: the window, for hands-on debugging. <c>--selftest</c> and <c>--screenshot</c> run
/// without anyone at the keyboard (the window is parked far off screen), write their result under <c>--out</c> and
/// exit: 0 when everything passed. <c>--scenario n</c> picks the board held, <c>--tick a,b</c> ticks guides,
/// <c>--choice k</c> opens a choice above the scene (discover, dark-gift, trinket), <c>--close-choice</c> closes it again
/// before the screenshot (the scene restored), <c>--power p</c> shows the board's power p in the inset (behind, even,
/// ahead, shiny, none, early), <c>--opp-power p</c> the opponent's (behind, even, ahead, shiny, none, next, unseen),
/// <c>--play</c> switches move mode off (as in a game), <c>--count n</c> clicks − or + until n compositions are wanted (move
/// mode off: the panel sized to them), and <c>--detail x</c> opens a guide's
/// detail before the screenshot (a, b, x: a target's rank, "1", or a guide's name). <c>--hover x</c> hovers a guide's
/// line (move mode off) so that its popup shows in the screenshot, <c>--hover-card k</c> also hovers the k-th oval of that
/// line (its card preview), <c>--no-skip</c> hides the Skip combat button, as in the tavern, and <c>--heroes</c> shows the
/// hero selection's badges (HarnessData.HeroRows: a consensus, a contest, one source, no data) over the scene.
/// <c>--mouse steps</c> drives the scene through HDT's overlay layer as the harness reproduces it (HdtOverlay: the probe on
/// an injected cursor, click-through outside the elements declared clickable), steps separated by ";" (HarnessWindow.RunMouse).
/// </summary>
internal sealed class Options
{
    public bool SelfTest { get; private set; }
    public bool Screenshot { get; private set; }
    public string Out { get; private set; } = Path.Combine(Path.GetTempPath(), "BronzebeardHarness", "out");
    public (int Width, int Height) Size { get; private set; } = (1920, 1080);
    public string? Layout { get; private set; }

    /// <summary>The board held (HarnessData.Scenarios): 2, three targets, by default.</summary>
    public int Scenario { get; private set; } = HarnessData.DefaultScenario;

    /// <summary>The board's power in the inset (HarnessData.PowerScenes: behind, even, ahead, shiny, none, early); null: even.</summary>
    public string? Power { get; private set; }

    /// <summary>The opponent's power in the inset (HarnessData.OpponentPowerScenes: behind, even, ahead, shiny, none, next, unseen); null: even.</summary>
    public string? OpponentPower { get; private set; }

    /// <summary>
    /// How many compositions are wanted before the screenshot (1 to 4), reached by clicking − or + in the inset as the player
    /// does, move mode off: the panel sized to them. 0: the default (3), nothing clicked.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>Move mode off before the screenshot (as in a game): the panel shows itself as the player sees it.</summary>
    public bool Play { get; private set; }

    /// <summary>A guide whose detail is opened before the screenshot: a target's rank ("1") or a guide's name; null: the list.</summary>
    public string? Detail { get; private set; }

    /// <summary>Guides ticked before the screenshot, as the player ticks them: a target's rank ("1") or a guide's name, comma-separated.</summary>
    public IReadOnlyList<string> Tick { get; private set; } = Array.Empty<string>();

    /// <summary>The choice open above the scene, as ChoiceClassifier names it (discover, dark-gift, trinket); null: none.</summary>
    public string? Choice { get; private set; }

    /// <summary>With <see cref="Choice"/>, the choice is closed again before the screenshot: the scene as the plugin restores it.</summary>
    public bool CloseChoice { get; private set; }

    /// <summary>A guide whose line is hovered before the screenshot (its popup shows): a target's rank or a guide's name; null: none.</summary>
    public string? Hover { get; private set; }

    /// <summary>With <see cref="Hover"/>, the oval of that line also hovered, from 1: its card preview shows too; 0: none.</summary>
    public int HoverCard { get; private set; }

    /// <summary>The Skip combat button hidden, as in the tavern (it shows in combat only in the plugin).</summary>
    public bool NoSkip { get; private set; }

    /// <summary>
    /// Steps of the injected mouse, run before the screenshot (HarnessWindow.RunMouse): "line:1;wait:300" hovers the first
    /// target's line through HDT's probe, "name:1;nudge;click" clicks its name; null: no injected mouse.
    /// </summary>
    public string? Mouse { get; private set; }

    /// <summary>Invented card stats at turn 6 (HarnessData.CardStats): the values on Bob's cards and in place of a choice's "—".</summary>
    public bool CardValues { get; private set; }

    /// <summary>The hero badges of the hero selection over the scene (component 3), from invented figures.</summary>
    public bool Heroes { get; private set; }

    /// <summary>Milliseconds the screenshot waits for card names and pictures, which arrive asynchronously.</summary>
    public int Wait { get; private set; } = 8000;

    public bool Headless => SelfTest || Screenshot;

    public static Options Parse(string[] args)
    {
        var options = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--selftest":
                    options.SelfTest = true;
                    break;
                case "--screenshot":
                    options.Screenshot = true;
                    break;
                case "--out" when i + 1 < args.Length:
                    options.Out = args[++i];
                    break;
                case "--layout" when i + 1 < args.Length:
                    options.Layout = args[++i];
                    break;
                case "--scenario" when i + 1 < args.Length:
                    options.Scenario = int.Parse(args[++i]);
                    break;
                case "--detail" when i + 1 < args.Length:
                    options.Detail = args[++i];
                    break;
                case "--tick" when i + 1 < args.Length:
                    options.Tick = args[++i].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    break;
                case "--choice" when i + 1 < args.Length:
                    options.Choice = args[++i];
                    break;
                case "--power" when i + 1 < args.Length:
                    options.Power = args[++i];
                    break;
                case "--opp-power" when i + 1 < args.Length:
                    options.OpponentPower = args[++i];
                    break;
                case "--count" when i + 1 < args.Length:
                    options.Count = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--play":
                    options.Play = true;
                    break;
                case "--close-choice":
                    options.CloseChoice = true;
                    break;
                case "--hover" when i + 1 < args.Length:
                    options.Hover = args[++i];
                    break;
                case "--hover-card" when i + 1 < args.Length:
                    options.HoverCard = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--no-skip":
                    options.NoSkip = true;
                    break;
                case "--mouse" when i + 1 < args.Length:
                    options.Mouse = args[++i];
                    break;
                case "--heroes":
                    options.Heroes = true;
                    break;
                case "--card-values":
                    options.CardValues = true;
                    break;
                case "--wait" when i + 1 < args.Length:
                    options.Wait = int.Parse(args[++i]);
                    break;
                case "--size" when i + 1 < args.Length:
                    var parts = args[++i].Split('x');
                    options.Size = (int.Parse(parts[0]), int.Parse(parts[1]));
                    break;
            }
        }

        return options;
    }
}

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var options = Options.Parse(args);
        var app = new Application();
        var window = new HarnessWindow(options);
        if (options.Headless)
        {
            window.ShowInTaskbar = false;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000;
            window.Top = -20000;
            window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => app.Shutdown(Headless.Run(window, options))));
        }

        return app.Run(window);
    }
}

internal static class Headless
{
    public static int Run(HarnessWindow window, Options options)
    {
        Directory.CreateDirectory(options.Out);
        var exit = 0;
        try
        {
            window.UpdateLayout();
            if (options.SelfTest)
            {
                var (passed, report) = SelfTest.Run(window);
                File.WriteAllText(Path.Combine(options.Out, "selftest.txt"), report);
                exit = passed ? 0 : 1;
            }

            if (options.Screenshot)
            {
                foreach (var which in options.Tick)
                {
                    window.Tick(which);
                }

                if (options.Play || options.Count > 0)
                {
                    window.SetMoveMode(false); // as in a game: the panel sized to its content, no handle
                    window.UpdateLayout();
                }

                if (options.Count > 0)
                {
                    window.ClickCountTo(options.Count);
                }

                if (options.Choice != null)
                {
                    // Advised again on the targets the ticks left; an unknown name fails the capture (error.txt).
                    window.ShowChoice(HarnessData.ChoiceOf(options.Choice));
                    if (options.CloseChoice)
                    {
                        window.UpdateLayout();
                        window.ShowChoice(BronzebeardHud.Stats.ChoiceKind.None); // what the player sees once it is made: everything back
                    }
                }

                if (options.Detail != null)
                {
                    window.OpenDetail(options.Detail);
                }

                if (options.NoSkip)
                {
                    window.ShowSkipCombat(false);
                }

                if (options.Hover != null)
                {
                    window.SetMoveMode(false); // no popup in move mode
                    window.UpdateLayout();
                    window.Hover(options.Hover);
                }

                if (options.Mouse != null)
                {
                    window.UpdateLayout();
                    window.RunMouse(options.Mouse);
                }

                if (options.Heroes)
                {
                    new BronzebeardHud.HdtPlugin.HeroPickPanel(window.Overlay).Show(HarnessData.HeroRows(), BronzebeardHud.Stats.PatchNotes.Banner(HarnessData.Nomi));
                }

                Pump(options.Wait);
                if (options.Hover != null && options.HoverCard > 0)
                {
                    window.UpdateLayout();
                    var ovals = HarnessWindow.Ovals(window.LineOf(options.Hover)); // drawn again meanwhile: the line as it is now
                    if (options.HoverCard > ovals.Count)
                    {
                        throw new ArgumentException($"--hover-card {options.HoverCard}: the line has {ovals.Count} ovals");
                    }

                    ovals[options.HoverCard - 1].RaiseEvent(new ProbeMouseEventArgs(UIElement.MouseEnterEvent)); // as HDT's probe raises it
                    Pump(Math.Min(options.Wait, 5000)); // the whole card's picture
                }

                if (options.Hover != null && !window.Comps.Popup.IsVisible)
                {
                    throw new InvalidOperationException($"--hover {options.Hover}: the popup did not show (see the log)");
                }

                Capture(window.Overlay, Path.Combine(options.Out, "shot.png"));
            }
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(options.Out, "error.txt"), e.ToString());
            exit = 2;
        }

        return exit;
    }

    /// <summary>Lets the window work (downloads land, panels redraw, timers tick) for a while, then comes back.</summary>
    internal static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    /// <summary>The overlay canvas at its own size, unscaled whatever the window shows (the host fits it with a Viewbox).</summary>
    private static void Capture(FrameworkElement overlay, string path)
    {
        var width = (int)overlay.ActualWidth;
        var height = (int)overlay.ActualHeight;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new VisualBrush(overlay), null, new Rect(0, 0, width, height));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
