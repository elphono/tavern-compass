using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>A frame around the next opponent's leaderboard tile, marked "ghost" when that player is dead.</summary>
internal sealed class NextOpponentMarker
{
    private readonly Canvas _canvas;
    private readonly Border _frame;
    private readonly TextBlock _label;
    private NextOpponentTile? _tile;

    public NextOpponentMarker(Canvas canvas)
    {
        _canvas = canvas;
        _label = new TextBlock { Foreground = Brushes.White, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center };
        _frame = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x00)),
            Background = Brushes.Transparent,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            Child = _label,
        };
        _canvas.Children.Add(_frame);
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    public void Show(NextOpponentTile tile)
    {
        _tile = tile;
        Relayout();
    }

    public void Hide()
    {
        _tile = null;
        _frame.Visibility = Visibility.Collapsed;
    }

    public void Detach()
    {
        _canvas.SizeChanged -= OnCanvasSizeChanged;
        _canvas.Children.Remove(_frame);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => Relayout();

    private void Relayout()
    {
        if (_tile == null || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            _frame.Visibility = Visibility.Collapsed;
            return;
        }

        var scale = _canvas.ActualHeight / 1080;
        var rect = LeaderboardLayout.Tile(_canvas.ActualWidth, _canvas.ActualHeight, _tile.LeaderboardPlace);
        _frame.Width = rect.Width;
        _frame.Height = rect.Height;
        _frame.BorderThickness = new Thickness(3 * scale);
        _frame.CornerRadius = new CornerRadius(6 * scale);
        _label.FontSize = 11 * scale;
        _label.Text = _tile.IsGhost ? "NEXT · ghost" : "NEXT";
        Canvas.SetLeft(_frame, rect.Left);
        Canvas.SetTop(_frame, rect.Top);
        _frame.Visibility = Visibility.Visible;
    }
}
