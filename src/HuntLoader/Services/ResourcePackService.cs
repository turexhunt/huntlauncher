// src/HuntLoader/Services/ResourcePackService.cs
using System;
using System.IO;
using System.IO.Compression;
using HuntLoader.Core;
using HuntLoader.Models;

namespace HuntLoader.Services;

public static class ResourcePackService
{
    // ── Установить кастомный сплеш ────────────────────────
    public static void InstallCustomSplash(MinecraftProfile profile)
    {
        try
        {
            var packDir = Path.Combine(
                profile.ResourcePacksDir, "TH_Splash");
            var packZip = Path.Combine(
                profile.ResourcePacksDir, "TH_Splash.zip");

            if (Directory.Exists(packDir))
                Directory.Delete(packDir, true);
            Directory.CreateDirectory(packDir);

            // pack.mcmeta — pack_format 46 для 1.21.4
            File.WriteAllText(
                Path.Combine(packDir, "pack.mcmeta"), """
                {
                    "pack": {
                        "pack_format": 46,
                        "supported_formats": [46, 46],
                        "description": "\u00a75TH \u00a77Project \u00a78| \u00a7dcustom"
                    }
                }
                """);

            // Кастомный язык
            var langDir = Path.Combine(
                packDir, "assets", "minecraft", "lang");
            Directory.CreateDirectory(langDir);

            File.WriteAllText(
                Path.Combine(langDir, "ru_ru.json"), """
                {
                    "menu.loadingLevel":      "TH Project...",
                    "menu.generatingTerrain": "TH | Генерация мира...",
                    "menu.preparingSpawn":    "TH | Подготовка спавна...",
                    "menu.savingChunks":      "TH | Сохранение...",
                    "gui.done":               "Готово"
                }
                """);

            File.WriteAllText(
                Path.Combine(langDir, "en_us.json"), """
                {
                    "menu.loadingLevel":      "TH Project...",
                    "menu.generatingTerrain": "TH | Generating terrain...",
                    "menu.preparingSpawn":    "TH | Preparing spawn...",
                    "menu.savingChunks":      "TH | Saving...",
                    "gui.done":               "Done"
                }
                """);

            if (File.Exists(packZip)) File.Delete(packZip);
            ZipFile.CreateFromDirectory(packDir, packZip);
            Directory.Delete(packDir, true);

            InstallToOptions(profile, "TH_Splash.zip");
            Logger.Info(
                "Custom splash installed (pack_format 46)",
                "ResourcePack");
        }
        catch (Exception ex)
        {
            Logger.Warning($"ResourcePack: {ex.Message}", "ResourcePack");
        }
    }

    // ── Записать в options.txt ─────────────────────────────
    private static void InstallToOptions(
        MinecraftProfile profile, string packName)
    {
        try
        {
            var optFile = Path.Combine(profile.ProfileDir, "options.txt");

            if (!File.Exists(optFile))
            {
                File.WriteAllText(optFile,
                    $"resourcePacks:[\"vanilla\",\"{packName}\"]\n" +
                    BuildOptimizedOptions(profile));
                return;
            }

            var lines = File.ReadAllLines(optFile);
            var found = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("resourcePacks:")) continue;

                if (!lines[i].Contains(packName))
                    lines[i] = lines[i].TrimEnd(']') +
                                $",\"{packName}\"]";
                found = true;
                break;
            }

            if (!found)
            {
                var list = new System.Collections.Generic.List<string>(lines);
                list.Insert(0,
                    $"resourcePacks:[\"vanilla\",\"{packName}\"]");
                lines = list.ToArray();
            }

            File.WriteAllLines(optFile, lines);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Options: {ex.Message}", "ResourcePack");
        }
    }

    // ── МАКСИМУМ FPS options.txt ───────────────────────────
    // Каждая строка объяснена — почему именно это значение
    public static string BuildOptimizedOptions(
        MinecraftProfile? profile = null)
    {
        // Определяем RAM профиля для подбора настроек
        var ram = profile?.MemoryMax ?? 2048;

        // renderDistance: меньше = больше FPS
        // 8 — хороший баланс; если RAM < 2GB — ставим 6
        var renderDist = ram < 2048 ? 6 : 8;

        // simulationDistance: обработка тиков мобов/редстоуна
        // Меньше = меньше нагрузка на CPU
        var simDist = ram < 2048 ? 4 : 6;

        return
            // ── Дальность прорисовки ──────────────────────
            $"renderDistance:{renderDist}\n"                 +
            $"simulationDistance:{simDist}\n"                +

            // ── FPS ───────────────────────────────────────
            // 260 = без ограничений (монитор 240Гц и выше)
            "maxFps:260\n"                                   +

            // ── VSync — ВЫКЛ для максимум FPS ─────────────
            "enableVsync:false\n"                            +

            // ── Графика: Fast = без прозрачных листьев ────
            // 0 = Fast, 1 = Fancy, 2 = Fabulous
            "graphicsMode:0\n"                               +

            // ── Плавное освещение — ВЫКЛ ──────────────────
            // Даёт +10-30 FPS на слабых GPU
            "ao:false\n"                                     +

            // ── Облака — ВЫКЛ (серьёзный буст) ───────────
            "renderClouds:false\n"                           +

            // ── Тени от сущностей — ВЫКЛ ─────────────────
            "entityShadows:false\n"                          +

            // ── Частицы: минимум ──────────────────────────
            // 0 = All, 1 = Decreased, 2 = Minimal
            "particles:2\n"                                  +

            // ── Mipmap: 4 — баланс качество/производительность
            "mipmapLevels:4\n"                               +

            // ── Смешивание биомов — минимум ───────────────
            // Меньше = меньше нагрузка на CPU при рендере
            "biomeBlendRadius:0\n"                           +

            // ── Дальность отрисовки сущностей — 50% ───────
            "entityDistanceScaling:0.5\n"                    +

            // ── Framebuffer — обязательно вкл ────────────
            "fboEnable:true\n"                               +

            // ── Масштаб GUI: 0 = авто ─────────────────────
            "guiScale:0\n"                                   +

            // ── Unicode шрифт — ВЫКЛ ──────────────────────
            // Растровый шрифт быстрее
            "forceUnicodeFont:false\n"                       +

            // ── Обновление чанков: 0 = Threaded ───────────
            // Многопоточная загрузка чанков — меньше лагов
            "prioritizeChunkUpdates:0\n"                     +

            // ── Анимации — ВЫКЛ ───────────────────────────
            // Sodium Extra управляет этим детально
            "bobView:false\n"                                +

            // ── Поле зрения — стандарт ────────────────────
            "fov:0\n"                                        +

            // ── Чувствительность мыши ─────────────────────
            "mouseSensitivity:0.5\n"                         +

            // ── Гамма: 1.0 = полная яркость ──────────────
            "gamma:1.0\n"                                    +

            // ── Автосохранение — реже ─────────────────────
            // Меньше пауз из-за сохранения
            "autoSaveInterval:60\n";
    }

    // ── Применить опции если ещё не применены ────────────
    public static void ApplyOptimizedOptions(MinecraftProfile profile)
    {
        try
        {
            var optFile = Path.Combine(profile.ProfileDir, "options.txt");
            if (!File.Exists(optFile))
            {
                File.WriteAllText(
                    optFile,
                    BuildOptimizedOptions(profile));
                Logger.Info(
                    "Optimized options applied", "ResourcePack");
                return;
            }

            // Если файл есть — только патчим критичные настройки
            PatchOptions(optFile, profile);
        }
        catch (Exception ex)
        {
            Logger.Warning(
                $"Options apply: {ex.Message}", "ResourcePack");
        }
    }

    // ── Патч только критичных FPS настроек ────────────────
    // Не затирает пользовательские настройки полностью
    private static void PatchOptions(
        string           optFile,
        MinecraftProfile profile)
    {
        var lines  = File.ReadAllLines(optFile);
        var patched = new System.Collections.Generic.Dictionary<string, string>
        {
            ["enableVsync"]           = "false",
            ["maxFps"]                = "260",
            ["renderClouds"]          = "false",
            ["entityShadows"]         = "false",
            ["ao"]                    = "false",
            ["graphicsMode"]          = "0",
            ["particles"]             = "2",
            ["biomeBlendRadius"]      = "0",
            ["entityDistanceScaling"] = "0.5",
            ["prioritizeChunkUpdates"]= "0",
            ["gamma"]                 = "1.0",
        };

        var result  = new System.Collections.Generic.List<string>();
        var applied = new System.Collections.Generic.HashSet<string>();

        foreach (var line in lines)
        {
            var key = line.Split(':')[0];
            if (patched.TryGetValue(key, out var val))
            {
                result.Add($"{key}:{val}");
                applied.Add(key);
            }
            else
            {
                result.Add(line);
            }
        }

        // Добавляем те которых не было в файле
        foreach (var kv in patched)
        {
            if (!applied.Contains(kv.Key))
                result.Add($"{kv.Key}:{kv.Value}");
        }

        File.WriteAllLines(optFile, result);
        Logger.Info("Options patched for max FPS", "ResourcePack");
    }
}