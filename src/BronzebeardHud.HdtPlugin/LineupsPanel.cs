using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.Utility.Extensions;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// "How top boards field it", for one of Bob's minions: its own movable panel ("lineups"), opened by the "?"
/// button above the card and closed by its ×, or when the shop ends. The minion's usual position, then for each
/// composition that fields it its label and its best final board (the minion ringed in white), with the MMR
/// and turn it was reached at. Every text wraps; a DownOnly Viewbox shrinks the content rather than cut it.
/// </summary>
internal sealed class LineupsPanel
{
    private readonly Canvas _canvas;
    private readonly PanelMover _mover;
    private readonly Border _panel;
    private MinionLineups? _lineups;
    private HashSet<string> _owned = new();

    public LineupsPanel(Canvas canvas, PanelMover mover)
    {
        _canvas = canvas;
        _mover = mover;
        _panel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x14, 0x14, 0x1E)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Visibility = Visibility.Collapsed,
        };
        OverlayLayer.Add(_canvas, _panel);
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    public bool IsOpen => _lineups != null;

    /// <param name="ownedCardIds">The player's cards (base ids): held minions get the green ring and tick.</param>
    public void Show(MinionLineups lineups, IEnumerable<string> ownedCardIds)
    {
        _lineups = lineups;
        _owned = new HashSet<string>(ownedCardIds);
        Relayout();
    }

    public void Hide()
    {
        _lineups = null;
        _panel.Visibility = Visibility.Collapsed;
    }

    public void Detach()
    {
        _canvas.SizeChanged -= OnCanvasSizeChanged;
        _canvas.Children.Remove(_panel);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => Relayout();

    private void Relayout()
    {
        if (_lineups is not { } lineups || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            _panel.Visibility = Visibility.Collapsed;
            return;
        }

        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;
        var scale = TavernLayout.Scale(height);
        var rect = TavernLayout.LineupsPanel(width, height);
        var oval = TavernLayout.VignetteSize * height * 0.8;
        var lines = new StackPanel { Margin = new Thickness(6 * scale) };

        var name = Hearthstone_Deck_Tracker.Hearthstone.Database.GetCardFromId(lineups.CardId)?.LocalizedName ?? lineups.CardId;
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 2 * scale) };
        var close = new Border
        {
            Width = 22 * scale,
            Height = 22 * scale,
            CornerRadius = new CornerRadius(11 * scale),
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x1E)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1.5 * scale),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock { Text = "×", FontSize = 14 * scale, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        close.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Hide();
        };
        OverlayExtensions.SetIsOverlayHitTestVisible(close, true);
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(new TextBlock { Text = $"{name}: how top boards field it", FontSize = 13 * scale, FontWeight = FontWeights.Bold, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
        lines.Children.Add(header);
        lines.Children.Add(new TextBlock { Text = lineups.Headline, FontSize = 12 * scale, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap });
        foreach (var lineup in lineups.Compositions)
        {
            lines.Children.Add(new TextBlock { Text = lineup.Label, FontSize = 12 * scale, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4 * scale, 0, 2 * scale) });
            var best = lineup.Boards[0];
            var board = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var cardId in best.Cards)
            {
                var baseId = CardIds.Normalize(cardId);
                var cell = CardImages.Vignette(cardId, _owned.Contains(baseId), oval, scale, TavernLayout.PreviewHeight * height, v => PreviewPlacer.Place(_canvas, _panel, v));
                board.Children.Add(baseId == lineups.CardId
                    ? new Border { BorderBrush = Brushes.White, BorderThickness = new Thickness(2.5 * scale), CornerRadius = new CornerRadius(oval), Child = cell }
                    : cell);
            }

            lines.Children.Add(board);
            lines.Children.Add(new TextBlock
            {
                Text = $"MMR {best.Mmr.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))}" + (best.Turn is { } turn ? $" · turn {turn}" : string.Empty),
                FontSize = 10 * scale,
                Foreground = Brushes.LightGray,
            });
        }

        _panel.Width = rect.Width;
        _panel.MinHeight = rect.Height;
        _mover.Place(_panel, "lineups", rect, interactive: true);
        lines.Width = rect.Width - 2 * 6 * scale - 4;
        var room = Math.Max(rect.Height, height - Canvas.GetTop(_panel) - 0.005 * height);
        _panel.MaxHeight = room;
        _panel.Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxHeight = room - 4, VerticalAlignment = VerticalAlignment.Top, Child = lines };
        _panel.Visibility = Visibility.Visible;
    }
}
