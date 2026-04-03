// src/HuntLoader/Core/Logger.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HuntLoader.Core;

public enum LogLevel { Debug, Info, Warning, Error, Fatal }

public static class Logger
{
    private static string _logFile = "";
    private static readonly object _lock = new();
    private static readonly List<LogEntry> _buffer = new();

    public static event Action<LogEntry>? OnLog;

    public record LogEntry(DateTime Time, LogLevel Level, string Message, string? Source);

    public static void Init()
    {
        Directory.CreateDirectory(Constants.LogsDir);
        var date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _logFile = Path.Combine(Constants.LogsDir, $"hunt_{date}.log");

        var logs = Directory.GetFiles(Constants.LogsDir, "*.log")
                            .OrderByDescending(f => f).Skip(10);
        foreach (var old in logs) File.Delete(old);

        Info("Logger initialized", "Logger");
        Info($"Hunt Loader {Constants.LauncherVersion}", "Logger");
    }

    public static void Debug(string msg,   string? src = null) => Write(LogLevel.Debug,   msg, src);
    public static void Info(string msg,    string? src = null) => Write(LogLevel.Info,    msg, src);
    public static void Warning(string msg, string? src = null) => Write(LogLevel.Warning, msg, src);
    public static void Error(string msg,   string? src = null) => Write(LogLevel.Error,   msg, src);
    public static void Fatal(string msg,   string? src = null) => Write(LogLevel.Fatal,   msg, src);

    public static void Error(Exception ex, string? src = null) =>
        Write(LogLevel.Error, $"{ex.Message}\n{ex.StackTrace}", src);

    private static void Write(LogLevel level, string message, string? source)
    {
        var entry = new LogEntry(DateTime.Now, level, message, source);
        lock (_lock)
        {
            _buffer.Add(entry);
            var line = Format(entry);
            try { File.AppendAllText(_logFile, line + Environment.NewLine); }
            catch { }
        }
        OnLog?.Invoke(entry);
    }

    private static string Format(LogEntry e) =>
        $"[{e.Time:HH:mm:ss}] [{e.Level,7}] [{e.Source ?? "App"}] {e.Message}";

    public static IReadOnlyList<LogEntry> GetBuffer() => _buffer.AsReadOnly();
}