using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// One row of the power inset under the "Compositions" panel (PowerInset; Ali, 2026-10-06: "a visual component for the power
/// of my board against the average, a red / yellow / green / shiny indicator", then "take it out into a small inset, with a
/// glowing traffic-light effect"), from a WarbandComparison: four lamps in a dark housing, red, yellow, green and gold from left
/// to right, the board's level lit and glowing in its colour (BoardPowerLevels.Halo), the others dimmed; a badge in the level's
/// colour with its sign and the percentage ("▲ +58%"); then the figures it rests on ("Board 190 · hero avg 120 at turn 8").
/// Without a level (no curve, too early, a curve that cannot be trusted at this turn: WarbandComparison.Note), every lamp is
/// grey and none glows, and the badge says "–": no colour is invented, and no percentage that the data cannot back. The level
/// never rests on the colour alone: the sign and the lit lamp's place say it too. The same code draws the player's row and the
/// opponent's, told apart by their tags. One row of PanelFit.InsetRow; the figures wrap rather than being cut, at
/// PanelTypography.Small.
/// </summary>
internal static class BoardPowerView
{
    /// <summary>Tag prefixes of the two rows, for the simulation's self-test: "board-power-gauge-0" to "-3", "board-power-badge".</summary>
    public const string Own = "board-power-";
    public const string Opponent = "opp-power-";

    public static string LampTag(string row, int lamp) => row + "gauge-" + lamp.ToString(CultureInfo.InvariantCulture);

    public static string BadgeTag(string row) => row + "badge";

    /// <summary>The lamp housing: the dark casing of a traffic light.</summary>
    private static readonly Brush HousingBrush = new SolidColorBrush(Color.FromRgb(0x08, 0x08, 0x0D));
    private static readonly Brush HousingEdge = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));

    /// <summary>A lamp that is off: its colour, faint, so that the four places read even when none is lit.</summary>
    public const double OffOpacity = 0.22;

    private const double LampSize = 12;
    private const double LampGap = 3;

    /// <param name="row"><see cref="Own"/> or <see cref="Opponent"/>: the tags of its pieces.</param>
    public static FrameworkElement Build(WarbandComparison comparison, double scale, string row)
    {
        var power = comparison.Power;
        var line = new DockPanel { LastChildFill = true, MinHeight = PanelFit.InsetRow * scale };

        var lamps = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var lit = BoardPowerLevels.LitLamp(power);
        for (var i = 0; i < BoardPowerLevels.Gauge.Count; i++)
        {
            var on = i == lit;
            var lamp = new Ellipse
            {
                Tag = LampTag(row, i),
                Width = LampSize * scale,
                Height = LampSize * scale,
                Margin = new Thickness(i > 0 ? LampGap * scale : 0, 0, 0, 0),
                Fill = HexBrush.Of(BoardPowerLevels.Colour(power == BoardPower.None ? BoardPower.None : BoardPowerLevels.Gauge[i])),
                Opacity = on ? 1 : OffOpacity,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (on && BoardPowerLevels.Halo(power) is { } halo)
            {
                // The traffic-light effect: the lit lamp glows in its level's colour, a white rim makes it read as a lamp.
                lamp.Effect = new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString(halo), BlurRadius = 20 * scale, ShadowDepth = 0, Opacity = 1 };
                lamp.Stroke = Brushes.White;
                lamp.StrokeThickness = 1;
            }

            lamps.Children.Add(lamp);
        }

        var housing = new Border
        {
            Background = HousingBrush,
            BorderBrush = HousingEdge,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8 * scale),
            Padding = new Thickness(4 * scale, 1 * scale, 4 * scale, 1 * scale),
            Margin = new Thickness(0, 0, 6 * scale, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = lamps,
        };
        DockPanel.SetDock(housing, Dock.Left);
        line.Children.Add(housing);

        var sign = power == BoardPower.None || comparison.Percent == null
            ? BoardPowerLevels.Symbol(BoardPower.None)
            : BoardPowerLevels.Symbol(power) + " " + comparison.Percent;
        var badge = new Border
        {
            Tag = BadgeTag(row),
            Height = PanelFit.InsetRow * scale,
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
            // Shiny shines: a gold glow around its badge too, on top of its star and its lamp at the end of the row.
            badge.Effect = new DropShadowEffect { Color = Color.FromRgb(0xFF, 0xC4, 0x00), BlurRadius = 8 * scale, ShadowDepth = 0, Opacity = 0.9 };
            badge.BorderBrush = HexBrush.Of("#FFB300");
            badge.BorderThickness = new Thickness(1);
        }

        DockPanel.SetDock(badge, Dock.Left);
        line.Children.Add(badge);

        var text = comparison.Note != null ? $"{comparison.Details} · {comparison.Note}" : comparison.Details;
        line.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = PanelTypography.Small * scale,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return line;
    }
}
