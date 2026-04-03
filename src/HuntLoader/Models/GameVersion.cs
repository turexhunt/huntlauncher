// src/HuntLoader/Models/GameVersion.cs
using System;
using System.Collections.Generic;
using System.IO;
using HuntLoader.Core;
using Newtonsoft.Json;

namespace HuntLoader.Models;

public enum VersionType { Release, Snapshot, OldBeta, OldAlpha }

public class GameVersion
{
    [JsonProperty("id")]          public string   Id          { get; set; } = "";
    [JsonProperty("type")]        public string   TypeRaw     { get; set; } = "release";
    [JsonProperty("url")]         public string   Url         { get; set; } = "";
    [JsonProperty("releaseTime")] public DateTime ReleaseTime { get; set; }

    [JsonIgnore]
    public VersionType Type => TypeRaw switch
    {
        "release"  => VersionType.Release,
        "snapshot" => VersionType.Snapshot,
        "old_beta" => VersionType.OldBeta,
        _          => VersionType.OldAlpha
    };

    [JsonIgnore]
    public bool IsFeatured =>
        System.Array.IndexOf(Constants.FeaturedVersions, Id) >= 0;

    [JsonIgnore]
    public bool IsDownloaded =>
        Directory.Exists(Path.Combine(Constants.VersionsDir, Id));

    [JsonIgnore]
    public string TypeLabel => Type switch
    {
        VersionType.Release  => "Release",
        VersionType.Snapshot => "Snapshot",
        VersionType.OldBeta  => "Beta",
        _                    => "Alpha"
    };

    [JsonIgnore]
    public string DisplayName => IsFeatured ? $"⭐ {Id}" : Id;
}

public class VersionManifest
{
    [JsonProperty("latest")]   public LatestVersions   Latest   { get; set; } = new();
    [JsonProperty("versions")] public List<GameVersion> Versions { get; set; } = new();
}

public class LatestVersions
{
    [JsonProperty("release")]  public string Release  { get; set; } = "";
    [JsonProperty("snapshot")] public string Snapshot { get; set; } = "";
}