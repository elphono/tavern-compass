using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// HDT's own Battlegrounds comp guides (HdtCompGuides), in their own movable panel ("comp-guides"), by default in the
/// lower left part of the frame (CompGuideLayout.DefaultPanel), in the shop and in combat. Grouped by tier the way HDT's
/// Tier 7 list groups them, with HDT's tier letters and colours; in each tier the guides the player's board and hand
/// make most probable (CompGuideMatch) come first, ringed in vivid green with their rank, with the cards held and the
/// key pieces still missing; the others keep HDT's order, with what is held of them ("★2/4 +1"). Nothing is clickable:
/// the panel only takes the mouse in the plugin's move mode (PanelMover), where it can also be resized like the other
/// panels (PanelResize: more or less room, never a zoom). Nothing is shrunk: what does not fit is left out, the
/// highlighted guides last (CompGuideLayout.Fit), and the panel says how many guides it shows.
/// </summary>
internal sealed class CompGuidesPanel
{
    private static readonly Brush PanelBrush = new SolidColorBrush(Color.FromArgb(0xF0, 0x14, 0x14, 0x1E));

    // HDT's S tier blue: the panel shows HDT's content.
    private static readonly Brush FrameBrush = new SolidColorBrush(Color.FromRgb(0x40, 0x8A, 0xBF));
    private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xCD, 0xD8));
    private static readonly Brush RuleBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));

    // The most probable guides: one vivid green, used nowhere else in this panel.
    private static readonly Color HighlightColour = Color.FromRgb(0x3D, 0xFF, 0x8B);
    private static readonly Brush HighlightBrush = new SolidColorBrush(HighlightColour);
    private static readonly Brush HighlightTint = new SolidColorBrush(Color.FromArgb(0x38, HighlightColour.R, HighlightColour.G, HighlightColour.B));

    private readonly Canvas _canvas;
    private readonly PanelMover _mover;
    private readonly Border _panel;
    private CompGuideBoard _board = CompGuideBoard.Empty;
    private string? _source;
    private string? _status;

    public CompGuidesPanel(Canvas canvas, PanelMover mover)
    {
        _canvas = canvas;
        _mover = mover;
        _panel = new Border
        {
            Background = PanelBrush,
            BorderBrush = FrameBrush,
            BorderThickness = new Thickness(PanelFit.Border),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Visibility = Visibility.Collapsed,
        };
        OverlayLayer.Add(_canvas, _panel);
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    public bool IsVisible { get; private set; }

    /// <param name="source">CompGuideSources.HdtFree or HdtTier7; null when HDT shows no guides.</param>
    /// <param name="status">A line saying why there are no guides, or null.</param>
    public void Show(CompGuideBoard board, string? source, string? status)
    {
        _board = board;
        _source = source;
        _status = status;
        IsVisible = true;
        Relayout();
    }

    public void Hide()
    {
        IsVisible = false;
        _panel.Visibility = Visibility.Collapsed;
    }

    public void Detach()
    {
        _canvas.SizeChanged -= OnCanvasSizeChanged;
        _canvas.Children.Remove(_panel);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => Relayout();

    private static TextBlock Text(string text, double size, double scale, Brush brush, bool bold = false) => new()
    {
        Text = text,
        FontSize = size * scale,
        Foreground = brush,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
    };

    private static string CardName(string cardId) =>
        Hearthstone_Deck_Tracker.Hearthstone.Database.GetCardFromId(cardId)?.LocalizedName ?? cardId;

    /// <summary>HDT's tier colours: BattlegroundsCompsGuidesViewModel.GetTierColor, a left-to-right gradient.</summary>
    private static Brush TierBrush(int tier)
    {
        var (from, to) = tier switch
        {
            1 => (Color.FromRgb(64, 138, 191), Color.FromRgb(56, 95, 122)),
            2 => (Color.FromRgb(107, 160, 54), Color.FromRgb(88, 121, 55)),
            3 => (Color.FromRgb(146, 160, 54), Color.FromRgb(104, 121, 55)),
            4 => (Color.FromRgb(160, 124, 54), Color.FromRgb(121, 95, 55)),
            5 => (Color.FromRgb(160, 72, 54), Color.FromRgb(121, 66, 55)),
            _ => (Color.FromRgb(112, 112, 112), Color.FromRgb(64, 64, 64)),
        };
        return new LinearGradientBrush(from, to, new Point(0, 0.5), new Point(1, 0.5));
    }

    /// <summary>A tier's bar, as in HDT's Tier 7 list: the letter on the tier's gradient.</summary>
    private static FrameworkElement TierHeader(CompGuideBoardTier tier, double scale) => new Border
    {
        Background = TierBrush(tier.Tier),
        CornerRadius = new CornerRadius(3 * scale),
        Padding = new Thickness(6 * scale, 1 * scale, 6 * scale, 2 * scale),
        Margin = new Thickness(0, 6 * scale, 0, 2 * scale),
        Child = Text(tier.Letter, PanelTypography.PanelTitle, scale, Brushes.White, bold: true),
    };

    /// <summary>One guide: its name and what is held of it; a highlighted guide also lists the cards held and the key pieces missing.</summary>
    private static FrameworkElement Row(CompGuideProgress row, double scale)
    {
        var top = new DockPanel { LastChildFill = true };
        if (row.HeldText.Length > 0)
        {
            var held = Text(row.HeldText, PanelTypography.Small, scale, row.IsHighlighted ? HighlightBrush : Brushes.White, bold: true);
            held.TextWrapping = TextWrapping.NoWrap; // a few characters, always given their room before the name
            held.Margin = new Thickness(6 * scale, 0, 0, 0);
            held.VerticalAlignment = VerticalAlignment.Top;
            DockPanel.SetDock(held, Dock.Right);
            top.Children.Add(held);
        }

        if (!row.IsHighlighted)
        {
            top.Children.Add(Text(row.Guide.Name, PanelTypography.Body, scale, row.Score > 0 ? Brushes.White : MutedBrush));
            top.Margin = new Thickness(9 * scale, 1 * scale, 4 * scale, 1 * scale);
            return top;
        }

        var badge = new Border
        {
            Width = 18 * scale,
            Height = 18 * scale,
            CornerRadius = new CornerRadius(9 * scale),
            Background = HighlightBrush,
            Margin = new Thickness(0, 0, 5 * scale, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = row.Highlight!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FontSize = PanelTypography.Small * scale,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        DockPanel.SetDock(badge, Dock.Left);
        top.Children.Add(badge);
        top.Children.Add(Text(row.Guide.Name, PanelTypography.Body, scale, HighlightBrush, bold: true));

        var cards = new TextBlock { FontSize = PanelTypography.Small * scale, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2 * scale, 0, 0) };
        cards.Inlines.Add(new Run("✓ " + string.Join(", ", row.Held.Select(CardName))) { Foreground = Brushes.White });
        var missing = row.KeyMissing;
        if (missing.Count > 0)
        {
            cards.Inlines.Add(new Run(" · need " + string.Join(", ", missing.Select(CardName))) { Foreground = MutedBrush });
        }

        var body = new StackPanel();
        body.Children.Add(top);
        body.Children.Add(cards);
        return new Border
        {
            Background = HighlightTint,
            BorderBrush = HighlightBrush,
            BorderThickness = new Thickness(3 * scale, 0, 0, 0),
            Padding = new Thickness(6 * scale, 3 * scale, 4 * scale, 3 * scale),
            Margin = new Thickness(0, 2 * scale, 0, 2 * scale),
            Child = body,
        };
    }

    /// <summary>
    /// The title, then the tiers and guides that fit between the panel's top (it may have been moved) and the gold:
    /// every piece is built and measured in place, then CompGuideLayout.Fit says which stay.
    /// </summary>
    private void Relayout()
    {
        if (!IsVisible || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            _panel.Visibility = Visibility.Collapsed;
            return;
        }

        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;
        var scale = TavernLayout.Scale(height);
        var rect = CompGuideLayout.DefaultPanel(width, height);
        var placed = _mover.Place(_panel, CompGuideLayout.PanelId, rect, interactive: false,
            new PanelResize(CompGuideLayout.MinWidth * scale, CompGuideLayout.MinHeight * scale, Relayout));
        _panel.Width = placed.Width;

        // The room the content has: the box the player gave the panel (PanelMover's handle), otherwise from wherever
        // the panel sits (it may have been moved) down to the gold, as for the other panels.
        var room = _mover.IsResized(CompGuideLayout.PanelId) ? placed.Height : PanelFit.BottomLimit * height - Canvas.GetTop(_panel);
        var inner = Math.Max(0, placed.Width - 2 * (PanelFit.Padding + PanelFit.Border) * scale);
        var lines = new StackPanel { Margin = new Thickness(PanelFit.Padding * scale), Width = inner };

        var title = new DockPanel { LastChildFill = true };
        if (_source != null)
        {
            var tag = Text(_source == CompGuideSources.HdtTier7 ? "Tier 7" : "free", PanelTypography.Small, scale, MutedBrush);
            tag.VerticalAlignment = VerticalAlignment.Center;
            tag.Margin = new Thickness(6 * scale, 0, 0, 0);
            DockPanel.SetDock(tag, Dock.Right);
            title.Children.Add(tag);
        }

        var titleText = Text("HDT comp guides", PanelTypography.PanelTitle, scale, Brushes.White, bold: true);
        titleText.VerticalAlignment = VerticalAlignment.Center;
        title.Children.Add(titleText);
        var chrome = new List<FrameworkElement>
        {
            new Border { BorderBrush = RuleBrush, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 4 * scale), Child = title },
        };
        if (!string.IsNullOrEmpty(_status))
        {
            chrome.Add(Text(_status!, PanelTypography.Small, scale, Brushes.Gold));
        }

        var pieces = new List<(FrameworkElement Element, CompGuideItemKind Kind, int Group, bool Highlighted)>();
        for (var g = 0; g < _board.Tiers.Count; g++)
        {
            var tier = _board.Tiers[g];
            pieces.Add((TierHeader(tier, scale), CompGuideItemKind.TierHeader, g, false));
            pieces.AddRange(tier.Rows.Select(r => (Row(r, scale), CompGuideItemKind.Row, g, r.IsHighlighted)));
        }

        var more = Text(string.Empty, PanelTypography.Small, scale, MutedBrush);
        more.Margin = new Thickness(0, 4 * scale, 0, 0);

        // Measured in place, so that the canvas's inherited font applies: the heights are the ones drawn.
        foreach (var element in chrome.Concat(pieces.Select(p => p.Element)).Append(more))
        {
            lines.Children.Add(element);
        }

        _panel.Child = lines;
        _panel.Visibility = Visibility.Visible;
        more.Text = "99 of 99 shown";
        lines.Measure(new Size(inner, double.PositiveInfinity));
        // The frame at its thickest (3 px in move mode, PanelMover) so that nothing is ever cut by MaxHeight.
        var used = chrome.Sum(e => e.DesiredSize.Height) + 2 * (PanelFit.Padding * scale + Math.Max(PanelFit.Border, 3));
        var fit = CompGuideLayout.Fit(
            pieces.Select(p => new CompGuideFitItem(p.Kind, p.Group, p.Element.DesiredSize.Height, p.Highlighted)).ToList(),
            room - used, more.DesiredSize.Height);

        lines.Children.Clear();
        foreach (var element in chrome)
        {
            lines.Children.Add(element);
        }

        foreach (var index in fit.Shown)
        {
            lines.Children.Add(pieces[index].Element);
        }

        if (fit.ShowsMoreLine)
        {
            more.Text = $"{fit.RowsShown} of {fit.RowsTotal} shown";
            lines.Children.Add(more);
        }

        _panel.MaxHeight = Math.Max(0, room);
    }
}
