using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.Utility.Extensions;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// The "Skip combat" button, shown in combat only (<see cref="SkipCombatState"/>). Its own movable panel
/// ("skip-combat", PanelMover), by default at the right end of the player's board row (SkipCombatLayout).
/// Clickable while the overlay stays locked, like the boxes of the target panel; in move mode a click drags
/// it and never skips.
/// </summary>
internal sealed class SkipCombatPanel
{
    private readonly Canvas _canvas;
    private readonly PanelMover _mover;
    private readonly Border _panel;
    private readonly Border _button;
    private readonly TextBlock _label;
    private bool _visible;

    public SkipCombatPanel(Canvas canvas, PanelMover mover, Action skip)
    {
        _canvas = canvas;
        _mover = mover;
        _label = new TextBlock
        {
            Text = "Skip combat",
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _button = new Border
        {
            // Vivid yellow, black text: the only button of the plugin that acts on the game client.
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xDC, 0x00)),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Cursor = System.Windows.Input.Cursors.Hand,
            // "Skip combat" at PanelTypography.SkipCombat (15 px at 1080p) is about 95 px wide in a 130 px button: no shrinking.
            Child = _label,
        };
        _button.MouseLeftButtonUp += (_, e) =>
        {
            if (_mover.MoveMode)
            {
                return; // the panel is being dragged, not clicked
            }

            e.Handled = true;
            skip();
        };
        OverlayExtensions.SetIsOverlayHitTestVisible(_button, true);
        _panel = new Border { Child = _button, Visibility = Visibility.Collapsed };
        OverlayLayer.Add(_canvas, _panel);
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    /// <summary>Called on every update while the button should show: lays it out only when it appears.</summary>
    public void Show()
    {
        if (_visible)
        {
            return;
        }

        _visible = true;
        Relayout();
    }

    public void Hide()
    {
        _visible = false;
        _panel.Visibility = Visibility.Collapsed;
    }

    public void Detach()
    {
        _canvas.SizeChanged -= OnCanvasSizeChanged;
        _canvas.Children.Remove(_panel);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => Relayout();

    private void Relayout()
    {
        if (!_visible || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            _panel.Visibility = Visibility.Collapsed;
            return;
        }

        var rect = SkipCombatLayout.Button(_canvas.ActualWidth, _canvas.ActualHeight);
        var scale = TavernLayout.Scale(_canvas.ActualHeight);
        _button.Width = rect.Width;
        _button.Height = rect.Height;
        _label.FontSize = PanelTypography.SkipCombat * scale;
        _label.Margin = new Thickness(6 * scale, 0, 6 * scale, 0);
        _mover.Place(_panel, "skip-combat", rect, interactive: true);
        _panel.Visibility = Visibility.Visible;
    }
}
