// src/HuntLoader/Core/Updater.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace HuntLoader.Core;

public class UpdateInfo
{
    public string Version     { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string ChangeLog   { get; set; } = "";
    public bool   IsRequired  { get; set; }
}

public class Updater
{
    private readonly HttpClient _http = new();

    private const string GithubApi =
        "https://api.github.com/repos/turexhunt/huntlauncher/releases/latest";

    public async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("User-Agent", "HuntLoader");

            var json    = await _http.GetStringAsync(GithubApi);
            var obj     = JObject.Parse(json);
            var tag     = obj["tag_name"]?.ToString()?.TrimStart('v') ?? "";
            var current = Constants.LauncherVersion; // теперь "2.0.0"

            Logger.Info(
                $"Текущая версия: {current} | GitHub версия: {tag}",
                "Updater");

            // IsNewer вернёт false если tag == "2.0.0" и current == "2.0.0"
            // Цикл обновления невозможен — версии совпадают
            if (IsNewer(tag, current))
            {
                var downloadUrl =
                    obj["assets"]?[0]?["browser_download_url"]?.ToString()
                    ?? "";

                if (string.IsNullOrEmpty(downloadUrl))
                    downloadUrl = obj["zipball_url"]?.ToString() ?? "";

                return new UpdateInfo
                {
                    Version     = tag,
                    DownloadUrl = downloadUrl,
                    ChangeLog   = obj["body"]?.ToString() ?? "Нет описания"
                };
            }

            Logger.Info("Обновлений нет", "Updater");
        }
        catch (Exception ex)
        {
            Logger.Warning(
                $"Проверка обновлений не удалась: {ex.Message}",
                "Updater");
        }
        return null;
    }

    public async Task DownloadAndInstallAsync(
        UpdateInfo         info,
        IProgress<double>? progress = null,
        CancellationToken  ct       = default)
    {
        var tempFile   = Path.Combine(
            Constants.TempDir, "HuntLoader_update.exe");
        var currentExe = Process.GetCurrentProcess().MainModule!.FileName;

        Logger.Info($"Скачиваю обновление {info.Version}...", "Updater");

        using var response = await _http.GetAsync(
            info.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var file   = File.Create(tempFile);

        var  buffer   = new byte[8192];
        long received = 0;
        int  read;

        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;
            if (total > 0)
                progress?.Report((double)received / total * 100);
        }

        // Закрываем файл перед запуском bat
        await file.FlushAsync(ct);

        Logger.Info("Скачано! Запускаю установку...", "Updater");

        var batContent =
            "@echo off\r\n"                                        +
            "timeout /t 2 /nobreak >nul\r\n"                      +
            $"copy /Y \"{tempFile}\" \"{currentExe}\"\r\n"        +
            $"start \"\" \"{currentExe}\"\r\n"                    +
            $"del \"{tempFile}\"\r\n"                              +
            "del \"%~f0\"\r\n";

        var batPath = Path.Combine(Constants.TempDir, "update.bat");
        await File.WriteAllTextAsync(batPath, batContent, ct);

        Process.Start(new ProcessStartInfo
        {
            FileName        = batPath,
            CreateNoWindow  = true,
            UseShellExecute = true,
            WindowStyle     = ProcessWindowStyle.Hidden
        });

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
            System.Windows.Application.Current.Shutdown());
    }

    // Возвращает true ТОЛЬКО если remote СТРОГО больше local
    // "2.0.0" vs "2.0.0" → false — цикла нет
    // "2.0.1" vs "2.0.0" → true  — есть обновление
    private static bool IsNewer(string remote, string local)
    {
        if (!Version.TryParse(remote, out var r)) return false;
        if (!Version.TryParse(local,  out var l)) return false;
        return r > l;
    }
}