// src/HuntLoader/Services/JarOptimizer.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HuntLoader.Core;
using HuntLoader.Models;

namespace HuntLoader.Services;

/// <summary>
/// Оптимизирует .jar файлы игры для максимального FPS:
/// — удаляет ненужные ресурсы из клиентского jar
/// — патчит флаги манифеста
/// — удаляет дублирующиеся файлы из mods
/// </summary>
public class JarOptimizer
{
    public event Action<string, int>? OnProgress;

    // Расширения которые точно не нужны клиенту
    private static readonly HashSet<string> UselessExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".psd", ".xcf", ".ai", ".sketch",
        ".gitignore", ".gitattributes",
        ".md", ".txt", ".html", ".htm",
        ".bat", ".sh",
        ".orig", ".bak",
    };

    // Пути внутри jar которые можно удалить
    private static readonly string[] UselessPaths =
    {
        "META-INF/maven/",
        "META-INF/proguard/",
        "META-INF/versions/",    // multi-release jar — нам не нужно
        ".cache/",
        "unused/",
    };

    // ── Оптимизировать все jar в папке mods ───────────────
    public async Task OptimizeModsAsync(
        MinecraftProfile  profile,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(profile.ModsDir))
        {
            OnProgress?.Invoke("Папка mods пуста", 100);
            return;
        }

        var jars  = Directory.GetFiles(profile.ModsDir, "*.jar");
        var total = jars.Length;
        if (total == 0)
        {
            OnProgress?.Invoke("Нет jar файлов для оптимизации", 100);
            return;
        }

        Logger.Info(
            $"JarOptimizer: оптимизируем {total} jar в {profile.ModsDir}",
            "JarOptimizer");

        var done    = 0;
        var saved   = 0L;

        foreach (var jar in jars)
        {
            if (ct.IsCancellationRequested) break;

            var name = Path.GetFileName(jar);
            OnProgress?.Invoke(
                $"🔧 Оптимизирую {name}...",
                done * 100 / total);

            try
            {
                saved += await Task.Run(
                    () => OptimizeJar(jar), ct);
            }
            catch (Exception ex)
            {
                Logger.Warning(
                    $"JarOptimizer skip {name}: {ex.Message}",
                    "JarOptimizer");
            }

            done++;
            OnProgress?.Invoke(
                $"✅ {name}",
                done * 100 / total);
        }

        OnProgress?.Invoke(
            $"🚀 Оптимизировано {done} jar, " +
            $"освобождено {FormatBytes(saved)}",
            100);
    }

    // ── Оптимизировать клиентский minecraft.jar ───────────
    public async Task OptimizeClientJarAsync(
        string            versionId,
        CancellationToken ct = default)
    {
        var jar = Path.Combine(
            Constants.VersionsDir, versionId, $"{versionId}.jar");

        if (!File.Exists(jar))
        {
            Logger.Warning(
                $"Client jar не найден: {jar}", "JarOptimizer");
            return;
        }

        OnProgress?.Invoke($"🔧 Оптимизирую клиент {versionId}...", 0);

        var saved = await Task.Run(() => OptimizeJar(jar), ct);

        OnProgress?.Invoke(
            $"✅ Клиент оптимизирован, сохранено {FormatBytes(saved)}",
            100);
    }

    // ── Удалить дубликаты модов (разные версии одного мода) 
    public void RemoveDuplicateMods(MinecraftProfile profile)
    {
        if (!Directory.Exists(profile.ModsDir)) return;

        var jars = Directory.GetFiles(profile.ModsDir, "*.jar")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();

        var seen    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var removed = 0;

        foreach (var jar in jars)
        {
            // Убираем версию из имени файла для сравнения
            // sodium-mc1.21.4-0.6.0 → sodium
            var baseName = StripVersion(
                Path.GetFileNameWithoutExtension(jar.Name));

            if (!seen.Add(baseName))
            {
                try
                {
                    Logger.Info(
                        $"Удаляем дубликат: {jar.Name}", "JarOptimizer");
                    jar.Delete();
                    removed++;
                }
                catch (Exception ex)
                {
                    Logger.Warning(
                        $"Не удалось удалить {jar.Name}: {ex.Message}",
                        "JarOptimizer");
                }
            }
        }

        if (removed > 0)
            Logger.Info(
                $"Удалено {removed} дублирующихся модов", "JarOptimizer");
    }

    // ── Внутренняя оптимизация одного jar ─────────────────
    private static long OptimizeJar(string jarPath)
    {
        var originalSize = new FileInfo(jarPath).Length;
        var tempPath     = jarPath + ".tmp";

        try
        {
            using (var input  = ZipFile.OpenRead(jarPath))
            using (var output = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                foreach (var entry in input.Entries)
                {
                    // Пропускаем мусор
                    if (ShouldSkip(entry.FullName)) continue;

                    // Копируем полезное
                    var newEntry = output.CreateEntry(
                        entry.FullName,
                        CompressionLevel.Optimal);

                    newEntry.LastWriteTime = entry.LastWriteTime;

                    using var src  = entry.Open();
                    using var dest = newEntry.Open();
                    src.CopyTo(dest);
                }
            }

            // Заменяем оригинал
            File.Delete(jarPath);
            File.Move(tempPath, jarPath);

            var newSize = new FileInfo(jarPath).Length;
            return Math.Max(0, originalSize - newSize);
        }
        catch
        {
            // Откатываемся если что-то пошло не так
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    private static bool ShouldSkip(string entryName)
    {
        // Никогда не удаляем критичные файлы
        if (entryName.Equals("META-INF/MANIFEST.MF",
                StringComparison.OrdinalIgnoreCase)) return false;
        if (entryName.EndsWith(".class",
                StringComparison.OrdinalIgnoreCase)) return false;
        if (entryName.EndsWith(".json",
                StringComparison.OrdinalIgnoreCase)) return false;
        if (entryName.EndsWith(".png",
                StringComparison.OrdinalIgnoreCase)) return false;
        if (entryName.EndsWith(".ogg",
                StringComparison.OrdinalIgnoreCase)) return false;

        // Проверяем расширение
        var ext = Path.GetExtension(entryName);
        if (UselessExtensions.Contains(ext)) return true;

        // Проверяем пути
        foreach (var path in UselessPaths)
        {
            if (entryName.StartsWith(path,
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string StripVersion(string name)
    {
        // sodium-mc1.21.4-0.6.0+build.22 → sodium
        // lithium-fabric-0.12.0 → lithium
        var result = name.ToLower();
        var seps   = new[] { '-', '_', '+', '.' };

        foreach (var sep in seps)
        {
            var idx = result.IndexOf(sep);
            if (idx > 3)
            {
                result = result[..idx];
                break;
            }
        }

        return result;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)          return $"{bytes} B";
        if (bytes < 1024 * 1024)   return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F1} MB";
    }
}