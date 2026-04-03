// src/HuntLoader/Services/GameLaunchService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HuntLoader.Core;
using HuntLoader.Models;
using Newtonsoft.Json.Linq;

namespace HuntLoader.Services;

public class GameLaunchService
{
    private Process?  _gameProcess;
    private DateTime  _sessionStart;

    public readonly JavaManager JavaManager = new();

    public bool IsRunning => _gameProcess is { HasExited: false };

    public event Action<string>? OnOutput;
    public event Action<string>? OnError;
    public event Action?         OnStarted;
    public event Action<int>?    OnExited;
    public event Action<string>? OnStatus;
    public event Action<int>?    OnProgress;

    public async Task LaunchAsync(
        MinecraftProfile  profile,
        Account           account,
        CancellationToken ct = default)
    {
        if (IsRunning)
            throw new InvalidOperationException("Игра уже запущена");

        Logger.Info("=== LAUNCH START ===", "Launch");
        Logger.Info(
            $"Profile: {profile.Name} | " +
            $"Version: {profile.GameVersion} | " +
            $"Loader: {profile.ModLoader}", "Launch");
        Logger.Info($"Account: {account.Username}", "Launch");

        profile.EnsureDirectories();

        var (versionId, versionDir, versionJson) = ResolveVersion(profile);
        Logger.Info($"VersionId: {versionId}", "Launch");

        if (!File.Exists(versionJson))
            throw new FileNotFoundException(
                $"Файл версии не найден: {versionJson}\n" +
                "Нажми ИГРАТЬ снова — версия скачается заново.");

        var meta       = JObject.Parse(
            await File.ReadAllTextAsync(versionJson, ct));
        var nativesDir = Path.Combine(versionDir, "natives");
        await ExtractNativesAsync(meta, nativesDir);

        // ── Автоматически найти или скачать Java ──────────
        var javaPath = await FindOrDownloadJavaAsync(profile, ct);
        Logger.Info($"Java: {javaPath}", "Launch");

        var cp = profile.ModLoader == ModLoader.Fabric
            ? BuildFabricClasspath(meta, profile)
            : BuildVanillaClasspath(meta, versionDir, versionId);

        Logger.Info($"CP entries: {cp.Split(';').Length}", "Launch");

        var jvmArgs  = BuildJvmArgs(profile, nativesDir, cp);
        var gameArgs = BuildGameArgs(meta, profile, account);
        var fullArgs = $"{jvmArgs} {gameArgs}";

        Logger.Info(
            $"Args[0..400]: {fullArgs[..Math.Min(400, fullArgs.Length)]}",
            "Launch");

        var psi = new ProcessStartInfo
        {
            FileName               = javaPath,
            Arguments              = fullArgs,
            WorkingDirectory       = profile.ProfileDir,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = false,
        };

        _gameProcess = new Process
        {
            StartInfo            = psi,
            EnableRaisingEvents  = true
        };

        _gameProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            OnOutput?.Invoke(e.Data);
            Logger.Debug(e.Data, "MC");
        };
        _gameProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            OnError?.Invoke(e.Data);
            Logger.Debug($"[ERR] {e.Data}", "MC");
        };
        _gameProcess.Exited += (_, _) =>
        {
            var code    = _gameProcess?.ExitCode ?? -1;
            var elapsed = DateTime.Now - _sessionStart;
            profile.TotalPlayTime += elapsed;
            Logger.Info($"=== GAME EXIT: {code} ===", "Launch");
            OnExited?.Invoke(code);
            _gameProcess = null;
        };

        _sessionStart      = DateTime.Now;
        account.LastUsed   = DateTime.Now;
        profile.LastPlayed = DateTime.Now;

        try
        {
            _gameProcess.Start();
            _gameProcess.BeginOutputReadLine();
            _gameProcess.BeginErrorReadLine();
            OnStarted?.Invoke();
            Logger.Info("=== GAME STARTED ===", "Launch");
        }
        catch (Exception ex)
        {
            _gameProcess = null;
            Logger.Error(ex, "Launch");
            throw new Exception(
                $"Не удалось запустить Java:\n{ex.Message}\n" +
                $"Java: {javaPath}\n" +
                "Укажи путь к Java в Настройках → Java.");
        }
    }

    public void Kill()
    {
        try { _gameProcess?.Kill(entireProcessTree: true); }
        catch (Exception ex) { Logger.Error(ex, "Launch"); }
    }

    // ── Find or Download Java ──────────────────────────────
    private async Task<string> FindOrDownloadJavaAsync(
        MinecraftProfile  profile,
        CancellationToken ct)
    {
        void StatusHandler(string s)  => OnStatus?.Invoke(s);
        void ProgressHandler(int p)   => OnProgress?.Invoke(p);

        JavaManager.OnStatus   += StatusHandler;
        JavaManager.OnProgress += ProgressHandler;

        try
        {
            return await JavaManager.GetJavaForMinecraftAsync(
                profile.GameVersion,
                profile.JavaPath,
                ct);
        }
        finally
        {
            JavaManager.OnStatus   -= StatusHandler;
            JavaManager.OnProgress -= ProgressHandler;
        }
    }

    // ── Resolve ────────────────────────────────────────────
    private static (string id, string dir, string json) ResolveVersion(
        MinecraftProfile profile)
    {
        if (profile.ModLoader == ModLoader.Fabric)
        {
            var fabricId = FindFabricId(
                profile.GameVersion,
                profile.ModLoaderVersion);
            if (fabricId != null)
            {
                var fd = Path.Combine(Constants.VersionsDir, fabricId);
                var fj = Path.Combine(fd, $"{fabricId}.json");
                if (File.Exists(fj))
                {
                    Logger.Info($"Using Fabric: {fabricId}", "Launch");
                    return (fabricId, fd, fj);
                }
            }
            Logger.Warning("Fabric JSON не найден, используем vanilla", "Launch");
        }

        var vid   = profile.GameVersion;
        var vdir  = Path.Combine(Constants.VersionsDir, vid);
        var vjson = Path.Combine(vdir, $"{vid}.json");
        return (vid, vdir, vjson);
    }

    private static string? FindFabricId(string gameVer, string loaderVer)
    {
        if (!Directory.Exists(Constants.VersionsDir)) return null;

        if (!string.IsNullOrEmpty(loaderVer))
        {
            var id = $"fabric-loader-{loaderVer}-{gameVer}";
            if (Directory.Exists(Path.Combine(Constants.VersionsDir, id)))
                return id;
        }

        return Directory.GetDirectories(Constants.VersionsDir)
            .Select(Path.GetFileName)
            .Where(d => d != null &&
                        d!.StartsWith("fabric-loader-") &&
                        d.EndsWith($"-{gameVer}"))
            .OrderByDescending(d => d)
            .FirstOrDefault();
    }

    // ── Natives ────────────────────────────────────────────
    private static async Task ExtractNativesAsync(
        JObject meta, string nativesDir)
    {
        try
        {
            Directory.CreateDirectory(nativesDir);
            if (Directory.GetFiles(nativesDir, "*.dll").Length > 0) return;

            var libs = meta["libraries"] as JArray ?? new JArray();
            foreach (var lib in libs)
            {
                var name = lib["name"]?.ToString() ?? "";
                if (name.Contains("natives-windows"))
                {
                    var path = lib["downloads"]?["artifact"]?["path"]
                        ?.ToString();
                    if (path != null)
                    {
                        var jar = Path.Combine(Constants.LibrariesDir, path);
                        if (File.Exists(jar)) ExtractDlls(jar, nativesDir);
                    }
                }

                var native =
                    lib["downloads"]?["classifiers"]?["natives-windows-64"] ??
                    lib["downloads"]?["classifiers"]?["natives-windows"]     ??
                    lib["downloads"]?["classifiers"]?["natives-windows-32"];

                var nPath = native?["path"]?.ToString();
                if (nPath == null) continue;
                var nJar = Path.Combine(Constants.LibrariesDir, nPath);
                if (File.Exists(nJar)) ExtractDlls(nJar, nativesDir);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Natives: {ex.Message}", "Launch");
        }
    }

    private static void ExtractDlls(string jar, string dest)
    {
        using var zip = ZipFile.OpenRead(jar);
        foreach (var e in zip.Entries)
        {
            if (e.FullName.StartsWith("META-INF")) continue;
            if (!e.Name.EndsWith(".dll") && !e.Name.EndsWith(".so")) continue;
            var dp = Path.Combine(dest, e.Name);
            if (!File.Exists(dp)) e.ExtractToFile(dp, false);
        }
    }

    // ── Classpath ──────────────────────────────────────────
    private static string BuildFabricClasspath(
        JObject meta, MinecraftProfile profile)
    {
        var parts = new List<string>();

        var fabricLibs = meta["libraries"] as JArray ?? new JArray();
        foreach (var lib in fabricLibs)
        {
            var name = lib["name"]?.ToString();
            if (name == null) continue;
            var path = MavenToPath(name);
            var full = Path.Combine(Constants.LibrariesDir, path);
            if (File.Exists(full) && !parts.Contains(full))
                parts.Add(full);
        }

        var inheritsFrom = meta["inheritsFrom"]?.ToString();
        if (inheritsFrom != null)
        {
            var pj = Path.Combine(
                Constants.VersionsDir,
                inheritsFrom,
                $"{inheritsFrom}.json");
            if (File.Exists(pj))
            {
                var parentMeta  = JObject.Parse(File.ReadAllText(pj));
                var vanillaLibs = parentMeta["libraries"] as JArray
                                  ?? new JArray();

                foreach (var lib in vanillaLibs)
                {
                    if (!IsCompatible(lib)) continue;

                    var name = lib["name"]?.ToString() ?? "";
                    if (name.StartsWith("org.ow2.asm:")) continue;

                    var path = lib["downloads"]?["artifact"]?["path"]
                        ?.ToString();
                    if (path == null) continue;
                    var full = Path.Combine(Constants.LibrariesDir, path);
                    if (File.Exists(full) && !parts.Contains(full))
                        parts.Add(full);
                }

                var vanillaJar = Path.Combine(
                    Constants.VersionsDir,
                    inheritsFrom,
                    $"{inheritsFrom}.jar");
                if (File.Exists(vanillaJar) && !parts.Contains(vanillaJar))
                    parts.Add(vanillaJar);
            }
        }

        return string.Join(";", parts);
    }

    private static string BuildVanillaClasspath(
        JObject meta, string versionDir, string versionId)
    {
        var parts = new List<string>();
        var libs  = meta["libraries"] as JArray ?? new JArray();

        foreach (var lib in libs)
        {
            if (!IsCompatible(lib)) continue;
            var path = lib["downloads"]?["artifact"]?["path"]?.ToString();
            if (path == null) continue;
            var full = Path.Combine(Constants.LibrariesDir, path);
            if (File.Exists(full) && !parts.Contains(full))
                parts.Add(full);
        }

        var jar = Path.Combine(versionDir, $"{versionId}.jar");
        if (File.Exists(jar) && !parts.Contains(jar))
            parts.Add(jar);

        return string.Join(";", parts);
    }

    // ── JVM Args ───────────────────────────────────────────
    private static string BuildJvmArgs(
        MinecraftProfile profile, string nativesDir, string cp)
    {
        var sb = new StringBuilder();

        sb.Append($"-Xms{profile.MemoryMin}m ");
        sb.Append($"-Xmx{profile.MemoryMax}m ");

        sb.Append("-XX:+UseG1GC ");
        sb.Append("-XX:+ParallelRefProcEnabled ");
        sb.Append("-XX:MaxGCPauseMillis=200 ");
        sb.Append("-XX:+UnlockExperimentalVMOptions ");
        sb.Append("-XX:+DisableExplicitGC ");
        sb.Append("-XX:+AlwaysPreTouch ");
        sb.Append("-XX:G1NewSizePercent=30 ");
        sb.Append("-XX:G1MaxNewSizePercent=40 ");
        sb.Append("-XX:G1HeapRegionSize=8M ");
        sb.Append("-XX:G1ReservePercent=20 ");
        sb.Append("-XX:G1HeapWastePercent=5 ");
        sb.Append("-XX:G1MixedGCCountTarget=4 ");
        sb.Append("-XX:InitiatingHeapOccupancyPercent=15 ");
        sb.Append("-XX:G1MixedGCLiveThresholdPercent=90 ");
        sb.Append("-XX:G1RSetUpdatingPauseTimePercent=5 ");
        sb.Append("-XX:SurvivorRatio=32 ");
        sb.Append("-XX:+PerfDisableSharedMem ");
        sb.Append("-XX:MaxTenuringThreshold=1 ");

        sb.Append("-Dfile.encoding=UTF-8 ");
        sb.Append("-Djava.rmi.server.useCodebaseOnly=true ");
        sb.Append("-Dcom.sun.jndi.rmi.object.trustURLCodebase=false ");
        sb.Append("-Dcom.sun.jndi.cosnaming.object.trustURLCodebase=false ");

        sb.Append($"-Djava.library.path=\"{nativesDir}\" ");
        sb.Append($"-Djna.tmpdir=\"{nativesDir}\" ");
        sb.Append(
            $"-Dorg.lwjgl.system.SharedLibraryExtractPath=\"{nativesDir}\" ");
        sb.Append($"-Dio.netty.native.workdir=\"{nativesDir}\" ");

        sb.Append("-Dminecraft.launcher.brand=HuntLoader ");
        sb.Append("-Dminecraft.launcher.version=1.0.0 ");

        if (!string.IsNullOrWhiteSpace(profile.JavaArgs))
            sb.Append($"{profile.JavaArgs} ");

        sb.Append($"-cp \"{cp}\" ");

        return sb.ToString().Trim();
    }

    // ── Game Args ──────────────────────────────────────────
    private static string BuildGameArgs(
        JObject meta, MinecraftProfile profile, Account account)
    {
        var sb        = new StringBuilder();
        var mainClass = meta["mainClass"]?.ToString()
                        ?? "net.minecraft.client.main.Main";
        sb.Append($"{mainClass} ");

        var assetIndex = meta["assetIndex"]?["id"]?.ToString();
        if (assetIndex == null)
        {
            var parent = meta["inheritsFrom"]?.ToString();
            if (parent != null)
            {
                var pj = Path.Combine(
                    Constants.VersionsDir,
                    parent,
                    $"{parent}.json");
                if (File.Exists(pj))
                {
                    var pm = JObject.Parse(File.ReadAllText(pj));
                    assetIndex = pm["assetIndex"]?["id"]?.ToString();
                }
            }
        }
        assetIndex ??= profile.GameVersion;

        if (meta["arguments"] != null)
        {
            sb.Append($"--username \"{account.Username}\" ");
            sb.Append($"--version \"{profile.GameVersion}\" ");
            sb.Append($"--gameDir \"{profile.ProfileDir}\" ");
            sb.Append($"--assetsDir \"{Constants.AssetsDir}\" ");
            sb.Append($"--assetIndex \"{assetIndex}\" ");
            sb.Append($"--uuid \"{account.UUID}\" ");
            sb.Append($"--accessToken \"{account.GetEffectiveToken()}\" ");
            sb.Append("--userType mojang ");
            sb.Append("--versionType release ");
            if (!profile.Fullscreen)
            {
                sb.Append($"--width {profile.ResolutionWidth} ");
                sb.Append($"--height {profile.ResolutionHeight} ");
            }
        }
        else
        {
            var old = (meta["minecraftArguments"]?.ToString() ?? "")
                .Replace("${auth_player_name}",  account.Username)
                .Replace("${version_name}",       profile.GameVersion)
                .Replace("${game_directory}",     profile.ProfileDir)
                .Replace("${assets_root}",        Constants.AssetsDir)
                .Replace("${assets_index_name}",  assetIndex)
                .Replace("${auth_uuid}",          account.UUID)
                .Replace("${auth_access_token}",  account.GetEffectiveToken())
                .Replace("${user_type}",          "mojang")
                .Replace("${version_type}",       "release")
                .Replace("${user_properties}",    "{}")
                .Replace("${game_assets}",
                    Path.Combine(
                        Constants.AssetsDir, "virtual", "legacy"))
                .Replace("${auth_session}",
                    account.GetEffectiveToken());
            sb.Append(old);
        }

        return sb.ToString().Trim();
    }

    // ── Helpers ────────────────────────────────────────────
    private static bool IsCompatible(JToken lib)
    {
        var rules = lib["rules"] as JArray;
        if (rules == null) return true;
        var ok = false;
        foreach (var r in rules)
        {
            var action = r["action"]?.ToString();
            var os     = r["os"]?["name"]?.ToString();
            if (action == "allow")
            {
                if (os == null || os == "windows") ok = true;
            }
            if (action == "disallow")
            {
                if (os == null || os == "windows") return false;
            }
        }
        return ok;
    }

    private static string MavenToPath(string name)
    {
        var p = name.Split(':');
        if (p.Length < 3) return name;
        return Path.Combine(
            p[0].Replace('.', Path.DirectorySeparatorChar),
            p[1], p[2], $"{p[1]}-{p[2]}.jar");
    }
}