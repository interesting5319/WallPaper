using System;
using System.Collections.Generic;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace WallPaper;

internal sealed class DisplayManager
{
    private readonly AppConfig _config;
    private readonly List<WallpaperWindow> _windows = new();
    private bool _manualPaused;
    private bool _governorPaused;

    public bool IsPaused => _manualPaused || _governorPaused;


    public event Action<string>? Notify;

    public DisplayManager(AppConfig config) => _config = config;

    public IReadOnlyList<WallpaperWindow> Windows => _windows;

    private DateTime _lastRebuild = DateTime.MinValue;

    public void Start()
    {
        Rebuild();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) =>
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                // 2 秒内只重建一次，防止事件风暴造成窗口反复重建、桌面狂闪
                if ((DateTime.Now - _lastRebuild).TotalSeconds < 2) return;
                _lastRebuild = DateTime.Now;
                Log.Info("显示器配置变化，重建壁纸窗口");
                Rebuild();
            }));
    }

    public void Rebuild()
    {
        _lastRebuild = DateTime.Now;   // 记录本次重建，压掉紧随其后的启动期显示事
        foreach (var w in _windows) { w.Teardown(); w.Close(); }
        _windows.Clear();

        foreach (var scr in WinForms.Screen.AllScreens)
        {
            var info = new ScreenInfo
            {
                DeviceName = scr.DeviceName,
                X = scr.Bounds.X, Y = scr.Bounds.Y,
                Width = scr.Bounds.Width, Height = scr.Bounds.Height,
                Primary = scr.Primary,
            };
            var w = new WallpaperWindow(info, _config);
            w.Notify += msg => Notify?.Invoke(msg);
            w.Show();
            _windows.Add(w);
        }
        ApplyPlaylists();
        ApplyPause();
    }

    public void ApplyPlaylists()
    {
        foreach (var w in _windows)
        {
            var binding = _config.BindingFor(w.DeviceName);
            w.SetPlaylist(new List<string>(binding.Files));
        }
    }


    public void ReloadAudio()
    {
        foreach (var w in _windows) w.ReloadAudio();
    }


    public void ToggleManualPause()
    {
        _manualPaused = !_manualPaused;
        Log.Info(_manualPaused ? "手动暂停壁纸" : "手动恢复壁纸");
        ApplyPause();
    }

    public void SetGovernorPaused(bool paused)
    {
        _governorPaused = paused;
        ApplyPause();
    }

    private void ApplyPause()
    {
        if (IsPaused)
            foreach (var w in _windows) w.PausePlayback();
        else
            foreach (var w in _windows) w.ResumePlayback();
    }
}