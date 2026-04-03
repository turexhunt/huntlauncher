// src/HuntLoader/ViewModels/ModsViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HuntLoader.Core;
using HuntLoader.Models;
using HuntLoader.Services;

namespace HuntLoader.ViewModels;

public record ModInfo(string FileName, string FullPath, long SizeBytes)
{
    public string SizeLabel =>
        SizeBytes > 1024 * 1024
            ? $"{SizeBytes / (1024.0 * 1024):F1} MB"
            : $"{SizeBytes / 1024.0:F1} KB";
    public bool   IsEnabled   => !FileName.EndsWith(".disabled");
    public string DisplayName => FileName.Replace(".disabled", "").Replace(".jar", "");
}

public class ModsViewModel : BaseViewModel
{
    private readonly MainViewModel _main;
    private readonly ModManager    _modManager = new();
    private CancellationTokenSource? _searchCts;

    // ── Установленные моды ────────────────────────────────
    public ObservableCollection<ModInfo>    Mods          { get; } = new();

    // ── Результаты поиска (унифицированные) ───────────────
    public ObservableCollection<UnifiedMod> SearchResults { get; } = new();

    // ── Состояния ─────────────────────────────────────────
    public bool IsModsEmpty   => Mods.Count == 0;
    public bool HasResults    => SearchResults.Count > 0;
    public bool IsSearching   => _isSearching;
    public bool IsDownloading => _isDownloading;

    private bool _isSearching;
    private bool _isDownloading;

    // ── Поиск ─────────────────────────────────────────────
    private string _searchQuery = "";
    public string SearchQuery
    {
        get => _searchQuery;
        set { Set(ref _searchQuery, value); _ = AutoSearchAsync(); }
    }

    private string _selectedLoader = "fabric";
    public string SelectedLoader
    {
        get => _selectedLoader;
        set { Set(ref _selectedLoader, value); _ = SearchAsync(); }
    }

    // Источник: "both" | "modrinth" | "curseforge"
    private string _selectedSource = "both";
    public string SelectedSource
    {
        get => _selectedSource;
        set { Set(ref _selectedSource, value); _ = SearchAsync(); }
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    private double _downloadProgress;
    public double DownloadProgress
    {
        get => _downloadProgress;
        set => Set(ref _downloadProgress, value);
    }

    private MinecraftProfile? _currentProfile;
    public MinecraftProfile? CurrentProfile
    {
        get => _currentProfile;
        set { Set(ref _currentProfile, value); LoadMods(); }
    }

    // ── Вкладки ───────────────────────────────────────────
    // 0 = установленные, 1 = Modrinth, 2 = CurseForge
    private int _activeTab;
    public int ActiveTab
    {
        get => _activeTab;
        set
        {
            if (Set(ref _activeTab, value))
            {
                // Обновляем источник при смене вкладки
                _selectedSource = value switch
                {
                    1 => "modrinth",
                    2 => "curseforge",
                    _ => "both"
                };
                // Сброс результатов при смене вкладки
                SearchResults.Clear();
                SearchQuery = "";
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(SelectedSource));
            }
        }
    }

    // ── Комбобоксы ────────────────────────────────────────
    public string[] Loaders { get; } =
        { "fabric", "forge", "quilt", "neoforge" };

    public string[] Sources { get; } =
        { "both", "modrinth", "curseforge" };

    public string[] SourceLabels { get; } =
        { "Все источники", "Modrinth", "CurseForge" };

    // ── Команды ───────────────────────────────────────────
    public RelayCommand               OpenFolderCommand        { get; }
    public RelayCommand               RefreshCommand           { get; }
    public RelayCommand               OpenResourcePacks        { get; }
    public RelayCommand               OpenShaderPacks          { get; }
    public RelayCommand               OpenScreenshots          { get; }

    // ✅ ИСПРАВЛЕНО: теперь принимает параметр string
    public RelayCommand<string>       SwitchToSearchCommand    { get; }
    public RelayCommand               SwitchToInstalledCommand { get; }

    public AsyncRelayCommand          SearchCommand            { get; }
    public AsyncRelayCommand<UnifiedMod> DownloadModCommand    { get; }
    public RelayCommand<ModInfo>      ToggleModCommand         { get; }
    public RelayCommand<ModInfo>      DeleteModCommand         { get; }

    public ModsViewModel(MainViewModel main)
    {
        _main = main;

        _modManager.OnProgress += p =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                StatusText       = p.Status;
                DownloadProgress = p.Percentage;
            });
        };

        OpenFolderCommand        = new RelayCommand(() => OpenDir(CurrentProfile?.ModsDir));
        RefreshCommand           = new RelayCommand(LoadMods);
        OpenResourcePacks        = new RelayCommand(() => OpenDir(CurrentProfile?.ResourcePacksDir));
        OpenShaderPacks          = new RelayCommand(() => OpenDir(CurrentProfile?.ShaderPacksDir));
        OpenScreenshots          = new RelayCommand(() => OpenDir(CurrentProfile?.ScreenshotsDir));
        SwitchToInstalledCommand = new RelayCommand(() => ActiveTab = 0);

        // ✅ ИСПРАВЛЕНО: корректно определяем вкладку по параметру
        SwitchToSearchCommand = new RelayCommand<string>(source =>
        {
            ActiveTab = source?.ToLower() switch
            {
                "curseforge" => 2,
                "modrinth"   => 1,
                _            => 1   // по умолчанию Modrinth
            };
        });

        SearchCommand      = new AsyncRelayCommand(SearchAsync);
        DownloadModCommand = new AsyncRelayCommand<UnifiedMod>(DownloadUnifiedModAsync);
        ToggleModCommand   = new RelayCommand<ModInfo>(ToggleMod);
        DeleteModCommand   = new RelayCommand<ModInfo>(DeleteMod);

        _main.ProfileManager.ProfilesChanged += () =>
            CurrentProfile = _main.ProfileManager.ActiveProfile;

        CurrentProfile = _main.ProfileManager.ActiveProfile;
    }

    // ── Автопоиск с дебаунсом ─────────────────────────────
    private async Task AutoSearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token  = _searchCts.Token;
        try
        {
            await Task.Delay(400, token);
            if (!token.IsCancellationRequested)
                await SearchAsync();
        }
        catch (TaskCanceledException) { }
    }

    // ── Поиск ─────────────────────────────────────────────
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults.Clear();
            OnPropertyChanged(nameof(HasResults));
            return;
        }

        _isSearching = true;
        OnPropertyChanged(nameof(IsSearching));

        // Определяем текущий источник по активной вкладке
        var source = ActiveTab switch
        {
            1 => "modrinth",
            2 => "curseforge",
            _ => _selectedSource
        };

        var sourceLabel = source switch
        {
            "modrinth"   => "Modrinth",
            "curseforge" => "CurseForge",
            _            => "Все источники"
        };

        StatusText = $"Поиск в {sourceLabel}...";

        try
        {
            var gameVersion = CurrentProfile?.GameVersion;
            var results = await _modManager.SearchUnifiedAsync(
                SearchQuery,
                gameVersion,
                SelectedLoader,
                source,
                limit: 24);

            SearchResults.Clear();
            foreach (var mod in results)
                SearchResults.Add(mod);

            var cfCount = results.Count(r => r.Source == "curseforge");
            var mrCount = results.Count(r => r.Source == "modrinth");

            StatusText = source == "both"
                ? $"Найдено: {results.Count} (Modrinth: {mrCount}, CurseForge: {cfCount})"
                : $"Найдено: {results.Count}";

            OnPropertyChanged(nameof(HasResults));
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка поиска: {ex.Message}";
            Logger.Error(ex, "ModsViewModel");
        }
        finally
        {
            _isSearching = false;
            OnPropertyChanged(nameof(IsSearching));
        }
    }

    // ── Скачивание ────────────────────────────────────────
    private async Task DownloadUnifiedModAsync(UnifiedMod? mod)
    {
        if (mod == null || CurrentProfile == null) return;

        _isDownloading   = true;
        DownloadProgress = 0;
        OnPropertyChanged(nameof(IsDownloading));
        StatusText = $"Скачивание {mod.Title} [{mod.SourceLabel}]...";

        try
        {
            await _modManager.DownloadUnifiedModAsync(mod, CurrentProfile);
            StatusText = $"✅ {mod.Title} установлен";
            LoadMods();
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Ошибка: {ex.Message}";
            Logger.Error(ex, "ModsViewModel");
        }
        finally
        {
            _isDownloading   = false;
            DownloadProgress = 0;
            OnPropertyChanged(nameof(IsDownloading));
        }
    }

    // ── Управление модами ─────────────────────────────────
    private void ToggleMod(ModInfo? mod)
    {
        if (mod == null) return;
        try
        {
            if (mod.IsEnabled)
            {
                File.Move(mod.FullPath, mod.FullPath + ".disabled");
                StatusText = $"⛔ {mod.DisplayName} отключён";
            }
            else
            {
                var newPath = mod.FullPath.Replace(".disabled", "");
                File.Move(mod.FullPath, newPath);
                StatusText = $"✅ {mod.DisplayName} включён";
            }
            LoadMods();
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка: {ex.Message}";
        }
    }

    private void DeleteMod(ModInfo? mod)
    {
        if (mod == null) return;
        try
        {
            File.Delete(mod.FullPath);
            StatusText = $"🗑 {mod.DisplayName} удалён";
            LoadMods();
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка: {ex.Message}";
        }
    }

    // ── Загрузка установленных ────────────────────────────
    public void LoadMods()
    {
        Mods.Clear();
        if (CurrentProfile == null) return;

        var dir = CurrentProfile.ModsDir;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            OnPropertyChanged(nameof(IsModsEmpty));
            return;
        }

        var files = Directory.GetFiles(dir, "*.jar")
            .Concat(Directory.GetFiles(dir, "*.jar.disabled"))
            .ToList();

        foreach (var file in files)
        {
            var info = new FileInfo(file);
            Mods.Add(new ModInfo(info.Name, file, info.Length));
        }

        OnPropertyChanged(nameof(IsModsEmpty));
        StatusText = $"Модов: {Mods.Count}";
    }

    private static void OpenDir(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        Directory.CreateDirectory(path);
        Process.Start("explorer.exe", path);
    }
}