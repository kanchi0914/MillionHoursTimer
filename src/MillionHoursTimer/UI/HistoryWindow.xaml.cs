using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MillionHoursTimer.Core;

namespace MillionHoursTimer.UI;

public sealed record HistoryRow(string Name, string TotalText, string AverageText, string ShareText);

public partial class HistoryWindow : Window
{
    private const int MaxSeries = 8;

    // 色の見分けやすさを優先したパレット (最後の灰色は「その他」)
    private static readonly Brush[] Palette =
    [
        Hex("#4E79A7"), Hex("#F28E2B"), Hex("#59A14F"), Hex("#E15759"),
        Hex("#B07AA1"), Hex("#76B7B2"), Hex("#EDC948"), Hex("#FF9DA7"),
    ];
    private static readonly Brush OtherBrush = Hex("#BAB0AC");

    private readonly TrackerService _tracker;

    internal HistoryWindow(TrackerService tracker)
    {
        _tracker = tracker;
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    private static Brush Hex(string hex)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    private void OnRangeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) Render();
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => Render();

    private void Render()
    {
        var tag = (RangeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "30";
        var today = DateOnly.FromDateTime(DateTime.Now);
        var monthly = tag == "12m";
        var from = monthly ? new DateOnly(today.Year, today.Month, 1).AddMonths(-11) : today.AddDays(1 - int.Parse(tag));
        var days = today.DayNumber - from.DayNumber + 1;

        var history = _tracker.GetHistory(from, today);
        var top = history.Take(MaxSeries).ToList();
        var others = history.Skip(MaxSeries).ToList();

        var series = top.Select((h, i) => new ChartSeries(h.Name, Palette[i])).ToList();
        if (others.Count > 0) series.Add(new ChartSeries($"その他 ({others.Count})", OtherBrush));

        long SecondsIn(HistoryEntry h, DateOnly start, DateOnly end) =>
            h.SecondsByDay.Where(d => d.Key >= start && d.Key <= end).Sum(d => d.Value);

        ChartBar Bar(DateOnly start, DateOnly end, string label, string tooltip)
        {
            var values = top.Select(h => SecondsIn(h, start, end)).ToList();
            if (others.Count > 0) values.Add(others.Sum(h => SecondsIn(h, start, end)));
            return new ChartBar(label, tooltip, values);
        }

        var bars = new List<ChartBar>();
        if (monthly)
        {
            for (var m = from; m <= today; m = m.AddMonths(1))
            {
                bars.Add(Bar(m, m.AddMonths(1).AddDays(-1), m.ToString("M月"), m.ToString("yyyy年M月")));
            }
        }
        else
        {
            for (var d = from; d <= today; d = d.AddDays(1))
            {
                bars.Add(Bar(d, d, d.ToString("M/d"), d.ToString("yyyy/M/d (ddd)")));
            }
        }
        Chart.SetData(series, bars);

        Legend.Children.Clear();
        foreach (var s in series)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 4) };
            item.Children.Add(new Rectangle { Width = 10, Height = 10, Fill = s.Brush, Margin = new Thickness(0, 0, 5, 0) });
            item.Children.Add(new TextBlock { Text = s.Name, FontSize = 12 });
            Legend.Children.Add(item);
        }

        var total = history.Sum(h => h.Total);
        Summary.Text = $"{from:yyyy/MM/dd} 〜 {today:yyyy/MM/dd}　合計 {TimeText.FormatShort(total)}";
        Table.ItemsSource = history.Select(h => new HistoryRow(
            h.Name,
            TimeText.FormatDuration(h.Total),
            TimeText.FormatDuration(h.Total / days),
            total > 0 ? $"{h.Total * 100.0 / total:0.0}%" : "-")).ToList();
    }
}
