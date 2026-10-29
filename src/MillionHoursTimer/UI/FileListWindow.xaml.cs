using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MillionHoursTimer.Core;

namespace MillionHoursTimer.UI;

public sealed class FileRowViewModel(string name) : ObservableObject
{
    private long _totalSeconds;
    private DateOnly? _lastDay;
    private bool _isRecording;

    public string Name { get; } = name;

    public long TotalSeconds
    {
        get => _totalSeconds;
        private set
        {
            if (Set(ref _totalSeconds, value)) Raise(nameof(TotalText));
        }
    }

    public DateOnly? LastDay
    {
        get => _lastDay;
        private set
        {
            if (Set(ref _lastDay, value)) Raise(nameof(LastDayText));
        }
    }

    public bool IsRecording
    {
        get => _isRecording;
        private set => Set(ref _isRecording, value);
    }

    public string TotalText => TimeText.FormatDuration(TotalSeconds);
    public string LastDayText => LastDay?.ToString("yyyy/MM/dd") ?? "-";

    public void Update(FileStatus s)
    {
        TotalSeconds = s.TotalSeconds;
        LastDay = s.LastDay;
        IsRecording = s.IsRecording;
    }
}

public partial class FileListWindow : Window
{
    private readonly TrackerService _tracker;
    private readonly string _process;
    private readonly ObservableCollection<FileRowViewModel> _rows = [];

    internal FileListWindow(TrackerService tracker, string process, string appName)
    {
        _tracker = tracker;
        _process = process;
        InitializeComponent();
        Title = $"{appName} のファイル別作業時間";
        FileList.ItemsSource = _rows;
        _ = new ListSorter(FileList, new Dictionary<string, string>
        {
            ["ファイル名"] = nameof(FileRowViewModel.Name),
            ["累積作業時間"] = nameof(FileRowViewModel.TotalSeconds),
            ["最終作業日"] = nameof(FileRowViewModel.LastDay),
        });
        Refresh();
    }

    public void Refresh()
    {
        var files = _tracker.GetFiles(_process);
        var existing = _rows.ToDictionary(r => r.Name, StringComparer.Ordinal);
        var current = files.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var row in _rows.Where(r => !current.Contains(r.Name)).ToList()) _rows.Remove(row);
        foreach (var file in files)
        {
            if (!existing.TryGetValue(file.Name, out var row))
            {
                row = new FileRowViewModel(file.Name);
                _rows.Add(row);
            }
            row.Update(file);
        }
        EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private List<FileRowViewModel> Selected => FileList.SelectedItems.Cast<FileRowViewModel>().ToList();

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e) => MergeMenu.IsEnabled = FileList.SelectedItems.Count > 1;

    private void OnMerge(object sender, RoutedEventArgs e)
    {
        var selected = Selected;
        if (selected.Count < 2) return;
        var name = InputDialog.Ask(this, "選択した項目をまとめる",
            $"{selected.Count}件の記録を1つにまとめます。まとめた後の名前を入力してください。", selected[0].Name);
        if (string.IsNullOrEmpty(name)) return;
        Run(() => _tracker.MergeFiles(_process, selected.Select(s => s.Name).ToList(), name));
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        var text = new StringBuilder();
        foreach (var row in Selected) text.AppendLine($"{row.Name}の作業時間 {row.TotalText.Trim()}");
        if (text.Length > 0) Clipboard.SetText(text.ToString());
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        var selected = Selected;
        if (selected.Count == 0) return;
        if (!Dialogs.Confirm(this, $"選択した {selected.Count}件 の記録を削除します。よろしいですか？\n(アプリ全体の作業時間は変わりません)")) return;
        Run(() => _tracker.DeleteFiles(_process, selected.Select(s => s.Name).ToList()));
    }

    private void OnDeleteAll(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0) return;
        if (!Dialogs.Confirm(this, "ファイル別の記録を全て削除します。よろしいですか？\n(アプリ全体の作業時間は変わりません)")) return;
        Run(() => _tracker.DeleteFiles(_process, _rows.Select(r => r.Name).ToList()));
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (Dialogs.IsSaveError(ex))
        {
            Dialogs.SaveFailed(this, ex);
        }
        Refresh();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
