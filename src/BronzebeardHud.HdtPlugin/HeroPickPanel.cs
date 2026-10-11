using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Path = System.Windows.Shapes.Path;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// One badge per offered hero, under the game's reroll button below that hero's portrait, plus a
/// status line under the row. Positions come from <see cref="HeroPickLayout"/> and are recomputed
/// whenever the overlay canvas changes size. Built in code rather than XAML so it compiles under WSL.
/// Each badge draws the note <see cref="HeroNote.For"/> gives (Ali, 2026-10-11, the game's hero choice as the model): the tier
/// letter in a shield of its colour, the figure large and outlined like the armour on a hero's portrait, then the small lines.
/// </summary>
internal sealed class HeroPickPanel
{
    /// <summary>Tags of a badge's pieces, for the simulation's self-test.</summary>
    public const string ShieldTag = "hero-shield";
    public const string FigureTag = "hero-figure";
    public const string CaptionTag = "hero-caption";
    public const string DetailTag = "hero-detail";

    /// <summary>A heater shield, drawn in a box ShieldWidth × HeadHeight (design pixels); stretched to the badge's scale.</summary>
    private static readonly Geometry ShieldShape = Freeze(Geometry.Parse("M 0,5 Q 17,-1 34,5 L 34,25 C 34,36 26,41 17,45 C 8,41 0,36 0,25 Z"));

    /// <summary>The badge's plate: the game's dark violet, a touch lighter at the top.</summary>
    private static readonly Brush Plate = Freeze(new LinearGradientBrush(Color.FromArgb(0xEE, 0x2C, 0x22, 0x42), Color.FromArgb(0xEE, 0x10, 0x0D, 0x1A), 90));

    /// <summary>Silver, as the armour's shield: the rim of a badge and of a tier's shield.</summary>
    private static readonly Brush Silver = Freeze(new LinearGradientBrush(Color.FromRgb(0xF0, 0xF3, 0xF8), Color.FromRgb(0x7E, 0x84, 0x92), 90));

    private readonly Canvas _canvas;
    private readonly List<Border> _badges = new();
    private readonly TextBlock _status = new() { Foreground = Brushes.Gold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, IsHitTestVisible = false };
    private bool _visible;

    public HeroPickPanel(Canvas canvas)
    {
        _canvas = canvas;
        _status.Visibility = Visibility.Collapsed;
        OverlayLayer.Add(_canvas, _status);
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    /// <summary>The badges on the canvas, left to right, for the simulation's self-test.</summary>
    public IReadOnlyList<Border> Badges => _badges;

    public void Show(IReadOnlyList<HeroPickRow> rows, string? status)
    {
        foreach (var badge in _badges)
        {
            _canvas.Children.Remove(badge);
        }

        _badges.Clear();
        foreach (var row in rows)
        {
            var badge = new Border
            {
                Background = Plate,
                CornerRadius = new CornerRadius(6),
                IsHitTestVisible = false,
                Tag = HeroNote.For(row),
            };
            _badges.Add(badge);
            OverlayLayer.Add(_canvas, badge);
        }

        _status.Text = status ?? string.Empty;
        _visible = true;
        Relayout();
    }

    public void Hide()
    {
        _visible = false;
        foreach (var badge in _badges)
        {
            badge.Visibility = Visibility.Collapsed;
        }

        _status.Visibility = Visibility.Collapsed;
    }

    public void Detach()
    {
        _canvas.SizeChanged -= OnCanvasSizeChanged;
        foreach (var badge in _badges)
        {
            _canvas.Children.Remove(badge);
        }

        _canvas.Children.Remove(_status);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => Relayout();

    /// <summary>Place every badge for the current canvas size; the content is rebuilt so fonts follow the scale.</summary>
    private void Relayout()
    {
        if (!_visible)
        {
            return;
        }

        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;
        var rects = HeroPickLayout.Compute(width, height, _badges.Count);
        if (rects.Count != _badges.Count)
        {
            return;
        }

        var scale = HeroPickLayout.Scale(height);
        for (var i = 0; i < _badges.Count; i++)
        {
            var badge = _badges[i];
            var rect = rects[i];
            var note = (HeroNote)badge.Tag;
            badge.Width = rect.Width;
            badge.Height = rect.Height;
            badge.BorderThickness = new Thickness(HeroPickLayout.BadgeBorder * scale);
            badge.BorderBrush = note.RimColour == HeroNote.Rim ? Silver : HexBrush.Of(note.RimColour);
            badge.CornerRadius = new CornerRadius(6 * scale);
            badge.Padding = new Thickness(HeroPickLayout.BadgePaddingX * scale, HeroPickLayout.BadgePaddingY * scale, HeroPickLayout.BadgePaddingX * scale, HeroPickLayout.BadgePaddingY * scale);
            badge.Child = BuildContent(note, scale);
            Canvas.SetLeft(badge, rect.Left);
            Canvas.SetTop(badge, rect.Top);
            badge.Visibility = Visibility.Visible;
        }

        if (string.IsNullOrEmpty(_status.Text) || rects.Count == 0)
        {
            _status.Visibility = Visibility.Collapsed;
            return;
        }

        var status = HeroPickLayout.Status(width, height, rects);
        _status.FontSize = PanelTypography.Small * scale;
        _status.Width = status.Width;
        Canvas.SetLeft(_status, status.Left);
        Canvas.SetTop(_status, status.Top);
        _status.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// The note's head — the shield, then the figure (<see cref="PanelTypography.HeroNote"/>) over its caption — and the small
    /// lines under it (<see cref="PanelTypography.Small"/>), each at its own size; what does not fit the badge is left out
    /// (<see cref="HeroPickLayout.ItemsThatFit"/>), never shrunk.
    /// </summary>
    private static UIElement BuildContent(HeroNote note, double scale)
    {
        var column = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(HeroPickLayout.ShieldGap * scale, 0, 0, 0) };
        if (note.Figure != null)
        {
            column.Children.Add(Outlined(Line(note.Figure, PanelTypography.HeroNote, HeroPickLayout.NoteLine, scale, HexBrush.Of(note.FigureColour), FontWeights.ExtraBold, FigureTag), scale));
            column.Children.Add(Line(note.Caption, PanelTypography.Small, HeroPickLayout.DetailLine, scale, HexBrush.Of(note.CaptionColour), FontWeights.SemiBold, CaptionTag));
        }
        else
        {
            column.Children.Add(Line(note.Caption, PanelTypography.HeroNoData, HeroPickLayout.NoDataLine, scale, HexBrush.Of(note.CaptionColour), FontWeights.SemiBold, CaptionTag));
        }

        var head = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Height = HeroPickLayout.HeadHeight * scale };
        head.Children.Add(Shield(note, scale));
        head.Children.Add(column);

        var items = new List<(double Height, UIElement Line)> { (HeroPickLayout.HeadHeight, head) };
        foreach (var detail in note.Details)
        {
            items.Add((HeroPickLayout.DetailLine, Detail(detail, scale)));
        }

        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var index in HeroPickLayout.ItemsThatFit(items.Select(i => i.Height).ToList(), HeroPickLayout.ContentHeight))
        {
            lines.Children.Add(items[index].Line);
        }

        return lines;
    }

    /// <summary>The tier letter in a shield of its colour, lit from above, rimmed in silver.</summary>
    private static FrameworkElement Shield(HeroNote note, double scale)
    {
        var width = HeroPickLayout.ShieldWidth * scale;
        var height = HeroPickLayout.HeadHeight * scale;
        var colour = (Color)ColorConverter.ConvertFromString(note.TierFill);
        var shield = new Grid { Width = width, Height = height, VerticalAlignment = VerticalAlignment.Center, Tag = ShieldTag };
        shield.Children.Add(new Path
        {
            Data = ShieldShape,
            Stretch = Stretch.Fill,
            Width = width,
            Height = height,
            Fill = new LinearGradientBrush(Mix(colour, Colors.White, 0.3), Mix(colour, Colors.Black, 0.45), 90),
            Stroke = Silver,
            StrokeThickness = 2 * scale,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4 * scale, ShadowDepth = 1 * scale, Opacity = 0.7 },
        });
        // A line shorter than the shield puts the letter in the shield's wide part, above its point.
        var letter = Outlined(Line(note.Tier, PanelTypography.HeroTier, HeroPickLayout.HeadHeight - 12, scale, Brushes.White, FontWeights.Black, null), scale);
        letter.VerticalAlignment = VerticalAlignment.Top;
        shield.Children.Add(letter);
        return shield;
    }

    /// <summary>A small line; a second source's carries its tier in a chip of its colour before the text.</summary>
    private static UIElement Detail(HeroNoteLine detail, double scale)
    {
        var text = Line(detail.Text, PanelTypography.Small, HeroPickLayout.DetailLine, scale, HexBrush.Of(detail.Colour), FontWeights.Normal, DetailTag);
        if (detail.Tier == null)
        {
            return text;
        }

        var line = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Height = HeroPickLayout.DetailLine * scale };
        line.Children.Add(new Border
        {
            Background = HexBrush.Of(HeroNote.TierColour(detail.Tier)),
            CornerRadius = new CornerRadius(3 * scale),
            Padding = new Thickness(4 * scale, 0, 4 * scale, 0),
            Margin = new Thickness(0, 0, 4 * scale, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Line(detail.Tier, PanelTypography.Small, HeroPickLayout.DetailLine - 1, scale, Brushes.Black, FontWeights.Bold, DetailTag),
        });
        line.Children.Add(text);
        return line;
    }

    /// <summary>One line of text, exactly <paramref name="lineHeight"/> design pixels tall, centred; never wrapped.</summary>
    private static TextBlock Line(string text, double size, double lineHeight, double scale, Brush brush, FontWeight weight, string? tag) => new()
    {
        Text = text,
        Tag = tag,
        FontSize = size * scale,
        LineHeight = lineHeight * scale,
        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        Foreground = brush,
        FontWeight = weight,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.NoWrap,
        IsHitTestVisible = false,
    };

    /// <summary>A dark outline around the glyphs, as the game draws its white numbers: readable on any colour.</summary>
    private static TextBlock Outlined(TextBlock text, double scale)
    {
        text.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 3 * scale, ShadowDepth = 0, Opacity = 1 };
        return text;
    }

    private static Color Mix(Color from, Color to, double amount) => Color.FromRgb(
        (byte)(from.R + (to.R - from.R) * amount), (byte)(from.G + (to.G - from.G) * amount), (byte)(from.B + (to.B - from.B) * amount));

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
