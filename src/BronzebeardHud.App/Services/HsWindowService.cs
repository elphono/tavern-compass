using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace BronzebeardHud.App.Services;

public class HsWindowService
{
    public record WindowRect(int X, int Y, int Width, int Height);

    private static readonly bool s_isWsl = !OperatingSystem.IsWindows()
        && File.Exists("/proc/version")
        && File.ReadAllText("/proc/version").Contains("microsoft", StringComparison.OrdinalIgnoreCase);

    private static readonly string? s_wslHelperPath = FindWslHelper();

    private bool _lastForeground;

    /// <summary>
    /// Optional: set to the overlay window title so the WSL helper can force it topmost
    /// via Win32 SetWindowPos (works around WSLg not supporting _NET_WM_STATE_ABOVE).
    /// </summary>
    public string? OverlayWindowTitle { get; set; }

    public WindowRect? GetHsWindowRect()
    {
        if (OperatingSystem.IsWindows())
            return GetHsWindowRectWindows();
        if (s_isWsl && s_wslHelperPath != null)
            return GetHsWindowRectWsl(out _);
        return null;
    }

    public bool IsHsForeground()
    {
        if (OperatingSystem.IsWindows())
            return IsHsForegroundWindows();
        if (s_isWsl && s_wslHelperPath != null)
        {
            GetHsWindowRectWsl(out var fg);
            return fg;
        }
        return true;
    }

    // --- WSL: call Windows-side helper exe ---

    private static readonly string? s_wslDistroName = GetWslDistroName();

    private static string? FindWslHelper()
    {
        // Look for the helper at a known location
        var path = "/mnt/c/Temp/HsHelper/pub/HsHelper.exe";
        if (File.Exists(path))
        {
            Console.WriteLine($"[HsWindowService] WSL helper found: {path}");
            return path;
        }
        Console.WriteLine("[HsWindowService] WSL helper NOT found at " + path);
        return null;
    }

    private static string? GetWslDistroName()
    {
        try { return Environment.GetEnvironmentVariable("WSL_DISTRO_NAME"); }
        catch { return null; }
    }

    private WindowRect? GetHsWindowRectWsl(out bool isForeground)
    {
        isForeground = _lastForeground;
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = s_wslHelperPath!,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            // Pass overlay title so helper can force it topmost via Win32
            // WSLg appends " (<distro>)" to window titles
            if (!string.IsNullOrEmpty(OverlayWindowTitle))
            {
                var wslTitle = !string.IsNullOrEmpty(s_wslDistroName)
                    ? $"{OverlayWindowTitle} ({s_wslDistroName})"
                    : OverlayWindowTitle;
                proc.StartInfo.ArgumentList.Add(wslTitle);
            }
            proc.Start();
            var output = proc.StandardOutput.ReadLine();
            proc.WaitForExit(2000);

            if (string.IsNullOrEmpty(output) || output == "NOT_FOUND" || output == "MINIMIZED" || output == "ERROR")
                return null;

            // Format: Left,Top,Right,Bottom,IsForeground
            var parts = output.Split(',');
            if (parts.Length < 4) return null;

            var left = int.Parse(parts[0]);
            var top = int.Parse(parts[1]);
            var right = int.Parse(parts[2]);
            var bottom = int.Parse(parts[3]);
            isForeground = parts.Length > 4 && parts[4] == "1";
            _lastForeground = isForeground;

            return new WindowRect(left, top, right - left, bottom - top);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HsWindowService] WSL helper error: {ex.Message}");
            return null;
        }
    }

    // --- Windows native P/Invoke ---

    private static WindowRect? GetHsWindowRectWindows()
    {
        var hwnd = FindWindowW("UnityWndClass", "Hearthstone");
        if (hwnd == IntPtr.Zero) return null;
        if (IsIconic(hwnd)) return null;
        if (!GetWindowRect(hwnd, out var rect)) return null;
        return new WindowRect(rect.Left, rect.Top,
            rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    private static bool IsHsForegroundWindows()
    {
        var fg = GetForegroundWindow();
        var hs = FindWindowW("UnityWndClass", "Hearthstone");
        return fg == hs && hs != IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>
    /// Apply WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE to make window overlay-friendly.
    /// </summary>
    public static void ApplyOverlayExStyle(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero) return;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_NOACTIVATE = 0x08000000;
        var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
}
