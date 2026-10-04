using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BronzebeardHud.Stats;
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
    /// <summary>Held card: solid green ring and a tick — the signal.</summary>
    private static readonly Brush TickBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x40));

    /// <summary>
    /// Card still to find: a light dashed ring, no tick (2026-09-27: a red ring on every missing card shouted for
    /// nothing; the green ring and tick of held cards are the signal). Every oval is in full colour.
    /// </summary>
    private static readonly Brush MissingBrush = new SolidColorBrush(Color.FromRgb(0xC9, 0xD1, 0xE0));

    /// <summary>Tier badge: yellow with black text, as in the game's tavern tier star.</summary>
    private static readonly Brush TierBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x3F));

    /// <summary>
    /// One card as an oval, cut like the game's minion portraits: <paramref name="width"/> wide,
    /// width × TavernLayout.OvalAspect tall, the central part of the art (TavernLayout.PortraitCut, as wide as the cut
    /// HDT gives its own minion portraits) clipped to an ellipse, in full colour; held: solid green ring
    /// and a tick, missing: light dashed ring. Badges (tick, tier) sit on the corners, at the text floor
    /// (PanelTypography.Badge), mostly outside the oval so they hide little of the art.
    /// </summary>
    /// <param name="placePreview">
    /// Called as the cursor enters the vignette, before HDT's own handler shows the preview (handlers of one
    /// element run in the order they were added), to set where the preview goes.
    /// </param>
    /// <param name="onClick">When given, a click on the vignette calls it with the card id (clickable while the overlay stays locked).</param>
    /// <param name="tier">When given, the tavern tier in a badge on the top left corner.</param>
    public static FrameworkElement Vignette(string cardId, bool owned, double width, double scale, double previewHeight, Action<FrameworkElement> placePreview,
        Action<string>? onClick = null, int? tier = null)
    {
        var height = width * TavernLayout.OvalAspect;
        var stroke = (owned ? 3 : 2) * scale;
        // Only the portrait's central part fills the oval (TavernLayout.PortraitCut): the whole square showed the white
        // margins around the art as a crescent inside the ring.
        var image = new Image
        {
            Width = width,
            Height = height,
            Stretch = Stretch.UniformToFill,
            Clip = new EllipseGeometry(new Point(width / 2, height / 2), width / 2 - stroke / 2, height / 2 - stroke / 2),
        };
        var ring = new System.Windows.Shapes.Ellipse
        {
            Width = width,
            Height = height,
            Stroke = owned ? TickBrush : MissingBrush,
            StrokeThickness = stroke,
            IsHitTestVisible = false,
        };
        if (!owned)
        {
            ring.StrokeDashArray = new DoubleCollection { 3, 2 }; // in stroke widths: 6 on, 4 off at 1080p
        }

        // The whole cell, not only the oval, catches the mouse: a transparent background makes it hit-testable.
        var cell = new Grid
        {
            Width = width,
            Height = height,
            Margin = new Thickness(0, 0, PanelFit.OvalGap * scale, 0),
            Background = Brushes.Transparent,
            Children = { image, ring },
        };

        // Hover shows the whole card, through HDT's own overlay tooltips: the overlay lets clicks through,
        // so WPF never sees the mouse, but HDT polls the cursor at 60 Hz over elements declared hoverable
        // (Windows/OverlayWindow.MouseOverDetection.cs:419-430, 493-494) and raises MouseEnter/MouseLeave on
        // them, which its ToolTip attached property turns into a tooltip drawn in the overlay, flipped and
        // kept inside the window (Utility/Extensions/OverlayExtensions.Tooltip.cs:34-47, 90-118;
        // Windows/OverlayWindow.Tooltips.cs:34-175). Hover-only: the game keeps every click.
        cell.MouseEnter += (_, _) => placePreview(cell);
        if (onClick != null)
        {
            // Hover still shows the card: HDT raises its hover events on a hoverable element even when it is
            // also clickable (Windows/OverlayWindow.MouseOverDetection.cs:537-548).
            OverlayExtensions.SetIsOverlayHitTestVisible(cell, true);
            cell.Cursor = System.Windows.Input.Cursors.Hand;
            cell.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick(cardId);
            };
        }

        OverlayExtensions.SetIsOverlayHoverVisible(cell, true);
        OverlayExtensions.SetToolTip(cell, FullCard(cardId, previewHeight));
        ToolTipService.SetInitialShowDelay(cell, 0);
        var badge = 16 * scale;
        var badgeFont = PanelTypography.Badge * scale;
        if (owned)
        {
            cell.Children.Add(new Border
            {
                Width = badge,
                Height = badge,
                CornerRadius = new CornerRadius(badge / 2),
                Background = TickBrush,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1.5 * scale),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -3 * scale, -2 * scale),
                IsHitTestVisible = false,
                Child = new TextBlock { Text = "✓", FontSize = badgeFont, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            });
        }

        if (tier is { } level)
        {
            cell.Children.Add(new Border
            {
                MinWidth = badge,
                Height = badge,
                CornerRadius = new CornerRadius(3 * scale),
                Background = TierBrush,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1.5 * scale),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(-3 * scale, -3 * scale, 0, 0),
                IsHitTestVisible = false,
                Child = new TextBlock { Text = level.ToString(System.Globalization.CultureInfo.InvariantCulture), FontSize = badgeFont, FontWeight = FontWeights.Bold, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            });
        }

        Load(AssetDownloaders.cardPortraitDownloader, cardId, image, CutPortrait);
        return cell;
    }

    /// <summary>
    /// The whole card as HDT's card tooltip shows it: its Battlegrounds render, 256 or 512 px wide
    /// (AssetDownloaders.cardImageDownloader, Utility/Assets/AssetDownloaders.cs:20, 59-65), fetched the
    /// first time the preview is shown.
    /// </summary>
    public static FrameworkElement FullCard(string cardId, double height)
    {
        var image = new Image { Height = height, Width = height * BronzebeardHud.Stats.TavernLayout.PreviewAspect, Stretch = Stretch.Uniform, IsHitTestVisible = false };
        var loaded = false;
        image.Loaded += (_, _) =>
        {
            if (!loaded)
            {
                loaded = true;
                Load(AssetDownloaders.cardImageDownloader, cardId, image);
            }
        };
        return image;
    }

    public static Image Hero(string heroCardId, double size)
    {
        var image = new Image { Width = size, Height = size, Stretch = Stretch.UniformToFill };
        Load(AssetDownloaders.heroImageDownloader, heroCardId, image);
        return image;
    }

    private static async void Load(AssetDownloader<HdtCard, BitmapImage>? downloader, string cardId, Image target, Func<BitmapSource, BitmapSource>? shape = null)
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

            target.Source = shape == null ? bitmap : shape(bitmap);
        }
        catch (Exception)
        {
            // No picture: the vignette keeps its frame and tooltip.
        }
    }

    /// <summary>
    /// The part of a card portrait that fills an oval (TavernLayout.PortraitCut, in pixels of a 256 square), at the
    /// bitmap's own resolution, kept inside the bitmap.
    /// </summary>
    private static BitmapSource CutPortrait(BitmapSource portrait)
    {
        var cut = TavernLayout.PortraitCut;
        var sx = portrait.PixelWidth / TavernLayout.PortraitSize;
        var sy = portrait.PixelHeight / TavernLayout.PortraitSize;
        var left = Math.Max(0, (int)Math.Round(cut.Left * sx));
        var top = Math.Max(0, (int)Math.Round(cut.Top * sy));
        var width = Math.Min(portrait.PixelWidth - left, (int)Math.Round(cut.Width * sx));
        var height = Math.Min(portrait.PixelHeight - top, (int)Math.Round(cut.Height * sy));
        if (width <= 0 || height <= 0)
        {
            return portrait;
        }

        var cropped = new CroppedBitmap(portrait, new Int32Rect(left, top, width, height));
        cropped.Freeze();
        return cropped;
    }
}
