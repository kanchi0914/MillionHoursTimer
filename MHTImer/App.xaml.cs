using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MHTimer.Components;


namespace MHTimer
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application
    {

        [System.Runtime.InteropServices.DllImport("Kernel32.dll")]
        public static extern bool AttachConsole(int processId);

        private System.Threading.Mutex mutex = new System.Threading.Mutex(false, "MHTimer");
        private MainWindow mainWindow;

        public void SetMainWindow(MainWindow mainWindow)
        {

        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {

            AttachConsole(-1);
            SetFont();
            // ミューテックスの所有権を要求
            if (!mutex.WaitOne(0, false))
            {
                // 既に起動しているため終了させる
                MessageBox.Show("MHTimer は既に起動しています。", "二重起動防止", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                mutex.Close();
                mutex = null;
                this.Shutdown();
            }

            foreach (string arg in e.Args)
            {
                switch (arg)
                {
                    case "-v":
                        Settings.IsLaunchedFromConsole = true;
                        break;
                    default:
                        break;
                }
            }

        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (mutex != null)
            {
                mutex.ReleaseMutex();
                mutex.Close();
            }
        }

        private void SetFont()
        {
            var font = new System.Windows.Media.FontFamily("ＭＳ ゴシック");

            var style = new Style(typeof(Window));
            style.Setters.Add(new Setter(Window.FontFamilyProperty, font));

            FrameworkElement.StyleProperty.OverrideMetadata(typeof(Window), new FrameworkPropertyMetadata(style));
        }

        private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {

        }

        //protected override void OnExit(ExitEventArgs e)
        //{
        //    base.OnExit(e);
        //    notifyIcon.Dispose();
        //}
    }
}
