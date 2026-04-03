// src/HuntLoader/Services/MicrosoftAuthService.cs
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using HuntLoader.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HuntLoader.Services;

public class MicrosoftAuthResult
{
    public string   Username     { get; set; } = "";
    public string   UUID         { get; set; } = "";
    public string   AccessToken  { get; set; } = "";
    public string   RefreshToken { get; set; } = "";
    public DateTime Expiry       { get; set; }
}

public class MicrosoftAuthService
{
    private readonly HttpClient _http;

    public MicrosoftAuthService()
    {
        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromMinutes(10);
        _http.DefaultRequestHeaders.Add("User-Agent", $"HuntLoader/{Constants.LauncherVersion}");
    }

    public async Task<MicrosoftAuthResult> LoginAsync()
    {
        try
        {
            var deviceResp = await GetDeviceCodeAsync();
            var msg = $"Открой в браузере:\n{deviceResp.VerificationUri}\n\nВведи код: {deviceResp.UserCode}\n\nБраузер откроется автоматически.";
            MessageBox.Show(msg, "Вход Microsoft", MessageBoxButton.OK, MessageBoxImage.Information);

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = deviceResp.VerificationUri,
                    UseShellExecute = true
                });
            }
            catch { }

            Logger.Info($"MS Auth: {deviceResp.VerificationUri} | Code: {deviceResp.UserCode}", "MsAuth");

            var msToken = await PollForTokenAsync(deviceResp.DeviceCode, deviceResp.Interval);
            var xblToken  = await GetXboxLiveTokenAsync(msToken.AccessToken);
            var xstsToken = await GetXstsTokenAsync(xblToken.Token);
            var mcToken   = await GetMinecraftTokenAsync(xblToken.UserHash, xstsToken.Token);

            var hasLicense = await CheckMinecraftLicenseAsync(mcToken.AccessToken);
            if (!hasLicense)
            {
                Logger.Warning("No Minecraft license", "MsAuth");
            }

            var profile = await GetMinecraftProfileAsync(mcToken.AccessToken);

            Logger.Info($"MS Auth success: {profile.Name}", "MsAuth");

            return new MicrosoftAuthResult
            {
                Username     = profile.Name,
                UUID         = profile.Id,
                AccessToken  = mcToken.AccessToken,
                RefreshToken = msToken.RefreshToken,
                Expiry       = DateTime.UtcNow.AddSeconds(mcToken.ExpiresIn)
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "MsAuth");
            throw; // Пробрасываем ошибку выше чтобы показать её пользователю
        }
    }

    private async Task<DeviceCodeResponse> GetDeviceCodeAsync()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = Constants.MsClientId,
            ["scope"]     = "XboxLive.signin offline_access"
        });

        var resp = await _http.PostAsync(
            "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode",
            content);

        var json = await resp.Content.ReadAsStringAsync();
        Logger.Debug($"DeviceCode response: {json}", "MsAuth");

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Ошибка авторизации Microsoft: {resp.StatusCode}");

        return JsonConvert.DeserializeObject<DeviceCodeResponse>(json)
               ?? throw new Exception("Не удалось получить код устройства");
    }

    private async Task<MsTokenResponse> PollForTokenAsync(string deviceCode, int interval)
    {
        var timeout = DateTime.UtcNow.AddMinutes(10);
        var delay   = Math.Max(interval, 5) * 1000;

        while (DateTime.UtcNow < timeout)
        {
            await Task.Delay(delay);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]  = "urn:ietf:params:oauth:grant-type:device_code",
                ["client_id"]   = Constants.MsClientId,
                ["device_code"] = deviceCode
            });

            var resp = await _http.PostAsync(
                "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
                content);

            var json = await resp.Content.ReadAsStringAsync();
            var obj  = JObject.Parse(json);

            if (obj["access_token"] != null)
                return JsonConvert.DeserializeObject<MsTokenResponse>(json)
                       ?? throw new Exception("Неверный ответ токена");

            var error = obj["error"]?.ToString();
            Logger.Debug($"Poll error: {error}", "MsAuth");

            switch (error)
            {
                case "authorization_pending": continue;
                case "slow_down":             delay += 5000; continue;
                case "authorization_declined": throw new Exception("Вход отклонён пользователем");
                case "expired_token":          throw new Exception("Время ожидания истекло. Попробуй снова");
                default:
                    if (error != null)
                        throw new Exception($"Ошибка: {error} - {obj["error_description"]}");
                    break;
            }
        }

        throw new TimeoutException("Время ожидания входа истекло (10 минут)");
    }

    private async Task<XboxToken> GetXboxLiveTokenAsync(string msAccessToken)
    {
        var body = new
        {
            Properties = new
            {
                AuthMethod = "RPS",
                SiteName   = "user.auth.xboxlive.com",
                RpsTicket  = $"d={msAccessToken}"
            },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType    = "JWT"
        };

        var req = new HttpRequestMessage(HttpMethod.Post, Constants.XboxAuthUrl)
        {
            Content = new StringContent(
                JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Accept", "application/json");

        var resp = await _http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Ошибка Xbox Live: {resp.StatusCode}\n{json}");

        var obj = JObject.Parse(json);
        return new XboxToken(
            obj["Token"]?.ToString() ?? throw new Exception("Нет Xbox токена"),
            obj["DisplayClaims"]?["xui"]?[0]?["uhs"]?.ToString() ?? ""
        );
    }

    private async Task<XboxToken> GetXstsTokenAsync(string xblToken)
    {
        var body = new
        {
            Properties = new
            {
                SandboxId  = "RETAIL",
                UserTokens = new[] { xblToken }
            },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType    = "JWT"
        };

        var req = new HttpRequestMessage(HttpMethod.Post, Constants.XstsAuthUrl)
        {
            Content = new StringContent(
                JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Accept", "application/json");

        var resp = await _http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        var obj  = JObject.Parse(json);

        if (obj["XErr"] != null)
        {
            var xerr = obj["XErr"]!.ToString();
            throw new Exception(xerr switch
            {
                "2148916233" => "Нет аккаунта Xbox Live.\nСоздай его на xbox.com",
                "2148916235" => "Xbox Live недоступен в твоей стране",
                "2148916236" or "2148916237" => "Требуется подтверждение взрослого возраста",
                "2148916238" => "Аккаунт детский, нужно разрешение родителей",
                _            => $"Ошибка Xbox XSTS: {xerr}"
            });
        }

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Ошибка XSTS: {resp.StatusCode}\n{json}");

        return new XboxToken(
            obj["Token"]?.ToString() ?? throw new Exception("Нет XSTS токена"),
            obj["DisplayClaims"]?["xui"]?[0]?["uhs"]?.ToString() ?? ""
        );
    }

    private async Task<McTokenResponse> GetMinecraftTokenAsync(
        string userHash, string xstsToken)
    {
        var body = new
        {
            identityToken = $"XBL3.0 x={userHash};{xstsToken}"
        };

        var req = new HttpRequestMessage(HttpMethod.Post, Constants.McAuthUrl)
        {
            Content = new StringContent(
                JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
        };

        var resp = await _http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Ошибка Minecraft Auth: {resp.StatusCode}\n{json}");

        return JsonConvert.DeserializeObject<McTokenResponse>(json)
               ?? throw new Exception("Неверный ответ Minecraft Auth");
    }

    private async Task<bool> CheckMinecraftLicenseAsync(string accessToken)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                "https://api.minecraftservices.com/entitlements/mcstore");
            req.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            var obj  = JObject.Parse(json);
            var items = obj["items"] as JArray;
            return items != null && items.Count > 0;
        }
        catch { return false; }
    }

    private async Task<McProfile> GetMinecraftProfileAsync(string accessToken)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, Constants.McProfileUrl);
        req.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var resp = await _http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Нет профиля Minecraft. Возможно аккаунт не купил игру.\nКод: {resp.StatusCode}");

        return JsonConvert.DeserializeObject<McProfile>(json)
               ?? throw new Exception("Не удалось получить профиль");
    }

    private class DeviceCodeResponse
    {
        [JsonProperty("device_code")]      public string DeviceCode      { get; set; } = "";
        [JsonProperty("user_code")]        public string UserCode        { get; set; } = "";
        [JsonProperty("verification_uri")] public string VerificationUri { get; set; } = "";
        [JsonProperty("interval")]         public int    Interval        { get; set; } = 5;
    }

    private class MsTokenResponse
    {
        [JsonProperty("access_token")]  public string AccessToken  { get; set; } = "";
        [JsonProperty("refresh_token")] public string RefreshToken { get; set; } = "";
        [JsonProperty("expires_in")]    public int    ExpiresIn    { get; set; }
    }

    private class XboxToken
    {
        public string Token    { get; }
        public string UserHash { get; }
        public XboxToken(string token, string userHash)
        { Token = token; UserHash = userHash; }
    }

    private class McTokenResponse
    {
        [JsonProperty("access_token")] public string AccessToken { get; set; } = "";
        [JsonProperty("expires_in")]   public int    ExpiresIn   { get; set; }
    }

    private class McProfile
    {
        [JsonProperty("id")]   public string Id   { get; set; } = "";
        [JsonProperty("name")] public string Name { get; set; } = "";
    }
}