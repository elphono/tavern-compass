using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using BronzebeardHud.App.Services;
using BronzebeardHud.App.ViewModels;
using BronzebeardHud.LogParser;

namespace BronzebeardHud.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainViewModel();
        DataContext = viewModel;

        // Start game state engine on background thread
        var service = new GameStateService(viewModel, LogPaths.DefaultLogsDir());
        _ = service.RunAsync(CancellationToken.None);

        if (OperatingSystem.IsWindows())
        {
            // Apply Win32 overlay styles once the window has a native handle
            Opened += (_, _) =>
            {
                var platformHandle = TryGetPlatformHandle();
                if (platformHandle != null)
                    HsWindowService.ApplyOverlayExStyle(platformHandle.Handle);
            };
        }

        // HS window tracking — runs helper on background thread
        // On WSL, Avalonia Position doesn't work (WSLg ignores X11 move requests),
        // so the helper moves the overlay via Win32 SetWindowPos directly.
        var overlayWidth = 400;
        var hsService = new HsWindowService { OverlayWindowTitle = Title };
        var pollMs = OperatingSystem.IsWindows() ? 250 : 500;
        _ = Task.Run(async () =>
        {
            var tickCount = 0;
            while (true)
            {
                await Task.Delay(pollMs);
                try
                {
                    var rect = hsService.GetHsWindowRect();
                    if (tickCount++ % 20 == 0)
                        Console.WriteLine($"[Overlay] tick={tickCount} rect={rect?.X},{rect?.Y},{rect?.Width},{rect?.Height} desired={hsService.DesiredOverlayRect?.X},{hsService.DesiredOverlayRect?.Y}");

                    if (rect != null)
                    {
                        // Calculate where overlay should go: right side of HS window
                        var ox = rect.X + rect.Width - overlayWidth;
                        var oy = rect.Y;
                        var oh = rect.Height;
                        // Set desired position for next helper call (WSL: helper moves it via Win32)
                        hsService.DesiredOverlayRect = new HsWindowService.WindowRect(ox, oy, overlayWidth, oh);

                        Dispatcher.UIThread.Post(() =>
                        {
                            // On Windows, Avalonia Position works fine
                            if (OperatingSystem.IsWindows())
                            {
                                Position = new Avalonia.PixelPoint(ox, oy);
                                Height = oh;
                            }
                            if (!IsVisible) Show();
                            Topmost = false;
                            Topmost = true;
                        });
                    }
                    else
                    {
                        hsService.DesiredOverlayRect = null;
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (IsVisible) Hide();
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Overlay] Error: {ex.Message}");
                }
            }
        });
    }
}
