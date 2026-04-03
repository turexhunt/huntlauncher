// src/HuntLoader/Views/Pages/ConsolePage.xaml.cs
using System.Windows.Controls;
using System.Windows;
using HuntLoader.ViewModels;

namespace HuntLoader.Views.Pages;

public partial class ConsolePage : UserControl
{
    public ConsolePage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is ConsoleViewModel vm)
        {
            vm.Lines.CollectionChanged += (_, _) =>
            {
                if (vm.AutoScroll && ConsoleList.Items.Count > 0)
                {
                    ConsoleList.ScrollIntoView(
                        ConsoleList.Items[^1]);
                }
            };
        }
    }
}