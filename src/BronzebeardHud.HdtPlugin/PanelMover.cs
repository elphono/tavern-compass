using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
/// </summary>
internal sealed class PanelMover
{
    private static readonly Brush MoveBorder = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF));

    private readonly Canvas _canvas;
    private readonly string _path;
    private readonly Dictionary<Border, (string Id, Brush Brush, Thickness Thickness, bool Interactive)> _panels = new();
    private readonly PanelLayout _layout;
    private Border? _dragged;
    private Point _grab;

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
    /// </summary>
    public void Place(Border panel, string panelId, LayoutRect defaultRect, bool interactive = false)
    {
        if (!_panels.ContainsKey(panel))
        {
            _panels[panel] = (panelId, panel.BorderBrush, panel.BorderThickness, interactive);
            panel.MouseLeftButtonDown += OnDown;
            panel.MouseMove += OnMove;
            panel.MouseLeftButtonUp += OnUp;
            Apply(panel);
        }

        if (_dragged == panel)
        {
            return;
        }

        var rect = _layout.Resolve(panelId, defaultRect, _canvas.ActualWidth, _canvas.ActualHeight);
        Canvas.SetLeft(panel, rect.Left);
        Canvas.SetTop(panel, rect.Top);
    }

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
    }

    private void Apply(Border panel)
    {
        var (_, brush, thickness, interactive) = _panels[panel];
        OverlayExtensions.SetIsOverlayHitTestVisible(panel, MoveMode);
        panel.IsHitTestVisible = MoveMode || interactive;
        panel.Cursor = MoveMode ? Cursors.SizeAll : null;
        panel.BorderBrush = MoveMode ? MoveBorder : brush;
        panel.BorderThickness = MoveMode ? new Thickness(3) : thickness;
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
