using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// Where the whole-card preview of a hovered vignette goes: beside the panel that holds it, wherever that panel
/// was moved, inside the window (TavernLayout.PreviewRect), handed to HDT's tooltip as a placement and offsets
/// (TavernLayout.HdtTooltipOffsets). Shared by every panel that shows card vignettes.
/// </summary>
internal static class PreviewPlacer
{
    public static void Place(Canvas canvas, FrameworkElement panel, FrameworkElement vignette)
    {
        if (canvas.ActualWidth <= 0 || double.IsNaN(Canvas.GetLeft(panel)) || double.IsNaN(Canvas.GetTop(panel)))
        {
            return;
        }

        // Bounds as drawn: the panel's content may be shrunk to fit the window, and HDT measures the scaled size.
        var bounds = vignette.TransformToAncestor(canvas).TransformBounds(new Rect(0, 0, vignette.ActualWidth, vignette.ActualHeight));
        var target = new LayoutRect(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, bounds.Width, bounds.Height);
        var box = new LayoutRect(Canvas.GetLeft(panel) + panel.ActualWidth / 2, Canvas.GetTop(panel) + panel.ActualHeight / 2,
            panel.ActualWidth, panel.ActualHeight);
        var (side, preview) = TavernLayout.PreviewRect(box, target, canvas.ActualWidth, canvas.ActualHeight);
        var (offsetX, offsetY) = TavernLayout.HdtTooltipOffsets(side, preview, target);
        ToolTipService.SetPlacement(vignette, side switch
        {
            PreviewSide.Left => PlacementMode.Left,
            PreviewSide.Right => PlacementMode.Right,
            PreviewSide.Above => PlacementMode.Top,
            _ => PlacementMode.Bottom,
        });
        ToolTipService.SetHorizontalOffset(vignette, offsetX);
        ToolTipService.SetVerticalOffset(vignette, offsetY);
    }
}
