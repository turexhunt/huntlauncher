// src/HuntLoader/Models/Account.cs
using System;
using Newtonsoft.Json;

namespace HuntLoader.Models;

public enum AccountType { Microsoft, Offline, ElyBy }

public class Account
{
    [JsonProperty("id")]           public string      Id           { get; set; } = Guid.NewGuid().ToString();
    [JsonProperty("type")]         public AccountType Type         { get; set; } = AccountType.Offline;
    [JsonProperty("username")]     public string      Username     { get; set; } = "";
    [JsonProperty("uuid")]         public string      UUID         { get; set; } = Guid.NewGuid().ToString("N");
    [JsonProperty("accessToken")]  public string      AccessToken  { get; set; } = "0";
    [JsonProperty("refreshToken")] public string      RefreshToken { get; set; } = "";
    [JsonProperty("tokenExpiry")]  public DateTime    TokenExpiry  { get; set; } = DateTime.MinValue;
    [JsonProperty("email")]        public string      Email        { get; set; } = "";
    [JsonProperty("skinUrl")]      public string      SkinUrl      { get; set; } = "";
    [JsonProperty("capeUrl")]      public string      CapeUrl      { get; set; } = "";
    [JsonProperty("isActive")]     public bool        IsActive     { get; set; } = false;
    [JsonProperty("addedAt")]      public DateTime    AddedAt      { get; set; } = DateTime.Now;
    [JsonProperty("lastUsed")]     public DateTime    LastUsed     { get; set; } = DateTime.Now;
    [JsonProperty("note")]         public string      Note         { get; set; } = "";

    [JsonIgnore] public bool IsLicensed  => Type == AccountType.Microsoft || Type == AccountType.ElyBy;
    [JsonIgnore] public bool IsTokenValid => TokenExpiry > DateTime.UtcNow.AddMinutes(5);

    [JsonIgnore]
    public string DisplayName => Type switch
    {
        AccountType.Microsoft => $"⭐ {Username}",
        AccountType.ElyBy     => $"🔵 {Username}",
        _                     => $"👤 {Username}"
    };

    [JsonIgnore]
    public string TypeLabel => Type switch
    {
        AccountType.Microsoft => "Microsoft",
        AccountType.ElyBy     => "Ely.By",
        _                     => "Offline"
    };

    public string GetEffectiveToken() =>
        Type == AccountType.Offline ? "0" : AccessToken;
}