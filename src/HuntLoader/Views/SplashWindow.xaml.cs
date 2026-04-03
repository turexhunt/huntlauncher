// src/HuntLoader/Views/SplashWindow.xaml.cs
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using HuntLoader.Core;
using HuntLoader.Views.Dialogs;

namespace HuntLoader.Views;

public partial class SplashWindow : Window
{
    private const int TotalSteps = 5;

    public SplashWindow() => InitializeComponent();

    public async Task RunInitAsync(MainWindow mainWindow)
    {
        await Task.Delay(300);

        // Шаг 1 — Старт
        SetProgress(0, "Инициализация Hunt Loader v2.0.0...", 1);
        await Task.Delay(400);

        // Шаг 2 — Обновления
        SetProgress(10, "Проверка обновлений...", 2);
        try
        {
            var updater = new Updater();
            var update  = await updater.CheckAsync();
            if (update != null)
            {
                Logger.Info(
                    $"Найдено обновление: {update.Version}", "Splash");

                await Dispatcher.InvokeAsync(() =>
                {
                    var dialog = new UpdateDialog(update);
                    dialog.Owner = this;
                    dialog.ShowDialog();
                    // Если нажал "Обновить" — лаунчер закроется сам
                    // Если "Пропустить" — продолжаем
                });
            }
            else
            {
                Logger.Info("Обновлений нет", "Splash");
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(
                $"Ошибка проверки обновлений: {ex.Message}", "Splash");
        }

        // Шаг 3 — Конфиг
        SetProgress(25, "Загрузка конфигурации...", 3);
        await Task.Delay(300);

        // Шаг 4 — Профили и интерфейс
        SetProgress(50, "Загрузка профилей...", 4);
        await Task.Delay(300);

        SetProgress(65, "Подготовка интерфейса...", 4);
        await Dispatcher.InvokeAsync(() =>
        {
            try   { mainWindow.InitDataContext(); }
            catch (Exception ex) { Logger.Error(ex, "Splash"); }
        });
        await Task.Delay(400);

        // Шаг 5 — Финал
        SetProgress(85, "Запуск сервисов...", 5);
        await Task.Delay(300);

        SetProgress(100, "✅ Добро пожаловать в Hunt Loader 2.0!", 5);
        await Task.Delay(600);
    }

    public void SetProgress(double percent, string status, int step)
    {
        Dispatcher.InvokeAsync(() =>
        {
            StatusLbl.Text = status;
            PctLbl.Text    = $"{(int)percent}%";
            StepLbl.Text   = $"{step}/{TotalSteps}";

            var anim = new DoubleAnimation
            {
                To             = 400 * (percent / 100.0),
                Duration       = TimeSpan.FromMilliseconds(500),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };
            ProgFill.BeginAnimation(WidthProperty, anim);
        });
    }
}