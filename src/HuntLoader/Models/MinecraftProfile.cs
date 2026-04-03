// src/HuntLoader/Models/MinecraftProfile.cs
using System;
using System.IO;
using System.Runtime.InteropServices;
using HuntLoader.Core;
using Newtonsoft.Json;

namespace HuntLoader.Models;

public enum ModLoader
{
    Vanilla,
    Forge,
    Fabric,
    Quilt,
    NeoForge
}

public class MinecraftProfile
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public string Name { get; set; } = "Default";

    [JsonProperty("gameVersion")]
    public string GameVersion { get; set; } = "1.21.4";

    [JsonProperty("modLoader")]
    public ModLoader ModLoader { get; set; } = ModLoader.Vanilla;

    [JsonProperty("modLoaderVersion")]
    public string ModLoaderVersion { get; set; } = "";

    [JsonProperty("javaPath")]
    public string JavaPath { get; set; } = "";

    [JsonProperty("javaArgs")]
    public string JavaArgs { get; set; } = "";

    [JsonProperty("memoryMin")]
    public int MemoryMin { get; set; } = 512;

    [JsonProperty("memoryMax")]
    public int MemoryMax { get; set; } = 2048;

    [JsonProperty("resolutionWidth")]
    public int ResolutionWidth { get; set; } = 1280;

    [JsonProperty("resolutionHeight")]
    public int ResolutionHeight { get; set; } = 720;

    [JsonProperty("fullscreen")]
    public bool Fullscreen { get; set; } = false;

    [JsonProperty("customIcon")]
    public string CustomIcon { get; set; } = "";

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonProperty("lastPlayed")]
    public DateTime LastPlayed { get; set; } = DateTime.MinValue;

    [JsonProperty("totalPlayTime")]
    public TimeSpan TotalPlayTime { get; set; } = TimeSpan.Zero;

    [JsonProperty("color")]
    public string Color { get; set; } = "#FF6B35";

    // ── Авто-оптимизация памяти ───────────────────────────
    [JsonIgnore]
    public int RecommendedMemoryMax
    {
        get
        {
            try
            {
                // P/Invoke для получения RAM без Microsoft.VisualBasic
                var status = new MemoryStatusEx();
                status.dwLength = (uint)Marshal.SizeOf(status);
                GlobalMemoryStatusEx(ref status);
                var totalRamMb = (long)(status.ullTotalPhys / 1024 / 1024);

                return totalRamMb switch
                {
                    >= 32768 => 8192,  // 32GB+ → 8GB
                    >= 16384 => 4096,  // 16GB  → 4GB
                    >= 8192  => 3072,  // 8GB   → 3GB
                    >= 4096  => 2048,  // 4GB   → 2GB
                    _        => 1024   // < 4GB → 1GB
                };
            }
            catch
            {
                return 2048; // fallback
            }
        }
    }

    // ── P/Invoke для RAM ──────────────────────────────────
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint  dwLength;
        public uint  dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    // ── Директории ────────────────────────────────────────
    [JsonIgnore]
    public string ProfileDir => Path.Combine(Constants.ProfilesDir, Id);

    [JsonIgnore]
    public string ModsDir => Path.Combine(ProfileDir, "mods");

    [JsonIgnore]
    public string ResourcePacksDir => Path.Combine(ProfileDir, "resourcepacks");

    [JsonIgnore]
    public string SavesDir => Path.Combine(ProfileDir, "saves");

    [JsonIgnore]
    public string ShaderPacksDir => Path.Combine(ProfileDir, "shaderpacks");

    [JsonIgnore]
    public string ScreenshotsDir => Path.Combine(ProfileDir, "screenshots");

    [JsonIgnore]
    public string ConfigDir => Path.Combine(ProfileDir, "config");

    // ── UI свойства ───────────────────────────────────────
    [JsonIgnore]
    public string PlayTimeFormatted
    {
        get => TotalPlayTime.TotalHours >= 1
            ? $"{(int)TotalPlayTime.TotalHours}ч {TotalPlayTime.Minutes}м"
            : $"{TotalPlayTime.Minutes}м";
        set { /* computed */ }
    }

    [JsonIgnore]
    public string ModLoaderLabel
    {
        get => ModLoader == ModLoader.Vanilla
            ? GameVersion
            : $"{GameVersion} - {ModLoader} {ModLoaderVersion}";
        set { /* computed */ }
    }

    // ── Создать все папки профиля ─────────────────────────
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(ProfileDir);
        Directory.CreateDirectory(ModsDir);
        Directory.CreateDirectory(ResourcePacksDir);
        Directory.CreateDirectory(SavesDir);
        Directory.CreateDirectory(ShaderPacksDir);
        Directory.CreateDirectory(ScreenshotsDir);
        Directory.CreateDirectory(ConfigDir);
    }

    // ── Применить рекомендуемую память ───────────────────
    public void ApplyRecommendedMemory()
    {
        MemoryMax = RecommendedMemoryMax;
        MemoryMin = Math.Min(512, MemoryMax / 4);
    }
}