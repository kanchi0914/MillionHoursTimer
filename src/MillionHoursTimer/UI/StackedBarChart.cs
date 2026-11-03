using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MillionHoursTimer.Core;

namespace MillionHoursTimer.UI;

public sealed record ChartSeries(string Name, Brush Brush);

/// <summary>棒1本分。Values は系列ごとの秒数。</summary>
public sealed record ChartBar(string Label, string Tooltip, IReadOnlyList<long> Values)
{
    public long Total => Values.Sum();
}

/// <summary>積み上げ棒グラフ (外部ライブラリを使わずに描く)</summary>
public sealed class StackedBarChart : FrameworkElement
{
    private static readonly Typeface Font = new("Yu Gothic UI");
    private static readonly Brush AxisBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6)), 1);
    private static readonly Brush HoverBrush = new SolidColorBrush(Color.FromArgb(0x18, 0, 0, 0));

    private const double LeftMargin = 44;
    private const double BottomMargin = 22;
    private const double TopMargin = 8;

    private IReadOnlyList<ChartBar> _bars = [];
    private IReadOnlyList<ChartSeries> _series = [];
    private int _hover = -1;
    private readonly ToolTip _tooltip = new() { Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse };

    static StackedBarChart()
    {
        AxisBrush.Freeze();
        GridPen.Freeze();
        HoverBrush.Freeze();
    }

    public StackedBarChart()
    {
        ToolTip = _tooltip;
        ToolTipService.SetInitialShowDelay(this, 0);
        ToolTipService.SetShowDuration(this, int.MaxValue);
    }

    public void SetData(IReadOnlyList<ChartSeries> series, IReadOnlyList<ChartBar> bars)
    {
        _series = series;
        _bars = bars;
        _hover = -1;
        InvalidateVisual();
    }

    private Rect PlotArea => new(LeftMargin, TopMargin,
        Math.Max(0, ActualWidth - LeftMargin - 8), Math.Max(0, ActualHeight - TopMargin - BottomMargin));

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        var plot = PlotArea;
        if (plot.Width <= 0 || plot.Height <= 0) return;

        var max = _bars.Count == 0 ? 0 : _bars.Max(b => b.Total);
        var (stepSeconds, maxSeconds) = NiceScale(max);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // 目盛り (時間単位)
        for (long v = 0; v <= maxSeconds; v += stepSeconds)
        {
            var y = plot.Bottom - plot.Height * v / maxSeconds;
            dc.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            var text = Text(FormatAxis(v), dpi);
            dc.DrawText(text, new Point(plot.Left - text.Width - 6, y - text.Height / 2));
        }

        if (_bars.Count == 0) return;
        var slot = plot.Width / _bars.Count;
        var barWidth = Math.Max(1, slot * 0.7);
        var labelEvery = Math.Max(1, (int)Math.Ceiling(40 / slot));

        for (var i = 0; i < _bars.Count; i++)
        {
            var x = plot.Left + slot * i + (slot - barWidth) / 2;
            if (i == _hover) dc.DrawRectangle(HoverBrush, null, new Rect(plot.Left + slot * i, plot.Top, slot, plot.Height));

            var y = plot.Bottom;
            var bar = _bars[i];
            for (var s = 0; s < bar.Values.Count && s < _series.Count; s++)
            {
                var h = plot.Height * bar.Values[s] / maxSeconds;
                if (h <= 0) continue;
                y -= h;
                dc.DrawRectangle(_series[s].Brush, null, new Rect(x, y, barWidth, h));
            }

            if (i % labelEvery == 0)
            {
                var label = Text(bar.Label, dpi);
                dc.DrawText(label, new Point(plot.Left + slot * i + slot / 2 - label.Width / 2, plot.Bottom + 4));
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var plot = PlotArea;
        var p = e.GetPosition(this);
        var index = _bars.Count == 0 || p.X < plot.Left || p.X > plot.Right ? -1
            : Math.Min(_bars.Count - 1, (int)((p.X - plot.Left) / (plot.Width / _bars.Count)));
        if (index == _hover) return;

        _hover = index;
        _tooltip.Content = index >= 0 ? BuildTooltip(_bars[index]) : null;
        _tooltip.IsOpen = index >= 0;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        _tooltip.IsOpen = false;
        InvalidateVisual();
    }

    private object BuildTooltip(ChartBar bar)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = $"{bar.Tooltip}　合計 {TimeText.FormatShort(bar.Total)}", FontWeight = FontWeights.SemiBold });
        for (var s = _series.Count - 1; s >= 0; s--)
        {
            if (s >= bar.Values.Count || bar.Values[s] == 0) continue;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            row.Children.Add(new Border { Width = 10, Height = 10, Background = _series[s].Brush, Margin = new Thickness(0, 0, 6, 0) });
            row.Children.Add(new TextBlock { Text = $"{_series[s].Name}  {TimeText.FormatShort(bar.Values[s])}" });
            panel.Children.Add(row);
        }
        return panel;
    }

    private FormattedText Text(string text, double dpi) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Font, 11, AxisBrush, dpi);

    private static string FormatAxis(long seconds) => seconds % 3600 == 0 ? $"{seconds / 3600}h" : $"{seconds / 60}m";

    /// <summary>目盛りの間隔と最大値をきりのいい値にする</summary>
    private static (long Step, long Max) NiceScale(long max)
    {
        long[] steps = [600, 1800, 3600, 7200, 3 * 3600, 6 * 3600, 12 * 3600, 24 * 3600, 48 * 3600, 120 * 3600, 240 * 3600];
        var step = steps.FirstOrDefault(s => max / s < 5, steps[^1]);
        var top = Math.Max(step, (max + step - 1) / step * step);
        return (step, top);
    }
}
