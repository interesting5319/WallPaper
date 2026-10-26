using System;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace WallPaper;

/// <summary>
/// 节能仲裁：全屏应用（含 D3D 全屏游戏、演示模式）或电池供电时暂停壁纸。
/// 轮询周期 1s，开销近零。
/// </summary>
internal sealed class PowerGovernor
{
    private readonly AppConfig _config;
    private readonly DispatcherTimer _timer;

    public event Action<bool>? PauseStateChanged;
    public bool IsPaused { get; private set; }

    public PowerGovernor(AppConfig config)
    {
        _config = config;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Evaluate();
    }

    public void Start()
    {
        _timer.Start();
        Evaluate();
    }

    private void Evaluate()
    {
        bool pause = false;
        if (_config.PauseOnFullscreen && IsFullscreenAppActive()) pause = true;
        if (_config.PauseOnBattery && IsOnBattery()) pause = true;

        if (pause != IsPaused)
        {
            IsPaused = pause;
            Log.Info(pause ? "暂停壁纸（全屏/电池）" : "恢复壁纸");
            PauseStateChanged?.Invoke(pause);
        }
    }

    private static bool IsFullscreenAppActive()
    {
        if (Native.SHQueryUserNotificationState(out var st) != 0) return false;
        return st is Native.QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY
                or Native.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN
                or Native.QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE;
    }

    private static bool IsOnBattery()
    {
        try
        {
            return WinForms.SystemInformation.PowerStatus.PowerLineStatus
                == WinForms.PowerLineStatus.Offline;
        }
        catch { return false; }
    }
}