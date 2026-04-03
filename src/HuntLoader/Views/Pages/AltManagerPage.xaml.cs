// src/HuntLoader/Views/Pages/AltManagerPage.xaml.cs
using System.Windows.Controls;
using System.Windows.Input;
using HuntLoader.Models;
using HuntLoader.ViewModels;

namespace HuntLoader.Views.Pages;

public partial class AltManagerPage : UserControl
{
    public AltManagerPage() => InitializeComponent();

    private void Account_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is Account acc)
        {
            if (DataContext is AltManagerViewModel vm)
            {
                vm.SelectedAccount = acc;
            }
        }
    }
}