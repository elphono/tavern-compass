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
/// A badge above each offered trinket: Firestone's average placement in the player's MMR bracket and
/// pick rate. Stats come from the daily cache, fetched off the UI thread on the first trinket choice;
/// positions from <see cref="TrinketLayout"/>, recomputed when the overlay canvas is resized.
/// </summary>
internal sealed class TrinketPickPanel : IDisposable
{
    private readonly Canvas _canvas;
    private readonly HttpStatsFetcher _fetcher = new();
    private readonly StatsCache _cache;
    private readonly List<Border> _badges = new();
    private Task<(TrinketStatsFile? File, bool Downloaded, string? Error)>? _download;
    private TrinketStatsFile? _stats;
    private IReadOnlyList<string> _offered = new List<string>();
    private int _bracket = MmrBracket.EveryPlayer;
    private bool _visible;

    public TrinketPickPanel(Canvas canvas, string statsDirectory)
    {
        _canvas = canvas;
        _cache = new StatsCache(statsDirectory, _fetcher, () => DateTimeOffset.UtcNow);
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    public string? Error { get; private set; }

    /// <summary>True when newly downloaded or cached stats became available since the last call.</summary>
    public bool Poll()
    {
        if (_download == null)
        {
            _download = Task.Run(() => _cache.GetTrinketStatsAsync("last-patch", RefreshPolicy.HeroStats, CancellationToken.None));
        }

        if (_stats != null || !_download.IsCompleted)
        {
            return false;
        }

        if (_download.Status == TaskStatus.RanToCompletion)
        {
            _stats = _download.Result.File;
            Error = _download.Result.Error;
        }
        else
        {
            Error = _download.Exception?.GetBaseException().Message;
        }

        return true;
    }

    public void Show(IReadOnlyList<string> offeredCardIds, int bracket)
    {
        _offered = offeredCardIds;
        _bracket = bracket;
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
        foreach (var badge in _badges)
        {
            _canvas.Children.Remove(badge);
        }

        _badges.Clear();
    }

    private void Relayout()
    {
        Clear();
        if (!_visible || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            return;
        }

        var scale = _canvas.ActualHeight / 1080;
        var rects = TrinketLayout.Compute(_canvas.ActualWidth, _canvas.ActualHeight, _offered.Count);
        for (var i = 0; i < rects.Count; i++)
        {
            var stat = _stats?.Find(_offered[i]);
            var text = stat == null
                ? (_stats == null ? "loading…" : "no data")
                : $"avg {stat.PlacementFor(_bracket).ToString("0.00", CultureInfo.InvariantCulture)}"
                  + (stat.PickRate is { } pick ? $" · {(pick * 100).ToString("0", CultureInfo.InvariantCulture)}%" : string.Empty)
                  + (_bracket < MmrBracket.EveryPlayer ? $" · FS {_bracket}%" : " · FS");
            var badge = new Border
            {
                Width = rects[i].Width,
                Height = rects[i].Height,
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x1E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x48, 0x0F)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 14 * scale,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            Canvas.SetLeft(badge, rects[i].Left);
            Canvas.SetTop(badge, rects[i].Top);
            _canvas.Children.Add(badge);
            _badges.Add(badge);
        }
    }
}
