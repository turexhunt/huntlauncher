// src/HuntLoader/Views/Dialogs/UpdateDialog.xaml.cs
using System;
using System.Threading.Tasks;
using System.Windows;
using HuntLoader.Core;

namespace HuntLoader.Views.Dialogs;

public partial class UpdateDialog : Window
{
    private readonly UpdateInfo _info;
    private readonly Updater    _updater;

    public UpdateDialog(UpdateInfo info)
    {
        InitializeComponent();
        _info    = info;
        _updater = new Updater();

        VersionText.Text   = $"Версия {Constants.LauncherVersion} → {info.Version}";
        ChangelogText.Text = info.ChangeLog;

        // Чтобы окно можно было перетаскивать
        MouseLeftButtonDown += (_, _) => DragMove();
    }

    private void SkipBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        UpdateBtn.IsEnabled = false;
        SkipBtn.IsEnabled   = false;
        ProgressPanel.Visibility = Visibility.Visible;

        var progress = new Progress<double>(p =>
        {
            Dispatcher.Invoke(() =>
            {
                ProgressText.Text  = $"Скачиваю... {p:F0}%";
                // Ширина прогресс-бара (максимум 432px)
                ProgressFill.Width = 432 * (p / 100.0);
            });
        });

        try
        {
            await _updater.DownloadAndInstallAsync(_info, progress);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка обновления:\n{ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            UpdateBtn.IsEnabled      = true;
            SkipBtn.IsEnabled        = true;
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
    }
}