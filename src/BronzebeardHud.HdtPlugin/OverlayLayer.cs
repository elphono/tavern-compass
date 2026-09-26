using System.Windows;
using System.Windows.Controls;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// The only way the plugin puts an element on HDT's overlay canvas (Core.OverlayCanvas, which is HDT's
/// CanvasInfo: API/Core.cs:15).
///
/// HDT draws its overlay tooltips in a grid it keeps as the last child of that canvas so that they come out
/// on top (Windows/OverlayWindow.xaml:611-612, "The tooltip needs to stay the bottom item"), and adds the
/// tooltip there (Windows/OverlayWindow.Tooltips.cs:95). Children added by a plugin come after it, and at
/// equal ZIndex a later child is drawn above an earlier one: the card previews shown on hover were hidden
/// under the plugin's own panels (Ali, 2026-09-26). Every plugin element therefore gets a ZIndex below HDT's
/// default of 0, so HDT's tooltips, and HDT's own panels, stay above it.
/// </summary>
internal static class OverlayLayer
{
    /// <summary>Below every element HDT declares (they all keep the default 0, or more).</summary>
    public const int PluginZIndex = -1;

    public static void Add(Canvas canvas, UIElement element)
    {
        Panel.SetZIndex(element, PluginZIndex);
        canvas.Children.Add(element);
    }
}
