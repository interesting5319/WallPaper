using System;
using System.Diagnostics;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace WallPaper;

internal sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ContextMenuStrip _menu;
    private readonly AppConfig _config;
    private readonly DisplayManager _displays;
    private readonly PowerGovernor _governor;
    private SettingsWindow? _settings;

    public TrayIcon(AppConfig config, DisplayManager displays, PowerGovernor governor)
    {
        _config = config;
        _displays = displays;
        _governor = governor;

        _menu = new WinForms.ContextMenuStrip();
        _menu.Opening += (_, _) => RebuildMenu();

        _icon = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)?? System.Drawing.SystemIcons.Application,
            Text = "WallPaper",
            ContextMenuStrip = _menu,
        };
        _icon.DoubleClick += (_, _) => OpenSettings();
    }

    public void Show() => _icon.Visible = true;

    public void Balloon(string title, string text, bool error = false)
    {
        try
        {
            _icon.ShowBalloonTip(5000, title, text,
                error ? WinForms.ToolTipIcon.Warning : WinForms.ToolTipIcon.Info);
        }
        catch { }
    }

    private void RebuildMenu()
    {
        _menu.Items.Clear();

        var setItem = new WinForms.ToolStripMenuItem("为屏幕设置壁纸...");
        foreach (var w in _displays.Windows)
        {
            var device = w.DeviceName;
            setItem.DropDownItems.Add(new WinForms.ToolStripMenuItem(Ui.DisplayName(device), null, (_, _) =>
                PickWallpaper(device)));
        }
        if (setItem.DropDownItems.Count == 0)
            setItem.Enabled = false;
        _menu.Items.Add(setItem);

        _menu.Items.Add(new WinForms.ToolStripSeparator());

        var pause = new WinForms.ToolStripMenuItem("暂停动态壁纸");
        pause.Click += (_, _) =>
        {
            if (!_displays.IsPaused) _displays.ToggleManualPause();  // 未暂停才生效
        };

        var resume = new WinForms.ToolStripMenuItem("恢复动态壁纸");
        resume.Click += (_, _) =>
        {
            if (_displays.IsPaused) _displays.ToggleManualPause();  // 暂停中才生效
        };

        _menu.Items.Add(pause);
        _menu.Items.Add(resume);

        var settings = new WinForms.ToolStripMenuItem("设置(&S)...");
        settings.Click += (_, _) => OpenSettings();
        _menu.Items.Add(settings);

        var autostart = new WinForms.ToolStripMenuItem("开机自启动") { CheckOnClick = true };
        autostart.Checked = Autostart.IsEnabled();
        autostart.CheckedChanged += (_, _) =>
        {
            _config.Autostart = autostart.Checked;
            Autostart.Apply(autostart.Checked);
            ConfigStore.Save(_config);
        };
        _menu.Items.Add(autostart);

        _menu.Items.Add(new WinForms.ToolStripMenuItem("打开配置目录", null, (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", ConfigStore.ConfigDir) { UseShellExecute = false }); }
            catch { }
        }));

        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(new WinForms.ToolStripMenuItem("退出", null, (_, _) =>
        {
            System.Windows.Application.Current?.Shutdown();
        }));
    }

    private void PickWallpaper(string deviceName)
    {
        var dlg = new WinForms.OpenFileDialog
        {
            Title = $"选择壁纸文件（{deviceName}）",
            Filter = "视频壁纸|*.mp4;*.webm|所有文件|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;

        var binding = _config.BindingFor(deviceName);
        binding.Files.Clear();
        binding.Files.AddRange(dlg.FileNames);
        ConfigStore.Save(_config);
        _displays.ApplyPlaylists();
    }

    private void OpenSettings()
    {
        if (_settings is not null)
        {
            _settings.Activate();
            return;
        }
        _settings = new SettingsWindow(_config, _displays.Windows)
        {
            Owner = null,
        };
        _settings.Saved += () =>
        {
            ConfigStore.Save(_config);
            _displays.ApplyPlaylists();
            _displays.ReloadAudio();
            Balloon("设置已保存", "壁纸与音频设置已生效。");
        };
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}