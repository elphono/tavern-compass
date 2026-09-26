using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Assets;
using Hearthstone_Deck_Tracker.Utility.Extensions;
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

    public static FrameworkElement Vignette(string cardId, bool owned, double size, double scale, double previewHeight, bool previewOnLeft)
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
        };

        // Hover shows the whole card, through HDT's own overlay tooltips: the overlay lets clicks through,
        // so WPF never sees the mouse, but HDT polls the cursor at 60 Hz over elements declared hoverable
        // (Windows/OverlayWindow.MouseOverDetection.cs:419-430, 493-494) and raises MouseEnter/MouseLeave on
        // them, which its ToolTip attached property turns into a tooltip drawn in the overlay, flipped and
        // kept inside the window (Utility/Extensions/OverlayExtensions.Tooltip.cs:34-47, 90-118;
        // Windows/OverlayWindow.Tooltips.cs:34-175). Hover-only: the game keeps every click.
        OverlayExtensions.SetIsOverlayHoverVisible(frame, true);
        OverlayExtensions.SetToolTip(frame, FullCard(cardId, previewHeight));
        ToolTipService.SetInitialShowDelay(frame, 0);
        ToolTipService.SetPlacement(frame, previewOnLeft ? PlacementMode.Left : PlacementMode.Right);
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

    /// <summary>
    /// The whole card as HDT's card tooltip shows it: its Battlegrounds render, 256 or 512 px wide
    /// (AssetDownloaders.cardImageDownloader, Utility/Assets/AssetDownloaders.cs:20, 59-65), fetched the
    /// first time the preview is shown.
    /// </summary>
    public static FrameworkElement FullCard(string cardId, double height)
    {
        var image = new Image { Height = height, Width = height * 256 / 388, Stretch = Stretch.Uniform, IsHitTestVisible = false };
        var loaded = false;
        image.Loaded += (_, _) =>
        {
            if (!loaded)
            {
                loaded = true;
                Load(AssetDownloaders.cardImageDownloader, cardId, image, grey: false);
            }
        };
        return image;
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
