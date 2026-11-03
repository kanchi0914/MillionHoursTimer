using MillionHoursTimer.Platform;
using Velopack;

namespace MillionHoursTimer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // インストール・更新・アンインストール時に Velopack から呼ばれたら、ここで処理して終了する
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => AutoStart.Set(false))
            .Run();

        using var instance = SingleInstance.TryAcquire();
        if (instance == null) return 0;

        var app = new App(args, instance);
        app.InitializeComponent();
        return app.Run();
    }
}
