// src/HuntLoader/Models/LaunchSettings.cs
using Newtonsoft.Json;

namespace HuntLoader.Models;

public class LaunchSettings
{
    [JsonProperty("profileId")]
    public string ProfileId { get; set; } = "";

    [JsonProperty("accountId")]
    public string AccountId { get; set; } = "";

    [JsonProperty("additionalArgs")]
    public string AdditionalArgs { get; set; } = "";

    [JsonProperty("serverIp")]
    public string ServerIp { get; set; } = "";

    [JsonProperty("serverPort")]
    public int ServerPort { get; set; } = 25565;

    [JsonProperty("autoConnect")]
    public bool AutoConnect { get; set; } = false;

    [JsonProperty("demoMode")]
    public bool DemoMode { get; set; } = false;

    public string GetServerArgs() =>
        !AutoConnect || string.IsNullOrEmpty(ServerIp)
            ? ""
            : $"--server {ServerIp} --port {ServerPort}";
}