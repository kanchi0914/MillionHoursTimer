using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MillionHoursTimer.UI;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>ListView の列見出しをクリックしたときの並べ替え (もう一度クリックで逆順)</summary>
internal sealed class ListSorter
{
    private readonly ListCollectionView _view;
    private readonly IReadOnlyDictionary<string, string> _properties;
    private string? _current;
    private ListSortDirection _direction;

    /// <param name="properties">見出しの文字列 → 並べ替えに使うプロパティ名</param>
    public ListSorter(ListView list, IReadOnlyDictionary<string, string> properties)
    {
        _view = (ListCollectionView)CollectionViewSource.GetDefaultView(list.ItemsSource);
        _view.IsLiveSorting = true;
        _properties = properties;
        list.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(OnHeaderClick));
    }

    private void OnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader { Column: not null } header) return;
        if (!_properties.TryGetValue(header.Content as string ?? "", out var property)) return;

        _direction = _current == property && _direction == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : property == "Name" ? ListSortDirection.Ascending : ListSortDirection.Descending;
        _current = property;

        using (_view.DeferRefresh())
        {
            _view.SortDescriptions.Clear();
            _view.SortDescriptions.Add(new SortDescription(property, _direction));
            _view.LiveSortingProperties.Clear();
            _view.LiveSortingProperties.Add(property);
        }
    }
}

internal static class Dialogs
{
    public static void Info(Window? owner, string message) =>
        Show(owner, message, MessageBoxImage.Information);

    public static void Warn(Window? owner, string message) =>
        Show(owner, message, MessageBoxImage.Warning);

    public static bool Confirm(Window? owner, string message) =>
        (owner != null
            ? MessageBox.Show(owner, message, "MillionHoursTimer", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel)
            : MessageBox.Show(message, "MillionHoursTimer", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel))
        == MessageBoxResult.OK;

    /// <summary>ファイルが Excel などで開かれていて保存できなかったときの案内</summary>
    public static void SaveFailed(Window? owner, Exception ex) =>
        Warn(owner, "保存できませんでした。データフォルダのファイルを Excel などで開いている場合は、閉じてからもう一度お試しください。\n\n" + ex.Message);

    public static bool IsSaveError(Exception ex) => ex is IOException or UnauthorizedAccessException;

    private static void Show(Window? owner, string message, MessageBoxImage image)
    {
        if (owner != null) MessageBox.Show(owner, message, "MillionHoursTimer", MessageBoxButton.OK, image);
        else MessageBox.Show(message, "MillionHoursTimer", MessageBoxButton.OK, image);
    }
}
