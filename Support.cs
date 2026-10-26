using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace WallPaper;

internal static class Log
{
    private static readonly object Gate = new();
    private static string _dir = "";

    public static void Init()
    {
        _dir = Path.Combine(ConfigStore.ConfigDir, "logs");
        Directory.CreateDirectory(_dir);
    }

    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARN", msg);
    public static void Error(string msg, Exception? ex = null)
        => Write("ERROR", ex is null ? msg : msg + " | " + ex);

    private const long MaxBytes = 512 * 1024;

    private static void Write(string level, string msg)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg.Replace(@"\\.\", "")}";
        lock (Gate)
        {
            try
            {
                var path = Path.Combine(_dir, "app.log");
                var fi = new FileInfo(path);
                if (fi.Exists && fi.Length > MaxBytes)
                    File.Move(path, Path.Combine(_dir, "app.old.log"), overwrite: true);
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch { }
        }
    }

    public static void DumpCrash(Exception? ex)
    {
        try
        {
            var path = Path.Combine(_dir, "crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
            File.WriteAllText(path,
                $"时间: {DateTime.Now:O}\n异常: {ex}\n\n进程: {Environment.ProcessPath}\n系统: {Environment.OSVersion}");
        }
        catch { }
    }
}

internal static class CrashGuard
{
    private static string MarkerPath => Path.Combine(ConfigStore.ConfigDir, "crash.marker");

    public static bool RecoveredFromCrash { get; private set; }

    public static void OnStarted()
    {
        try
        {
            if (File.Exists(MarkerPath))
            {
                RecoveredFromCrash = true;
                File.Delete(MarkerPath);
                Log.Warn("检测到上次异常退出标记");
            }
        }
        catch { }
    }

    /// <summary>
    /// 崩溃时打标记并尝试拉起自身。60 秒内崩 3 次则放弃，防止崩溃循环。
    /// </summary>
    public static void MarkAndRelaunch()
    {
        try
        {
            Directory.CreateDirectory(ConfigStore.ConfigDir);
            var now = DateTime.Now;
            var recent = 0;
            if (File.Exists(MarkerPath))
            {
                if (DateTime.TryParse(File.ReadAllText(MarkerPath), out var last)
                    && (now - last).TotalSeconds < 60)
                    recent = 1;
            }
            File.WriteAllText(MarkerPath, now.ToString("O"));

            if (recent == 0 && Environment.ProcessPath is string exe)
            {
                Process.Start(new ProcessStartInfo(exe, "--restore") { UseShellExecute = false });
                Log.Info("崩溃后已自动重启");
            }
            else
            {
                Log.Error("短时间内多次崩溃，放弃自动重启");
            }
        }
        catch { }
    }
}

internal static class Ui
{
    /// <summary>\\.\DISPLAY1 → DISPLAY1（仅用于界面显示，配置里仍存完整名）</summary>
    public static string DisplayName(string deviceName)
    {
        int i = deviceName.LastIndexOf('\\');
        return i >= 0 ? deviceName.Substring(i + 1) : deviceName;
    }
}

internal static class Err
{
    public static string Explain(string reason)
    {
        if (reason.Contains("0xC00D11B1", StringComparison.OrdinalIgnoreCase))
            return "解码失败：片源编码不受支持（常见于 10-bit H.264 压制片），建议用 HandBrake 转码为 H.264 8-bit 或更换片源";
        if (reason.Contains("0x8898050C", StringComparison.OrdinalIgnoreCase))
            return "媒体管线内部错误（常见于对已失效管线的操作），重启播放即可";
        return reason;
    }
}