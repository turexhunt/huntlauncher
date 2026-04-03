// src/HuntLoader/Services/DownloadService.cs
using System.Net.Http;
using HuntLoader.Core;

namespace HuntLoader.Services;

public class DownloadProgress
{
    public string FileName     { get; set; } = "";
    public long   Downloaded   { get; set; }
    public long   Total        { get; set; }
    public int    Percentage   => Total > 0 ? (int)(Downloaded * 100 / Total) : 0;
    public int    FilesTotal   { get; set; }
    public int    FilesCurrent { get; set; }
    public string Speed        { get; set; } = "";
    public string Status       { get; set; } = "";
}

public class DownloadService
{
    private readonly HttpClient    _http;
    private readonly SemaphoreSlim _semaphore;

    // ❌ Убрали: public event Action<DownloadProgress>? OnProgress;
    // Событие объявлялось но нигде не вызывалось — CS0067

    public DownloadService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent",
            $"HuntLoader/{Constants.LauncherVersion}");
        _http.Timeout  = TimeSpan.FromMinutes(30);
        _semaphore     = new SemaphoreSlim(
            AppConfig.Instance.ConcurrentDownloads);
    }

    public async Task DownloadFileAsync(
        string url,
        string destPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        await _semaphore.WaitAsync(ct);
        try
        {
            var startTime  = DateTime.Now;
            var fileName   = Path.GetFileName(destPath);
            var downloaded = 0L;

            using var response = await _http.GetAsync(
                url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var file   = File.Create(destPath);

            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                downloaded += read;

                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                var speed   = elapsed > 0
                    ? FormatBytes((long)(downloaded / elapsed)) + "/s"
                    : "";

                progress?.Report(new DownloadProgress
                {
                    FileName   = fileName,
                    Downloaded = downloaded,
                    Total      = total,
                    Speed      = speed,
                    Status     = $"Загрузка {fileName}..."
                });
            }

            Logger.Debug($"Downloaded: {fileName}", "DownloadService");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DownloadManyAsync(
        IEnumerable<(string Url, string Path)> files,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var list    = files.ToList();
        var total   = list.Count;
        var current = 0;

        var tasks = list.Select(async file =>
        {
            if (File.Exists(file.Path))
            {
                Interlocked.Increment(ref current);
                return;
            }

            await DownloadFileAsync(file.Url, file.Path, null, ct);

            var cur = Interlocked.Increment(ref current);
            progress?.Report(new DownloadProgress
            {
                FilesCurrent = cur,
                FilesTotal   = total,
                FileName     = Path.GetFileName(file.Path),
                Status       = $"[{cur}/{total}] {Path.GetFileName(file.Path)}"
            });
        });

        await Task.WhenAll(tasks);
    }

    public static string FormatBytes(long bytes) => bytes switch
    {
        < 1024               => $"{bytes} B",
        < 1024 * 1024        => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _                    => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}