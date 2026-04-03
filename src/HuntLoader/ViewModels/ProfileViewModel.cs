// src/HuntLoader/ViewModels/ProfileViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HuntLoader.Core;
using HuntLoader.Models;
using HuntLoader.Services;

namespace HuntLoader.ViewModels;

public class ProfileViewModel : BaseViewModel
{
    private readonly MainViewModel _main;
    private CancellationTokenSource? _optimizeCts;

    // ── Список профилей ───────────────────────────────────
    public ObservableCollection<MinecraftProfile> Profiles        { get; } = new();
    public ObservableCollection<string>           AvailableVersions { get; } = new();

    // ── Выбранный профиль ─────────────────────────────────
    private MinecraftProfile? _selected;
    public MinecraftProfile? Selected
    {
        get => _selected;
        set
        {
            Set(ref _selected, value);
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanOptimize));
        }
    }

    public bool HasSelection => Selected != null;

    // Можно оптимизировать только Fabric/Forge/Quilt/NeoForge
    public bool CanOptimize =>
        HasSelection &&
        !IsOptimizing &&
        Selected?.ModLoader != ModLoader.Vanilla;

    // ── Поля нового профиля ───────────────────────────────
    private string    _newName          = "Мой профиль";
    private string    _newVersion       = "1.21.4";
    private ModLoader _newLoader        = ModLoader.Vanilla;
    private string    _newLoaderVersion = "";
    private string    _newColor         = "#8B6FD4";
    private int       _newMemMin        = 512;
    private int       _newMemMax        = 2048;
    private bool      _installOptimizations = true;

    public string NewName
    {
        get => _newName;
        set => Set(ref _newName, value);
    }

    public string NewVersion
    {
        get => _newVersion;
        set => Set(ref _newVersion, value);
    }

    public ModLoader NewLoader
    {
        get => _newLoader;
        set
        {
            Set(ref _newLoader, value);
            OnPropertyChanged(nameof(IsLoaderSelected));
        }
    }

    public string NewLoaderVersion
    {
        get => _newLoaderVersion;
        set => Set(ref _newLoaderVersion, value);
    }

    public string NewColor
    {
        get => _newColor;
        set => Set(ref _newColor, value);
    }

    public int NewMemMin
    {
        get => _newMemMin;
        set
        {
            Set(ref _newMemMin, value);
            OnPropertyChanged(nameof(NewMemLabel));
        }
    }

    public int NewMemMax
    {
        get => _newMemMax;
        set
        {
            Set(ref _newMemMax, value);
            OnPropertyChanged(nameof(NewMemLabel));
        }
    }

    public bool InstallOptimizations
    {
        get => _installOptimizations;
        set => Set(ref _installOptimizations, value);
    }

    // Показывать чекбокс только если лоадер не Vanilla
    public bool IsLoaderSelected => NewLoader != ModLoader.Vanilla;

    public string NewMemLabel => $"RAM:  {NewMemMin} MB  —  {NewMemMax} MB";

    public IEnumerable<ModLoader> ModLoaders => Enum.GetValues<ModLoader>();

    // ── Оптимизация ───────────────────────────────────────
    private bool   _isOptimizing;
    private string _optStatusText = "";
    private double _optProgress;

    public bool IsOptimizing
    {
        get => _isOptimizing;
        set
        {
            Set(ref _isOptimizing, value);
            OnPropertyChanged(nameof(CanOptimize));
        }
    }

    public string OptStatusText
    {
        get => _optStatusText;
        set => Set(ref _optStatusText, value);
    }

    public double OptProgress
    {
        get => _optProgress;
        set => Set(ref _optProgress, value);
    }

    // ── Статус ───────────────────────────────────────────
    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    // ── Команды ──────────────────────────────────────────
    public AsyncRelayCommand  CreateCommand       { get; }
    public RelayCommand       DeleteCommand       { get; }
    public RelayCommand       DuplicateCommand    { get; }
    public RelayCommand       SetActiveCommand    { get; }
    public RelayCommand       OpenFolderCommand   { get; }
    public AsyncRelayCommand  OptimizeCommand     { get; }
    public AsyncRelayCommand  LoadVersionsCommand { get; }

    public ProfileViewModel(MainViewModel main)
    {
        _main = main;

        CreateCommand     = new AsyncRelayCommand(CreateAsync);
        DeleteCommand     = new RelayCommand(Delete,     () => HasSelection);
        DuplicateCommand  = new RelayCommand(Duplicate,  () => HasSelection);
        SetActiveCommand  = new RelayCommand(SetActive,  () => HasSelection);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => HasSelection);
        OptimizeCommand   = new AsyncRelayCommand(OptimizeAsync);
        LoadVersionsCommand = new AsyncRelayCommand(LoadVersionsAsync);

        // Подписываемся на прогресс оптимизации
        _main.ProfileManager.OnOptimizationProgress += (status, percent) =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                OptStatusText = status;
                OptProgress   = percent;
                StatusText    = status;
            });
        };

        Reload();
        _main.ProfileManager.ProfilesChanged += Reload;
        _ = LoadVersionsAsync();
    }

    // ── Перезагрузка ─────────────────────────────────────
    private void Reload()
    {
        Profiles.Clear();
        foreach (var p in _main.ProfileManager.Profiles)
            Profiles.Add(p);
    }

    // ── Создать профиль ───────────────────────────────────
    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            StatusText = "❌ Введи название профиля";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewVersion))
        {
            StatusText = "❌ Выбери версию Minecraft";
            return;
        }

        var installOpt = InstallOptimizations && NewLoader != ModLoader.Vanilla;

        IsOptimizing = installOpt;
        StatusText   = installOpt
            ? "⬇ Создаю профиль и устанавливаю моды..."
            : "⬇ Создаю профиль...";

        _optimizeCts = new CancellationTokenSource();

        try
        {
            var profile = await _main.ProfileManager.CreateWithOptimizationAsync(
                NewName, NewVersion, NewLoader, NewColor,
                installOptimizations: installOpt,
                ct: _optimizeCts.Token);

            profile.MemoryMin        = NewMemMin;
            profile.MemoryMax        = NewMemMax;
            profile.ModLoaderVersion = NewLoaderVersion;
            _main.ProfileManager.Save(profile);

            if (_main.ProfileManager.Profiles.Count == 1)
                _main.ProfileManager.SetActive(profile.Id);

            StatusText   = $"✅ Профиль '{NewName}' создан!";
            OptProgress  = 0;
            NewName      = "Мой профиль";

            Logger.Info($"Created profile: {profile.Name}", "ProfileVM");
        }
        catch (OperationCanceledException)
        {
            StatusText = "⚠ Отменено";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ {ex.Message}";
            Logger.Error(ex, "ProfileVM");
        }
        finally
        {
            IsOptimizing = false;
        }
    }

    // ── Оптимизировать существующий профиль ───────────────
    private async Task OptimizeAsync()
    {
        if (Selected == null) return;
        if (Selected.ModLoader == ModLoader.Vanilla)
        {
            StatusText = "⚠ Vanilla не поддерживает моды";
            return;
        }

        IsOptimizing  = true;
        OptProgress   = 0;
        OptStatusText = "⬇ Начинаю установку модов...";
        StatusText    = $"⚡ Оптимизирую {Selected.Name}...";

        _optimizeCts = new CancellationTokenSource();

        try
        {
            await _main.ProfileManager.OptimizeProfileAsync(
                Selected.Id, _optimizeCts.Token);

            StatusText = $"✅ {Selected.Name} оптимизирован!";
        }
        catch (OperationCanceledException)
        {
            StatusText = "⚠ Отменено";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ {ex.Message}";
            Logger.Error(ex, "ProfileVM");
        }
        finally
        {
            IsOptimizing = false;
            OptProgress  = 0;
        }
    }

    // ── Удалить ──────────────────────────────────────────
    private void Delete()
    {
        if (Selected == null) return;
        var name = Selected.Name;
        try
        {
            _main.ProfileManager.Delete(Selected.Id);
            Selected   = null;
            StatusText = $"✅ Профиль '{name}' удалён";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ {ex.Message}";
            Logger.Error(ex, "ProfileVM");
        }
    }

    // ── Дублировать ──────────────────────────────────────
    private void Duplicate()
    {
        if (Selected == null) return;
        try
        {
            var copy = _main.ProfileManager.Duplicate(Selected.Id);
            StatusText = $"✅ Создана копия: {copy.Name}";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ {ex.Message}";
            Logger.Error(ex, "ProfileVM");
        }
    }

    // ── Сделать активным ─────────────────────────────────
    private void SetActive()
    {
        if (Selected == null) return;
        _main.ProfileManager.SetActive(Selected.Id);
        StatusText = $"✅ Активный профиль: {Selected.Name}";
        OnPropertyChanged(nameof(Profiles));
    }

    // ── Открыть папку ────────────────────────────────────
    private void OpenFolder()
    {
        if (Selected == null) return;
        try
        {
            Selected.EnsureDirectories();
            Process.Start("explorer.exe", Selected.ProfileDir);
        }
        catch (Exception ex)
        {
            StatusText = $"❌ {ex.Message}";
            Logger.Error(ex, "ProfileVM");
        }
    }

    // ── Загрузить версии ─────────────────────────────────
    private async Task LoadVersionsAsync()
    {
        try
        {
            var versions = await _main.VersionManager.GetVersionsAsync(
                AppConfig.Instance.ShowSnapshots,
                AppConfig.Instance.ShowOldVersions);

            AvailableVersions.Clear();

            foreach (var fv in Constants.FeaturedVersions)
                AvailableVersions.Add(fv);

            foreach (var v in versions)
            {
                if (!AvailableVersions.Contains(v.Id))
                    AvailableVersions.Add(v.Id);
            }

            if (AvailableVersions.Contains("1.21.4"))
                NewVersion = "1.21.4";
            else if (AvailableVersions.Count > 0)
                NewVersion = AvailableVersions[0];
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ProfileVM");
            AvailableVersions.Clear();
            foreach (var v in Constants.FeaturedVersions)
                AvailableVersions.Add(v);
            NewVersion = "1.21.4";
            StatusText = "⚠ Нет интернета, версии загружены локально";
        }
    }
}