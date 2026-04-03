// src/HuntLoader/Services/OptimizationService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HuntLoader.Core;
using HuntLoader.Models;
using Newtonsoft.Json.Linq;

namespace HuntLoader.Services;

public class OptimizationPack
{
    public string Name        { get; set; } = "";
    public string ProjectId   { get; set; } = "";
    public string Description { get; set; } = "";
}

public class OptimizationService
{
    private readonly HttpClient      _http = new();
    private readonly DownloadService _dl   = new();
    private const string ModrinthApi = "https://api.modrinth.com/v2";

    public event Action<string, double>? OnProgress;

    private static readonly List<OptimizationPack> FabricPack = new()
    {
        new() {
            Name        = "Fabric API",
            ProjectId   = "P7dR8mSH",
            Description = "Fabric API (обязательно для всех модов)"
        },
        new() {
            Name        = "Sodium",
            ProjectId   = "AANobbMI",
            Description = "Главный буст FPS +200-400%"
        },
        new() {
            Name        = "Indium",
            ProjectId   = "Orvt0mRa",
            Description = "Совместимость Sodium с Fabric Rendering API"
        },
        new() {
            Name        = "Sodium Extra",
            ProjectId   = "PtjYWJkn",
            Description = "Доп. настройки Sodium"
        },
        new() {
            Name        = "Reese's Sodium Options",
            ProjectId   = "Bh37bMuy",
            Description = "Улучшенное меню настроек Sodium"
        },
        new() {
            Name        = "Lithium",
            ProjectId   = "gvQqBUqZ",
            Description = "Оптимизация логики игры и тиков"
        },
        new() {
            Name        = "FerriteCore",
            ProjectId   = "uXXizFIs",
            Description = "Снижение потребления RAM на 30-50%"
        },
        new() {
            Name        = "EntityCulling",
            ProjectId   = "NNAgCjsB",
            Description = "Не рендерит мобов за стенами"
        },
        new() {
            Name        = "ImmediatelyFast",
            ProjectId   = "5ZwdcRci",
            Description = "Ускорение рендера HUD и GUI"
        },
        new() {
            Name        = "ModernFix",
            ProjectId   = "nmDcB62a",
            Description = "Быстрый запуск, меньше RAM при загрузке"
        },
        new() {
            Name        = "Krypton",
            ProjectId   = "fQEb0iXm",
            Description = "Оптимизация сетевого стека"
        },
        new() {
            Name        = "Zoomify",
            ProjectId   = "w7ThoJFB",
            Description = "Zoom как в OptiFine/Lunar"
        },
        new() {
            Name        = "BetterF3",
            ProjectId   = "8shC1gFX",
            Description = "Красивый и читаемый F3 экран"
        },
    };

    private static readonly List<OptimizationPack> ForgePack = new()
    {
        new() {
            Name        = "Embeddium",
            ProjectId   = "rtzza57Y",
            Description = "Sodium для Forge — главный буст FPS"
        },
        new() {
            Name        = "FerriteCore",
            ProjectId   = "uXXizFIs",
            Description = "Снижение потребления RAM"
        },
        new() {
            Name        = "EntityCulling",
            ProjectId   = "NNAgCjsB",
            Description = "Не рендерит мобов за стенами"
        },
        new() {
            Name        = "ModernFix",
            ProjectId   = "nmDcB62a",
            Description = "Быстрый запуск, оптимизация загрузки"
        },
    };

    public OptimizationService()
    {
        _http.DefaultRequestHeaders.Add(
            "User-Agent",
            $"HuntLoader/{Constants.LauncherVersion} (github.com/huntloader)");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public List<OptimizationPack> GetPackForProfile(MinecraftProfile profile)
    {
        return profile.ModLoader switch
        {
            ModLoader.Fabric   => FabricPack,
            ModLoader.Forge    => ForgePack,
            ModLoader.NeoForge => ForgePack,
            ModLoader.Quilt    => FabricPack,
            _                  => new List<OptimizationPack>()
        };
    }

    public async Task InstallPackAsync(
        MinecraftProfile  profile,
        CancellationToken ct = default)
    {
        var pack = GetPackForProfile(profile);
        if (pack.Count == 0)
        {
            OnProgress?.Invoke("Vanilla не поддерживает моды", 100);
            return;
        }

        Directory.CreateDirectory(profile.ModsDir);

        var total     = pack.Count;
        var installed = 0;

        foreach (var mod in pack)
        {
            if (ct.IsCancellationRequested) break;

            if (IsAlreadyInstalled(profile.ModsDir, mod.Name))
            {
                installed++;
                OnProgress?.Invoke(
                    $"✅ {mod.Name} уже установлен",
                    (double)installed / total * 100);
                continue;
            }

            OnProgress?.Invoke(
                $"⬇ Устанавливаю {mod.Name}...",
                (double)installed / total * 100);

            try
            {
                await InstallOneModAsync(mod, profile, ct);
                installed++;
                OnProgress?.Invoke(
                    $"✅ {mod.Name} установлен",
                    (double)installed / total * 100);
            }
            catch (Exception ex)
            {
                Logger.Warning(
                    $"Не удалось установить {mod.Name}: {ex.Message}",
                    "OptimizationService");
                installed++;
                OnProgress?.Invoke(
                    $"⚠ {mod.Name} — пропущен",
                    (double)installed / total * 100);
            }

            await Task.Delay(350, ct);
        }

        OnProgress?.Invoke(
            $"🚀 Готово! Установлено {installed}/{total} модов",
            100);
    }

    private async Task InstallOneModAsync(
        OptimizationPack  mod,
        MinecraftProfile  profile,
        CancellationToken ct)
    {
        var loader = profile.ModLoader switch
        {
            ModLoader.Fabric   => "fabric",
            ModLoader.Quilt    => "quilt",
            ModLoader.Forge    => "forge",
            ModLoader.NeoForge => "neoforge",
            _                  => "fabric"
        };

        var versions = await FetchVersionsAsync(
            mod.ProjectId, profile.GameVersion, loader, ct);

        if (versions.Count == 0)
            versions = await FetchVersionsAsync(
                mod.ProjectId, null, loader, ct);

        if (versions.Count == 0 && loader == "quilt")
            versions = await FetchVersionsAsync(
                mod.ProjectId, profile.GameVersion, "fabric", ct);

        if (versions.Count == 0)
            throw new Exception($"Нет версий для {profile.GameVersion}/{loader}");

        var latest  = versions[0];
        var files   = latest["files"] as JArray ?? new JArray();
        var primary = files.FirstOrDefault(f => f["primary"]?.Value<bool>() == true)
                      ?? files.FirstOrDefault()
                      ?? throw new Exception("Нет файлов для скачивания");

        var fileUrl  = primary["url"]?.ToString()  ?? throw new Exception("Нет URL");
        var fileName = primary["filename"]?.ToString() ?? $"{mod.Name}.jar";
        var dest     = Path.Combine(profile.ModsDir, fileName);

        if (File.Exists(dest)) return;

        var progress = new Progress<DownloadProgress>(p =>
            OnProgress?.Invoke($"⬇ {mod.Name}: {p.Percentage:F0}%", p.Percentage));

        await _dl.DownloadFileAsync(fileUrl, dest, progress, ct);
    }

    private async Task<JArray> FetchVersionsAsync(
        string            projectId,
        string?           gameVersion,
        string            loader,
        CancellationToken ct)
    {
        try
        {
            var url = $"{ModrinthApi}/project/{projectId}/version?loaders=[\"{loader}\"]";
            if (!string.IsNullOrEmpty(gameVersion))
                url += $"&game_versions=[\"{gameVersion}\"]";

            var json = await _http.GetStringAsync(url, ct);
            return JArray.Parse(json);
        }
        catch { return new JArray(); }
    }

    private static bool IsAlreadyInstalled(string modsDir, string modName)
    {
        if (!Directory.Exists(modsDir)) return false;

        var search = modName.ToLower()
            .Replace(" ", "").Replace("'", "")
            .Replace("-", "").Replace("s", "");

        foreach (var file in Directory.GetFiles(modsDir, "*.jar"))
        {
            var name = Path.GetFileNameWithoutExtension(file)
                .ToLower().Replace("-", "").Replace("_", "").Replace(" ", "");

            var minLen = Math.Min(6, Math.Min(search.Length, name.Length));
            if (name.Contains(search) || search.Contains(name[..minLen]))
                return true;
        }
        return false;
    }
}