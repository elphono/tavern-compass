using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

// What the plugin's panels ask of Hearthstone Deck Tracker, answered without it. The namespaces are HDT's own, so the
// panel sources compile unchanged. Only the members the panels use exist here.

namespace Hearthstone_Deck_Tracker.Utility.Extensions
{
    /// <summary>
    /// HDT's attached properties that let clicks and hover through its click-through overlay window. In a plain window
    /// the mouse reaches every element WPF hit-tests, so the first two do nothing. The tooltip is drawn as HDT draws it
    /// (<see cref="BronzebeardHud.Harness.HdtTooltip"/>): on the overlay canvas, in its one slot, where PreviewPlacer's
    /// placement and offsets put it, so that it shows in a capture and its place can be checked.
    /// </summary>
    public static class OverlayExtensions
    {
        public static void SetIsOverlayHitTestVisible(UIElement element, bool value)
        {
        }

        public static void SetIsOverlayHoverVisible(UIElement element, bool value)
        {
        }

        /// <summary>
        /// Shown on the element's MouseEnter, hidden on its MouseLeave, as HDT's ToolTip property does
        /// (Utility/Extensions/OverlayExtensions.cs ShowTooltip/HideTooltip). The handlers come after the plugin's own
        /// MouseEnter (PreviewPlacer), which sets the placement first, as in HDT.
        /// </summary>
        public static void SetToolTip(FrameworkElement element, UIElement tip)
        {
            element.MouseEnter += (_, _) => BronzebeardHud.Harness.HdtTooltip.Show(element, (FrameworkElement)tip);
            element.MouseLeave += (_, _) => BronzebeardHud.Harness.HdtTooltip.Hide(element);
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
