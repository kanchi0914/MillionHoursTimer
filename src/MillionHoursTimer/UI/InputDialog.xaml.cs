using System.Windows;

namespace MillionHoursTimer.UI;

public partial class InputDialog : Window
{
    private InputDialog()
    {
        InitializeComponent();
    }

    /// <returns>OK なら入力された文字列、キャンセルなら null</returns>
    public static string? Ask(Window owner, string title, string message, string initial, string? hint = null)
    {
        var dialog = new InputDialog { Owner = owner, Title = title };
        dialog.Message.Text = message;
        dialog.Input.Text = initial;
        dialog.Input.SelectAll();
        if (hint != null)
        {
            dialog.Hint.Text = hint;
            dialog.Hint.Visibility = Visibility.Visible;
        }
        return dialog.ShowDialog() == true ? dialog.Input.Text.Trim() : null;
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
