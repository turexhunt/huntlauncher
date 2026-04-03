// src/HuntLoader/Services/CurseForgeService.cs
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

public class CurseForgeMod
{
    public int    Id          { get; set; }
    public string Name        { get; set; } = "";
    public string Summary     { get; set; } = "";
    public long   Downloads   { get; set; }
    public string IconUrl     { get; set; } = "";
    public string Slug        { get; set; } = "";
    public string WebsiteUrl  { get; set; } = "";
    public List<string> Categories { get; set; } = new();

    public string DownloadsFormatted =>
        Downloads > 1_000_000 ? $"{Downloads / 1_000_000.0:F1}M" :
        Downloads > 1_000     ? $"{Downloads / 1000.0:F0}K"       :
                                Downloads.ToString();
}

public class CurseForgeService
{
    // ── Получить API ключ: https://console.curseforge.com/ ──
    private const string ApiKey    = "";
    private const string BaseUrl   = "https://api.curseforge.com/v1";
    private const int    McGameId  = 432; // Minecraft game ID на CurseForge

    private readonly HttpClient      _http;
    private readonly DownloadService _dl = new();

    public event Action<DownloadProgress>? OnProgress;

    public CurseForgeService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("x-api-key", ApiKey);
        _http.DefaultRequestHeaders.Add("User-Agent",
            $"HuntLoader/{Constants.LauncherVersion}");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    // ── Проверка доступности API ───────────────────────────
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var resp = await _http.GetAsync($"{BaseUrl}/games/{McGameId}");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Маппинг loader → CurseForge modLoaderType ─────────
    private static int GetLoaderType(string loader) => loader.ToLower() switch
    {
        "forge"    => 1,
        "cauldron" => 2,
        "liteloader"=> 3,
        "fabric"   => 4,
        "quilt"    => 5,
        "neoforge" => 6,
        _          => 0
    };

    // ── Поиск модов ───────────────────────────────────────
    public async Task<List<CurseForgeMod>> SearchAsync(
        string query,
        string? gameVersion = null,
        string? loader      = null,
        int     limit       = 20,
        CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/mods/search" +
                  $"?gameId={McGameId}" +
                  $"&searchFilter={Uri.EscapeDataString(query)}" +
                  $"&pageSize={limit}" +
                  $"&sortField=2" +   // TotalDownloads
                  $"&sortOrder=desc" +
                  $"&classId=6";      // Mods class

        if (!string.IsNullOrEmpty(gameVersion))
            url += $"&gameVersion={gameVersion}";

        if (!string.IsNullOrEmpty(loader))
        {
            var lt = GetLoaderType(loader);
            if (lt > 0) url += $"&modLoaderType={lt}";
        }

        try
        {
            var json = await _http.GetStringAsync(url, ct);
            var obj  = JObject.Parse(json);
            var data = obj["data"] as JArray ?? new JArray();

            return data.Select(ParseMod).ToList();
        }
        catch (Exception ex)
        {
            Logger.Warning($"CurseForge search failed: {ex.Message}", "CurseForge");
            return new List<CurseForgeMod>();
        }
    }

    // ── Скачивание мода ───────────────────────────────────
    public async Task<string> DownloadModAsync(
        int               modId,
        MinecraftProfile  profile,
        CancellationToken ct = default)
    {
        var loader = profile.ModLoader.ToString().ToLower();

        // Получаем список файлов мода
        var url  = $"{BaseUrl}/mods/{modId}/files" +
                   $"?gameVersion={profile.GameVersion}" +
                   $"&modLoaderType={GetLoaderType(loader)}" +
                   $"&pageSize=5";

        var json  = await _http.GetStringAsync(url, ct);
        var obj   = JObject.Parse(json);
        var files = obj["data"] as JArray ?? new JArray();

        if (files.Count == 0)
        {
            // Пробуем без фильтра лоадера
            url   = $"{BaseUrl}/mods/{modId}/files" +
                    $"?gameVersion={profile.GameVersion}" +
                    $"&pageSize=10";
            json  = await _http.GetStringAsync(url, ct);
            obj   = JObject.Parse(json);
            files = obj["data"] as JArray ?? new JArray();
        }

        if (files.Count == 0)
            throw new Exception("Нет совместимых версий мода для CurseForge");

        // Берём первый (новейший) файл
        var file     = files[0];
        var fileUrl  = file["downloadUrl"]?.ToString();
        var fileName = file["fileName"]?.ToString() ?? $"mod_{modId}.jar";

        if (string.IsNullOrEmpty(fileUrl))
            throw new Exception("CurseForge не предоставил URL для скачивания");

        var dest = Path.Combine(profile.ModsDir, fileName);

        var progress = new Progress<DownloadProgress>(p =>
        {
            p.Status = $"CurseForge: {fileName} {p.Percentage}%";
            OnProgress?.Invoke(p);
        });

        await _dl.DownloadFileAsync(fileUrl, dest, progress, ct);
        return dest;
    }

    // ── Популярные моды ───────────────────────────────────
    public async Task<List<CurseForgeMod>> GetPopularAsync(
        string? gameVersion = null,
        string? loader      = null,
        CancellationToken ct = default)
    {
        return await SearchAsync("", gameVersion, loader, 20, ct);
    }

    // ── Детали мода ───────────────────────────────────────
    public async Task<CurseForgeMod?> GetModAsync(int modId, CancellationToken ct = default)
    {
        try
        {
            var json = await _http.GetStringAsync($"{BaseUrl}/mods/{modId}", ct);
            var obj  = JObject.Parse(json);
            return ParseMod(obj["data"] ?? new JObject());
        }
        catch { return null; }
    }

    // ── Парсинг мода ──────────────────────────────────────
    private static CurseForgeMod ParseMod(JToken t) => new()
    {
        Id         = t["id"]?.Value<int>() ?? 0,
        Name       = t["name"]?.ToString() ?? "",
        Summary    = t["summary"]?.ToString() ?? "",
        Downloads  = t["downloadCount"]?.Value<long>() ?? 0,
        Slug       = t["slug"]?.ToString() ?? "",
        WebsiteUrl = t["links"]?["websiteUrl"]?.ToString() ?? "",
        IconUrl    = t["logo"]?["url"]?.ToString() ?? "",
        Categories = (t["categories"] as JArray ?? new JArray())
                     .Select(c => c["name"]?.ToString() ?? "")
                     .Where(s => !string.IsNullOrEmpty(s))
                     .ToList()
    };
}