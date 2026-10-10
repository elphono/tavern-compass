using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

// What the plugin's panels ask of Hearthstone Deck Tracker, answered without it. The namespaces are HDT's own, so the
// panel sources compile unchanged. Only the members the panels use exist here.

namespace Hearthstone_Deck_Tracker.Utility.Extensions
{
    /// <summary>
    /// HDT's attached properties that let clicks and hover through its click-through overlay window, registered as HDT
    /// registers them (Utility/Extensions/OverlayExtensions.cs, HDT 1.58.9 decompiled, lines 14-146): setting one raises
    /// <see cref="OnRegisterHitTestVisible"/> or <see cref="OnRegisterHoverVisible"/> when its value changes, and the
    /// element is registered again at its Loaded and unregistered at its Unloaded while the value is true. HDT's overlay
    /// window keeps the lists (OverlayWindow ctor, lines 4280-4301); the simulation's one is
    /// <see cref="BronzebeardHud.Harness.HdtOverlay"/>, which only acts on them while its injected mouse runs. Without it,
    /// the plain window lets the mouse reach every element WPF hit-tests, as before.
    ///
    /// The tooltip is drawn as HDT draws it (<see cref="BronzebeardHud.Harness.HdtTooltip"/>): on the overlay canvas, in its
    /// one slot, where PreviewPlacer's placement and offsets put it, so that it shows in a capture and its place can be checked.
    /// </summary>
    public static class OverlayExtensions
    {
        public static readonly DependencyProperty IsOverlayHitTestVisibleProperty = DependencyProperty.RegisterAttached(
            "IsOverlayHitTestVisible", typeof(bool), typeof(OverlayExtensions), new FrameworkPropertyMetadata(false, OnHitTestVisibleChanged));

        public static readonly DependencyProperty IsOverlayHoverVisibleProperty = DependencyProperty.RegisterAttached(
            "IsOverlayHoverVisible", typeof(bool), typeof(OverlayExtensions), new FrameworkPropertyMetadata(false, OnHoverVisibleChanged));

        /// <summary>An element declared clickable (true) or no longer (false), as HDT raises it.</summary>
        public static event Action<FrameworkElement, bool>? OnRegisterHitTestVisible;

        /// <summary>An element declared hoverable (true) or no longer (false), as HDT raises it.</summary>
        public static event Action<FrameworkElement, bool>? OnRegisterHoverVisible;

        /// <summary>
        /// While true (the injected mouse runs, <see cref="BronzebeardHud.Harness.HdtOverlay"/>), the tooltip of a hoverable
        /// element answers HDT's probe only, as HDT's ShowTooltip and HideTooltip do (lines 222-238: a WPF MouseEnter or
        /// MouseLeave on an element declared hoverable is ignored). False: every MouseEnter, as the plain window had it.
        /// </summary>
        internal static bool ProbeOnlyOnHoverables { get; set; }

        public static bool GetIsOverlayHitTestVisible(DependencyObject element) => (bool)element.GetValue(IsOverlayHitTestVisibleProperty);

        public static void SetIsOverlayHitTestVisible(DependencyObject element, bool value) => element.SetValue(IsOverlayHitTestVisibleProperty, value);

        public static bool GetIsOverlayHoverVisible(DependencyObject element) => (bool)element.GetValue(IsOverlayHoverVisibleProperty);

        public static void SetIsOverlayHoverVisible(DependencyObject element, bool value) => element.SetValue(IsOverlayHoverVisibleProperty, value);

        /// <summary>
        /// Shown on the element's MouseEnter, hidden on its MouseLeave, as HDT's ToolTip property does
        /// (Utility/Extensions/OverlayExtensions.cs ShowTooltip/HideTooltip). The handlers come after the plugin's own
        /// MouseEnter (PreviewPlacer), which sets the placement first, as in HDT.
        /// </summary>
        public static void SetToolTip(FrameworkElement element, UIElement tip)
        {
            element.MouseEnter += (_, e) =>
            {
                if (Answers(element, e))
                {
                    BronzebeardHud.Harness.HdtTooltip.Show(element, (FrameworkElement)tip);
                }
            };
            element.MouseLeave += (_, e) =>
            {
                if (Answers(element, e))
                {
                    BronzebeardHud.Harness.HdtTooltip.Hide(element);
                }
            };
        }

        private static bool Answers(FrameworkElement element, System.Windows.Input.MouseEventArgs e) =>
            !ProbeOnlyOnHoverables || !GetIsOverlayHoverVisible(element) || e is BronzebeardHud.Harness.ProbeMouseEventArgs;

        private static void OnHitTestVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                Register(element, (bool)e.NewValue, HitTestLoaded, HitTestUnloaded, (x, on) => OnRegisterHitTestVisible?.Invoke(x, on));
            }
        }

        private static void OnHoverVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                Register(element, (bool)e.NewValue, HoverLoaded, HoverUnloaded, (x, on) => OnRegisterHoverVisible?.Invoke(x, on));
            }
        }

        private static void Register(FrameworkElement element, bool on, RoutedEventHandler loaded, RoutedEventHandler unloaded, Action<FrameworkElement, bool> raise)
        {
            element.Loaded -= loaded;
            element.Unloaded -= unloaded;
            if (on)
            {
                element.Loaded += loaded;
                element.Unloaded += unloaded;
            }

            raise(element, on);
        }

        private static void HitTestLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && GetIsOverlayHitTestVisible(element))
            {
                OnRegisterHitTestVisible?.Invoke(element, true);
            }
        }

        private static void HitTestUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && GetIsOverlayHitTestVisible(element))
            {
                OnRegisterHitTestVisible?.Invoke(element, false);
            }
        }

        private static void HoverLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && GetIsOverlayHoverVisible(element))
            {
                OnRegisterHoverVisible?.Invoke(element, true);
            }
        }

        private static void HoverUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && GetIsOverlayHoverVisible(element))
            {
                OnRegisterHoverVisible?.Invoke(element, false);
            }
        }
    }
}

namespace BronzebeardHud.Harness
{
    using System.Windows.Controls.Primitives;
    using System.Windows.Media;

    /// <summary>
    /// HDT's overlay tooltip, as Windows/OverlayWindow.cs SetTooltip draws it (HDT 1.58.6 decompiled, lines 2640-2805): ONE
    /// slot for the whole overlay, a grid on the canvas above the plugin's elements (ZIndex 0, theirs −1), that takes
    /// nothing while it holds a tooltip (`Children.Count > 0 → return`); placed from the hovered element's rectangle, its
    /// ToolTipService placement (Bottom, Top, Left, else Right) and offsets, flipped to the other side when it would leave
    /// the window and the other side has room, then kept inside the window; removed on that element's MouseLeave.
    /// </summary>
    internal static class HdtTooltip
    {
        public const string SlotTag = "hdt-tooltip";

        private static readonly Dictionary<Canvas, (Grid Slot, FrameworkElement? Owner)> Slots = new();

        /// <summary>The slot of the overlay canvas, and the tooltip it shows; null when none shows.</summary>
        public static FrameworkElement? Showing(Canvas canvas) =>
            Slots.TryGetValue(canvas, out var slot) && slot.Slot.Children.Count > 0 ? (FrameworkElement)slot.Slot.Children[0] : null;

        /// <summary>The slot's rectangle on the canvas, while it shows a tooltip.</summary>
        public static Rect? ShowingRect(Canvas canvas) =>
            Showing(canvas) is { } tip ? new Rect(Canvas.GetLeft(Slots[canvas].Slot), Canvas.GetTop(Slots[canvas].Slot), tip.ActualWidth, tip.ActualHeight) : null;

        public static void Show(FrameworkElement target, FrameworkElement tip)
        {
            if (Overlay(target) is not { } canvas)
            {
                return;
            }

            if (!Slots.TryGetValue(canvas, out var entry))
            {
                var grid = new Grid { IsHitTestVisible = false, Tag = SlotTag };
                Panel.SetZIndex(grid, 0);
                canvas.Children.Add(grid);
                entry = (grid, null);
            }

            var slot = entry.Slot;
            if (slot.Children.Count > 0)
            {
                return; // one tooltip at a time for the whole overlay
            }

            Point origin;
            try
            {
                origin = target.TransformToAncestor(canvas).Transform(new Point(0, 0));
            }
            catch (InvalidOperationException)
            {
                return;
            }

            slot.Children.Add(tip);
            Slots[canvas] = (slot, target);
            tip.UpdateLayout();
            double ew = target.ActualWidth, eh = target.ActualHeight, tw = tip.ActualWidth, th = tip.ActualHeight;
            double cw = canvas.ActualWidth, ch = canvas.ActualHeight;
            var placement = ToolTipService.GetPlacement(target) switch
            {
                PlacementMode.Bottom => PlacementMode.Bottom,
                PlacementMode.Left => PlacementMode.Left,
                PlacementMode.Top => PlacementMode.Top,
                _ => PlacementMode.Right,
            };
            placement = placement switch
            {
                PlacementMode.Right when origin.X + ew + tw > cw && origin.X - tw >= 0 => PlacementMode.Left,
                PlacementMode.Bottom when origin.Y + eh + th > ch && origin.Y - th >= 0 => PlacementMode.Top,
                PlacementMode.Left when origin.X - tw < 0 && origin.X + ew + tw <= cw => PlacementMode.Right,
                PlacementMode.Top when origin.Y - th < 0 && origin.Y + eh + th <= ch => PlacementMode.Bottom,
                _ => placement,
            };
            var dx = ToolTipService.GetHorizontalOffset(target);
            var dy = ToolTipService.GetVerticalOffset(target);
            var (x, y) = placement switch
            {
                PlacementMode.Right => (origin.X + ew + dx, origin.Y + eh / 2 - th / 2 + dy),
                PlacementMode.Bottom => (origin.X + ew / 2 - tw / 2 + dx, origin.Y + eh + dy),
                PlacementMode.Top => (origin.X + ew / 2 - tw / 2 + dx, origin.Y - th - dy),
                _ => (origin.X - tw - dx, origin.Y + eh / 2 - th / 2 + dy),
            };
            Canvas.SetLeft(slot, Math.Max(0, Math.Min(x, cw - tw)));
            Canvas.SetTop(slot, Math.Max(0, Math.Min(y, ch - th)));
        }

        public static void Hide(FrameworkElement target)
        {
            if (Overlay(target) is { } canvas && Slots.TryGetValue(canvas, out var entry) && ReferenceEquals(entry.Owner, target))
            {
                entry.Slot.Children.Clear();
                Slots[canvas] = (entry.Slot, null);
            }
        }

        /// <summary>The overlay canvas an element is drawn on: its first Canvas ancestor (the panels hold none of their own).</summary>
        private static Canvas? Overlay(DependencyObject element)
        {
            for (var up = VisualTreeHelper.GetParent(element); up != null; up = VisualTreeHelper.GetParent(up))
            {
                if (up is Canvas canvas)
                {
                    return canvas;
                }
            }

            return null;
        }
    }
}

namespace Hearthstone_Deck_Tracker.Utility.Logging
{
    /// <summary>HDT's log, written to the harness's log pane (and its file) in HDT's format: time|level|caller >> text.</summary>
    public static class Log
    {
        public static event Action<string>? Written;

        public static void Info(string message, [CallerMemberName] string member = "") => Write("Info", message, member);

        public static void Warn(string message, [CallerMemberName] string member = "") => Write("Warning", message, member);

        public static void Error(string message, [CallerMemberName] string member = "") => Write("Error", message, member);

        private static void Write(string level, string message, string member) =>
            Written?.Invoke($"{DateTime.Now:HH:mm:ss}|{level}|{member} >> {message}");
    }
}

namespace Hearthstone_Deck_Tracker.Hearthstone
{
    public class Card
    {
        public Card(string id, string name, int techLevel)
        {
            Id = id;
            LocalizedName = name;
            TechLevel = techLevel;
        }

        public string Id { get; }
        public string LocalizedName { get; }

        /// <summary>The tavern tier, 0 when the card has none (as HDT's).</summary>
        public int TechLevel { get; }
    }

    public static class Database
    {
        /// <summary>Where the harness answers "which card is this id?" (names and tiers from HearthstoneJSON, when it has them).</summary>
        public static Func<string, Card?> Lookup { get; set; } = id => new Card(id, id, 0);

        public static Card? GetCardFromId(string id) => Lookup(id);
    }
}

namespace Hearthstone_Deck_Tracker.Utility.Assets
{
    using Hearthstone_Deck_Tracker.Hearthstone;

    /// <summary>
    /// HDT's picture downloader, reduced to what CardImages uses: a memory then disk cache read at once
    /// (<see cref="TryGetAssetData"/>), and an asynchronous download on a miss (<see cref="GetAssetData"/>).
    /// </summary>
    public class AssetDownloader<TKey, TValue> where TValue : class
    {
        private readonly Func<TKey, string> _url;
        private readonly Func<TKey, string> _fileName;
        private readonly Func<string, TValue> _decode;
        private readonly string _directory;
        private readonly Dictionary<string, TValue> _memory = new();

        public AssetDownloader(string directory, Func<TKey, string> url, Func<TKey, string> fileName, Func<string, TValue> decode)
        {
            _directory = directory;
            _url = url;
            _fileName = fileName;
            _decode = decode;
        }

        public TValue? TryGetAssetData(TKey key)
        {
            var path = Path.Combine(_directory, _fileName(key));
            if (_memory.TryGetValue(path, out var known))
            {
                return known;
            }

            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return _memory[path] = _decode(path);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async System.Threading.Tasks.Task<TValue?> GetAssetData(TKey key)
        {
            var known = TryGetAssetData(key);
            if (known != null)
            {
                return known;
            }

            var path = Path.Combine(_directory, _fileName(key));
            try
            {
                Directory.CreateDirectory(_directory);
                var temp = path + ".part";
                using (var client = new System.Net.WebClient())
                {
                    await client.DownloadFileTaskAsync(_url(key), temp);
                }

                File.Delete(path);
                File.Move(temp, path);
                return TryGetAssetData(key);
            }
            catch (Exception)
            {
                return null; // offline, or no such picture: the vignette keeps its frame
            }
        }
    }

    public static class AssetDownloaders
    {
        public static AssetDownloader<Card, BitmapImage>? cardPortraitDownloader { get; private set; }
        public static AssetDownloader<Card, BitmapImage>? cardImageDownloader { get; private set; }
        public static AssetDownloader<Card, BitmapImage>? heroImageDownloader { get; private set; }

        /// <summary>Pictures from art.hearthstonejson.com, cached under <paramref name="directory"/>.</summary>
        public static void Initialize(string directory)
        {
            cardPortraitDownloader = new(Path.Combine(directory, "portraits"),
                c => $"https://art.hearthstonejson.com/v1/256x/{c.Id}.jpg", c => c.Id + ".jpg", Decode);
            cardImageDownloader = new(Path.Combine(directory, "cards"),
                c => $"https://art.hearthstonejson.com/v1/bgs/latest/enUS/256x/{c.Id}.png", c => c.Id + ".png", Decode);
            heroImageDownloader = new(Path.Combine(directory, "heroes"),
                c => $"https://art.hearthstonejson.com/v1/heroes/latest/256x/{c.Id}.png", c => c.Id + ".png", Decode);
        }

        private static BitmapImage Decode(string path)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
