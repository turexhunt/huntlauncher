// src/HuntLoader/Controls/ProgressButton.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HuntLoader.Controls;

public partial class ProgressButton : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string),
            typeof(ProgressButton), new PropertyMetadata("Играть"));

    public static readonly DependencyProperty LoadingTextProperty =
        DependencyProperty.Register(nameof(LoadingText), typeof(string),
            typeof(ProgressButton), new PropertyMetadata("Загрузка..."));

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool),
            typeof(ProgressButton), new PropertyMetadata(false));

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double),
            typeof(ProgressButton), new PropertyMetadata(0.0));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand),
            typeof(ProgressButton));

    public string  Text        { get => (string)GetValue(TextProperty);        set => SetValue(TextProperty, value); }
    public string  LoadingText { get => (string)GetValue(LoadingTextProperty); set => SetValue(LoadingTextProperty, value); }
    public bool    IsLoading   { get => (bool)GetValue(IsLoadingProperty);     set => SetValue(IsLoadingProperty, value); }
    public double  Progress    { get => (double)GetValue(ProgressProperty);    set => SetValue(ProgressProperty, value); }
    public ICommand? Command   { get => (ICommand?)GetValue(CommandProperty);  set => SetValue(CommandProperty, value); }

    public ProgressButton() => InitializeComponent();
}