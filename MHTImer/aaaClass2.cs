using System.Windows.Forms;
using System.Security.Permissions;
using System.ComponentModel;
using System;

/// <summary>
/// 数字とバックスペース以外の入力を無効にしたTextBox
/// </summary>
public class NumericTextBox : TextBox
{
    private const int WM_PASTE = 0x302;

    [SecurityPermission(SecurityAction.Demand,
        Flags = SecurityPermissionFlag.UnmanagedCode)]
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_PASTE)
        {
            IDataObject iData = Clipboard.GetDataObject();
            //文字列がクリップボードにあるか
            if (iData != null && iData.GetDataPresent(DataFormats.Text))
            {
                string clipStr = (string)iData.GetData(DataFormats.Text);
                //クリップボードの文字列が数字のみか調べる
                if (!System.Text.RegularExpressions.Regex.IsMatch(
                    clipStr,
                    @"^[0-9]+$"))
                {
                    return;
                }
            }
        }

        base.WndProc(ref m);
    }

    public NumericTextBox()
        : base()
    {
        //IMEを無効にする
        base.ImeMode = ImeMode.Disable;
        //数字以外で入力が可能な文字
        this.SetAllowKeyChars(new char[] { '\b' });
    }

    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public new ImeMode ImeMode
    {
        get { return base.ImeMode; }
        set { }
    }

    private char[] _allowKeyChars;
    /// <summary>
    /// 数字以外で入力が可能な文字を設定する
    /// </summary>
    public void SetAllowKeyChars(char[] keyChars)
    {
        this._allowKeyChars = keyChars;
    }
    /// <summary>
    /// 数字以外で入力が可能な文字を取得する
    /// </summary>
    public char[] GetAllowKeyChars()
    {
        return this._allowKeyChars;
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        //数字以外が入力された時はキャンセルする
        if ((e.KeyChar < '0' || '9' < e.KeyChar) &&
            Array.IndexOf(this._allowKeyChars, e.KeyChar) < 0)
        {
            e.Handled = true;
        }
    }
}