using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace WallPaper;

/// <summary>
/// MediaElement 封装：事件驱动循环（MediaEnded 重启，无定时器）、
/// 播放中断自动重启、失败跳转信号、音量控制。解码走系统 Media Foundation + DXVA 硬解。
/// </summary>
internal sealed class PlayerControl
{
    private readonly MediaElement _media;
    private TimeSpan _duration = TimeSpan.Zero;
    private bool _wantPlay;
    private DateTime _lastRestart = DateTime.MinValue;
    private bool _broken;
    private DateTime _openedAt = DateTime.MinValue;
    private DispatcherTimer? _restartTimer;

    /// <summary>file, reason</summary>
    public event Action<string, string>? Failed;

    public string? CurrentFile { get; private set; }
    public bool IsPlaying => _wantPlay;

    public PlayerControl(MediaElement media)
    {
        _media = media;
        _media.LoadedBehavior = MediaState.Manual;
        _media.UnloadedBehavior = MediaState.Stop;
        _media.Stretch = Stretch.UniformToFill;
        _media.IsMuted = true;

        _media.MediaOpened += (_, _) =>
        {
            _openedAt = DateTime.Now;
            if (_media.NaturalDuration.HasTimeSpan)
                _duration = _media.NaturalDuration.TimeSpan;
            Log.Info($"片源: {_media.NaturalVideoWidth}x{_media.NaturalVideoHeight} 时长 {_duration.TotalSeconds:F1}s");
        };
        _media.MediaEnded += (_, _) =>
        {
            if (!_wantPlay) return;
            if ((DateTime.Now - _lastRestart).TotalMilliseconds < 300) return;
            _lastRestart = DateTime.Now;
            _media.Position = TimeSpan.Zero;
            _media.Play();
        };
        _media.MediaFailed += (_, e) =>
        {
            var reason = e.ErrorException?.Message ?? "无法播放该文件";
            Log.Warn($"播放失败 {CurrentFile}: {reason}");
            if ((DateTime.Now - _openedAt).TotalSeconds > 5)
            {
                // 播了很久才死 = 播放中途管线崩（老片源常见），当"循环中断"自动重启
                ScheduleRestart();
            }
            else
            {
                // 几秒内就死 = 文件本身解不了，跳过
                _broken = true;
                Failed?.Invoke(CurrentFile ?? "", reason);
            }
        };
    }

    public void Open(string path)
    {
        CurrentFile = path;
        _broken = false;
        _openedAt = DateTime.Now;
        _duration = TimeSpan.Zero;
        _wantPlay = true;
        try
        {
            _media.Source = new Uri(path, UriKind.Absolute);
            _media.Position = TimeSpan.Zero;
            _media.Play();
            Log.Info($"开始播放: {path}");
        }
        catch (Exception ex)
        {
            _broken = true;
            Failed?.Invoke(path, ex.Message);
        }
    }

    public void Pause()
    {
        if (_broken) return;   // 死管线调 Pause 会抛 MILAVERR
        _wantPlay = false;
        _media.Pause();
    }

    public void Resume()
    {
        if (CurrentFile is null) return;
        _wantPlay = true;
        if (_broken) { Open(CurrentFile); return; }   // 死管线不 Play，重开
        _media.Play();
    }

    public void Stop()
    {
        _wantPlay = false;
        if (_broken) return;
        try { _media.Stop(); } catch { }
    }

    public void ApplyAudio(bool mute, double volume)
    {
        _media.IsMuted = mute;
        _media.Volume = volume;
    }

    private void ScheduleRestart()
    {
        if (_restartTimer is not null) return;
        _restartTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _restartTimer.Tick += (_, _) =>
        {
            _restartTimer.Stop();
            _restartTimer = null;
            if (_wantPlay && CurrentFile is not null)
            {
                Log.Info($"播放中断，自动重启: {CurrentFile}");
                Open(CurrentFile);
            }
        };
        _restartTimer.Start();
    }
}