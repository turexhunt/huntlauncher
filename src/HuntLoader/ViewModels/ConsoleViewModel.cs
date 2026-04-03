// src/HuntLoader/ViewModels/ConsoleViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using HuntLoader.Core;

namespace HuntLoader.ViewModels;

public class ConsoleViewModel : BaseViewModel
{
    private readonly MainViewModel _main;
    private readonly StringBuilder _fullLog = new();

    public ObservableCollection<ConsoleLine> Lines { get; } = new();

    private string _filter = "";
    public string Filter
    {
        get => _filter;
        set { Set(ref _filter, value); ApplyFilter(); }
    }

    private bool _autoScroll = true;
    public bool AutoScroll
    {
        get => _autoScroll;
        set => Set(ref _autoScroll, value);
    }

    public RelayCommand ClearCommand    { get; }
    public RelayCommand CopyAllCommand  { get; }
    public RelayCommand SaveLogCommand  { get; }
    public RelayCommand KillGameCommand { get; }

    public ConsoleViewModel(MainViewModel main)
    {
        _main = main;
        ClearCommand    = new RelayCommand(Clear);
        CopyAllCommand  = new RelayCommand(CopyAll);
        SaveLogCommand  = new RelayCommand(SaveLog);
        KillGameCommand = new RelayCommand(
            () => _main.LaunchService.Kill(),
            () => _main.LaunchService.IsRunning);

        Logger.OnLog += entry =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                AppendLine($"[{entry.Level}] {entry.Message}",
                    entry.Level == LogLevel.Error || entry.Level == LogLevel.Fatal
                        ? LineType.Error : LineType.Info));
        };
    }

    public void AppendLine(string text, LineType type = LineType.Info)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            _fullLog.AppendLine(text);
            if (string.IsNullOrEmpty(_filter) ||
                text.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            {
                Lines.Add(new ConsoleLine(text, type));
                if (Lines.Count > 5000) Lines.RemoveAt(0);
            }
        });
    }

    private void ApplyFilter()
    {
        Lines.Clear();
        foreach (var line in _fullLog.ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(l => string.IsNullOrEmpty(_filter) ||
                        l.Contains(_filter, StringComparison.OrdinalIgnoreCase)))
        {
            Lines.Add(new ConsoleLine(line, LineType.Info));
        }
    }

    private void Clear()
    {
        Lines.Clear();
        _fullLog.Clear();
    }

    private void CopyAll() =>
        System.Windows.Clipboard.SetText(_fullLog.ToString());

    private void SaveLog()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "Text Files|*.txt|Log Files|*.log",
            FileName = $"hunt_session_{DateTime.Now:yyyy-MM-dd_HH-mm}.txt"
        };
        if (dlg.ShowDialog() == true)
            File.WriteAllText(dlg.FileName, _fullLog.ToString());
    }
}

public enum LineType { Info, Warning, Error, System }

public record ConsoleLine(string Text, LineType Type)
{
    public string Prefix => Type switch
    {
        LineType.Warning => "⚠ ",
        LineType.Error   => "✖ ",
        LineType.System  => "► ",
        _                => "  "
    };
}