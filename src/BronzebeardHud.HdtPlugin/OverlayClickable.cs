using System.Windows;
using Hearthstone_Deck_Tracker.Utility.Extensions;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// Declares an element clickable to HDT (OverlayExtensions.IsOverlayHitTestVisible) once it is loaded, never before.
///
/// HDT 1.58.9 and 1.58.10 (decompiled) put a clickable in OverlayWindow._clickableElements, a List, when the property turns
/// true (OverlayExtensions.OnIsOverlayHitTestVisibleChange), again at each Loaded of the element
/// (HitTestVisible_OnElementLoaded), and take it out once at each Unloaded (HitTestVisible_OnElementUnloaded). An element
/// declared before its first Loaded is in that list twice while it is loaded, and stays in it once when it is unloaded: a dead
/// entry, holding the element, at every redraw of a panel rebuilt from new elements (issue #17: 38 more entries per redraw
/// of the "Compositions" panel in the simulation, measured on 2026-10-10).
///
/// Declared at its Loaded, the element is listed once: HDT's own Loaded handler, added while the event is being raised, is
/// not called for that event (WPF builds an event's route before it calls the handlers); HDT then takes the element out at
/// its Unloaded and puts it back at a later Loaded, and the property, already true, is not set again. What catches the mouse
/// does not change: HDT counts a clickable only while it is loaded (OverlayWindow.ElementContains), so an element is
/// clickable from its Loaded on either way; and one taken away before it was ever loaded is now never listed at all.
///
/// The only way the plugin declares a clickable: a test refuses SetIsOverlayHitTestVisible anywhere else, PanelMover aside
/// (it switches the property on and off on the panels themselves when the move mode changes, not on elements rebuilt at
/// every redraw).
/// </summary>
internal static class OverlayClickable
{
    public static void Declare(FrameworkElement element)
    {
        if (element.IsLoaded)
        {
            OverlayExtensions.SetIsOverlayHitTestVisible(element, true);
            return;
        }

        element.Loaded += OnFirstLoaded;
    }

    private static void OnFirstLoaded(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        element.Loaded -= OnFirstLoaded;
        OverlayExtensions.SetIsOverlayHitTestVisible(element, true);
    }
}
