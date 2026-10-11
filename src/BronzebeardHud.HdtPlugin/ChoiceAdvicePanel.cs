using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// A label above each option of a Battlegrounds choice (discover, Dark Gift, trinket): what it does for
/// the targets (HDT's comp guides, ChoiceAdvisor), rimmed in the colour of the first target it serves (the colour of that
/// target in the panel and on Bob's cards), and for a trinket its Firestone placement, adjusted when it suits them.
/// Drawn as notes (<see cref="ChoiceNote"/>; Ali, 2026-10-11, "les couleurs, les notes"): a pill per line that says the role
/// at a glance, the guide's name in its target's colour; a trinket's placement large in the colour of its band.
/// Text from <see cref="ChoiceNote.For"/>, the log's lines from <see cref="ChoiceAdvisor.Lines"/>, positions from
/// <see cref="ChoiceLayout"/> (HDT's constants), all recomputed when the overlay is resized. Trinket stats come from the daily
/// cache, fetched off the UI thread from the plugin's start on (the first fetch always asks Firestone's server, see
/// StatsCache), then asked again at each trinket choice so that the daily rule holds in a long session (TrinketStatsRefresh).
/// </summary>
internal sealed class ChoiceAdvicePanel
{
    /// <summary>Tags of a label's pieces, for the simulation's self-test: a line, a role's pill, a tier chip, a trinket's figure.</summary>
    public const string LineTag = "choice-line";
    public const string PillTag = "choice-pill";
    public const string TierTag = "choice-tier";
    public const string FigureTag = "choice-figure";

    /// <summary>The rim's thickness, in design pixels.</summary>
    private const double Rim = 1.5;

    /// <summary>The plate: the game's dark violet, tinted at the top by the rim's colour.</summary>
    private static readonly Color PlateTop = Color.FromRgb(0x2A, 0x22, 0x38);
    private static readonly Color PlateBottom = Color.FromRgb(0x12, 0x0F, 0x1A);

    private readonly Canvas _canvas;
    private readonly StatsCache _cache;
    private readonly List<UIElement> _labels = new();
    private readonly TrinketStatsRefresh _trinkets;
    private ChoiceAdvice? _advice;
    private TrinketReference? _reference;

    /// <param name="cache">The plugin's one cache, shared by every service.</param>
    public ChoiceAdvicePanel(Canvas canvas, StatsCache cache)
    {
        _canvas = canvas;
        _cache = cache;
        _trinkets = new TrinketStatsRefresh("trinket-stats last-patch",
            () => Task.Run(() => _cache.GetTrinketStatsAsync("last-patch", RefreshPolicy.HeroStats, CancellationToken.None)));
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    public string? TrinketError => _trinkets.Error;

    public bool TrinketStatsLoaded => _trinkets.Loaded;

    /// <summary>Changes each time a trinket load finishes, so that a choice on screen is advised again with its result.</summary>
    public int TrinketStatsVersion => _trinkets.Version;

    /// <summary>The first label drawn by the last layout, for the diagnostic line; null when none.</summary>
    public LayoutRect? FirstLabel { get; private set; }

    /// <summary>The lines of the last layout, as HDT's log writes them (ChoiceAdvisor.Lines): the labels draw them as notes.</summary>
    public IReadOnlyList<IReadOnlyList<string>> LastLines { get; private set; } = Array.Empty<IReadOnlyList<string>>();

    /// <summary>The notes drawn above each option by the last layout.</summary>
    public IReadOnlyList<ChoiceNote> LastNotes { get; private set; } = Array.Empty<ChoiceNote>();

    public TrinketStat? TrinketStat(string cardId) => _trinkets.File?.Find(cardId);

    /// <summary>The trinket stats loaded; null until a load brings them.</summary>
    public TrinketStatsFile? TrinketFile => _trinkets.File;

    /// <summary>The diagnostic line of the finished load, until the plugin logs it; see <see cref="DataRefresh"/>.</summary>
    public string? PendingLogLine { get; set; }

    /// <summary>A trinket choice is on screen, named by an id unique to it: asks the cache again, once per choice.</summary>
    public void BeginTrinketChoice(string choiceId) => _trinkets.BeginChoice(choiceId);

    /// <summary>Starts the first trinket load on first call; true when a load finished since the last call.</summary>
    public bool PollTrinketStats()
    {
        if (!_trinkets.Poll())
        {
            return false;
        }

        PendingLogLine = _trinkets.LastLine;
        return true;
    }

    /// <param name="reference">The trinkets of the player's bracket, to grade a trinket's placement (PlacementGrade); null: not graded.</param>
    public void Show(ChoiceAdvice advice, TrinketReference? reference = null)
    {
        _advice = advice;
        _reference = reference;
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
        LastNotes = Array.Empty<ChoiceNote>();
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
        var fontSize = PanelTypography.Px(PanelTypography.Marker, height);
        var cardWidth = ChoiceLayout.Cards(advice.Kind, advice.Options.Count, width, height).FirstOrDefault().Width;
        var maxChars = MarkerText.MaxChars(cardWidth, fontSize, TavernLayout.MarkerPadding * scale);
        // The lines of before still write HDT's log and size the labels; the notes are what is drawn, line for line.
        var lines = advice.Options.Select(o => ChoiceAdvisor.Lines(o, maxChars, TrinketStatsLoaded)).ToList();
        var notes = advice.Options.Select(o => ChoiceNote.For(o, maxChars, TrinketStatsLoaded, 2, _reference)).ToList();
        var rects = ChoiceLayout.Labels(advice.Kind, advice.Options.Count, width, height, lines.Max(l => l.Count));
        LastLines = lines;
        LastNotes = notes;

        for (var i = 0; i < rects.Count; i++)
        {
            var note = notes[i];
            var rim = (Color)ColorConverter.ConvertFromString(note.Rim);
            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            if (note.Trinket is { } trinket)
            {
                content.Children.Add(TrinketRow(trinket, rects[i].Height - 2 * Rim * scale, scale));
            }
            else
            {
                foreach (var line in note.Lines)
                {
                    content.Children.Add(Row(line, scale));
                }
            }

            var label = new Border
            {
                Width = rects[i].Width,
                Height = rects[i].Height,
                // A dark plate tinted at the top by its rim's colour: the target's, the trinket's band, or neutral.
                Background = new LinearGradientBrush(WithAlpha(Mix(PlateTop, rim, 0.28)), WithAlpha(PlateBottom), 90),
                BorderBrush = new SolidColorBrush(rim),
                BorderThickness = new Thickness(Rim * scale),
                CornerRadius = new CornerRadius(5 * scale),
                Padding = new Thickness(TavernLayout.MarkerPadding * scale, 0, TavernLayout.MarkerPadding * scale, 0),
                IsHitTestVisible = false,
                // The lines were built to fit (ChoiceNote, generous glyph width): drawn at their size, never shrunk.
                Child = content,
            };
            Canvas.SetLeft(label, rects[i].Left);
            Canvas.SetTop(label, rects[i].Top);
            OverlayLayer.Add(_canvas, label);
            _labels.Add(label);
            FirstLabel ??= rects[i];
        }
    }

    /// <summary>One line of a minion's or a spell's label: its pill, the guide's name, a tier chip, the count, the suffix.</summary>
    private static FrameworkElement Row(ChoiceNoteLine line, double scale)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Height = PanelTypography.Marker * 1.3 * scale,
            Tag = LineTag,
        };

        void Add(FrameworkElement piece)
        {
            piece.VerticalAlignment = VerticalAlignment.Center;
            if (row.Children.Count > 0)
            {
                piece.Margin = new Thickness(4 * scale, 0, 0, 0);
            }

            row.Children.Add(piece);
        }

        if (line.Pill.Length > 0)
        {
            var fill = line.PillFill;
            Add(new Border
            {
                Tag = PillTag,
                Background = fill != null ? HexBrush.Of(fill) : Brushes.Transparent,
                BorderBrush = HexBrush.Of(fill ?? ChoiceNote.GuideOutline),
                BorderThickness = new Thickness(1 * scale),
                CornerRadius = new CornerRadius(8 * scale),
                Padding = new Thickness(5 * scale, 0, 5 * scale, 0),
                Child = Text(line.Pill, PanelTypography.Small, scale, HexBrush.Of(fill != null ? ChoiceNote.PillInk : ChoiceNote.GuideOutline), FontWeights.Bold),
            });
        }

        if (line.Name.Length > 0)
        {
            Add(Text(line.Name, PanelTypography.Marker, scale, HexBrush.Of(line.NameColour), FontWeights.Bold));
        }

        if (line.Tier != null)
        {
            // HDT's tier colours, as on the panel's tier bars: S is 1.
            Add(new Border
            {
                Tag = TierTag,
                Background = GuideView.TierBrush("SABCD".IndexOf(line.Tier, StringComparison.Ordinal) + 1),
                CornerRadius = new CornerRadius(3 * scale),
                Padding = new Thickness(4 * scale, 0, 4 * scale, 0),
                Child = Text(line.Tier, PanelTypography.Small, scale, Brushes.White, FontWeights.Bold),
            });
        }

        if (line.Count.Length > 0)
        {
            Add(Text(line.Count, PanelTypography.Marker, scale, Brushes.White, FontWeights.Bold));
        }

        if (line.Suffix.Length > 0)
        {
            Add(Text(line.Suffix, PanelTypography.Small, scale, HexBrush.Of(HeroNote.Secondary), FontWeights.Normal));
        }

        return row;
    }

    /// <summary>
    /// A trinket's label: the figure large in its band's colour, outlined, then the small lines beside it. In a one-line label
    /// the figure's line is the label's inside (<paramref name="inside"/>): its digits stand on the baseline, well below the
    /// line's top, and hold there.
    /// </summary>
    private static FrameworkElement TrinketRow(TrinketFigure trinket, double inside, double scale)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Tag = LineTag };
        if (trinket.Figure != null)
        {
            var figure = Text(trinket.Figure, PanelTypography.ChoiceNote, scale, HexBrush.Of(trinket.Colour), FontWeights.ExtraBold);
            figure.Tag = FigureTag;
            if (inside < figure.FontSize * figure.FontFamily.LineSpacing)
            {
                figure.LineHeight = inside;
                figure.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            }

            figure.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 3 * scale, ShadowDepth = 0, Opacity = 1 };
            row.Children.Add(figure);
        }

        var details = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(trinket.Figure != null ? PanelTypography.Marker * MarkerText.GlyphWidthEm * scale : 0, 0, 0, 0),
        };
        foreach (var detail in trinket.Details)
        {
            details.Children.Add(Text(detail.Text, PanelTypography.Small, scale, HexBrush.Of(detail.Colour), detail.Colour == HeroNote.Secondary ? FontWeights.Normal : FontWeights.Bold));
        }

        row.Children.Add(details);
        return row;
    }

    private static TextBlock Text(string text, double size, double scale, Brush brush, FontWeight weight) => new()
    {
        Text = text,
        FontSize = size * scale,
        FontWeight = weight,
        Foreground = brush,
        TextWrapping = TextWrapping.NoWrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static Color Mix(Color from, Color to, double amount) => Color.FromRgb(
        (byte)(from.R + (to.R - from.R) * amount), (byte)(from.G + (to.G - from.G) * amount), (byte)(from.B + (to.B - from.B) * amount));

    private static Color WithAlpha(Color colour) => Color.FromArgb(0xEE, colour.R, colour.G, colour.B);
}
