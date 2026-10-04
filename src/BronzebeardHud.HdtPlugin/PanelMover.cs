using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// Lets the player drag the plugin's panels and remembers where they were dropped.
///
/// HDT's own "unlock overlay" mode cannot take plugin elements: its movable elements live in a private
/// dictionary (Windows/OverlayWindow.xaml.cs:82) handled element by element in
/// MouseInputOnMouseMoved (Windows/OverlayWindow.Input.cs:32-210), and its unlocked state is a private
/// field (OverlayWindow.xaml.cs:100) that UnlockUi() toggles rather than reads (OverlayWindow.Input.cs:285).
/// What a plugin can use is OverlayExtensions.SetIsOverlayHitTestVisible: HDT then lets clicks through to
/// the element while the cursor is over it (Utility/Extensions/OverlayExtensions.cs:25-41, registered in
/// OverlayWindow.xaml.cs:194-200). So panels are clickable only in the plugin's own move mode, switched
/// from HDT's Plugins menu; outside it the overlay stays click-through above the game.
///
/// In the same mode a panel that declared a <see cref="PanelResize"/> gets a handle at its bottom right corner and a
/// dashed frame around the room it has. Dragging the handle gives the panel more or less room, never a zoom: the
/// panel redraws itself in the box (PanelResize.Relayout) and shows as many lines as fit, at the same text size.
/// The handle and the frame are elements of the canvas, not children of the panel, because a panel replaces its
/// whole content at every redraw.
/// </summary>
internal sealed class PanelMover
{
    private static readonly Brush MoveBorder = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF));

    /// <summary>The corner handle's side, in design pixels at 1080p (× TavernLayout.Scale).</summary>
    private const double HandleSize = 22;

    private sealed class Resizer
    {
        public Resizer(string id, Border handle, Rectangle frame, PanelResize resize)
        {
            Id = id;
            Handle = handle;
            Frame = frame;
            Resize = resize;
        }

        public string Id { get; }
        public Border Handle { get; }
        public Rectangle Frame { get; }
        public PanelResize Resize { get; set; }

        /// <summary>The rectangle the last <see cref="PanelMover.Place"/> gave the panel.</summary>
        public LayoutRect Placed { get; set; }
    }

    private readonly Canvas _canvas;
    private readonly string _path;
    private readonly Dictionary<Border, (string Id, Brush Brush, Thickness Thickness, bool Interactive)> _panels = new();
    private readonly Dictionary<Border, Resizer> _resizers = new();
    private readonly PanelLayout _layout;
    private Border? _dragged;
    private Point _grab;
    private Border? _resized;
    private Point _handleGrab;

    public PanelMover(Canvas canvas, string layoutPath)
    {
        _canvas = canvas;
        _path = layoutPath;
        string? json = null;
        try
        {
            json = File.Exists(layoutPath) ? File.ReadAllText(layoutPath) : null;
        }
        catch (IOException e)
        {
            Log.Warn($"Bronzebeard HUD: cannot read {layoutPath}: {e.Message}");
        }

        string? error;
        (_layout, error) = PanelLayout.Parse(json);
        if (error != null)
        {
            Log.Warn("Bronzebeard HUD: " + error);
        }
    }

    public bool MoveMode { get; private set; }

    /// <summary>
    /// Puts a panel at its remembered (or default) place, and makes it draggable in move mode. An
    /// <paramref name="interactive"/> panel keeps WPF hit-testing on outside move mode, so that its own
    /// clickable children (declared to HDT one by one) receive their clicks; the panel as a whole is still
    /// declared clickable to HDT only in move mode, so the game keeps its clicks around those children.
    /// A panel given a <paramref name="resize"/> can also be resized in move mode (pass it at every call: the
    /// minimum follows the window's scale). Returns the rectangle the panel has: its default one, or the one the
    /// player gave it, raised to the minimum and kept inside the overlay.
    /// </summary>
    public LayoutRect Place(Border panel, string panelId, LayoutRect defaultRect, bool interactive = false, PanelResize? resize = null)
    {
        if (!_panels.ContainsKey(panel))
        {
            _panels[panel] = (panelId, panel.BorderBrush, panel.BorderThickness, interactive);
            panel.MouseLeftButtonDown += OnDown;
            panel.MouseMove += OnMove;
            panel.MouseLeftButtonUp += OnUp;
            if (resize != null)
            {
                AddResizer(panel, panelId, resize);
            }

            Apply(panel);
        }

        var rect = _layout.Resolve(panelId, defaultRect, _canvas.ActualWidth, _canvas.ActualHeight, resize?.Minimum);
        if (_dragged != panel)
        {
            Canvas.SetLeft(panel, rect.Left);
            Canvas.SetTop(panel, rect.Top);
        }

        if (resize != null && _resizers.TryGetValue(panel, out var resizer))
        {
            resizer.Resize = resize;
            resizer.Placed = rect;
            UpdateResizer(panel);
        }

        return rect;
    }

    /// <summary>Takes the handles and frames off the canvas, so that a plugin disabled in move mode leaves none behind.</summary>
    public void Detach()
    {
        foreach (var resizer in _resizers.Values)
        {
            _canvas.Children.Remove(resizer.Handle);
            _canvas.Children.Remove(resizer.Frame);
        }

        _resizers.Clear();
    }

    /// <summary>True once the player gave this panel a size: its content then has that room, not the default one.</summary>
    public bool IsResized(string panelId) => _layout.IsResized(panelId);

    public void ToggleMoveMode()
    {
        MoveMode = !MoveMode;
        foreach (var panel in _panels.Keys)
        {
            Apply(panel);
        }

        if (!MoveMode)
        {
            Save();
        }
    }

    /// <summary>Back to the default places for every panel.</summary>
    public void Reset()
    {
        foreach (var id in new List<string>(_layout.MovedPanels))
        {
            _layout.Forget(id);
        }

        Save();
        foreach (var resizer in _resizers.Values)
        {
            resizer.Resize.Relayout(); // back to the default room at once, not at the next update
        }
    }

    private void Apply(Border panel)
    {
        var (_, brush, thickness, interactive) = _panels[panel];
        OverlayExtensions.SetIsOverlayHitTestVisible(panel, MoveMode);
        panel.IsHitTestVisible = MoveMode || interactive;
        panel.Cursor = MoveMode ? Cursors.SizeAll : null;
        panel.BorderBrush = MoveMode ? MoveBorder : brush;
        panel.BorderThickness = MoveMode ? new Thickness(3) : thickness;
        if (_resizers.TryGetValue(panel, out var resizer))
        {
            OverlayExtensions.SetIsOverlayHitTestVisible(resizer.Handle, MoveMode);
            resizer.Handle.IsHitTestVisible = MoveMode;
            UpdateResizer(panel);
        }
    }

    private void AddResizer(Border panel, string panelId, PanelResize resize)
    {
        var frame = new Rectangle
        {
            Stroke = MoveBorder,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        var handle = new Border
        {
            Background = MoveBorder,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.SizeNWSE,
            Tag = panel,
            Visibility = Visibility.Collapsed,
            Child = new TextBlock { Text = "◢", Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom },
        };
        handle.MouseLeftButtonDown += OnHandleDown;
        handle.MouseMove += OnHandleMove;
        handle.MouseLeftButtonUp += OnHandleUp;
        OverlayLayer.Add(_canvas, frame);
        OverlayLayer.Add(_canvas, handle);
        _resizers[panel] = new Resizer(panelId, handle, frame, resize);
        panel.SizeChanged += (_, _) => UpdateResizer(panel);
        panel.IsVisibleChanged += (_, _) => UpdateResizer(panel);
    }

    /// <summary>
    /// Puts the frame and the handle around the room the panel has: the box it was given when it was resized (never
    /// smaller than the content it shows), otherwise the box it takes now. Shown in move mode, on a visible panel only.
    /// </summary>
    private void UpdateResizer(Border panel)
    {
        if (!_resizers.TryGetValue(panel, out var resizer))
        {
            return;
        }

        var left = Canvas.GetLeft(panel);
        var top = Canvas.GetTop(panel);
        var show = MoveMode && panel.Visibility == Visibility.Visible && panel.ActualWidth > 0 && _canvas.ActualHeight > 0
                   && !double.IsNaN(left) && !double.IsNaN(top);
        resizer.Frame.Visibility = resizer.Handle.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show)
        {
            return;
        }

        var width = panel.ActualWidth;
        var height = _layout.IsResized(resizer.Id) ? Math.Max(resizer.Placed.Height, panel.ActualHeight) : panel.ActualHeight;
        var side = HandleSize * TavernLayout.Scale(_canvas.ActualHeight);
        Canvas.SetLeft(resizer.Frame, left);
        Canvas.SetTop(resizer.Frame, top);
        resizer.Frame.Width = width;
        resizer.Frame.Height = height;
        resizer.Handle.Width = resizer.Handle.Height = side;
        ((TextBlock)resizer.Handle.Child).FontSize = PanelTypography.RoundButton * TavernLayout.Scale(_canvas.ActualHeight);
        Canvas.SetLeft(resizer.Handle, left + width - side);
        Canvas.SetTop(resizer.Handle, top + height - side);
    }

    private void OnHandleDown(object sender, MouseButtonEventArgs e)
    {
        if (!MoveMode || sender is not Border { Tag: Border panel } handle)
        {
            return;
        }

        _resized = panel;
        _handleGrab = e.GetPosition(handle);
        handle.CaptureMouse();
        e.Handled = true;
    }

    private void OnHandleMove(object sender, MouseEventArgs e)
    {
        if (_resized is not { } panel || sender is not Border { Tag: Border owner } || owner != panel || !_resizers.TryGetValue(panel, out var resizer))
        {
            return;
        }

        // The handle sits inside the box's bottom right corner, so the corner is where the pointer is, less where
        // the handle was grabbed, plus the handle's own side.
        var pointer = e.GetPosition(_canvas);
        var side = resizer.Handle.Width;
        _layout.Resize(resizer.Id, resizer.Placed, pointer.X - _handleGrab.X + side, pointer.Y - _handleGrab.Y + side,
            resizer.Resize.Minimum, _canvas.ActualWidth, _canvas.ActualHeight);
        resizer.Resize.Relayout(); // the panel shows what fits in the new box, right now: "2 of 8 shown" follows the hand
        UpdateResizer(panel);
        e.Handled = true;
    }

    private void OnHandleUp(object sender, MouseButtonEventArgs e)
    {
        if (_resized == null || sender is not Border handle)
        {
            return;
        }

        _resized = null;
        handle.ReleaseMouseCapture();
        Save();
        e.Handled = true;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (!MoveMode || sender is not Border panel)
        {
            return;
        }

        _dragged = panel;
        _grab = e.GetPosition(panel);
        panel.CaptureMouse();
        e.Handled = true;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_dragged == null || sender != _dragged)
        {
            return;
        }

        var position = e.GetPosition(_canvas);
        Canvas.SetLeft(_dragged, position.X - _grab.X);
        Canvas.SetTop(_dragged, position.Y - _grab.Y);
        UpdateResizer(_dragged);
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragged == null || sender != _dragged)
        {
            return;
        }

        var panel = _dragged;
        _dragged = null;
        panel.ReleaseMouseCapture();
        _layout.Store(_panels[panel].Id, Canvas.GetLeft(panel), Canvas.GetTop(panel), _canvas.ActualWidth, _canvas.ActualHeight);
        Save();
        UpdateResizer(panel);
        e.Handled = true;
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, _layout.Serialize());
            if (File.Exists(_path))
            {
                File.Replace(temp, _path, null);
            }
            else
            {
                File.Move(temp, _path);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Bronzebeard HUD: cannot save {_path}: {e.Message}");
        }
    }
}

/// <summary>
/// What a panel that can be resized tells <see cref="PanelMover"/>: the smallest box it accepts (overlay pixels)
/// and how to redraw itself in whatever room it is given, which the mover asks for while the handle is dragged.
/// </summary>
internal sealed class PanelResize
{
    public PanelResize(double minWidth, double minHeight, Action relayout)
    {
        Minimum = (minWidth, minHeight);
        Relayout = relayout;
    }

    public (double Width, double Height) Minimum { get; }

    public Action Relayout { get; }
}
