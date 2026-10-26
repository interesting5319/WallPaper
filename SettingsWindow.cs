using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;

namespace WallPaper;

internal sealed class SettingsWindow : Window
{
    private readonly AppConfig _config;
    private readonly List<string> _devices = new();
    private string _currentDevice = "";

    private readonly ComboBox _screenBox = new() { Margin = new Thickness(0, 4, 0, 8) };
    private readonly ListBox _fileList = new() { Height = 150, Margin = new Thickness(0, 4, 0, 4) };
    private readonly CheckBox _mute = new() { Content = "静音", Margin = new Thickness(0, 6, 0, 0) };
    private readonly CheckBox _battery = new() { Content = "使用电池时暂停壁纸", Margin = new Thickness(0, 6, 0, 0) };
    private readonly CheckBox _fullscreen = new() { Content = "全屏应用/游戏运行时暂停", Margin = new Thickness(0, 6, 0, 0) };
    private readonly CheckBox _autostart = new() { Content = "开机自启动", Margin = new Thickness(0, 6, 0, 0) };
    private readonly Slider _volume = new()
    {
        Minimum = 0, Maximum = 1, TickFrequency = 0.05,
        IsSnapToTickEnabled = true, Margin = new Thickness(0, 4, 0, 0),
    };
    private readonly TextBox _fallback = new() { Margin = new Thickness(0, 4, 0, 0) };

    public event Action? Saved;

    public SettingsWindow(AppConfig config, IReadOnlyList<WallpaperWindow> windows)
    {
        _config = config;

        Title = "WallPaper 设置";
        Width = 560;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;

        foreach (var w in windows) _devices.Add(w.DeviceName);
        if (_devices.Count == 0) _devices.Add(@"\\.\DISPLAY1");
        foreach (var d in _devices) _screenBox.Items.Add(Ui.DisplayName(d));
        _screenBox.SelectionChanged += (_, _) => OnScreenChanged();

        var panel = new StackPanel { Margin = new Thickness(16) };

        panel.Children.Add(Label("目标屏幕："));
        panel.Children.Add(_screenBox);

        panel.Children.Add(Label("壁纸文件列表（失败时自动尝试下一个）："));
        panel.Children.Add(_fileList);

        var fileButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        fileButtons.Children.Add(Button("添加...", (_, _) => AddFiles()));
        fileButtons.Children.Add(Button("移除", (_, _) => RemoveSelected()));
        fileButtons.Children.Add(Button("上移", (_, _) => Move(-1)));
        fileButtons.Children.Add(Button("下移", (_, _) => Move(1)));
        fileButtons.Children.Add(Button("清空", (_, _) => _fileList.Items.Clear()));
        panel.Children.Add(fileButtons);

        _mute.Checked += (_, _) => UpdateMute();
        _mute.Unchecked += (_, _) => UpdateMute();
        panel.Children.Add(_mute);
        panel.Children.Add(Label("音量："));
        panel.Children.Add(_volume);

        panel.Children.Add(_battery);
        panel.Children.Add(_fullscreen);
        panel.Children.Add(_autostart);

        panel.Children.Add(Label("备用静态壁纸（全部视频失败时显示）："));
        panel.Children.Add(_fallback);
        var fbBtn = Stack(Button("浏览...", (_, _) => BrowseFallback()));
        fbBtn.Margin = new Thickness(0, 4, 0, 8);
        panel.Children.Add(fbBtn);

        var saveRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        saveRow.Children.Add(Button("保存", (_, _) => Save()));
        var cancel = Button("取消", (_, _) => Close());
        cancel.Margin = new Thickness(8, 0, 0, 0);
        saveRow.Children.Add(cancel);
        panel.Children.Add(saveRow);

        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        // 载入当前配置
        _mute.IsChecked = _config.MuteAudio;
        _volume.Value = _config.Volume;
        _battery.IsChecked = _config.PauseOnBattery;
        _fullscreen.IsChecked = _config.PauseOnFullscreen;
        _autostart.IsChecked = _config.Autostart;
        _fallback.Text = _config.FallbackImage ?? "";
        _screenBox.SelectedIndex = 0;
    }

    private static TextBlock Label(string text) =>
        new() { Text = text, Margin = new Thickness(0, 8, 0, 0) };

    private static Button Button(string text, RoutedEventHandler onClick)
    {
        var b = new Button { Content = text, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 0) };
        b.Click += onClick;
        return b;
    }

    private static StackPanel Stack(UIElement e)
    {
        var p = new StackPanel();
        p.Children.Add(e);
        return p;
    }

    private void UpdateMute()
    {
        _volume.IsEnabled = _mute.IsChecked != true;
    }

    private void OnScreenChanged()
    {
        if (_currentDevice != "") SaveListForCurrent();
        int idx = _screenBox.SelectedIndex;
        if (idx >= 0 && idx < _devices.Count)
        {
            _currentDevice = _devices[idx];
            _fileList.Items.Clear();
            foreach (var f in _config.BindingFor(_currentDevice).Files)
                _fileList.Items.Add(f);
        }
    }

    private void SaveListForCurrent()
    {
        if (_currentDevice == "") return;
        var binding = _config.BindingFor(_currentDevice);
        binding.Files.Clear();
        foreach (string f in _fileList.Items) binding.Files.Add(f);
    }

    private void AddFiles()
    {
        var dlg = new WinForms.OpenFileDialog
        {
            Title = "添加壁纸文件",
            Filter = "视频壁纸|*.mp4;*.webm|所有文件|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            foreach (var f in dlg.FileNames)
                _fileList.Items.Add(f);
    }

    private void RemoveSelected()
    {
        for (int i = _fileList.SelectedItems.Count - 1; i >= 0; i--)
            _fileList.Items.Remove(_fileList.SelectedItems[i]);
    }

    private void Move(int delta)
    {
        int idx = _fileList.SelectedIndex;
        if (idx < 0) return;
        int target = idx + delta;
        if (target < 0 || target >= _fileList.Items.Count) return;
        var item = _fileList.Items[idx];
        _fileList.Items.RemoveAt(idx);
        _fileList.Items.Insert(target, item);
        _fileList.SelectedIndex = target;
    }

    private void BrowseFallback()
    {
        var dlg = new WinForms.OpenFileDialog
        {
            Title = "选择备用静态壁纸",
            Filter = "图片|*.jpg;*.jpeg;*.png;*.bmp|所有文件|*.*",
        };
        if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            _fallback.Text = dlg.FileName;
    }

    private void Save()
    {
        SaveListForCurrent();
        _config.MuteAudio = _mute.IsChecked == true;
        _config.Volume = _volume.Value;
        _config.PauseOnBattery = _battery.IsChecked == true;
        _config.PauseOnFullscreen = _fullscreen.IsChecked == true;
        _config.Autostart = _autostart.IsChecked == true;
        _config.FallbackImage = string.IsNullOrWhiteSpace(_fallback.Text) ? null : _fallback.Text.Trim();
        Autostart.Apply(_config.Autostart);
        Saved?.Invoke();
        Close();
    }
}