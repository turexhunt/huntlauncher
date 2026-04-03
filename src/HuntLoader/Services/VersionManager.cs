// src/HuntLoader/Services/VersionManager.cs
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

public class VersionManager
{
    private readonly HttpClient      _http     = new();
    private readonly DownloadService _dl       = new();
    private readonly FabricInstaller _fabric   = new();
    private VersionManifest?         _manifest;

    // ❌ Убрали: public event Action<DownloadProgress>? OnProgress;
    // Событие объявлялось но нигде не вызывалось — CS0067

    public async Task<VersionManifest> GetManifestAsync(bool forceRefresh = false)
    {
        if (_manifest != null && !forceRefresh) return _manifest;

        Logger.Info("Fetching version manifest...", "VersionManager");
        var json  = await _http.GetStringAsync(Constants.VersionManifestUrl);
        _manifest = JsonConvert.DeserializeObject<VersionManifest>(json)
                    ?? throw new Exception("Failed to parse manifest");

        Logger.Info($"Got {_manifest.Versions.Count} versions", "VersionManager");
        return _manifest;
    }

    public async Task<List<GameVersion>> GetVersionsAsync(
        bool includeSnapshots   = false,
        bool includeOldVersions = false)
    {
        var manifest = await GetManifestAsync();
        return manifest.Versions
            .Where(v =>
                v.Type == VersionType.Release ||
                (includeSnapshots   && v.Type == VersionType.Snapshot) ||
                (includeOldVersions && (v.Type == VersionType.OldBeta
                                     || v.Type == VersionType.OldAlpha)))
            .ToList();
    }

    public async Task InstallVersionAsync(
        string versionId,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default,
        ModLoader loader = ModLoader.Vanilla,
        string loaderVersion = "")
    {
        Logger.Info($"Installing {versionId} [{loader}]", "VersionManager");

        await InstallVanillaAsync(versionId, progress, ct);

        if (loader == ModLoader.Fabric)
        {
            progress?.Report(new DownloadProgress { Status = "Установка Fabric..." });

            var fabricLoader = loaderVersion;
            if (string.IsNullOrEmpty(fabricLoader))
            {
                var versions = await _fabric.GetLoaderVersionsAsync(versionId);
                fabricLoader = versions.FirstOrDefault() ?? "";
            }

            if (!string.IsNullOrEmpty(fabricLoader))
            {
                var fabricProgress = new Progress<string>(s =>
                    progress?.Report(new DownloadProgress { Status = s }));

                await _fabric.InstallFabricAsync(versionId, fabricLoader, fabricProgress, ct);
            }
        }

        Logger.Info($"Version {versionId} [{loader}] installed!", "VersionManager");
    }

    private async Task InstallVanillaAsync(
        string versionId,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        progress?.Report(new DownloadProgress
            { Status = $"Загрузка метаданных {versionId}..." });

        var manifest = await GetManifestAsync();
        var ver = manifest.Versions.FirstOrDefault(v => v.Id == versionId)
                  ?? throw new Exception($"Version {versionId} not found");

        var versionDir  = Path.Combine(Constants.VersionsDir, versionId);
        Directory.CreateDirectory(versionDir);

        var versionJson = Path.Combine(versionDir, $"{versionId}.json");
        if (!File.Exists(versionJson))
            await _dl.DownloadFileAsync(ver.Url, versionJson, null, ct);

        var meta = JObject.Parse(await File.ReadAllTextAsync(versionJson, ct));

        var clientJar = Path.Combine(versionDir, $"{versionId}.jar");
        var clientUrl = meta["downloads"]?["client"]?["url"]?.ToString();
        if (clientUrl != null && !File.Exists(clientJar))
        {
            progress?.Report(new DownloadProgress
                { Status = $"Загрузка {versionId}.jar..." });

            await _dl.DownloadFileAsync(clientUrl, clientJar,
                new Progress<DownloadProgress>(p =>
                {
                    p.Status = $"Загрузка клиента {p.Percentage}%";
                    progress?.Report(p);
                }), ct);
        }

        await InstallLibrariesAsync(meta, progress, ct);
        await InstallAssetsAsync(meta, progress, ct);
    }

    private async Task InstallLibrariesAsync(
        JObject meta,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        var libs       = meta["libraries"] as JArray ?? new JArray();
        var toDownload = new List<(string Url, string Path)>();

        foreach (var lib in libs)
        {
            if (!IsLibraryCompatible(lib)) continue;

            var artifact = lib["downloads"]?["artifact"];
            if (artifact == null) continue;

            var url  = artifact["url"]?.ToString();
            var path = artifact["path"]?.ToString();
            if (url == null || path == null) continue;

            var dest = Path.Combine(Constants.LibrariesDir, path);
            if (!File.Exists(dest))
                toDownload.Add((url, dest));
        }

        progress?.Report(new DownloadProgress
        {
            Status     = $"Загрузка библиотек ({toDownload.Count})...",
            FilesTotal = toDownload.Count
        });

        await _dl.DownloadManyAsync(toDownload,
            new Progress<DownloadProgress>(p => progress?.Report(p)), ct);
    }

    private async Task InstallAssetsAsync(
        JObject meta,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        var assetIndex = meta["assetIndex"];
        if (assetIndex == null) return;

        var indexId  = assetIndex["id"]?.ToString()!;
        var indexUrl = assetIndex["url"]?.ToString()!;

        var indexDir  = Path.Combine(Constants.AssetsDir, "indexes");
        var indexFile = Path.Combine(indexDir, $"{indexId}.json");
        Directory.CreateDirectory(indexDir);

        if (!File.Exists(indexFile))
            await _dl.DownloadFileAsync(indexUrl, indexFile, null, ct);

        var indexJson = JObject.Parse(await File.ReadAllTextAsync(indexFile, ct));
        var objects   = indexJson["objects"] as JObject ?? new JObject();

        var toDownload = new List<(string Url, string Path)>();
        foreach (var obj in objects.Properties())
        {
            var hash   = obj.Value["hash"]?.ToString();
            if (hash == null) continue;
            var prefix = hash[..2];
            var dest   = Path.Combine(Constants.AssetsDir, "objects", prefix, hash);
            var url    = $"{Constants.AssetsBaseUrl}{prefix}/{hash}";
            if (!File.Exists(dest))
                toDownload.Add((url, dest));
        }

        progress?.Report(new DownloadProgress
        {
            Status     = $"Загрузка ресурсов ({toDownload.Count})...",
            FilesTotal = toDownload.Count
        });

        await _dl.DownloadManyAsync(toDownload,
            new Progress<DownloadProgress>(p => progress?.Report(p)), ct);
    }

    private static bool IsLibraryCompatible(JToken lib)
    {
        var rules = lib["rules"] as JArray;
        if (rules == null) return true;

        var allowed = false;
        foreach (var rule in rules)
        {
            var action = rule["action"]?.ToString();
            var osName = rule["os"]?["name"]?.ToString();
            if (action == "allow")
            {
                if (osName == null || osName == "windows") allowed = true;
            }
            else if (action == "disallow")
            {
                if (osName == null || osName == "windows") return false;
            }
        }
        return allowed;
    }

    public bool IsVersionInstalled(string versionId) =>
        File.Exists(Path.Combine(
            Constants.VersionsDir, versionId, $"{versionId}.jar"));

    public bool IsFabricInstalled(string gameVersion, string loaderVersion) =>
        _fabric.IsFabricInstalled(gameVersion, loaderVersion);

    public string? GetFabricVersionId(string gameVersion, string loaderVersion) =>
        _fabric.GetFabricVersionId(gameVersion, loaderVersion);

    public FabricInstaller FabricInstaller => _fabric;
}