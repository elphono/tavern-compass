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
/// "How top boards field it", for one of Bob's minions: its own movable panel ("lineups"), by default in the
/// right-hand column (PanelFit.LineupsPanel: clear of the target panel, the Skip combat button, the cards and
/// their buttons), opened by the "?" above the card and closed by its ×, or when the shop ends. The minion's usual
/// position, then for each composition that fields it its label and its best final board as ovals (the minion
/// ringed in white), with the MMR and turn it was reached at. Nothing is shrunk: as many compositions as fit
/// (PanelFit.LineupCompositions), the headline saying how many are shown.
/// </summary>
internal sealed class LineupsPanel
{
    private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xCD, 0xD8));

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
            BorderThickness = new Thickness(PanelFit.Border),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
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

    private static TextBlock Text(string text, double size, double scale, Brush brush, bool bold = false, double top = 0) => new()
    {
        Text = text,
        FontSize = size * scale,
        Foreground = brush,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, top * scale, 0, 0),
    };

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
        var rect = PanelFit.LineupsPanel(width, height);
        var placed = _mover.Place(_panel, "lineups", rect, interactive: true,
            new PanelResize(PanelFit.LineupsMinWidth * scale, PanelFit.LineupsMinHeight * scale, Relayout));
        _panel.Width = placed.Width;

        // The room the content has: the box the player gave the panel (its width sets the ovals per line, its height
        // the compositions shown), otherwise from wherever the panel sits (it may have been moved) down to the gold,
        // as for the target panel.
        var top = Canvas.GetTop(_panel);
        var roomHeight = _mover.IsResized("lineups") ? placed.Height : Math.Min(rect.Height, PanelFit.BottomLimit * height - top);
        var room = new LayoutRect(placed.CenterX, 0, placed.Width, roomHeight);
        var shown = PanelFit.LineupCompositions(room, height, lineups.Compositions.Select(c => c.Boards[0].Cards.Count).ToList());
        var lines = new StackPanel { Margin = new Thickness(PanelFit.Padding * scale), Width = placed.Width - 2 * (PanelFit.Padding + PanelFit.Border) * scale };

        var name = Hearthstone_Deck_Tracker.Hearthstone.Database.GetCardFromId(lineups.CardId)?.LocalizedName ?? lineups.CardId;
        var header = new DockPanel { LastChildFill = true };
        var close = new Border
        {
            Width = 22 * scale,
            Height = 22 * scale,
            CornerRadius = new CornerRadius(11 * scale),
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x1E)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1.5 * scale),
            Margin = new Thickness(8 * scale, 0, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock { Text = "×", FontSize = PanelTypography.RoundButton * scale, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        close.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Hide();
        };
        OverlayExtensions.SetIsOverlayHitTestVisible(close, true);
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(Text($"{name}: how top boards field it", PanelTypography.Body, scale, Brushes.White, bold: true));
        lines.Children.Add(header);
        lines.Children.Add(Text(lineups.Headline + (shown < lineups.Compositions.Count ? $" · {shown} shown" : string.Empty), PanelTypography.Small, scale, MutedBrush, top: 2));
        foreach (var lineup in lineups.Compositions.Take(shown))
        {
            lines.Children.Add(Text(lineup.Label, PanelTypography.Small, scale, Brushes.White, bold: true, top: 6));
            var best = lineup.Boards[0];
            var board = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2 * scale, 0, 0) };
            foreach (var cardId in best.Cards)
            {
                var baseId = CardIds.Normalize(cardId);
                var cell = CardImages.Vignette(cardId, _owned.Contains(baseId), PanelFit.OvalWidth * scale, scale, TavernLayout.PreviewHeight * height,
                    v => PreviewPlacer.Place(_canvas, _panel, v));
                var gap = new Thickness(0, 0, PanelFit.OvalGap * scale, PanelFit.RowGap * scale);
                if (baseId == lineups.CardId)
                {
                    cell.Margin = new Thickness(0);
                    board.Children.Add(new Border { BorderBrush = Brushes.White, BorderThickness = new Thickness(2.5 * scale), CornerRadius = new CornerRadius(PanelFit.OvalWidth * scale), Margin = gap, Child = cell });
                }
                else
                {
                    cell.Margin = gap;
                    board.Children.Add(cell);
                }
            }

            lines.Children.Add(board);
            lines.Children.Add(Text($"MMR {best.Mmr.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))}" + (best.Turn is { } turn ? $" · turn {turn}" : string.Empty),
                PanelTypography.Small, scale, MutedBrush));
        }

        _panel.MaxHeight = room.Height;
        _panel.Child = lines;
        _panel.Visibility = Visibility.Visible;
    }
}
