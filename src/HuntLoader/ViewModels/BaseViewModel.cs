// src/HuntLoader/ViewModels/BaseViewModel.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HuntLoader.ViewModels;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute == null ? null : _ => canExecute()) { }

    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? p) => _canExecute?.Invoke(p) ?? true;
    public void Execute(object? p)    => _execute(p);

    public void RaiseCanExecuteChanged() =>
        CommandManager.InvalidateRequerySuggested();
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? p) =>
        _canExecute?.Invoke(p is T t ? t : default) ?? true;

    public void Execute(object? p) =>
        _execute(p is T t ? t : default);
}

public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? can = null)
        : this(_ => execute(), can == null ? null : _ => can()) { }

    public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? can = null)
    {
        _execute    = execute;
        _canExecute = can;
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            _isExecuting = value;
            System.Windows.Application.Current?.Dispatcher.Invoke(
                () => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
        }
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? p) => !_isExecuting && (_canExecute?.Invoke(p) ?? true);

    public async void Execute(object? p)
    {
        if (!CanExecute(p)) return;
        IsExecuting = true;
        try   { await _execute(p); }
        finally { IsExecuting = false; }
    }

    public void RaiseCanExecuteChanged() =>
        System.Windows.Application.Current?.Dispatcher.Invoke(
            () => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
}

// ✅ ДОБАВЛЕНО — AsyncRelayCommand с типизированным параметром
public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            _isExecuting = value;
            System.Windows.Application.Current?.Dispatcher.Invoke(
                () => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
        }
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? p)
    {
        var param = p is T t ? t : default;
        return !_isExecuting && (_canExecute?.Invoke(param) ?? true);
    }

    public async void Execute(object? p)
    {
        if (!CanExecute(p)) return;
        IsExecuting = true;
        try
        {
            var param = p is T t ? t : default;
            await _execute(param);
        }
        finally { IsExecuting = false; }
    }

    public void RaiseCanExecuteChanged() =>
        System.Windows.Application.Current?.Dispatcher.Invoke(
            () => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
}