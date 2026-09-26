using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// Opponents' MMR next to their tile on the in-game leaderboard. The leaderboard slice
/// (<see cref="LeaderboardRange.Default"/>) is downloaded once per HDT session, in the background,
/// one page per second; players outside the slice get no label.
/// </summary>
internal sealed class OpponentMmrPanel : IDisposable
{
    private readonly Canvas _canvas;
    private readonly HttpStatsFetcher _fetcher = new();
    private readonly List<Border> _labels = new();
    private Task<LeaderboardFetchResult>? _download;
    private LeaderboardIndex? _index;
    private IReadOnlyList<OpponentMmr> _opponents = new List<OpponentMmr>();
    private bool _visible;

    public OpponentMmrPanel(Canvas canvas)
    {
        _canvas = canvas;
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    /// <summary>Last download problem, for diagnostics; null when all went well.</summary>
    public string? Error { get; private set; }

    /// <summary>Start the session's single download if it has not run yet.</summary>
    public void EnsureDownloadStarted()
    {
        if (_download != null)
        {
            return;
        }

        var client = new LeaderboardClient(_fetcher, (pause, token) => Task.Delay(pause, token));
        _download = Task.Run(() => client.FetchRangeAsync(LeaderboardRange.Default, CancellationToken.None));
    }

    /// <summary>The index once downloaded; null while downloading or after a failed first page.</summary>
    public LeaderboardIndex? Index
    {
        get
        {
            if (_index == null && _download is { IsCompleted: true } done)
            {
                if (done.Status == TaskStatus.RanToCompletion)
                {
                    _index = done.Result.Index;
                    Error = done.Result.Error;
                }
                else
                {
                    _index = new LeaderboardIndex();
                    Error = done.Exception?.GetBaseException().Message;
                }
            }

            return _index;
        }
    }

    public void Show(IReadOnlyList<OpponentMmr> opponents)
    {
        _opponents = opponents;
        _visible = true;
        Relayout();
    }

    public void Hide()
    {
        _visible = false;
        Clear();
    }

    public void Detach()
    {
        _canvas.SizeChanged -= OnCanvasSizeChanged;
        Clear();
    }

    public void Dispose() => _fetcher.Dispose();

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => Relayout();

    private void Clear()
    {
        foreach (var label in _labels)
        {
            _canvas.Children.Remove(label);
        }

        _labels.Clear();
    }

    private void Relayout()
    {
        Clear();
        if (!_visible || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            return;
        }

        var scale = _canvas.ActualHeight / 1080;
        foreach (var opponent in _opponents)
        {
            if (opponent.Row is not { } row)
            {
                continue;
            }

            var rect = LeaderboardLayout.MmrLabel(_canvas.ActualWidth, _canvas.ActualHeight, opponent.LeaderboardPlace);
            var label = new Border
            {
                Width = rect.Width,
                Height = rect.Height,
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x1E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x4D, 0xA6, 0xFF)),
                BorderThickness = new Thickness(1.5 * scale),
                CornerRadius = new CornerRadius(4 * scale),
                IsHitTestVisible = false,
                // "8045 · #2614" can be wider than the label at small scales: shrink, never clip.
                Child = new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    StretchDirection = StretchDirection.DownOnly,
                    Child = new TextBlock
                    {
                        Text = $"{row.Rating.ToString(CultureInfo.InvariantCulture)} · #{row.Rank.ToString(CultureInfo.InvariantCulture)}",
                        FontSize = 12 * scale,
                        Foreground = Brushes.White,
                    },
                },
            };
            Canvas.SetLeft(label, rect.Left);
            Canvas.SetTop(label, rect.Top);
            _canvas.Children.Add(label);
            _labels.Add(label);
        }
    }
}
