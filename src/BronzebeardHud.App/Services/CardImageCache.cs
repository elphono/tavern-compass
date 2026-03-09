using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace BronzebeardHud.App.Services;

public class CardImageCache
{
    public static readonly CardImageCache Instance = new();

    private readonly ConcurrentDictionary<string, Bitmap?> _cache = new();
    private readonly ConcurrentDictionary<string, bool> _loading = new();
    private readonly HttpClient _http = new();
    private readonly string _diskCachePath;

    public event Action<string>? ImageLoaded;

    private CardImageCache()
    {
        _diskCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BronzebeardHud", "cards");
        Directory.CreateDirectory(_diskCachePath);
    }

    public Bitmap? Get(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return null;

        if (_cache.TryGetValue(cardId, out var bitmap))
            return bitmap;

        // Start async download if not already loading
        if (_loading.TryAdd(cardId, true))
            _ = DownloadAsync(cardId);

        return null;
    }

    private async Task DownloadAsync(string cardId)
    {
        try
        {
            var diskPath = Path.Combine(_diskCachePath, $"{cardId}.png");

            // Check disk cache first
            if (File.Exists(diskPath))
            {
                var bitmap = new Bitmap(diskPath);
                _cache[cardId] = bitmap;
                NotifyLoaded(cardId);
                return;
            }

            // Download from HearthstoneJSON
            var url = $"https://art.hearthstonejson.com/v1/render/latest/enUS/256x/{cardId}.png";
            var data = await _http.GetByteArrayAsync(url);

            // Save to disk
            await File.WriteAllBytesAsync(diskPath, data);

            // Load into memory
            using var ms = new MemoryStream(data);
            var bmp = new Bitmap(ms);
            _cache[cardId] = bmp;
            NotifyLoaded(cardId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CardImageCache] Failed to load {cardId}: {ex.Message}");
            _cache[cardId] = null;
        }
    }

    private void NotifyLoaded(string cardId)
    {
        Dispatcher.UIThread.Post(() => ImageLoaded?.Invoke(cardId));
    }
}
