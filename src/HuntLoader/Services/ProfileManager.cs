// src/HuntLoader/Services/ProfileManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HuntLoader.Core;
using HuntLoader.Models;
using Newtonsoft.Json;

namespace HuntLoader.Services;

public class ProfileManager
{
    private List<MinecraftProfile>    _profiles  = new();
    private readonly string           _dir       = Constants.ProfilesDir;
    private readonly OptimizationService _optimizer = new();

    public IReadOnlyList<MinecraftProfile> Profiles => _profiles.AsReadOnly();

    public MinecraftProfile? ActiveProfile =>
        _profiles.FirstOrDefault(p => p.Id == AppConfig.Instance.ActiveProfileId)
        ?? _profiles.FirstOrDefault();

    public event Action?              ProfilesChanged;
    public event Action<string, double>? OnOptimizationProgress;

    public ProfileManager()
    {
        _optimizer.OnProgress += (status, percent) =>
            OnOptimizationProgress?.Invoke(status, percent);
    }

    // ── Загрузка профилей ─────────────────────────────────
    public void Load()
    {
        _profiles.Clear();
        Directory.CreateDirectory(_dir);

        foreach (var dir in Directory.GetDirectories(_dir))
        {
            var file = Path.Combine(dir, "profile.json");
            if (!File.Exists(file)) continue;
            try
            {
                var json    = File.ReadAllText(file);
                var profile = JsonConvert.DeserializeObject<MinecraftProfile>(json);
                if (profile != null) _profiles.Add(profile);
            }
            catch (Exception ex) { Logger.Error(ex, "ProfileManager"); }
        }

        if (_profiles.Count == 0) CreateDefault();
        Logger.Info($"Loaded {_profiles.Count} profiles", "ProfileManager");
    }

    // ── Создать профиль (без модов) ───────────────────────
    public MinecraftProfile Create(
        string    name,
        string    version,
        ModLoader loader = ModLoader.Vanilla,
        string    color  = "#FF6B35")
    {
        var profile = new MinecraftProfile
        {
            Name        = name,
            GameVersion = version,
            ModLoader   = loader,
            Color       = color
        };

        profile.EnsureDirectories();
        _profiles.Add(profile);
        Save(profile);
        ProfilesChanged?.Invoke();
        Logger.Info($"Created profile: {name} ({version})", "ProfileManager");
        return profile;
    }

    // ── Создать профиль + автоустановка оптимизирующих модов
    public async Task<MinecraftProfile> CreateWithOptimizationAsync(
        string            name,
        string            version,
        ModLoader         loader               = ModLoader.Vanilla,
        string            color                = "#FF6B35",
        bool              installOptimizations = true,
        CancellationToken ct                   = default)
    {
        // Создаём профиль
        var profile = Create(name, version, loader, color);

        // Устанавливаем моды если нужно и лоадер не Vanilla
        if (installOptimizations && loader != ModLoader.Vanilla)
        {
            Logger.Info($"Installing optimization pack for {name}...", "ProfileManager");
            try
            {
                await _optimizer.InstallPackAsync(profile, ct);
                Logger.Info("Optimization pack installed!", "ProfileManager");
            }
            catch (Exception ex)
            {
                Logger.Warning(
                    $"Optimization pack partial fail: {ex.Message}",
                    "ProfileManager");
            }
        }

        return profile;
    }

    // ── Оптимизировать существующий профиль ───────────────
    public async Task OptimizeProfileAsync(
        string            profileId,
        CancellationToken ct = default)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == profileId)
                      ?? throw new Exception("Профиль не найден");

        if (profile.ModLoader == ModLoader.Vanilla)
        {
            OnOptimizationProgress?.Invoke("Vanilla не поддерживает моды", 100);
            return;
        }

        await _optimizer.InstallPackAsync(profile, ct);
    }

    // ── Получить список модов оптимизации для профиля ─────
    public List<OptimizationPack> GetOptimizationPack(string profileId)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null) return new();
        return _optimizer.GetPackForProfile(profile);
    }

    // ── Остальные методы ──────────────────────────────────
    public void SetActive(string profileId)
    {
        AppConfig.Instance.ActiveProfileId = profileId;
        AppConfig.Instance.Save();
        ProfilesChanged?.Invoke();
    }

    public void Save(MinecraftProfile profile)
    {
        profile.EnsureDirectories();
        var file = Path.Combine(profile.ProfileDir, "profile.json");
        var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
        File.WriteAllText(file, json);
    }

    public void Delete(string profileId)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null) return;
        _profiles.Remove(profile);
        if (Directory.Exists(profile.ProfileDir))
            Directory.Delete(profile.ProfileDir, recursive: true);
        if (AppConfig.Instance.ActiveProfileId == profileId
            && _profiles.Count > 0)
            SetActive(_profiles[0].Id);
        ProfilesChanged?.Invoke();
        Logger.Info($"Deleted profile: {profile.Name}", "ProfileManager");
    }

    public MinecraftProfile Duplicate(string profileId)
    {
        var src = _profiles.FirstOrDefault(p => p.Id == profileId)
                  ?? throw new Exception("Profile not found");

        var copy = new MinecraftProfile
        {
            Name             = src.Name + " (копия)",
            GameVersion      = src.GameVersion,
            ModLoader        = src.ModLoader,
            ModLoaderVersion = src.ModLoaderVersion,
            MemoryMin        = src.MemoryMin,
            MemoryMax        = src.MemoryMax,
            JavaArgs         = src.JavaArgs,
            Color            = src.Color,
            Description      = src.Description,
            ResolutionWidth  = src.ResolutionWidth,
            ResolutionHeight = src.ResolutionHeight,
        };

        copy.EnsureDirectories();
        _profiles.Add(copy);
        Save(copy);
        ProfilesChanged?.Invoke();
        return copy;
    }

    public List<string> GetModsList(string profileId)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null || !Directory.Exists(profile.ModsDir)) return new();
        return Directory.GetFiles(profile.ModsDir, "*.jar")
            .Select(f => Path.GetFileName(f))
            .Where(f => f != null)
            .ToList()!;
    }

    private void CreateDefault()
    {
        var profile = Create("Default", "1.21.4");
        SetActive(profile.Id);
    }
}