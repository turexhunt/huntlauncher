// src/HuntLoader/Services/OfflineAuthService.cs
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HuntLoader.Models;

namespace HuntLoader.Services;

public static class OfflineAuthService
{
    public static Account CreateOfflineAccount(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Имя не может быть пустым");
        if (username.Length < 3 || username.Length > 16)
            throw new ArgumentException("Ник должен быть от 3 до 16 символов");
        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
            throw new ArgumentException("Только латинские буквы, цифры и _");

        return new Account
        {
            Username    = username,
            Type        = AccountType.Offline,
            UUID        = GenerateOfflineUUID(username),
            AccessToken = "0",
            AddedAt     = DateTime.Now
        };
    }

    private static string GenerateOfflineUUID(string username)
    {
        var data = Encoding.UTF8.GetBytes("OfflinePlayer:" + username);
        var hash = MD5.HashData(data);
        hash[6]  = (byte)((hash[6] & 0x0f) | 0x30);
        hash[8]  = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash).ToString();
    }
}