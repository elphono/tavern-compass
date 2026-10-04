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
    /// the mouse reaches every element WPF hit-tests, so the first two do nothing; the tooltip becomes a WPF one, so
    /// that hovering a card still shows the whole card, placed as PreviewPlacer asks.
    /// </summary>
    public static class OverlayExtensions
    {
        public static void SetIsOverlayHitTestVisible(UIElement element, bool value)
        {
        }

        public static void SetIsOverlayHoverVisible(UIElement element, bool value)
        {
        }

        public static void SetToolTip(FrameworkElement element, UIElement tip) =>
            element.ToolTip = new ToolTip { Content = tip, Background = null, BorderThickness = new Thickness(0), Padding = new Thickness(0), HasDropShadow = false };
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
