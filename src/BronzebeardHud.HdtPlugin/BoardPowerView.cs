using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// The board's power under the list of the "Compositions" panel (Ali, 2026-10-06: "a visual component for the power of my
/// board against the average, a red / yellow / green / shiny indicator"), from WarbandCurve.Compare: a gauge of four
/// segments (red, yellow, green, gold, left to right; the board's level lit, the others dimmed), a badge in the level's
/// colour with its sign and the percentage ("▲ +58%"), then the figures it rests on ("Board 190 · hero avg 120 at turn 8").
/// Without a level (no curve, too early, a curve that cannot be trusted at this turn: WarbandComparison.Note), every segment
/// is grey and the badge says "–": no colour is invented, and no percentage that the data cannot back. The level never
/// rests on the colour alone: the sign and the lit segment's place say it too. One line of PanelFit.FooterLine; the figures
/// wrap rather than being cut, at PanelTypography.Small.
/// </summary>
internal static class BoardPowerView
{
    /// <summary>Tags of the drawn pieces, for the simulation's self-test: "board-power-gauge-0" to "-3", "board-power-badge".</summary>
    public const string GaugeTag = "board-power-gauge-";
    public const string BadgeTag = "board-power-badge";

    private static readonly Brush DetailsBrush = Brushes.White;

    public static FrameworkElement Build(WarbandComparison comparison, double scale)
    {
        var power = comparison.Power;
        var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 4 * scale, 0, 0) };

        var gauge = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6 * scale, 0) };
        for (var i = 0; i < BoardPowerLevels.Gauge.Count; i++)
        {
            var level = BoardPowerLevels.Gauge[i];
            var lit = level == power;
            gauge.Children.Add(new Border
            {
                Tag = GaugeTag + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Width = 11 * scale,
                Height = (lit ? 14 : 8) * scale,
                Margin = new Thickness(0, 0, i < BoardPowerLevels.Gauge.Count - 1 ? 2 * scale : 0, 0),
                CornerRadius = new CornerRadius(2 * scale),
                Background = HexBrush.Of(power == BoardPower.None ? BoardPowerLevels.Colour(BoardPower.None) : BoardPowerLevels.Colour(level)),
                BorderBrush = lit ? Brushes.White : null,
                BorderThickness = new Thickness(lit ? 1 : 0),
                Opacity = lit ? 1 : 0.3,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        DockPanel.SetDock(gauge, Dock.Left);
        row.Children.Add(gauge);

        var sign = power == BoardPower.None || comparison.Percent == null
            ? BoardPowerLevels.Symbol(BoardPower.None)
            : BoardPowerLevels.Symbol(power) + " " + comparison.Percent;
        var badge = new Border
        {
            Tag = BadgeTag,
            Height = 16 * scale,
            CornerRadius = new CornerRadius(4 * scale),
            Padding = new Thickness(5 * scale, 0, 5 * scale, 0),
            Margin = new Thickness(0, 0, 6 * scale, 0),
            Background = HexBrush.Of(BoardPowerLevels.Colour(power)),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = sign,
                FontSize = PanelTypography.Small * scale,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
            },
        };
        if (power == BoardPower.Shiny)
        {
            // Shiny shines: a gold glow around its badge, on top of its star and its place at the end of the gauge.
            badge.Effect = new DropShadowEffect { Color = Color.FromRgb(0xFF, 0xC4, 0x00), BlurRadius = 8 * scale, ShadowDepth = 0, Opacity = 0.9 };
            badge.BorderBrush = HexBrush.Of("#FFB300");
            badge.BorderThickness = new Thickness(1);
        }

        DockPanel.SetDock(badge, Dock.Left);
        row.Children.Add(badge);

        var text = comparison.Note != null ? $"{comparison.Details} · {comparison.Note}" : comparison.Details;
        row.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = PanelTypography.Small * scale,
            FontWeight = FontWeights.Bold,
            Foreground = DetailsBrush,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return row;
    }
}
