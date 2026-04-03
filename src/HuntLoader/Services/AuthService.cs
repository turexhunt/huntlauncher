// src/HuntLoader/Services/AuthService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HuntLoader.Core;
using HuntLoader.Models;
using Newtonsoft.Json;

namespace HuntLoader.Services;

public class AuthService
{
    private List<Account>   _accounts = new();
    private readonly string _file     = Constants.AccountsFile;

    public IReadOnlyList<Account> Accounts     => _accounts.AsReadOnly();
    public Account? ActiveAccount              => _accounts.FirstOrDefault(a => a.IsActive);

    public event Action? AccountsChanged;

    public void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            var json  = File.ReadAllText(_file);
            _accounts = JsonConvert.DeserializeObject<List<Account>>(json) ?? new();
            Logger.Info($"Loaded {_accounts.Count} accounts", "AuthService");
        }
        catch (Exception ex) { Logger.Error(ex, "AuthService"); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Constants.AppDataRoot);
            var json = JsonConvert.SerializeObject(_accounts, Formatting.Indented);
            File.WriteAllText(_file, json);
        }
        catch (Exception ex) { Logger.Error(ex, "AuthService"); }
    }

    public Account AddOfflineAccount(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty");
        if (username.Length < 3 || username.Length > 16)
            throw new ArgumentException("Username must be 3-16 characters");
        if (_accounts.Any(a => a.Username == username && a.Type == AccountType.Offline))
            throw new Exception($"Account '{username}' already exists");

        var account = new Account
        {
            Username = username,
            Type     = AccountType.Offline,
            UUID     = GenerateOfflineUUID(username)
        };

        _accounts.Add(account);
        if (_accounts.Count == 1) SetActive(account.Id);
        Save();
        AccountsChanged?.Invoke();
        Logger.Info($"Added offline account: {username}", "AuthService");
        return account;
    }

    public async Task<Account> AddMicrosoftAccountAsync(
        string accessToken, string refreshToken,
        string username, string uuid, DateTime expiry)
    {
        var existing = _accounts.FirstOrDefault(a =>
            a.UUID == uuid && a.Type == AccountType.Microsoft);

        if (existing != null)
        {
            existing.AccessToken  = accessToken;
            existing.RefreshToken = refreshToken;
            existing.TokenExpiry  = expiry;
            existing.Username     = username;
            Save();
            AccountsChanged?.Invoke();
            return existing;
        }

        var account = new Account
        {
            Username     = username,
            UUID         = uuid,
            Type         = AccountType.Microsoft,
            AccessToken  = accessToken,
            RefreshToken = refreshToken,
            TokenExpiry  = expiry
        };

        _accounts.Add(account);
        if (!_accounts.Any(a => a.IsActive)) SetActive(account.Id);
        Save();
        AccountsChanged?.Invoke();
        Logger.Info($"Added Microsoft account: {username}", "AuthService");
        return account;
    }

    public void SetActive(string accountId)
    {
        foreach (var a in _accounts) a.IsActive = false;
        var target = _accounts.FirstOrDefault(a => a.Id == accountId);
        if (target != null)
        {
            target.IsActive = true;
            target.LastUsed = DateTime.Now;
            AppConfig.Instance.ActiveAccountId = accountId;
            AppConfig.Instance.Save();
        }
        Save();
        AccountsChanged?.Invoke();
    }

    public void RemoveAccount(string accountId)
    {
        var acc = _accounts.FirstOrDefault(a => a.Id == accountId);
        if (acc == null) return;
        _accounts.Remove(acc);
        if (acc.IsActive && _accounts.Count > 0) SetActive(_accounts[0].Id);
        Save();
        AccountsChanged?.Invoke();
        Logger.Info($"Removed account: {acc.Username}", "AuthService");
    }

    public void UpdateNote(string accountId, string note)
    {
        var acc = _accounts.FirstOrDefault(a => a.Id == accountId);
        if (acc == null) return;
        acc.Note = note;
        Save();
        AccountsChanged?.Invoke();
    }

    private static string GenerateOfflineUUID(string username)
    {
        var data = Encoding.UTF8.GetBytes("OfflinePlayer:" + username);
        var hash = MD5.HashData(data);
        hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash).ToString();
    }
}