using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Assets;
using HdtCard = Hearthstone_Deck_Tracker.Hearthstone.Card;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// Card and hero pictures through HDT's own downloaders and cache (Utility/Assets/AssetDownloaders.cs:18-21):
/// card art from art.hearthstonejson.com/v1/256x/{id}.jpg, hero portraits from …/heroes/latest/256x/{id}.png,
/// both cached on disk and in memory by HDT (AssetDownloader.cs: GetAssetData 337, TryGetAssetData 400).
/// Nothing is downloaded on the UI thread: a cache hit is shown at once, a miss is filled in when it arrives.
/// </summary>
internal static class CardImages
{
    private static readonly Brush TickBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x40));

    public static FrameworkElement Vignette(string cardId, bool owned, double size, double scale)
    {
        var image = new Image { Width = size, Height = size, Stretch = Stretch.UniformToFill };
        var frame = new Border
        {
            Width = size,
            Height = size,
            Margin = new Thickness(0, 0, 0.08 * size, 0),
            CornerRadius = new CornerRadius(0.15 * size),
            BorderThickness = new Thickness(2 * scale),
            BorderBrush = owned ? TickBrush : Brushes.DimGray,
            ClipToBounds = true,
            Child = image,
            ToolTip = Database.GetCardFromId(cardId)?.LocalizedName ?? cardId,
        };
        var grid = new Grid { Children = { frame } };
        if (owned)
        {
            grid.Children.Add(new Border
            {
                Background = TickBrush,
                CornerRadius = new CornerRadius(0.12 * size),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0.08 * size, 0),
                Padding = new Thickness(3 * scale, 0, 3 * scale, 0),
                Child = new TextBlock { Text = "✓", FontSize = 0.3 * size, FontWeight = FontWeights.Bold, Foreground = Brushes.White },
            });
        }
        else
        {
            image.Opacity = 0.45;
        }

        Load(AssetDownloaders.cardPortraitDownloader, cardId, image, grey: !owned);
        return grid;
    }

    public static Image Hero(string heroCardId, double size)
    {
        var image = new Image { Width = size, Height = size, Stretch = Stretch.UniformToFill };
        Load(AssetDownloaders.heroImageDownloader, heroCardId, image, grey: false);
        return image;
    }

    private static async void Load(AssetDownloader<HdtCard, BitmapImage>? downloader, string cardId, Image target, bool grey)
    {
        // async void: every failure must be caught here, or it would take HDT down.
        try
        {
            var card = Database.GetCardFromId(cardId);
            if (downloader == null || card == null)
            {
                return;
            }

            var bitmap = downloader.TryGetAssetData(card) ?? await downloader.GetAssetData(card);
            if (bitmap == null)
            {
                return;
            }

            target.Source = grey ? new FormatConvertedBitmap(bitmap, PixelFormats.Gray8, null, 0) : bitmap;
        }
        catch (Exception)
        {
            // No picture: the vignette keeps its frame and tooltip.
        }
    }
}
