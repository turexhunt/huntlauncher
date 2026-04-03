// src/HuntLoader/Views/Dialogs/AddAccountDialog.xaml.cs
using System.Windows;
using System.Windows.Controls;
using HuntLoader.Services;

namespace HuntLoader.Views.Dialogs;

public partial class AddAccountDialog : Window
{
    public string? ResultUsername { get; private set; }
    public bool    ResultSuccess  { get; private set; }

    private readonly AuthService _auth;

    public AddAccountDialog(AuthService auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    private void CloseClick(object s, RoutedEventArgs e) => Close();

    private void TypeCombo_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        var idx = TypeCombo.SelectedIndex;
        OfflinePanel.Visibility   = idx == 0 ? Visibility.Visible : Visibility.Collapsed;
        MicrosoftPanel.Visibility = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddOfflineClick(object s, RoutedEventArgs e)
    {
        try
        {
            var acc = _auth.AddOfflineAccount(UsernameBox.Text.Trim());
            ResultUsername = acc.Username;
            ResultSuccess  = true;
            StatusText.Text = $"✅ Добавлен: {acc.Username}";
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ {ex.Message}";
        }
    }

    private async void AddMicrosoftClick(object s, RoutedEventArgs e)
    {
        StatusText.Text = "🔄 Открываем браузер...";
        try
        {
            var ms     = new MicrosoftAuthService();
            var result = await ms.LoginAsync();
            await _auth.AddMicrosoftAccountAsync(
                result.AccessToken, result.RefreshToken,
                result.Username, result.UUID, result.Expiry);
            ResultUsername = result.Username;
            ResultSuccess  = true;
            StatusText.Text = $"✅ Вошли как {result.Username}";
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ {ex.Message}";
        }
    }
}