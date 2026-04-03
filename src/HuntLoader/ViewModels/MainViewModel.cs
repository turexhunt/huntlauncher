// src/HuntLoader/ViewModels/MainViewModel.cs
using System;
using HuntLoader.Core;
using HuntLoader.Services;

namespace HuntLoader.ViewModels;

public class MainViewModel : BaseViewModel
{
    public AuthService       AuthService    { get; } = new();
    public ProfileManager    ProfileManager { get; } = new();
    public VersionManager    VersionManager { get; } = new();
    public GameLaunchService LaunchService  { get; } = new();
    public DiscordService    Discord        { get; } = new();

    public HomeViewModel       HomeVM       { get; }
    public AltManagerViewModel AltManagerVM { get; }
    public ProfileViewModel    ProfileVM    { get; }
    public ModsViewModel       ModsVM       { get; }
    public SettingsViewModel   SettingsVM   { get; }

    public event EventHandler? PageChanged;

    private BaseViewModel _currentPage;
    public BaseViewModel CurrentPage
    {
        get => _currentPage;
        set
        {
            if (Set(ref _currentPage, value))
                PageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private string _statusText = "Готов";
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    public RelayCommand NavHomeCommand       { get; }
    public RelayCommand NavAltManagerCommand { get; }
    public RelayCommand NavProfilesCommand   { get; }
    public RelayCommand NavModsCommand       { get; }
    public RelayCommand NavSettingsCommand   { get; }

    public MainViewModel()
    {
        try
        {
            AuthService.Load();
            ProfileManager.Load();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "MainViewModel");
        }

        HomeVM       = new HomeViewModel(this);
        AltManagerVM = new AltManagerViewModel(this);
        ProfileVM    = new ProfileViewModel(this);
        ModsVM       = new ModsViewModel(this);
        SettingsVM   = new SettingsViewModel(this);

        _currentPage = HomeVM;

        NavHomeCommand       = new RelayCommand(() => CurrentPage = HomeVM);
        NavAltManagerCommand = new RelayCommand(() => CurrentPage = AltManagerVM);
        NavProfilesCommand   = new RelayCommand(() => CurrentPage = ProfileVM);
        NavModsCommand       = new RelayCommand(() => CurrentPage = ModsVM);
        NavSettingsCommand   = new RelayCommand(() => CurrentPage = SettingsVM);

        Discord.Initialize();

        // ── LaunchService события ────────────────────────
        LaunchService.OnStarted += () =>
        {
            StatusText = "Игра запущена";
            var profile = ProfileManager.ActiveProfile;
            if (profile != null)
                Discord.SetPlaying(profile.Name, profile.GameVersion);
        };

        LaunchService.OnExited += code =>
        {
            StatusText = code == 0
                ? "Готов к запуску"
                : $"Игра завершилась (код {code})";
            Discord.SetIdle();
        };

        LaunchService.OnOutput += msg => Logger.Debug(msg, "MC");
        LaunchService.OnError  += msg => Logger.Debug(msg, "MC");

        // ── Java статусы → StatusText внизу лаунчера ────
        LaunchService.OnStatus += s =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(
                () => StatusText = s);
        };

        Logger.Info("MainViewModel готов", "MainViewModel");
    }
}