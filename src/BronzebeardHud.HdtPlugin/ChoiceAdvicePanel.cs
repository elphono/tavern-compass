using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// A label above each option of a Battlegrounds choice (discover, Dark Gift, trinket): what it does for
/// the compositions in reach, and for a trinket its Firestone placement, adjusted when it suits them.
/// Text from <see cref="ChoiceAdvisor.Lines"/>, positions from <see cref="ChoiceLayout"/> (HDT's constants),
/// both recomputed when the overlay is resized. Trinket stats come from the daily cache, fetched off the
/// UI thread on the first trinket choice.
/// </summary>
internal sealed class ChoiceAdvicePanel : IDisposable
{
    private static readonly Brush NeutralBrush = new SolidColorBrush(Color.FromArgb(0xE6, 0x3A, 0x3A, 0x44));
    private static readonly Brush TrinketBrush = new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x1E));
    private static readonly Brush TrinketBorder = new SolidColorBrush(Color.FromRgb(0xD9, 0x48, 0x0F));

    private readonly Canvas _canvas;
    private readonly CompositionSelection _selection;
    private readonly HttpStatsFetcher _fetcher = new();
    private readonly StatsCache _cache;
    private readonly List<UIElement> _labels = new();
    private Task<(TrinketStatsFile? File, bool Downloaded, string? Error)>? _download;
    private TrinketStatsFile? _stats;
    private ChoiceAdvice? _advice;

    public ChoiceAdvicePanel(Canvas canvas, string statsDirectory, CompositionSelection selection)
    {
        _canvas = canvas;
        _selection = selection;
        _cache = new StatsCache(statsDirectory, _fetcher, () => DateTimeOffset.UtcNow);
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    public string? TrinketError { get; private set; }

    public bool TrinketStatsLoaded => _stats != null || _download is { IsCompleted: true };

    /// <summary>The first label drawn by the last layout, for the diagnostic line; null when none.</summary>
    public LayoutRect? FirstLabel { get; private set; }

    /// <summary>The lines drawn above each option by the last layout.</summary>
    public IReadOnlyList<IReadOnlyList<string>> LastLines { get; private set; } = Array.Empty<IReadOnlyList<string>>();

    public TrinketStat? TrinketStat(string cardId) => _stats?.Find(cardId);

    /// <summary>Starts the trinket download on first call; true when stats became available since the last call.</summary>
    public bool PollTrinketStats()
    {
        _download ??= Task.Run(() => _cache.GetTrinketStatsAsync("last-patch", RefreshPolicy.HeroStats, CancellationToken.None));
        if (_stats != null || !_download.IsCompleted)
        {
            return false;
        }

        if (_download.Status == TaskStatus.RanToCompletion)
        {
            _stats = _download.Result.File;
            TrinketError = _download.Result.Error;
        }
        else
        {
            TrinketError = _download.Exception?.GetBaseException().Message;
        }

        return true;
    }

    public void Show(ChoiceAdvice advice)
    {
        _advice = advice;
        Relayout();
    }

    public void Hide()
    {
        _advice = null;
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
        FirstLabel = null;
        LastLines = Array.Empty<IReadOnlyList<string>>();
    }

    private void Relayout()
    {
        Clear();
        if (_advice is not { HasMarkers: true } advice || _canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            return;
        }

        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;
        var scale = TavernLayout.Scale(height);
        var fontSize = TavernLayout.MarkerFontSize * scale;
        var cardWidth = ChoiceLayout.Cards(advice.Kind, advice.Options.Count, width, height).FirstOrDefault().Width;
        var maxChars = MarkerText.MaxChars(cardWidth, fontSize, TavernLayout.MarkerPadding * scale);
        var lines = advice.Options.Select(o => ChoiceAdvisor.Lines(o, maxChars, TrinketStatsLoaded)).ToList();
        var rects = ChoiceLayout.Labels(advice.Kind, advice.Options.Count, width, height, lines.Max(l => l.Count));
        LastLines = lines;

        for (var i = 0; i < rects.Count; i++)
        {
            var option = advice.Options[i];
            // The same colours as the tavern markers: the ticked composition's, white when nothing is ticked.
            var background = option.Trinket != null ? TrinketBrush
                : option.Effects.Count == 0 ? NeutralBrush
                : TavernAdvicePanel.Brush(_selection.MarkerColour(option.Effects.Select(e => e.Composition.Id)));
            var foreground = background == TrinketBrush || background == NeutralBrush ? Brushes.White : Brushes.Black;
            var text = new StackPanel();
            foreach (var line in lines[i])
            {
                text.Children.Add(new TextBlock
                {
                    Text = line,
                    FontSize = fontSize,
                    FontWeight = FontWeights.Bold,
                    Foreground = foreground,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            }

            var label = new Border
            {
                Width = rects[i].Width,
                Height = rects[i].Height,
                Background = background,
                BorderBrush = option.Trinket != null ? TrinketBorder : Brushes.Black,
                BorderThickness = new Thickness(1.5 * scale),
                CornerRadius = new CornerRadius(5 * scale),
                Padding = new Thickness(TavernLayout.MarkerPadding * scale, 0, TavernLayout.MarkerPadding * scale, 0),
                IsHitTestVisible = false,
                // Safety net: if the glyph-width estimate is ever short, shrink rather than clip.
                Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = text },
            };
            Canvas.SetLeft(label, rects[i].Left);
            Canvas.SetTop(label, rects[i].Top);
            OverlayLayer.Add(_canvas, label);
            _labels.Add(label);
            FirstLabel ??= rects[i];
        }
    }
}
