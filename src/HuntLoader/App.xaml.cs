// src/HuntLoader/App.xaml.cs
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using HuntLoader.Core;
using HuntLoader.Views; // ✅ добавили

namespace HuntLoader;

public partial class App : Application
{
    private bool _errorShown = false;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        EnsureDirectories();
        Logger.Init();
        Logger.Info("=== Hunt Loader Starting ===", "App");
        AppConfig.Instance.Save();

        DispatcherUnhandledException += (_, ex) =>
        {
            if (ex.Exception.Message.Contains("PlayTimeFormatted") ||
                ex.Exception.Message.Contains("TwoWay")            ||
                ex.Exception.Message.Contains("OneWayToSource"))
            {
                Logger.Warning($"Binding warning: {ex.Exception.Message}", "App");
                ex.Handled = true;
                return;
            }

            Logger.Fatal($"UI Exception: {ex.Exception}", "App");

            if (!_errorShown)
            {
                _errorShown = true;
                MessageBox.Show(
                    $"Критическая ошибка:\n{ex.Exception.Message}\n\nЛог: {Constants.LogsDir}",
                    "Hunt Loader - Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                _errorShown = false;
            }
            ex.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            Logger.Error($"Task Exception: {ex.Exception.Message}", "App");
            ex.SetObserved();
        };

        ShowSplashAndStart();
    }

    private async void ShowSplashAndStart()
    {
        var mainWindow = new MainWindow();
        var splash     = new SplashWindow();
        splash.Show();
        MainWindow = splash;

        try
        {
            await splash.RunInitAsync(mainWindow);
        }
        catch (Exception ex)
        {
            Logger.Fatal($"Init failed: {ex}", "App");
        }

        mainWindow.Show();
        MainWindow = mainWindow;
        splash.Close();
    }

    private static void EnsureDirectories()
    {
        var dirs = new[]
        {
            Constants.AppDataRoot,
            Constants.ProfilesDir,
            Constants.VersionsDir,
            Constants.LogsDir,
            Constants.JavaDir,
            Constants.AssetsDir,
            Constants.LibrariesDir,
            Constants.TempDir
        };
        foreach (var d in dirs) Directory.CreateDirectory(d);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppConfig.Instance.Save();
        Logger.Info("=== Hunt Loader Exiting ===", "App");
        base.OnExit(e);
    }
}