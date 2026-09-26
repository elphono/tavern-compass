using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BronzebeardHud.Stats;

namespace BronzebeardHud.HdtPlugin;

/// <summary>
/// One column per offered hero, drawn on HDT's overlay canvas under the hero picker.
/// Built in code rather than XAML so that the plugin compiles under WSL.
/// </summary>
internal sealed class HeroPickPanel : Border
{
    private const double ColumnWidth = 190;

    private readonly StackPanel _columns = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _status = new() { Foreground = Brushes.Gold, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

    public HeroPickPanel()
    {
        Background = new SolidColorBrush(Color.FromArgb(0xD9, 0x14, 0x14, 0x1E));
        BorderBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x48, 0x0F));
        BorderThickness = new Thickness(2);
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(10);
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
        Child = new StackPanel { Children = { _columns, _status } };
    }

    public void Show(IReadOnlyList<HeroPickRow> rows, string? status)
    {
        _columns.Children.Clear();
        foreach (var row in rows)
        {
            _columns.Children.Add(BuildColumn(row));
        }

        _status.Text = status ?? string.Empty;
        _status.Visibility = string.IsNullOrEmpty(status) ? Visibility.Collapsed : Visibility.Visible;
        _status.MaxWidth = ColumnWidth * rows.Count;
        Visibility = Visibility.Visible;
    }

    public void Hide() => Visibility = Visibility.Collapsed;

    /// <summary>Centred horizontally, just under the offered hero portraits.</summary>
    public void Reposition(Canvas canvas)
    {
        if (Visibility != Visibility.Visible || canvas.ActualWidth <= 0)
        {
            return;
        }

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(this, (canvas.ActualWidth - DesiredSize.Width) / 2);
        Canvas.SetTop(this, canvas.ActualHeight * 0.70);
    }

    private static UIElement BuildColumn(HeroPickRow row)
    {
        var column = new StackPanel { Width = ColumnWidth, Margin = new Thickness(4, 0, 4, 0) };
        if (!row.HasData)
        {
            column.Children.Add(Text("no data", 14, Brushes.LightGray));
            return column;
        }

        foreach (var figures in row.Figures)
        {
            column.Children.Add(Text(figures.Tier ?? "–", 30, TierBrush(figures.Tier), FontWeights.Bold));
            column.Children.Add(Text($"avg place {figures.AveragePlacement.ToString("0.00", CultureInfo.InvariantCulture)}", 15, Brushes.White));
            if (figures.PickRate is { } pickRate)
            {
                column.Children.Add(Text($"picked {(pickRate * 100).ToString("0.0", CultureInfo.InvariantCulture)} %", 13, Brushes.White));
            }

            var games = figures.DataPoints > 0 ? $" · {figures.DataPoints.ToString("N0", CultureInfo.InvariantCulture)} games" : string.Empty;
            column.Children.Add(Text(figures.Source + games, 11, Brushes.LightGray));
        }

        return column;
    }

    private static TextBlock Text(string text, double size, Brush brush, FontWeight? weight = null) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = brush,
        FontWeight = weight ?? FontWeights.Normal,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private static Brush TierBrush(string? tier) => tier switch
    {
        "S" => new SolidColorBrush(Color.FromRgb(0xFF, 0x4D, 0x4D)),
        "A" => new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x1C)),
        "B" => new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x3D)),
        "C" => new SolidColorBrush(Color.FromRgb(0x7C, 0xD9, 0x5C)),
        "D" => new SolidColorBrush(Color.FromRgb(0x4D, 0xA6, 0xFF)),
        _ => Brushes.LightGray,
    };
}
