// src/HuntLoader/Core/AppConfig.cs
using System;
using System.IO;
using Newtonsoft.Json;

namespace HuntLoader.Core;

public class AppConfig
{
    private static AppConfig? _instance;
    private static readonly string ConfigPath =
        Path.Combine(Constants.AppDataRoot, "config.json");

    public static AppConfig Instance => _instance ??= Load();

    // ── Внешний вид ───────────────────────────────────────
    public bool   ShowParticles     { get; set; } = true;
    public bool   AnimationsEnabled { get; set; } = true;
    public double BackgroundOpacity { get; set; } = 0.85;
    public string BackgroundImage   { get; set; } = "";

    // ── Глобальная кастомизация цветов ────────────────────
    public string AccentColor        { get; set; } = "#7C5CBF";
    public string AccentColor2       { get; set; } = "#5B8FE8";
    public string AccentColorHex     { get; set; } = "#7C5CBF";
    public int    CornerRadius       { get; set; } = 14;
    public string FontFamily         { get; set; } = "Segoe UI";
    public double UiScale            { get; set; } = 1.0;

    // ── Лаунчер ───────────────────────────────────────────
    public bool   CloseOnLaunch       { get; set; } = false;
    public bool   ShowSnapshots       { get; set; } = false;
    public bool   ShowOldVersions     { get; set; } = false;  // ✅ восстановлено
    public bool   DiscordRpc          { get; set; } = true;
    public int    ConcurrentDownloads { get; set; } = 4;
    public string GlobalJavaPath      { get; set; } = "";

    // ── Аккаунты / профили ────────────────────────────────
    public string ActiveAccountId  { get; set; } = "";  // ✅ восстановлено
    public string ActiveProfileId  { get; set; } = "";  // ✅ восстановлено

    // ── Загрузка / сохранение ─────────────────────────────
    public static AppConfig Load()
    {
        try
        {
            Directory.CreateDirectory(Constants.AppDataRoot);
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch { }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Constants.AppDataRoot);
            File.WriteAllText(ConfigPath,
                JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        catch { }
    }

    public static void Reset()
    {
        _instance = new AppConfig();
        _instance.Save();
    }
}