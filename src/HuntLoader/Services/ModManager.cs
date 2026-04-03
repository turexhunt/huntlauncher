// src/HuntLoader/Services/ModManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HuntLoader.Core;
using HuntLoader.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HuntLoader.Services;

public class ModrinthMod
{
    [JsonProperty("project_id")]  public string       ProjectId   { get; set; } = "";
    [JsonProperty("slug")]        public string       Slug        { get; set; } = "";
    [JsonProperty("title")]       public string       Title       { get; set; } = "";
    [JsonProperty("description")] public string       Description { get; set; } = "";
    [JsonProperty("downloads")]   public long         Downloads   { get; set; }
    [JsonProperty("icon_url")]    public string       IconUrl     { get; set; } = "";
    [JsonProperty("categories")]  public List<string> Categories  { get; set; } = new();
    [JsonProperty("versions")]    public List<string> Versions    { get; set; } = new();

    public string DownloadsFormatted =>
        Downloads > 1_000_000 ? $"{Downloads / 1_000_000.0:F1}M" :
        Downloads > 1_000     ? $"{Downloads / 1000.0:F0}K"       :
                                Downloads.ToString();
}

// Унифицированная модель для отображения в UI
public class UnifiedMod
{
    public string Source      { get; set; } = "modrinth"; // "modrinth" | "curseforge"
    public string Id          { get; set; } = "";         // ProjectId или CurseForge int
    public string Title       { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconUrl     { get; set; } = "";
    public long   Downloads   { get; set; }
    public List<string> Categories { get; set; } = new();

    // Оригинальные объекты для скачивания
    public ModrinthMod?   ModrinthData   { get; set; }
    public CurseForgeMod? CurseForgeData { get; set; }

    public string DownloadsFormatted =>
        Downloads > 1_000_000 ? $"{Downloads / 1_000_000.0:F1}M" :
        Downloads > 1_000     ? $"{Downloads / 1000.0:F0}K"       :
                                Downloads.ToString();

    public string SourceLabel => Source == "curseforge" ? "CurseForge" : "Modrinth";
    public string SourceColor => Source == "curseforge" ? "#F16436"    : "#1BD96A";

    public static UnifiedMod FromModrinth(ModrinthMod m) => new()
    {
        Source        = "modrinth",
        Id            = m.ProjectId,
        Title         = m.Title,
        Description   = m.Description,
        IconUrl       = m.IconUrl,
        Downloads     = m.Downloads,
        Categories    = m.Categories,
        ModrinthData  = m
    };

    public static UnifiedMod FromCurseForge(CurseForgeMod m) => new()
    {
        Source          = "curseforge",
        Id              = m.Id.ToString(),
        Title           = m.Name,
        Description     = m.Summary,
        IconUrl         = m.IconUrl,
        Downloads       = m.Downloads,
        Categories      = m.Categories,
        CurseForgeData  = m
    };
}

public class ModManager
{
    private readonly HttpClient         _http = new();
    private readonly DownloadService    _dl   = new();
    private readonly CurseForgeService  _cf   = new();

    private const string ModrinthApi       = "https://api.modrinth.com/v2";
    private const string DarkLoadingScreenId = "dark-loading-screen";

    public event Action<DownloadProgress>? OnProgress;

    public ModManager()
    {
        _http.DefaultRequestHeaders.Add("User-Agent",
            $"HuntLoader/{Constants.LauncherVersion}");
        _http.Timeout = TimeSpan.FromSeconds(30);

        _cf.OnProgress += p => OnProgress?.Invoke(p);
    }

    // ── Unified поиск (Modrinth + CurseForge) ─────────────
    public async Task<List<UnifiedMod>> SearchUnifiedAsync(
        string  query,
        string? gameVersion = null,
        string? loader      = null,
        string  source      = "both", // "modrinth" | "curseforge" | "both"
        int     limit       = 20,
        CancellationToken ct = default)
    {
        var results = new List<UnifiedMod>();

        var tasks = new List<Task>();

        if (source is "modrinth" or "both")
        {
            tasks.Add(Task.Run(async () =>
            {
                var mods = await SearchModrinthAsync(query, gameVersion, loader, limit, ct);
                lock (results)
                    results.AddRange(mods.Select(UnifiedMod.FromModrinth));
            }, ct));
        }

        if (source is "curseforge" or "both")
        {
            tasks.Add(Task.Run(async () =>
            {
                var mods = await _cf.SearchAsync(query, gameVersion, loader, limit, ct);
                lock (results)
                    results.AddRange(mods.Select(UnifiedMod.FromCurseForge));
            }, ct));
        }

        await Task.WhenAll(tasks);

        // Сортируем по загрузкам — перемешиваем результаты обоих источников
        return results
            .OrderByDescending(m => m.Downloads)
            .ToList();
    }

    // ── Скачивание UnifiedMod ─────────────────────────────
    public async Task<string> DownloadUnifiedModAsync(
        UnifiedMod       mod,
        MinecraftProfile profile,
        CancellationToken ct = default)
    {
        if (mod.Source == "curseforge" && mod.CurseForgeData != null)
        {
            return await _cf.DownloadModAsync(
                mod.CurseForgeData.Id, profile, ct);
        }

        if (mod.ModrinthData != null)
        {
            return await DownloadModAsync(mod.ModrinthData.ProjectId, profile, ct);
        }

        throw new Exception("Неизвестный источник мода");
    }

    // ── Modrinth поиск ────────────────────────────────────
    public async Task<List<ModrinthMod>> SearchModrinthAsync(
        string  query,
        string? gameVersion = null,
        string? loader      = null,
        int     limit       = 20,
        CancellationToken ct = default)
    {
        var url    = $"{ModrinthApi}/search?query={Uri.EscapeDataString(query)}&limit={limit}";
        var facets = new List<string>();

        if (!string.IsNullOrEmpty(gameVersion))
            facets.Add($"[\"versions:{gameVersion}\"]");
        if (!string.IsNullOrEmpty(loader))
            facets.Add($"[\"categories:{loader.ToLower()}\"]");
        if (facets.Count > 0)
            url += $"&facets=[{string.Join(",", facets)}]";

        try
        {
            var json = await _http.GetStringAsync(url, ct);
            var obj  = JObject.Parse(json);
            return obj["hits"]?.ToObject<List<ModrinthMod>>() ?? new();
        }
        catch (Exception ex)
        {
            Logger.Warning($"Modrinth search: {ex.Message}", "ModManager");
            return new();
        }
    }

    // ── Старый метод для совместимости ────────────────────
    public Task<List<ModrinthMod>> SearchAsync(
        string  query,
        string? gameVersion = null,
        string? loader      = null,
        int     limit       = 20) =>
        SearchModrinthAsync(query, gameVersion, loader, limit);

    // ── Скачать Modrinth мод ──────────────────────────────
    public async Task<string> DownloadModAsync(
        string           projectId,
        MinecraftProfile profile,
        CancellationToken ct = default)
    {
        var loader      = profile.ModLoader.ToString().ToLower();
        var versionsUrl = $"{ModrinthApi}/project/{projectId}/version" +
                          $"?game_versions=[\"{profile.GameVersion}\"]" +
                          $"&loaders=[\"{loader}\"]";

        var json     = await _http.GetStringAsync(versionsUrl, ct);
        var versions = JArray.Parse(json);

        if (versions.Count == 0)
            throw new Exception("Нет совместимых версий мода");

        var latest  = versions[0];
        var files   = latest["files"] as JArray ?? new JArray();
        var primary = files.FirstOrDefault(f => f["primary"]?.Value<bool>() == true)
                      ?? files.FirstOrDefault()
                      ?? throw new Exception("Нет файлов");

        var fileUrl  = primary["url"]?.ToString()!;
        var fileName = primary["filename"]?.ToString()!;
        var dest     = Path.Combine(profile.ModsDir, fileName);

        var progress = new Progress<DownloadProgress>(p =>
        {
            p.Status = $"Загрузка {fileName}... {p.Percentage}%";
            OnProgress?.Invoke(p);
        });

        await _dl.DownloadFileAsync(fileUrl, dest, progress, ct);
        return dest;
    }

    // ── Автоустановка Dark Loading Screen ─────────────────
    public async Task InstallCustomSplashModAsync(
        MinecraftProfile profile,
        CancellationToken ct = default)
    {
        if (profile.ModLoader != ModLoader.Fabric) return;
        try
        {
            var modsDir = profile.ModsDir;
            Directory.CreateDirectory(modsDir);

            var existing = Directory.GetFiles(modsDir, "*.jar")
                .Any(f =>
                {
                    var n = Path.GetFileName(f).ToLower();
                    return n.Contains("dark-loading-screen") ||
                           n.Contains("dark_loading_screen") ||
                           n.Contains("darkloadingscreen");
                });

            if (existing)
            {
                ApplyDarkLoadingScreenConfig(profile);
                return;
            }

            var versions = await TryGetVersionsAsync(
                DarkLoadingScreenId, profile.GameVersion, "fabric", ct);

            if (versions == null || versions.Count == 0)
                versions = await TryGetVersionsAsync(
                    DarkLoadingScreenId, null, "fabric", ct);

            if (versions == null || versions.Count == 0)
            {
                ApplyDarkLoadingScreenConfig(profile);
                return;
            }

            var latest  = versions[0];
            var files   = latest["files"] as JArray ?? new JArray();
            var primary = files.FirstOrDefault(f => f["primary"]?.Value<bool>() == true)
                          ?? files.FirstOrDefault();

            if (primary == null) return;

            var fileUrl  = primary["url"]?.ToString()!;
            var fileName = primary["filename"]?.ToString()!;
            var dest     = Path.Combine(modsDir, fileName);

            var prog = new Progress<DownloadProgress>(p => OnProgress?.Invoke(p));
            await _dl.DownloadFileAsync(fileUrl, dest, prog, ct);

            await InstallFabricApiAsync(profile, ct);
            ApplyDarkLoadingScreenConfig(profile);
        }
        catch (Exception ex)
        {
            Logger.Warning($"InstallCustomSplashMod: {ex.Message}", "ModManager");
        }
    }

    private async Task<JArray?> TryGetVersionsAsync(
        string projectId, string? gameVersion,
        string loader, CancellationToken ct)
    {
        try
        {
            var url = $"{ModrinthApi}/project/{projectId}/version?loaders=[\"{loader}\"]";
            if (!string.IsNullOrEmpty(gameVersion))
                url += $"&game_versions=[\"{gameVersion}\"]";
            var json = await _http.GetStringAsync(url, ct);
            return JArray.Parse(json);
        }
        catch { return null; }
    }

    private static void ApplyDarkLoadingScreenConfig(MinecraftProfile profile)
    {
        try
        {
            var cfgDir  = Path.Combine(profile.ProfileDir, "config");
            Directory.CreateDirectory(cfgDir);
            File.WriteAllText(
                Path.Combine(cfgDir, "dark-loading-screen.toml"),
                "# Dark Loading Screen config\n\n" +
                "[colors]\n" +
                "background = 0x0A0A15\n" +
                "bar = 0x8B6FD4\n" +
                "bar_background = 0x1A1A2E\n" +
                "bar_outline = 0x6B9FFF\n");
        }
        catch (Exception ex)
        {
            Logger.Warning($"ApplyDarkLoadingScreenConfig: {ex.Message}", "ModManager");
        }
    }

    private async Task InstallFabricApiAsync(
        MinecraftProfile profile, CancellationToken ct = default)
    {
        try
        {
            var modsDir = profile.ModsDir;
            var has     = Directory.GetFiles(modsDir, "*.jar")
                .Any(f => Path.GetFileName(f).ToLower().Contains("fabric-api"));
            if (has) return;

            var url  = $"{ModrinthApi}/project/fabric-api/version" +
                       $"?game_versions=[\"{profile.GameVersion}\"]" +
                       $"&loaders=[\"fabric\"]";
            var json = await _http.GetStringAsync(url, ct);
            var vers = JArray.Parse(json);
            if (vers.Count == 0) return;

            var files   = vers[0]["files"] as JArray ?? new JArray();
            var primary = files.FirstOrDefault(f => f["primary"]?.Value<bool>() == true)
                          ?? files.FirstOrDefault();
            if (primary == null) return;

            var fileUrl  = primary["url"]?.ToString()!;
            var fileName = primary["filename"]?.ToString()!;
            var dest     = Path.Combine(modsDir, fileName);
            var prog     = new Progress<DownloadProgress>(p => OnProgress?.Invoke(p));
            await _dl.DownloadFileAsync(fileUrl, dest, prog, ct);
        }
        catch (Exception ex)
        {
            Logger.Warning($"InstallFabricApi: {ex.Message}", "ModManager");
        }
    }

    public async Task<JObject?> GetProjectInfoAsync(string projectId)
    {
        try
        {
            var json = await _http.GetStringAsync($"{ModrinthApi}/project/{projectId}");
            return JObject.Parse(json);
        }
        catch { return null; }
    }

    public async Task<List<string>> GetDependenciesAsync(
        string projectId, string gameVersion, string loader)
    {
        try
        {
            var url  = $"{ModrinthApi}/project/{projectId}/version" +
                       $"?game_versions=[\"{gameVersion}\"]" +
                       $"&loaders=[\"{loader.ToLower()}\"]";
            var json = await _http.GetStringAsync(url);
            var vers = JArray.Parse(json);
            if (vers.Count == 0) return new();

            var deps = vers[0]["dependencies"] as JArray ?? new JArray();
            return deps
                .Where(d => d["dependency_type"]?.ToString() == "required")
                .Select(d => d["project_id"]?.ToString() ?? "")
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();
        }
        catch { return new(); }
    }
}