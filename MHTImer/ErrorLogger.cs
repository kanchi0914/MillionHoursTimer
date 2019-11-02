using System;
using System.Windows;

namespace MHTimer
{
    public static class ErrorLogger
    {
        public static void ShowErrorMessage(Exception ex)
        {
            MessageBox.Show($"エラー:{ex.ToString()}");
        }
    }
}
