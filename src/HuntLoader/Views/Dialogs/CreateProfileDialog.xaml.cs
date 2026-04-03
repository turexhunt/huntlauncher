// src/HuntLoader/Views/Dialogs/CreateProfileDialog.xaml.cs
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using HuntLoader.Models;
using HuntLoader.Services;

namespace HuntLoader.Views.Dialogs;

public partial class CreateProfileDialog : Window
{
    private readonly ProfileManager   _profileManager;
    private          MinecraftProfile? _createdProfile;
    private          CancellationTokenSource? _cts;

    // Возвращает созданный профиль после закрытия
    public MinecraftProfile? CreatedProfile => _createdProfile;

    public CreateProfileDialog(ProfileManager profileManager)
    {
        InitializeComponent();
        _profileManager = profileManager;

        // Подписываемся на прогресс оптимизации
        _profileManager.OnOptimizationProgress += OnOptimizationProgress;

        // Заполняем версии
        foreach (var ver in Constants.FeaturedVersions)
            VersionCombo.Items.Add(ver);
        VersionCombo.SelectedIndex = 0;
        LoaderCombo.SelectedIndex  = 0;

        // Перетаскивание окна
        MouseLeftButtonDown += (_, _) => DragMove();
    }

    // ── Показать/скрыть панель оптимизации ───────────────
    private void LoaderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OptimizationPanel == null) return;

        var selected = (LoaderCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        OptimizationPanel.Visibility = selected == "Vanilla"
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    // ── Создать профиль ───────────────────────────────────
    private async void CreateClick(object sender, RoutedEventArgs e)
    {
        var name    = NameBox.Text.Trim();
        var version = VersionCombo.SelectedItem?.ToString() ?? "1.21.4";
        var loader  = ParseLoader(
            (LoaderCombo.SelectedItem as ComboBoxItem)?.Content?.ToString());

        if (string.IsNullOrEmpty(name))
        {
            StatusText.Text = "⚠ Введи название профиля";
            return;
        }

        if (!int.TryParse(MemMinBox.Text, out var memMin)) memMin = 512;
        if (!int.TryParse(MemMaxBox.Text, out var memMax)) memMax = 2048;

        var installOptimizations = OptimizeCheck.IsChecked == true
                                   && loader != ModLoader.Vanilla;

        // Блокируем UI
        SetUiEnabled(false);
        StatusText.Text = installOptimizations
            ? "⬇ Создаю профиль и устанавливаю моды..."
            : "⬇ Создаю профиль...";

        if (installOptimizations)
            ProgressPanel.Visibility = Visibility.Visible;

        _cts = new CancellationTokenSource();

        try
        {
            _createdProfile = await _profileManager.CreateWithOptimizationAsync(
                name, version, loader,
                installOptimizations: installOptimizations,
                ct: _cts.Token);

            // Устанавливаем RAM
            _createdProfile.MemoryMin = memMin;
            _createdProfile.MemoryMax = memMax;
            _profileManager.Save(_createdProfile);

            StatusText.Text = "✅ Профиль создан!";
            await System.Threading.Tasks.Task.Delay(800);

            DialogResult = true;
            Close();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "⚠ Отменено";
            SetUiEnabled(true);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ Ошибка: {ex.Message}";
            SetUiEnabled(true);
        }
    }

    // ── Прогресс установки модов ──────────────────────────
    private void OnOptimizationProgress(string status, double percent)
    {
        Dispatcher.InvokeAsync(() =>
        {
            StatusText.Text    = status;
            ProgressFill.Width = 392 * (percent / 100.0);
        });
    }

    private void SetUiEnabled(bool enabled)
    {
        NameBox.IsEnabled       = enabled;
        VersionCombo.IsEnabled  = enabled;
        LoaderCombo.IsEnabled   = enabled;
        MemMinBox.IsEnabled     = enabled;
        MemMaxBox.IsEnabled     = enabled;
        OptimizeCheck.IsEnabled = enabled;
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _profileManager.OnOptimizationProgress -= OnOptimizationProgress;
        DialogResult = false;
        Close();
    }

    private static ModLoader ParseLoader(string? s) => s switch
    {
        "Fabric"   => ModLoader.Fabric,
        "Forge"    => ModLoader.Forge,
        "Quilt"    => ModLoader.Quilt,
        "NeoForge" => ModLoader.NeoForge,
        _          => ModLoader.Vanilla
    };
}