using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WallPaper;

public sealed class ScreenBinding
{
    public string DeviceName { get; set; } = "";
    public List<string> Files { get; set; } = new();
}

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public bool Autostart { get; set; } = true;
    public bool MuteAudio { get; set; } = true;
    public double Volume { get; set; } = 0.2;
    public bool PauseOnFullscreen { get; set; } = true;
    public bool PauseOnBattery { get; set; } = true;
    public string? FallbackImage { get; set; }
    public List<ScreenBinding> Screens { get; set; } = new();

    public ScreenBinding BindingFor(string deviceName)
    {
        var b = Screens.Find(s => s.DeviceName == deviceName);
        if (b is null)
        {
            b = new ScreenBinding { DeviceName = deviceName };
            Screens.Add(b);
        }
        return b;
    }
}

internal static class ConfigStore
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 配置与日志目录：
    /// Debug（VS F5 / dotnet run 调试）→ %APPDATA%\WallPaper（默认位置）；
    /// Release（publish 产物）→ exe 所在文件夹（便携式，随 exe 走）。
    /// </summary>
    public static string ConfigDir =>
#if DEBUG
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WallPaper");
#else
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
#endif

    public static string ConfigPath => Path.Combine(ConfigDir, "settings.json");
    private static string BackupPath => ConfigPath + ".bak";

    public static AppConfig Load()
    {
        Directory.CreateDirectory(ConfigDir);

#if !DEBUG
        // 一次性迁移旧位置（%APPDATA%\WallPaper）的配置到 exe 文件夹
        if (!File.Exists(ConfigPath))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            foreach (var legacy in new[]
            {
                Path.Combine(appData, "WallPaper", "settings.json"),
                Path.Combine(appData, "WorkerWallpaper", "settings.json"),
            })
            {
                if (!File.Exists(legacy)) continue;
                try
                {
                    File.Copy(legacy, ConfigPath);
                    Log.Info($"已从旧位置迁移配置: {legacy}");
                    break;
                }
                catch (Exception ex) { Log.Warn("配置迁移失败: " + ex.Message); }
            }
        }
#endif

        foreach (var path in new[] { ConfigPath, BackupPath })
        {
            try
            {
                if (!File.Exists(path)) continue;
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), Opts);
                if (cfg is not null)
                {
                    Migrate(cfg);
                    Log.Info($"配置已载入: {path}");
                    return cfg;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"配置读取失败 {path}: {ex.Message}");
                try { File.Move(path, path + ".bad-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); }
                catch { }
            }
        }
        Log.Info("使用默认配置");
        return new AppConfig();
    }

    public static void Save(AppConfig cfg)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(cfg, Opts));
            if (File.Exists(ConfigPath)) File.Replace(tmp, ConfigPath, BackupPath);
            else File.Move(tmp, ConfigPath);
        }
        catch (Exception ex) { Log.Error("配置保存失败", ex); }
    }

    private static void Migrate(AppConfig cfg)
    {
        if (cfg.Version < 1) cfg.Version = 1;
        cfg.Volume = Math.Clamp(cfg.Volume, 0, 1);
    }
}