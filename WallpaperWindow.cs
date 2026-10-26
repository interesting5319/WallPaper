using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WallPaper;

internal sealed class ScreenInfo
{
    public string DeviceName = "";
    public int X, Y, Width, Height;
    public bool Primary;
}

/// <summary>
/// 单块屏幕的壁纸窗口：挂到 WorkerW 壁纸层，带心跳检测与自动重挂、
/// 文件失败跳转、全部失败回退静态图。
/// </summary>
internal sealed class WallpaperWindow : Window
{
    private readonly ScreenInfo _screen;
    private readonly AppConfig _config;
    private readonly MediaElement _media;
    private readonly Image _fallback;
    private readonly PlayerControl _player;
    private readonly DispatcherTimer _heartbeat;
    private HwndSource? _source;
    private IntPtr _hwnd;
    private IntPtr _worker;
    private uint _msgTaskbarCreated;
    private bool _attached;
    private List<string> _files = new();
    private int _fileIndex;
    private DateTime _lastAttachAttempt = DateTime.MinValue;

    /// <summary>给托盘的提示文本</summary>
    public event Action<string>? Notify;

    public string DeviceName => _screen.DeviceName;

    public WallpaperWindow(ScreenInfo screen, AppConfig config)
    {
        _screen = screen;
        _config = config;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        AllowsTransparency = false;
        Background = System.Windows.Media.Brushes.Black;
        Title = "WallPaper-" + screen.DeviceName;

        _media = new MediaElement();
        _fallback = new Image
        {
            Stretch = System.Windows.Media.Stretch.UniformToFill,
            Visibility = Visibility.Collapsed,
        };
        Content = new Grid { Children = { _media, _fallback } };

        _player = new PlayerControl(_media);
        _player.Failed += OnFileFailed;

        _heartbeat = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _heartbeat.Tick += (_, _) => CheckAttachment();

        SourceInitialized += (_, _) => OnHandleReady();
    }

    private void OnHandleReady()
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        if (_source is not null) _source.AddHook(WndProc);
        _msgTaskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        // 壁纸窗口永不抢焦点、不进任务栏
        int ex = Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE,
            ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
        Attach();
        _heartbeat.Start();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // explorer 重启后 Taskbar 广播此消息，重新挂接壁纸层
        if (_msgTaskbarCreated != 0 && msg == (int)_msgTaskbarCreated)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Log.Info("explorer 重启，重新挂接 WorkerW");
                Attach();
            }));
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void Attach()
    {
        _lastAttachAttempt = DateTime.Now;
        _worker = Native.FindWorkerW();
        if (_worker != IntPtr.Zero)
        {
            Native.SetParent(_hwnd, _worker);
            // 必须用 GetAncestor(GA_PARENT)：GetParent 对 WS_POPUP 窗口返回的是 owner，恒不等于 WorkerW
            _attached = Native.GetAncestor(_hwnd, Native.GA_PARENT) == _worker;
            if (_attached)
                Log.Info($"[{DeviceName}] WorkerW 挂接成功");
            else
                Log.Warn($"[{DeviceName}] SetParent 未生效，降级为置底窗口");
        }
        else
        {
            _attached = false;
            Log.Warn($"[{DeviceName}] 未找到壁纸层，降级为置底窗口");
        }
        if (!_attached)
            Notify?.Invoke("未能挂接到桌面壁纸层，已降级运行（桌面图标可能被遮挡）。");
        Place();
    }

    private void Place()
    {
        if (_attached)
            Native.SetWindowPos(_hwnd, IntPtr.Zero,
                _screen.X, _screen.Y, _screen.Width, _screen.Height,
                Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
        else
            Native.SetWindowPos(_hwnd, Native.HWND_BOTTOM,
                _screen.X, _screen.Y, _screen.Width, _screen.Height,
                Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }

    private void CheckAttachment()
    {
        if (_attached)
        {
            if (Native.GetAncestor(_hwnd, Native.GA_PARENT) != _worker)
            {
                Log.Warn($"[{DeviceName}] 附着失效，尝试重挂");
                Attach();
            }
            return;
        }
        // 降级模式限频重试，避免搅动桌面
        if ((DateTime.Now - _lastAttachAttempt).TotalSeconds >= 60)
            Attach();
    }

    // ---- 播放控制 ----

    public void SetPlaylist(List<string> files)
    {
        _files = files;
        _fileIndex = 0;
        _fallback.Visibility = Visibility.Collapsed;
        PlayCurrent();
    }

    private void PlayCurrent()
    {
        if (_files.Count == 0)
        {
            ShowFallback("尚未设置壁纸文件。请右键托盘图标选择视频。");
            return;
        }
        if (_fileIndex < _files.Count)
        {
            _player.ApplyAudio(_config.MuteAudio, _config.Volume);
            _player.Open(_files[_fileIndex]);
        }
        else
        {
            ShowFallback("壁纸文件全部无法播放。");
        }
    }

    private void OnFileFailed(string file, string reason)
    {
        _fileIndex++;
        if (_fileIndex < _files.Count)
        {
            Log.Warn($"[{DeviceName}] 跳过失败文件，尝试下一个: {reason}");
            PlayCurrent();
        }
        else
        {
            ShowFallback($"壁纸无法播放：{reason}");
        }
    }

    private void ShowFallback(string message)
    {
        Log.Warn($"[{DeviceName}] {message}");
        var img = _config.FallbackImage;
        if (!string.IsNullOrEmpty(img) && File.Exists(img))
        {
            try
            {
                _fallback.Source = new BitmapImage(new Uri(img, UriKind.Absolute));
                _fallback.Visibility = Visibility.Visible;
            }
            catch { }
        }
        Notify?.Invoke(Err.Explain(message));
    }

    public void PausePlayback() => _player.Pause();
    public void ResumePlayback() => _player.Resume();
    public void ReloadAudio() => _player.ApplyAudio(_config.MuteAudio, _config.Volume);

    public void Teardown()
    {
        _heartbeat.Stop();
        _player.Stop();
    }
}