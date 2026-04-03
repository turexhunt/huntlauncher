// src/HuntLoader/MainWindow.xaml.cs
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using HuntLoader.ViewModels;

namespace HuntLoader;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public void InitDataContext()
    {
        var vm = new MainViewModel();
        vm.PageChanged += OnPageChanged;
        DataContext = vm;
    }

    private async void OnPageChanged(object? sender, EventArgs e)
    {
        var fadeOut = new DoubleAnimation
        {
            From           = 1,
            To             = 0,
            Duration       = TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var slideOut = new DoubleAnimation
        {
            From           = 0,
            To             = -10,
            Duration       = TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        PageContent.BeginAnimation(OpacityProperty, fadeOut);
        if (PageContent.RenderTransform
            is System.Windows.Media.TranslateTransform tt)
            tt.BeginAnimation(
                System.Windows.Media.TranslateTransform.YProperty,
                slideOut);

        await Task.Delay(120);

        var fadeIn = new DoubleAnimation
        {
            From           = 0,
            To             = 1,
            Duration       = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slideIn = new DoubleAnimation
        {
            From           = 12,
            To             = 0,
            Duration       = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        PageContent.BeginAnimation(OpacityProperty, fadeIn);
        if (PageContent.RenderTransform
            is System.Windows.Media.TranslateTransform tt2)
            tt2.BeginAnimation(
                System.Windows.Media.TranslateTransform.YProperty,
                slideIn);
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseClick(object sender, RoutedEventArgs e)
        => Close();
}