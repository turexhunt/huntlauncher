// src/HuntLoader/ViewModels/AltManagerViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HuntLoader.Core;
using HuntLoader.Models;
using HuntLoader.Services;
using Newtonsoft.Json;

namespace HuntLoader.ViewModels;

public class AltManagerViewModel : BaseViewModel
{
    private readonly MainViewModel _main;

    public ObservableCollection<Account> Accounts { get; } = new();

    private Account? _selectedAccount;
    public Account? SelectedAccount
    {
        get => _selectedAccount;
        set { Set(ref _selectedAccount, value); OnPropertyChanged(nameof(HasSelection)); OnPropertyChanged(nameof(SelectedNote)); }
    }

    public bool HasSelection => SelectedAccount != null;

    private string _newUsername = "";
    public string NewUsername
    {
        get => _newUsername;
        set => Set(ref _newUsername, value);
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    private bool _isMicrosoftLogging;
    public bool IsMicrosoftLogging
    {
        get => _isMicrosoftLogging;
        set => Set(ref _isMicrosoftLogging, value);
    }

    public string SelectedNote
    {
        get => SelectedAccount?.Note ?? "";
        set { if (SelectedAccount == null) return; _main.AuthService.UpdateNote(SelectedAccount.Id, value); OnPropertyChanged(); }
    }

    public RelayCommand       AddOfflineCommand     { get; }
    public AsyncRelayCommand  AddMicrosoftCommand   { get; }
    public RelayCommand       RemoveCommand         { get; }
    public RelayCommand       SetActiveCommand      { get; }
    public RelayCommand       CopyUsernameCommand   { get; }
    public RelayCommand       CopyUUIDCommand       { get; }
    public RelayCommand       ImportAccountsCommand { get; }
    public RelayCommand       ExportAccountsCommand { get; }

    public AltManagerViewModel(MainViewModel main)
    {
        _main = main;

        AddOfflineCommand     = new RelayCommand(AddOffline);
        AddMicrosoftCommand   = new AsyncRelayCommand(AddMicrosoftAsync, () => !IsMicrosoftLogging);
        RemoveCommand         = new RelayCommand(Remove,      () => SelectedAccount != null);
        SetActiveCommand      = new RelayCommand(SetActive,   () => SelectedAccount != null && !SelectedAccount.IsActive);
        CopyUsernameCommand   = new RelayCommand(CopyUsername,() => SelectedAccount != null);
        CopyUUIDCommand       = new RelayCommand(CopyUUID,    () => SelectedAccount != null);
        ImportAccountsCommand = new RelayCommand(ImportAccounts);
        ExportAccountsCommand = new RelayCommand(ExportAccounts);

        Reload();
        _main.AuthService.AccountsChanged += Reload;
    }

    private void Reload()
    {
        Accounts.Clear();
        foreach (var a in _main.AuthService.Accounts) Accounts.Add(a);
    }

    private void AddOffline()
    {
        var name = NewUsername.Trim();
        if (string.IsNullOrEmpty(name)) { StatusText = "❌ Введи никнейм"; return; }
        try
        {
            _main.AuthService.AddOfflineAccount(name);
            NewUsername = "";
            StatusText  = $"✅ Аккаунт {name} добавлен";
        }
        catch (Exception ex) { StatusText = $"❌ {ex.Message}"; }
    }

    private async Task AddMicrosoftAsync()
    {
        IsMicrosoftLogging = true;
        StatusText = "🔄 Открываем окно Microsoft...";
        try
        {
            var msAuth = new MicrosoftAuthService();
            var result = await msAuth.LoginAsync();
            await _main.AuthService.AddMicrosoftAccountAsync(
                result.AccessToken, result.RefreshToken,
                result.Username, result.UUID, result.Expiry);
            StatusText = $"✅ Вошли как {result.Username}";
        }
        catch (Exception ex) { Logger.Error(ex, "AltManagerVM"); StatusText = $"❌ {ex.Message}"; }
        finally { IsMicrosoftLogging = false; }
    }

    private void Remove()
    {
        if (SelectedAccount == null) return;
        _main.AuthService.RemoveAccount(SelectedAccount.Id);
        StatusText = "✅ Аккаунт удалён";
    }

    private void SetActive()
    {
        if (SelectedAccount == null) return;
        _main.AuthService.SetActive(SelectedAccount.Id);
        StatusText = $"✅ Активен: {SelectedAccount.Username}";
    }

    private void CopyUsername()
    {
        if (SelectedAccount == null) return;
        System.Windows.Clipboard.SetText(SelectedAccount.Username);
        StatusText = "📋 Скопировано!";
    }

    private void CopyUUID()
    {
        if (SelectedAccount == null) return;
        System.Windows.Clipboard.SetText(SelectedAccount.UUID);
        StatusText = "📋 UUID скопирован!";
    }

    private void ImportAccounts()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON Files|*.json|All Files|*.*", Title = "Импорт аккаунтов" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json     = File.ReadAllText(dlg.FileName);
            var accounts = JsonConvert.DeserializeObject<List<Account>>(json);
            if (accounts == null) return;
            foreach (var acc in accounts)
                if (acc.Type == AccountType.Offline)
                    try { _main.AuthService.AddOfflineAccount(acc.Username); } catch { }
            StatusText = $"✅ Импортировано {accounts.Count} аккаунтов";
        }
        catch (Exception ex) { StatusText = $"❌ {ex.Message}"; }
    }

    private void ExportAccounts()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "JSON Files|*.json", FileName = "hunt_accounts.json" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var safe = _main.AuthService.Accounts
                .Where(a => a.Type == AccountType.Offline)
                .Select(a => new { a.Username, a.UUID, a.Type, a.Note })
                .ToList();
            File.WriteAllText(dlg.FileName, JsonConvert.SerializeObject(safe, Formatting.Indented));
            StatusText = $"✅ Экспортировано {safe.Count} аккаунтов";
        }
        catch (Exception ex) { StatusText = $"❌ {ex.Message}"; }
    }
}