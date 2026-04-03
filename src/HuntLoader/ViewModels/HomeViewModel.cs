// src/HuntLoader/ViewModels/HomeViewModel.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HuntLoader.Core;
using HuntLoader.Models;
using HuntLoader.Services;

namespace HuntLoader.ViewModels;

public class HomeViewModel : BaseViewModel
{
    private readonly MainViewModel _main;
    private CancellationTokenSource? _cts;

    private string _statusText = "Готов к запуску";
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    private int _progress;
    public int Progress
    {
        get => _progress;
        set => Set(ref _progress, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => Set(ref _isLoading, value);
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set => Set(ref _isPlaying, value);
    }

    public MinecraftProfile? ActiveProfile => _main.ProfileManager.ActiveProfile;
    public Account?          ActiveAccount => _main.AuthService.ActiveAccount;

    public string WelcomeText => ActiveAccount != null
        ? $"Привет, {ActiveAccount.Username}!"
        : "Добро пожаловать!";

    public int AccountsCount => _main.AuthService.Accounts.Count;

    public AsyncRelayCommand PlayCommand                  { get; }
    public AsyncRelayCommand StopCommand                  { get; }
    public RelayCommand       OpenFolderCommand            { get; }
    public RelayCommand       OpenModsFolderCommand        { get; }
    public RelayCommand       OpenSavesFolderCommand       { get; }
    public RelayCommand       OpenScreenshotsFolderCommand { get; }

    public HomeViewModel(MainViewModel main)
    {
        _main = main;

        PlayCommand = new AsyncRelayCommand(
            PlayAsync, () => !IsPlaying && !IsLoading);
        StopCommand = new AsyncRelayCommand(
            StopAsync, () => IsPlaying || IsLoading);

        OpenFolderCommand = new RelayCommand(
            () => OpenDir(ActiveProfile?.ProfileDir));
        OpenModsFolderCommand = new RelayCommand(
            () => OpenDir(ActiveProfile?.ModsDir));
        OpenSavesFolderCommand = new RelayCommand(
            () => OpenDir(ActiveProfile?.SavesDir));
        OpenScreenshotsFolderCommand = new RelayCommand(
            () => OpenDir(ActiveProfile?.ScreenshotsDir));

        _main.AuthService.AccountsChanged += () =>
        {
            OnPropertyChanged(nameof(ActiveAccount));
            OnPropertyChanged(nameof(WelcomeText));
            OnPropertyChanged(nameof(AccountsCount));
        };

        _main.ProfileManager.ProfilesChanged += () =>
            OnPropertyChanged(nameof(ActiveProfile));

        _main.LaunchService.OnExited += code =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsPlaying  = false;
                IsLoading  = false;
                StatusText = code == 0
                    ? "Готов к запуску"
                    : $"Игра завершилась (код {code})";
                PlayCommand.RaiseCanExecuteChanged();
                StopCommand.RaiseCanExecuteChanged();
            });
        };

        // Пробрасываем статусы Java из LaunchService → UI
        _main.LaunchService.OnStatus += s =>
            Application.Current.Dispatcher.Invoke(() => StatusText = s);
        _main.LaunchService.OnProgress += p =>
            Application.Current.Dispatcher.Invoke(() => Progress = p);
    }

    private async Task PlayAsync()
    {
        var profile = ActiveProfile;
        var account = ActiveAccount;

        if (profile == null) { StatusText = "❌ Создайте профиль"; return; }
        if (account == null) { StatusText = "❌ Добавьте аккаунт"; return; }

        _cts = new CancellationTokenSource();

        try
        {
            IsLoading  = true;
            Progress   = 0;
            StatusText = "Подготовка...";
            PlayCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();

            // ── Шаг 1: Проверяем / скачиваем Java ────────
            Progress   = 5;
            StatusText = "☕ Проверка Java...";

            var javaManager = new JavaManager();

            javaManager.OnStatus += s =>
                Application.Current.Dispatcher.Invoke(() => StatusText = s);
            javaManager.OnProgress += p =>
                Application.Current.Dispatcher.Invoke(() =>
                    Progress = 5 + p / 20); // 5–10%

            var required = javaManager.GetRequiredJavaVersion(
                profile.GameVersion);
            await javaManager.EnsureJavaAsync(required, _cts.Token);

            if (_cts.Token.IsCancellationRequested)
            {
                StatusText = "⛔ Отменено";
                return;
            }

            // ── Шаг 2: Скачиваем Minecraft если нужно ────
            var needInstall = !_main.VersionManager
                .IsVersionInstalled(profile.GameVersion);
            var needFabric  = profile.ModLoader == ModLoader.Fabric &&
                              !_main.VersionManager.IsFabricInstalled(
                                  profile.GameVersion,
                                  profile.ModLoaderVersion);

            if (needInstall || needFabric)
            {
                StatusText = needInstall
                    ? $"⬇ Загрузка Minecraft {profile.GameVersion}..."
                    : "⬇ Установка Fabric...";
                Progress = 10;

                await _main.VersionManager.InstallVersionAsync(
                    profile.GameVersion,
                    new Progress<DownloadProgress>(p =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            StatusText = p.Status;
                            Progress   = 10 + (p.FilesTotal > 0
                                ? p.FilesCurrent * 55 / p.FilesTotal
                                : p.Percentage / 2); // 10–65%
                        });
                    }),
                    loader: profile.ModLoader,
                    loaderVersion: profile.ModLoaderVersion);
            }

            if (_cts.Token.IsCancellationRequested)
            {
                StatusText = "⛔ Отменено";
                return;
            }

            // ── Шаг 3: Оптимизации и resource pack ───────
            Progress   = 65;
            StatusText = "⚡ Применяем оптимизации...";
            await Task.Run(() =>
            {
                ResourcePackService.InstallCustomSplash(profile);
                ResourcePackService.ApplyOptimizedOptions(profile);
            }, _cts.Token);

            if (_cts.Token.IsCancellationRequested)
            {
                StatusText = "⛔ Отменено";
                return;
            }

            // ── Шаг 4: Кастомный сплеш мод для Fabric ────
            if (profile.ModLoader == ModLoader.Fabric)
            {
                Progress   = 75;
                StatusText = "🎨 Установка кастомного сплеша...";
                var modMgr = new ModManager();
                modMgr.OnProgress += p =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusText = p.Status;
                        Progress   = 75 + p.Percentage / 5; // 75–95
                    });
                };
                await modMgr.InstallCustomSplashModAsync(
                    profile, _cts.Token);
            }

            if (_cts.Token.IsCancellationRequested)
            {
                StatusText = "⛔ Отменено";
                return;
            }

            // ── Шаг 5: Запуск ─────────────────────────────
            IsLoading  = false;
            IsPlaying  = true;
            Progress   = 100;
            StatusText = $"🎮 Запущено: {profile.Name}";
            PlayCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();

            await _main.LaunchService.LaunchAsync(
                profile, account, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText = "⛔ Отменено";
            Logger.Info("Launch cancelled by user", "HomeVM");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "HomeVM");
            StatusText = $"❌ {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            IsPlaying = false;
            _cts?.Dispose();
            _cts = null;
            PlayCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
        }
    }

    private Task StopAsync()
    {
        _cts?.Cancel();
        if (_main.LaunchService.IsRunning)
            _main.LaunchService.Kill();
        StatusText = "⛔ Остановлено";
        return Task.CompletedTask;
    }

    private static void OpenDir(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show(
                "Сначала создайте профиль!",
                "Hunt Loader",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName        = "explorer.exe",
                Arguments       = $"\"{path}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "OpenDir");
            MessageBox.Show(
                $"Не удалось открыть папку:\n{ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}