using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace WallPaper;

internal static class RuntimeChecks
{
    private static bool _mediaWarned;
    private static bool _vp9Warned;

    /// <summary>启动时后台检测，缺失则弹出指引（只弹一次）。</summary>
    public static void Startup()
    {
        Task.Run(() =>
        {
            CheckMediaFoundation();
            if (!HasWebmSupport())
            {
                _vp9Warned = true;
                ShowPrompt(
                    "缺少 WebM/VP9 解码组件",
                    "当前系统缺少 VP9 解码扩展，无法播放 WebM 壁纸（MP4 不受影响）。\n\n" +
                    "请打开 Microsoft Store，搜索并安装：\n" +
                    "  · VP9 Video Extensions\n" +
                    "  · Web Media Extensions\n\n" +
                    "安装后重启本程序即可。");
            }
        });
    }

    /// <summary>播放失败时按格式给出针对性提示。</summary>
    public static void OnPlaybackFailure(string file, string reason)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext == ".webm" && !_vp9Warned)
        {
            _vp9Warned = true;
            ShowPrompt(
                "WebM 播放失败",
                $"文件：{file}\n原因：{reason}\n\n" +
                "WebM 需要 VP9 解码组件。请在 Microsoft Store 搜索安装 " +
                "“VP9 Video Extensions”或“Web Media Extensions”后重试。");
        }
    }

    private static void CheckMediaFoundation()
    {
        var mf = Path.Combine(Environment.SystemDirectory, "mfplat.dll");
        if (File.Exists(mf) || _mediaWarned) return;
        _mediaWarned = true;
        ShowPrompt(
            "缺少系统媒体组件",
            "未找到 Media Foundation（mfplat.dll），常见于 Windows N/KN 版本。\n" +
            "所有视频壁纸将无法播放。\n\n" +
            "请安装 Media Feature Pack：\n" +
            "  「设置 → 应用 → 可选功能 → 添加功能 → Media Feature Pack」\n" +
            "或访问微软官网下载，安装后重启本程序。");
    }

    private static bool HasWebmSupport()
    {
        // 方式 1：扫描 AppX 包（VP9/WMExtensions）
        try
        {
            var pm = new Windows.Management.Deployment.PackageManager();
            foreach (var pkg in pm.FindPackagesForUser(string.Empty))
            {
                var n = pkg.Id.Name;
                if (n.Contains("VP9", StringComparison.OrdinalIgnoreCase)
                    || n.Contains("WebMedia", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch (Exception ex) { Log.Warn("包扫描失败: " + ex.Message); }

        // 方式 2：注册表启发式（.webm 关联存在则认为有希望）
        try
        {
            using var k = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(".webm");
            if (k is not null) return true;
        }
        catch { }

        return false;
    }

    private static void ShowPrompt(string title, string body)
    {
        try
        {
            var app = System.Windows.Application.Current;
            if (app is not null && !app.Dispatcher.CheckAccess())
                app.Dispatcher.Invoke(() => MessageBox.Show(body, title, MessageBoxButton.OK, MessageBoxImage.Warning));
            else
                MessageBox.Show(body, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch { }
    }
}