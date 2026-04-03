// src/HuntLoader/Views/Pages/ProfilesPage.xaml.cs
using System.Windows.Controls;
using System.Windows.Input;
using HuntLoader.Models;
using HuntLoader.ViewModels;

namespace HuntLoader.Views.Pages;

public partial class ProfilesPage : UserControl
{
    public ProfilesPage() => InitializeComponent();

    private void Profile_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b &&
            b.DataContext is MinecraftProfile p &&
            DataContext is ProfileViewModel vm)
        {
            vm.Selected = p;
        }
    }
}