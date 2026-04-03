// src/HuntLoader/Views/Dialogs/ColorPickerDialog.xaml.cs
using System;
using System.Windows;
using System.Windows.Media;

namespace HuntLoader.Views.Dialogs;

public partial class ColorPickerDialog : Window
{
    public string SelectedColor { get; private set; } = "#7C5CBF";

    public ColorPickerDialog(string currentColor)
    {
        InitializeComponent();
        HexInput.Text = currentColor;
        UpdatePreview(currentColor);
    }

    private void HexInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var hex = HexInput.Text.Trim();
        if (!hex.StartsWith("#")) hex = "#" + hex;
        UpdatePreview(hex);
    }

    private void UpdatePreview(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            ColorPreview.Background = new SolidColorBrush(color);
            // Определяем цвет текста для контраста
            var brightness = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
            PreviewLabel.Foreground = brightness > 128
                ? new SolidColorBrush(Colors.Black)
                : new SolidColorBrush(Colors.White);
        }
        catch
        {
            ColorPreview.Background = new SolidColorBrush(Color.FromRgb(30, 30, 46));
        }
    }

    private void QuickColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string hex)
        {
            HexInput.Text = hex;
            UpdatePreview(hex);
        }
    }

    private void ApplyClick(object sender, RoutedEventArgs e)
    {
        var hex = HexInput.Text.Trim();
        if (!hex.StartsWith("#")) hex = "#" + hex;

        try
        {
            ColorConverter.ConvertFromString(hex); // валидация
            SelectedColor = hex;
            DialogResult  = true;
        }
        catch
        {
            MessageBox.Show("Неверный HEX код! Пример: #7C5CBF",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e)
        => DialogResult = false;
}