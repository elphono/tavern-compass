using System;
using System.Threading;
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

        // HS window tracking timer — works on both Windows (P/Invoke) and WSL (helper exe)
        var hsService = new HsWindowService { OverlayWindowTitle = Title };
        var pollInterval = OperatingSystem.IsWindows()
            ? TimeSpan.FromMilliseconds(250)
            : TimeSpan.FromMilliseconds(500); // WSL helper is ~90ms, so 500ms is fine
        var timer = new DispatcherTimer { Interval = pollInterval };
        timer.Tick += (_, _) =>
        {
            var rect = hsService.GetHsWindowRect();
            if (rect != null)
            {
                // Position on the right side of the HS window, inside it
                var overlayWidth = (int)Width;
                Position = new Avalonia.PixelPoint(rect.X + rect.Width - overlayWidth, rect.Y);
                Height = rect.Height;
                if (!IsVisible) Show();
            }
            else
            {
                // HS not running or minimized — hide overlay
                if (IsVisible) Hide();
            }

            // Re-assert topmost (Avalonia property, reinforced by Win32 on WSL via helper)
            Topmost = false;
            Topmost = true;
        };
        timer.Start();
    }
}
