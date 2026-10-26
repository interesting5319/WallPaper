using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace WallPaper;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        bool createdNew;
        using var mutex = new Mutex(true, @"Local\WallPaper_SingleInstance", out createdNew);
        if (!createdNew) return; // 已有实例在运行

        Log.Init();
        Log.Info("构建版本 2026-10-23-B");
        CrashGuard.OnStarted();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.DumpCrash(e.ExceptionObject as Exception);
            Log.Error("未处理异常", e.ExceptionObject as Exception);
            try { } catch { }
            CrashGuard.MarkAndRelaunch();
            Environment.Exit(1);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("未观察任务异常", e.Exception);
            e.SetObserved();
        };

        var config = ConfigStore.Load();

        var app = new System.Windows.Application
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown,
        };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log.Error("UI 线程异常", e.Exception);
            e.Handled = true; // 单次异常不带走整个程序
        };

        var displays = new DisplayManager(config);
        var governor = new PowerGovernor(config);
        var tray = new TrayIcon(config, displays, governor);

        displays.Notify += msg => tray.Balloon("WallPaper", msg, error: true);
        displays.Start();

        governor.PauseStateChanged += paused => displays.SetGovernorPaused(paused);
        governor.Start();

        // 壁纸失败时补格式化提示（VP9 等）
        displays.Notify += msg =>
        {
            if (msg.Contains("无法播放", StringComparison.Ordinal))
                RuntimeChecks.OnPlaybackFailure("", msg);
        };

        tray.Show();
        Log.Info($"WPF 渲染层级 Tier={System.Windows.Media.RenderCapability.Tier >> 16}（0=软件渲染 1=部分硬件 2=完整硬件）");
        RuntimeChecks.Startup();

        if (CrashGuard.RecoveredFromCrash)
            tray.Balloon("WallPaper", "检测到上次异常退出，已恢复上次壁纸设置。");
        else
            tray.Balloon("WallPaper", "正在运行。右键托盘图标设置壁纸，视频格式支持 MP4 / WebM。");

        Autostart.Apply(config.Autostart);
        ConfigStore.Save(config);

        app.Exit += (_, _) =>
        {
            ConfigStore.Save(config);
            tray.Dispose();
        };

        app.Run();
    }
}