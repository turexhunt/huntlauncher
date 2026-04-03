// src/HuntLoader/Services/JavaManager.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HuntLoader.Core;

namespace HuntLoader.Services;

public class JavaManager
{
    private readonly DownloadService _dl = new();

    private const string AdoptiumApi =
        "https://api.adoptium.net/v3/binary/latest/{0}/ga/windows/x64/jdk/hotspot/normal/eclipse";

    private static readonly Dictionary<int, string> FallbackUrls = new()
    {
        [21] = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.5%2B11/OpenJDK21U-jdk_x64_windows_hotspot_21.0.5_11.zip",
        [17] = "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.13%2B11/OpenJDK17U-jdk_x64_windows_hotspot_17.0.13_11.zip",
        [11] = "https://github.com/adoptium/temurin11-binaries/releases/download/jdk-11.0.25%2B9/OpenJDK11U-jdk_x64_windows_hotspot_11.0.25_9.zip",
        [8]  = "https://github.com/adoptium/temurin8-binaries/releases/download/jdk8u432-b06/OpenJDK8U-jdk_x64_windows_hotspot_8u432b06.zip",
    };

    public event Action<string>? OnStatus;
    public event Action<int>?    OnProgress;

    // ── Поиск установленной Java ──────────────────────────
    public string? FindInstalledJava(int requiredMajor = 0)
    {
        // 1. Бандлированная Java в AppData лаунчера (корень)
        var bundled = Path.Combine(Constants.JavaDir, "bin", "javaw.exe");
        if (File.Exists(bundled))
        {
            if (requiredMajor == 0) return bundled;
            var bv = GetMajorVersionSync(bundled);
            if (bv >= requiredMajor)
            {
                Logger.Info($"Using bundled Java {bv}: {bundled}", "JavaManager");
                return bundled;
            }
        }

        // 2. Папки под конкретные версии jdk21, jdk17 и т.д.
        if (Directory.Exists(Constants.JavaDir))
        {
            foreach (var dir in Directory.GetDirectories(Constants.JavaDir)
                         .OrderByDescending(d => d))
            {
                var jw = Path.Combine(dir, "bin", "javaw.exe");
                if (!File.Exists(jw)) continue;
                if (requiredMajor == 0) return jw;
                var v = GetMajorVersionSync(jw);
                if (v >= requiredMajor)
                {
                    Logger.Info($"Using bundled Java {v}: {jw}", "JavaManager");
                    return jw;
                }
            }
        }

        // 3. Глобальный путь из настроек
        var g = AppConfig.Instance.GlobalJavaPath;
        if (!string.IsNullOrEmpty(g) && File.Exists(g))
        {
            if (requiredMajor == 0) return g;
            var gv = GetMajorVersionSync(g);
            if (gv >= requiredMajor)
            {
                Logger.Info($"Using config Java {gv}: {g}", "JavaManager");
                return g;
            }
        }

        // 4. JAVA_HOME
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            var p = Path.Combine(javaHome, "bin", "javaw.exe");
            if (File.Exists(p))
            {
                if (requiredMajor == 0) return p;
                var v = GetMajorVersionSync(p);
                if (v >= requiredMajor)
                {
                    Logger.Info($"Using JAVA_HOME Java {v}: {p}", "JavaManager");
                    return p;
                }
            }
        }

        // 5. PATH
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';'))
        {
            var p = Path.Combine(dir.Trim(), "javaw.exe");
            if (!File.Exists(p)) continue;
            if (requiredMajor == 0) return p;
            var v = GetMajorVersionSync(p);
            if (v >= requiredMajor)
            {
                Logger.Info($"Using PATH Java {v}: {p}", "JavaManager");
                return p;
            }
        }

        // 6. Стандартные директории установки
        var searchRoots = new[]
        {
            @"C:\Program Files\Eclipse Adoptium",
            @"C:\Program Files\Java",
            @"C:\Program Files\Microsoft",
            @"C:\Program Files\BellSoft",
            @"C:\Program Files\Zulu",
            @"C:\Program Files\Amazon Corretto",
            @"C:\Program Files\OpenJDK",
        };

        var candidates = new List<(string path, int version)>();

        foreach (var root in searchRoots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var dir in Directory.GetDirectories(root))
            {
                var jw = Path.Combine(dir, "bin", "javaw.exe");
                if (!File.Exists(jw)) continue;
                var v = GetMajorVersionSync(jw);
                if (v > 0) candidates.Add((jw, v));
            }
        }

        if (requiredMajor > 0)
        {
            var best = candidates
                .Where(c => c.version >= requiredMajor)
                .OrderBy(c => c.version)
                .FirstOrDefault();
            if (best.path != null)
            {
                Logger.Info(
                    $"Found system Java {best.version}: {best.path}",
                    "JavaManager");
                return best.path;
            }
        }
        else if (candidates.Count > 0)
        {
            var best = candidates
                .OrderByDescending(c => c.version)
                .First();
            return best.path;
        }

        return null;
    }

    // ── Получить мажорную версию Java синхронно ───────────
    public int GetMajorVersionSync(string javaPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName              = javaPath,
                Arguments             = "-version",
                RedirectStandardError = true,
                UseShellExecute       = false,
                CreateNoWindow        = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return 0;
            var ver = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            return ParseMajorVersion(ver);
        }
        catch { return 0; }
    }

    // ── Получить полную строку версии асинхронно ──────────
    public async Task<string> GetJavaVersionAsync(string javaPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName              = javaPath,
                Arguments             = "-version",
                RedirectStandardError = true,
                UseShellExecute       = false,
                CreateNoWindow        = true
            };
            var proc = Process.Start(psi)!;
            var ver  = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return ver.Split('\n')[0].Trim();
        }
        catch { return "Unknown"; }
    }

    // ── Скачать и установить Java ─────────────────────────
    public async Task<string> DownloadJavaAsync(
        int               majorVersion = 21,
        CancellationToken ct           = default)
    {
        Logger.Info($"Downloading Java {majorVersion}...", "JavaManager");
        OnStatus?.Invoke($"Загрузка Java {majorVersion}...");

        var javaVersionDir = Path.Combine(
            Constants.JavaDir, $"jdk{majorVersion}");
        var tempFile = Path.Combine(
            Constants.TempDir, $"java{majorVersion}.zip");

        Directory.CreateDirectory(Constants.TempDir);
        Directory.CreateDirectory(javaVersionDir);

        // Проверяем — вдруг уже скачана
        var existingJava = Path.Combine(javaVersionDir, "bin", "javaw.exe");
        if (File.Exists(existingJava))
        {
            Logger.Info(
                $"Java {majorVersion} already installed: {existingJava}",
                "JavaManager");
            OnStatus?.Invoke($"✅ Java {majorVersion} уже установлена!");
            OnProgress?.Invoke(100);
            UpdateConfig(existingJava);
            return existingJava;
        }

        // Получаем URL
        var url = await ResolveDownloadUrlAsync(majorVersion, ct);
        Logger.Info($"Download URL: {url}", "JavaManager");

        // DownloadProgress использует Downloaded и Total — не BytesDownloaded
        var progress = new Progress<DownloadProgress>(p =>
        {
            var mb = p.Total > 0
                ? $"{p.Downloaded / 1024 / 1024} MB / {p.Total / 1024 / 1024} MB"
                : $"{p.Downloaded / 1024 / 1024} MB";
            OnStatus?.Invoke(
                $"Загрузка Java {majorVersion}: {p.Percentage}% ({mb})");
            OnProgress?.Invoke(p.Percentage);
        });

        await _dl.DownloadFileAsync(url, tempFile, progress, ct);

        // Распаковываем
        OnStatus?.Invoke("Распаковка Java...");
        OnProgress?.Invoke(0);

        var extractDir = Path.Combine(
            Constants.TempDir, $"java{majorVersion}_extract");
        if (Directory.Exists(extractDir))
            Directory.Delete(extractDir, true);
        Directory.CreateDirectory(extractDir);

        await Task.Run(() =>
        {
            ZipFile.ExtractToDirectory(
                tempFile, extractDir, overwriteFiles: true);
        }, ct);

        File.Delete(tempFile);

        // Находим папку JDK внутри архива
        var jdkDir = Directory.GetDirectories(extractDir).FirstOrDefault()
                     ?? throw new Exception(
                         "Папка JDK не найдена в архиве");

        // Очищаем целевую директорию и копируем
        if (Directory.Exists(javaVersionDir))
            Directory.Delete(javaVersionDir, true);
        Directory.CreateDirectory(javaVersionDir);

        await Task.Run(
            () => CopyDirectory(jdkDir, javaVersionDir), ct);

        Directory.Delete(extractDir, true);

        var javaExe = Path.Combine(javaVersionDir, "bin", "javaw.exe");
        if (!File.Exists(javaExe))
            throw new Exception(
                $"javaw.exe не найден после установки: {javaExe}");

        Logger.Info(
            $"Java {majorVersion} installed: {javaExe}", "JavaManager");
        OnStatus?.Invoke($"✅ Java {majorVersion} установлена!");
        OnProgress?.Invoke(100);

        UpdateConfig(javaExe);
        return javaExe;
    }

    // ── Главный метод: найти или скачать нужную Java ──────
    public async Task<string> EnsureJavaAsync(
        int               requiredMajor = 21,
        CancellationToken ct            = default)
    {
        Logger.Info($"EnsureJava: need Java {requiredMajor}+", "JavaManager");
        OnStatus?.Invoke($"☕ Поиск Java {requiredMajor}+...");

        var found = FindInstalledJava(requiredMajor);
        if (found != null)
        {
            Logger.Info($"Java found: {found}", "JavaManager");
            OnStatus?.Invoke($"✅ Java найдена: {found}");

            if (string.IsNullOrEmpty(AppConfig.Instance.GlobalJavaPath))
                UpdateConfig(found);

            return found;
        }

        Logger.Info(
            $"Java {requiredMajor} not found, downloading...",
            "JavaManager");
        OnStatus?.Invoke(
            $"☕ Java {requiredMajor} не найдена. Скачиваем...");

        return await DownloadJavaAsync(requiredMajor, ct);
    }

    // ── Определить нужную версию Java по версии MC ────────
    public int GetRequiredJavaVersion(string minecraftVersion)
    {
        try
        {
            var parts = minecraftVersion.Split('.');
            if (parts.Length < 2) return 21;
            if (!int.TryParse(parts[1], out var minor)) return 21;

            return minor switch
            {
                >= 21 => 21,
                >= 17 => 17,
                >= 16 => 11,
                _     => 8
            };
        }
        catch { return 21; }
    }

    // ── Получить путь к Java для конкретной версии MC ─────
    public async Task<string> GetJavaForMinecraftAsync(
        string            minecraftVersion,
        string?           profileJavaPath = null,
        CancellationToken ct              = default)
    {
        // 1. Путь из профиля
        if (!string.IsNullOrEmpty(profileJavaPath) &&
            File.Exists(profileJavaPath))
        {
            Logger.Info(
                $"Using profile Java: {profileJavaPath}", "JavaManager");
            return profileJavaPath;
        }

        // 2. Глобальный путь из настроек
        var g = AppConfig.Instance.GlobalJavaPath;
        if (!string.IsNullOrEmpty(g) && File.Exists(g))
        {
            Logger.Info($"Using global Java: {g}", "JavaManager");
            return g;
        }

        // 3. Определяем нужную версию и ищем/скачиваем
        var required = GetRequiredJavaVersion(minecraftVersion);
        return await EnsureJavaAsync(required, ct);
    }

    // ── Вспомогательные методы ────────────────────────────
    private async Task<string> ResolveDownloadUrlAsync(
        int               majorVersion,
        CancellationToken ct)
    {
        try
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            var apiUrl  = string.Format(AdoptiumApi, majorVersion);
            var request = new HttpRequestMessage(HttpMethod.Head, apiUrl);
            var resp    = await client.SendAsync(request, ct);

            var location = resp.Headers.Location?.ToString();
            if (!string.IsNullOrEmpty(location))
                return location;

            return apiUrl;
        }
        catch (Exception ex)
        {
            Logger.Warning(
                $"Adoptium API error: {ex.Message}, using fallback",
                "JavaManager");

            if (FallbackUrls.TryGetValue(majorVersion, out var fallback))
                return fallback;

            return string.Format(AdoptiumApi, majorVersion);
        }
    }

    private static int ParseMajorVersion(string versionOutput)
    {
        try
        {
            var lines = versionOutput.Split('\n');
            foreach (var line in lines)
            {
                if (!line.Contains("version")) continue;
                var start = line.IndexOf('"');
                var end   = line.LastIndexOf('"');
                if (start < 0 || end <= start) continue;
                var ver = line.Substring(start + 1, end - start - 1);

                // Java 8: "1.8.0_xxx"
                if (ver.StartsWith("1."))
                {
                    var p = ver.Split('.');
                    if (p.Length >= 2 &&
                        int.TryParse(p[1], out var old))
                        return old;
                }
                else
                {
                    // Java 9+: "21.0.5" или "17.0.13+11"
                    var majorStr = ver.Split('.')[0].Split('+')[0];
                    if (int.TryParse(majorStr, out var major))
                        return major;
                }
            }
        }
        catch { }
        return 0;
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(
                file,
                Path.Combine(dest, Path.GetFileName(file)),
                overwrite: true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(
                dir,
                Path.Combine(dest, Path.GetFileName(dir)));
    }

    private static void UpdateConfig(string javaPath)
    {
        try
        {
            AppConfig.Instance.GlobalJavaPath = javaPath;
            AppConfig.Instance.Save();
            Logger.Info(
                $"Config updated: GlobalJavaPath = {javaPath}",
                "JavaManager");
        }
        catch (Exception ex)
        {
            Logger.Warning(
                $"Failed to save config: {ex.Message}",
                "JavaManager");
        }
    }
}