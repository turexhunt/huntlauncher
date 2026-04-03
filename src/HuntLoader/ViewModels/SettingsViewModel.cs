// src/HuntLoader/ViewModels/SettingsViewModel.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using HuntLoader.Core;
using HuntLoader.Services;

namespace HuntLoader.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly MainViewModel _main;
    private readonly AppConfig _cfg = AppConfig.Instance;

    // ── Внешний вид ──────────────────────────────────────
    public bool ShowParticles
    {
        get => _cfg.ShowParticles;
        set { _cfg.ShowParticles = value; _cfg.Save(); OnPropertyChanged(); }
    }

    public bool AnimationsEnabled
    {
        get => _cfg.AnimationsEnabled;
        set { _cfg.AnimationsEnabled = value; _cfg.Save(); OnPropertyChanged(); }
    }

    public double BackgroundOpacity
    {
        get => _cfg.BackgroundOpacity;
        set { _cfg.BackgroundOpacity = value; _cfg.Save(); OnPropertyChanged(); }
    }

    // ── Глобальная кастомизация цвета ────────────────────
    public string AccentColor
    {
        get => _cfg.AccentColor;
        set
        {
            if (_cfg.AccentColor == value) return;
            _cfg.AccentColor = value;
            _cfg.Save();
            OnPropertyChanged();
            ApplyAccentColor();
        }
    }

    public string AccentColor2
    {
        get => _cfg.AccentColor2;
        set
        {
            if (_cfg.AccentColor2 == value) return;
            _cfg.AccentColor2 = value;
            _cfg.Save();
            OnPropertyChanged();
            ApplyAccentColor();
        }
    }

    // ── Пресеты тем ──────────────────────────────────────
    public string[] ThemePresets { get; } =
    {
        "Фиолетовый (по умолчанию)",
        "Синий океан",
        "Розово-красный",
        "Зелёный лес",
        "Оранжевый закат",
        "Бирюзовый"
    };

    private string _selectedTheme = "Фиолетовый (по умолчанию)";
    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            Set(ref _selectedTheme, value);
            ApplyThemePreset(value);
        }
    }

    // ── Скруглённость углов ───────────────────────────────
    public int CornerRadius
    {
        get => _cfg.CornerRadius;
        set { _cfg.CornerRadius = value; _cfg.Save(); OnPropertyChanged(); }
    }

    // ── Масштаб UI ───────────────────────────────────────
    public double UiScale
    {
        get => _cfg.UiScale;
        set
        {
            _cfg.UiScale = Math.Clamp(value, 0.8, 1.5);
            _cfg.Save();
            OnPropertyChanged();
            ApplyUiScale();
        }
    }

    // ── Лаунчер ──────────────────────────────────────────
    public bool CloseOnLaunch
    {
        get => _cfg.CloseOnLaunch;
        set { _cfg.CloseOnLaunch = value; _cfg.Save(); OnPropertyChanged(); }
    }

    public bool ShowSnapshots
    {
        get => _cfg.ShowSnapshots;
        set { _cfg.ShowSnapshots = value; _cfg.Save(); OnPropertyChanged(); }
    }

    public bool DiscordRpc
    {
        get => _cfg.DiscordRpc;
        set
        {
            _cfg.DiscordRpc = value;
            _cfg.Save();
            OnPropertyChanged();
            if (value) _main.Discord.Initialize();
            else       _main.Discord.Disable();
        }
    }

    public int ConcurrentDownloads
    {
        get => _cfg.ConcurrentDownloads;
        set { _cfg.ConcurrentDownloads = value; _cfg.Save(); OnPropertyChanged(); }
    }

    public string GlobalJavaPath
    {
        get => _cfg.GlobalJavaPath;
        set { _cfg.GlobalJavaPath = value; _cfg.Save(); OnPropertyChanged(); }
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    // ── Команды ──────────────────────────────────────────
    public RelayCommand      BrowseJavaCommand       { get; }
    public RelayCommand      BrowseBgCommand         { get; }
    public RelayCommand      OpenAppDataCommand      { get; }
    public RelayCommand      ResetSettingsCommand    { get; }
    public RelayCommand      ClearCacheCommand       { get; }
    public RelayCommand      OpenDiscordCommand      { get; }
    public AsyncRelayCommand CheckJavaCommand        { get; }
    public RelayCommand      PickAccentColorCommand  { get; }
    public RelayCommand      PickAccentColor2Command { get; }

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;

        BrowseJavaCommand       = new RelayCommand(BrowseJava);
        BrowseBgCommand         = new RelayCommand(BrowseBg);
        OpenAppDataCommand      = new RelayCommand(OpenAppData);
        ResetSettingsCommand    = new RelayCommand(ResetSettings);
        ClearCacheCommand       = new RelayCommand(ClearCache);
        OpenDiscordCommand      = new RelayCommand(OpenDiscord);
        CheckJavaCommand        = new AsyncRelayCommand(CheckJavaAsync);
        PickAccentColorCommand  = new RelayCommand(PickAccentColor);
        PickAccentColor2Command = new RelayCommand(PickAccentColor2);

        ApplyAccentColor();
    }

    // ── Применение акцент цвета ──────────────────────────
    private void ApplyAccentColor()
    {
        try
        {
            var c1 = (Color)ColorConverter.ConvertFromString(_cfg.AccentColor);
            var c2 = (Color)ColorConverter.ConvertFromString(_cfg.AccentColor2);

            if (Application.Current.Resources["AccentBrush"] is SolidColorBrush ab)
                ab.Color = c1;
            else
                Application.Current.Resources["AccentBrush"] =
                    new SolidColorBrush(c1);

            var lightColor = Color.FromArgb(255,
                (byte)Math.Min(255, c1.R + 35),
                (byte)Math.Min(255, c1.G + 35),
                (byte)Math.Min(255, c1.B + 35));

            if (Application.Current.Resources["AccentLightBrush"]
                is SolidColorBrush alb)
                alb.Color = lightColor;
            else
                Application.Current.Resources["AccentLightBrush"] =
                    new SolidColorBrush(lightColor);

            if (Application.Current.Resources["Accent2Brush"]
                is SolidColorBrush a2b)
                a2b.Color = c2;
            else
                Application.Current.Resources["Accent2Brush"] =
                    new SolidColorBrush(c2);

            Application.Current.Resources["AccentGradient"] =
                MakeLinearGradient(0, 0, 1, 0, c1, c2);

            Application.Current.Resources["AccentGradientDiag"] =
                MakeLinearGradient(0, 0, 1, 1, c1, c2);

            var play = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint   = new Point(1, 0)
            };
            play.GradientStops.Add(new GradientStop(c1, 0));
            play.GradientStops.Add(new GradientStop(c2, 0.6));
            play.GradientStops.Add(
                new GradientStop(Color.FromRgb(0x4E, 0xCD, 0xC4), 1));
            Application.Current.Resources["PlayGradient"] = play;

            var side = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint   = new Point(0, 1)
            };
            side.GradientStops.Add(new GradientStop(c1, 0));
            side.GradientStops.Add(new GradientStop(c2, 0.5));
            side.GradientStops.Add(
                new GradientStop(Color.FromRgb(0xE8, 0x60, 0x9A), 0.8));
            side.GradientStops.Add(
                new GradientStop(Color.FromRgb(0x4E, 0xCD, 0xC4), 1));
            Application.Current.Resources["SidebarBorderGradient"] = side;

            StatusText =
                $"✅ Тема применена: {_cfg.AccentColor} → {_cfg.AccentColor2}";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Ошибка цвета: {ex.Message}";
        }
    }

    private static LinearGradientBrush MakeLinearGradient(
        double x1, double y1, double x2, double y2,
        Color  c1, Color  c2)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(x1, y1),
            EndPoint   = new Point(x2, y2)
        };
        brush.GradientStops.Add(new GradientStop(c1, 0));
        brush.GradientStops.Add(new GradientStop(c2, 1));
        return brush;
    }

    // ── Масштаб UI ───────────────────────────────────────
    private void ApplyUiScale()
    {
        if (Application.Current.MainWindow is MainWindow mw)
            mw.LayoutTransform = new ScaleTransform(
                _cfg.UiScale, _cfg.UiScale);
    }

    // ── Пресеты тем ──────────────────────────────────────
    private void ApplyThemePreset(string preset)
    {
        (AccentColor, AccentColor2) = preset switch
        {
            "Синий океан"     => ("#2196F3", "#03A9F4"),
            "Розово-красный"  => ("#E91E63", "#F44336"),
            "Зелёный лес"     => ("#4CAF50", "#8BC34A"),
            "Оранжевый закат" => ("#FF9800", "#FF5722"),
            "Бирюзовый"       => ("#00BCD4", "#009688"),
            _                 => ("#7C5CBF", "#5B8FE8")
        };
        StatusText = $"✅ Тема применена: {preset}";
    }

    // ── Выбор цвета через диалог ─────────────────────────
    private void PickAccentColor()
    {
        var dlg = new Views.Dialogs.ColorPickerDialog(_cfg.AccentColor);
        if (dlg.ShowDialog() == true)
            AccentColor = dlg.SelectedColor;
    }

    private void PickAccentColor2()
    {
        var dlg = new Views.Dialogs.ColorPickerDialog(_cfg.AccentColor2);
        if (dlg.ShowDialog() == true)
            AccentColor2 = dlg.SelectedColor;
    }

    // ── Discord ──────────────────────────────────────────
    private void OpenDiscord()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = Constants.DiscordUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex) { StatusText = $"❌ {ex.Message}"; }
    }

    // ── Java ─────────────────────────────────────────────
    private void BrowseJava()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Java|javaw.exe;java.exe|All|*.*",
            Title  = "Выбери javaw.exe"
        };
        if (dlg.ShowDialog() == true)
        {
            GlobalJavaPath = dlg.FileName;
            StatusText     = $"✅ Java: {dlg.FileName}";
        }
    }

    // ── ИСПРАВЛЕННЫЙ CheckJavaAsync ──────────────────────
    private async Task CheckJavaAsync()
    {
        StatusText = "🔄 Проверка Java...";
        try
        {
            var jm = new JavaManager();

            // Если путь указан вручную — проверяем его
            if (!string.IsNullOrEmpty(GlobalJavaPath))
            {
                if (!File.Exists(GlobalJavaPath))
                {
                    StatusText = $"❌ Файл не найден: {GlobalJavaPath}";
                    return;
                }
                var ver   = await jm.GetJavaVersionAsync(GlobalJavaPath);
                var major = jm.GetMajorVersionSync(GlobalJavaPath);
                StatusText = major > 0
                    ? $"✅ {ver} (Java {major})"
                    : $"✅ {ver}";
                return;
            }

            // Иначе — ищем автоматически
            StatusText = "🔍 Поиск Java на компьютере...";
            var found = jm.FindInstalledJava();

            if (found != null)
            {
                var ver   = await jm.GetJavaVersionAsync(found);
                var major = jm.GetMajorVersionSync(found);
                GlobalJavaPath = found; // сохраняем найденный путь
                StatusText = major > 0
                    ? $"✅ {ver} (Java {major}) — {found}"
                    : $"✅ {ver} — {found}";
            }
            else
            {
                // Не нашли — предлагаем скачать
                StatusText = "⬇ Java не найдена. Скачиваем Java 21...";

                jm.OnStatus   += s => StatusText = s;
                jm.OnProgress += p => { /* прогресс в StatusText */ };

                var path = await jm.DownloadJavaAsync(21);
                GlobalJavaPath = path;

                var ver   = await jm.GetJavaVersionAsync(path);
                var major = jm.GetMajorVersionSync(path);
                StatusText = $"✅ Java {major} установлена: {path}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Ошибка: {ex.Message}";
            Logger.Error(ex, "SettingsVM");
        }
    }

    // ── Фон ──────────────────────────────────────────────
    private void BrowseBg()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg|All|*.*",
            Title  = "Выбери фон"
        };
        if (dlg.ShowDialog() == true)
        {
            _cfg.BackgroundImage = dlg.FileName;
            _cfg.Save();
            StatusText = "✅ Фон установлен";
        }
    }

    // ── AppData ───────────────────────────────────────────
    private void OpenAppData()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = "explorer.exe",
                Arguments       = $"\"{Constants.AppDataRoot}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex) { StatusText = $"❌ {ex.Message}"; }
    }

    // ── Сброс ────────────────────────────────────────────
    private void ResetSettings()
    {
        AppConfig.Reset();
        OnPropertyChanged(nameof(ShowParticles));
        OnPropertyChanged(nameof(AnimationsEnabled));
        OnPropertyChanged(nameof(BackgroundOpacity));
        OnPropertyChanged(nameof(CloseOnLaunch));
        OnPropertyChanged(nameof(ShowSnapshots));
        OnPropertyChanged(nameof(DiscordRpc));
        OnPropertyChanged(nameof(ConcurrentDownloads));
        OnPropertyChanged(nameof(GlobalJavaPath));
        OnPropertyChanged(nameof(AccentColor));
        OnPropertyChanged(nameof(AccentColor2));
        OnPropertyChanged(nameof(CornerRadius));
        OnPropertyChanged(nameof(UiScale));
        ApplyAccentColor();
        StatusText = "✅ Настройки сброшены";
    }

    // ── Кеш ──────────────────────────────────────────────
    private void ClearCache()
    {
        try
        {
            if (Directory.Exists(Constants.TempDir))
            {
                Directory.Delete(Constants.TempDir, true);
                Directory.CreateDirectory(Constants.TempDir);
            }
            StatusText = "✅ Кеш очищен";
        }
        catch (Exception ex) { StatusText = $"❌ {ex.Message}"; }
    }
}