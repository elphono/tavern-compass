using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BronzebeardHud.Harness;

/// <summary>
/// Command line. Without argument: the window, for hands-on debugging. <c>--selftest</c> and <c>--screenshot</c> run
/// without anyone at the keyboard (the window is parked far off screen), write their result under <c>--out</c> and
/// exit: 0 when everything passed. <c>--scenario n</c> picks the board held, <c>--tick a,b</c> ticks guides and
/// <c>--detail x</c> opens a guide's detail before the screenshot (a, b, x: a target's rank, "1", or a guide's name).
/// </summary>
internal sealed class Options
{
    public bool SelfTest { get; private set; }
    public bool Screenshot { get; private set; }
    public string Out { get; private set; } = Path.Combine(Path.GetTempPath(), "BronzebeardHarness", "out");
    public (int Width, int Height) Size { get; private set; } = (1920, 1080);
    public string? Layout { get; private set; }

    /// <summary>The board held (HarnessData.Scenarios): 2, three targets, by default.</summary>
    public int Scenario { get; private set; } = 2;

    /// <summary>A guide whose detail is opened before the screenshot: a target's rank ("1") or a guide's name; null: the list.</summary>
    public string? Detail { get; private set; }

    /// <summary>Guides ticked before the screenshot, as the player ticks them: a target's rank ("1") or a guide's name, comma-separated.</summary>
    public IReadOnlyList<string> Tick { get; private set; } = Array.Empty<string>();

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

                if (options.Detail != null)
                {
                    window.OpenDetail(options.Detail);
                }

                Pump(options.Wait);
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

    /// <summary>Lets the window work (downloads land, panels redraw) for a while, then comes back.</summary>
    private static void Pump(int milliseconds)
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
