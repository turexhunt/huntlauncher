// src/HuntLoader/Services/FabricInstaller.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HuntLoader.Core;
using Newtonsoft.Json.Linq;

namespace HuntLoader.Services;

public class FabricInstaller
{
    private readonly HttpClient      _http = new();
    private readonly DownloadService _dl   = new();

    private const string FabricMeta =
        "https://meta.fabricmc.net/v2";

    // ❌ Убрали: public event Action<string>? OnStatus;
    // Событие объявлялось но нигде не вызывалось — CS0067

    public async Task<List<string>> GetLoaderVersionsAsync(string gameVersion)
    {
        var url  = $"{FabricMeta}/versions/loader/{gameVersion}";
        var json = await _http.GetStringAsync(url);
        var arr  = JArray.Parse(json);
        return arr
            .Select(v => v["loader"]?["version"]?.ToString() ?? "")
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();
    }

    public async Task InstallFabricAsync(
        string gameVersion,
        string loaderVersion,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        Logger.Info($"Installing Fabric {loaderVersion} for {gameVersion}", "Fabric");

        var fabricVersionId = $"fabric-loader-{loaderVersion}-{gameVersion}";
        var versionDir      = Path.Combine(Constants.VersionsDir, fabricVersionId);
        Directory.CreateDirectory(versionDir);

        var profileJson = Path.Combine(versionDir, $"{fabricVersionId}.json");

        progress?.Report("Загрузка метаданных Fabric...");
        var profileUrl = $"{FabricMeta}/versions/loader/{gameVersion}/{loaderVersion}/profile/json";
        var profileContent = await _http.GetStringAsync(profileUrl, ct);
        await File.WriteAllTextAsync(profileJson, profileContent, ct);

        var meta = JObject.Parse(profileContent);
        var libs = meta["libraries"] as JArray ?? new JArray();

        var toDownload = new List<(string Url, string Path)>();
        foreach (var lib in libs)
        {
            var name = lib["name"]?.ToString();
            var url  = lib["url"]?.ToString();
            if (name == null || url == null) continue;

            var path  = MavenNameToPath(name);
            var dest  = Path.Combine(Constants.LibrariesDir, path);
            var dlUrl = url.TrimEnd('/') + "/" + path.Replace('\\', '/');

            if (!File.Exists(dest))
                toDownload.Add((dlUrl, dest));
        }

        progress?.Report($"Загрузка библиотек Fabric ({toDownload.Count})...");
        Logger.Info($"Downloading {toDownload.Count} Fabric libraries", "Fabric");

        foreach (var (dlUrl, dest) in toDownload)
        {
            try
            {
                await _dl.DownloadFileAsync(dlUrl, dest, null, ct);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to download {dlUrl}: {ex.Message}", "Fabric");
            }
        }

        var vanillaJar = Path.Combine(Constants.VersionsDir, gameVersion, $"{gameVersion}.jar");
        var fabricJar  = Path.Combine(versionDir, $"{fabricVersionId}.jar");

        if (File.Exists(vanillaJar) && !File.Exists(fabricJar))
            File.Copy(vanillaJar, fabricJar);

        progress?.Report($"✅ Fabric {loaderVersion} установлен!");
        Logger.Info($"Fabric installed: {fabricVersionId}", "Fabric");
    }

    public bool IsFabricInstalled(string gameVersion, string loaderVersion)
    {
        var id  = $"fabric-loader-{loaderVersion}-{gameVersion}";
        var dir = Path.Combine(Constants.VersionsDir, id);
        return File.Exists(Path.Combine(dir, $"{id}.json"));
    }

    public string? GetFabricVersionId(string gameVersion, string loaderVersion)
    {
        var id = $"fabric-loader-{loaderVersion}-{gameVersion}";
        if (IsFabricInstalled(gameVersion, loaderVersion)) return id;
        return null;
    }

    private static string MavenNameToPath(string name)
    {
        var parts = name.Split(':');
        if (parts.Length < 3) return name;

        var group    = parts[0].Replace('.', Path.DirectorySeparatorChar);
        var artifact = parts[1];
        var version  = parts[2];
        var fileName = $"{artifact}-{version}.jar";

        return Path.Combine(group, artifact, version, fileName);
    }
}