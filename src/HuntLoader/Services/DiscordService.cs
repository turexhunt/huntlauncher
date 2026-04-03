// src/HuntLoader/Services/DiscordService.cs
using System;
using DiscordRPC;
using DiscordRPC.Logging;
using HuntLoader.Core;

namespace HuntLoader.Services;

public class DiscordService
{
    private const string AppId = "1488578627225387241";

    private DiscordRpcClient? _client;
    private bool _enabled;
    private bool _ready;

    public void Initialize()
    {
        if (_client != null) return;

        try
        {
            _enabled = true;
            _client  = new DiscordRpcClient(AppId)
            {
                Logger = new NullLogger()
            };

            _client.OnReady += (_, e) =>
            {
                _ready = true;
                Logger.Info($"[Discord] Ready: {e.User.Username}", "Discord");
                SetIdle();
            };

            _client.OnError += (_, e) =>
                Logger.Error($"[Discord] Error: {e.Message}", "Discord");

            _client.OnConnectionFailed += (_, _) =>
                Logger.Warning("[Discord] Discord не запущен", "Discord");

            _client.Initialize();
            Logger.Info("[Discord] RPC инициализирован", "Discord");
        }
        catch (Exception ex)
        {
            Logger.Error($"[Discord] Init failed: {ex.Message}", "Discord");
        }
    }

    public void Disable()
    {
        _enabled = false;
        _ready   = false;
        try
        {
            _client?.ClearPresence();
            _client?.Dispose();
            _client = null;
        }
        catch { }
    }

    // ── Статусы ────────────────────────────────────────────

    public void SetIdle()
    {
        Set(new RichPresence
        {
            Details    = "В лаунчере",
            State      = "Выбирает версию...",
            Timestamps = Timestamps.Now,
            Assets     = new Assets
            {
                LargeImageKey  = "minecraft",
                LargeImageText = "Minecraft",
                SmallImageKey  = "huntloader",
                SmallImageText = "Hunt Loader"
            },
            Buttons =
            [
                new Button { Label = "Discord сервер", Url = Constants.DiscordUrl }
            ]
        });
    }

    public void SetLaunching(string version)
    {
        Set(new RichPresence
        {
            Details    = "Запускает Minecraft",
            State      = $"Версия {version}",
            Timestamps = Timestamps.Now,
            Assets     = new Assets
            {
                LargeImageKey  = "minecraft",
                LargeImageText = $"Minecraft {version}",
                SmallImageKey  = "huntloader",
                SmallImageText = "Hunt Loader"
            },
            Buttons =
            [
                new Button { Label = "Discord сервер", Url = Constants.DiscordUrl }
            ]
        });
    }

    public void SetPlaying(string version, string username)
    {
        Set(new RichPresence
        {
            Details    = $"Играет как {username}",
            State      = $"Minecraft {version}",
            Timestamps = Timestamps.Now,
            Assets     = new Assets
            {
                LargeImageKey  = "minecraft",
                LargeImageText = $"Minecraft {version}",
                SmallImageKey  = "huntloader",
                SmallImageText = "Hunt Loader"
            },
            Buttons =
            [
                new Button { Label = "Discord сервер", Url = Constants.DiscordUrl }
            ]
        });
    }

    public void SetGameClosed() => SetIdle();

    private void Set(RichPresence presence)
    {
        if (!_enabled || _client == null || !_ready) return;
        try { _client.SetPresence(presence); }
        catch (Exception ex)
        {
            Logger.Error($"[Discord] SetPresence: {ex.Message}", "Discord");
        }
    }

    public void Dispose() => Disable();
}