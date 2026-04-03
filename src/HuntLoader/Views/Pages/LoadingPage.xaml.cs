// src/HuntLoader/Views/Pages/LoadingPage.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace HuntLoader.Views.Pages;

public partial class LoadingPage : UserControl
{
    private int _currentStep = 0;
    private int _totalSteps  = 4;

    public LoadingPage() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e) { }

    public void SetProgress(double percent, string status)
    {
        Dispatcher.InvokeAsync(() =>
        {
            StatusLabel.Text  = status;
            PercentLabel.Text = $"{(int)percent}%";

            _currentStep = percent switch
            {
                < 25 => 1,
                < 50 => 2,
                < 75 => 3,
                _    => 4
            };
            StepLabel.Text = $"{_currentStep}/{_totalSteps}";

            var targetWidth = 380 * (percent / 100.0);
            var anim = new DoubleAnimation
            {
                To             = targetWidth,
                Duration       = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressFill.BeginAnimation(WidthProperty, anim);
        });
    }

    public void SetSteps(int total) => _totalSteps = total;
}