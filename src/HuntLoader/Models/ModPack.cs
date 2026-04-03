// src/HuntLoader/Models/ModPack.cs
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HuntLoader.Models;

public class ModPack
{
    [JsonProperty("id")]                public string         Id               { get; set; } = Guid.NewGuid().ToString();
    [JsonProperty("name")]              public string         Name             { get; set; } = "";
    [JsonProperty("version")]           public string         Version          { get; set; } = "1.0.0";
    [JsonProperty("gameVersion")]       public string         GameVersion      { get; set; } = "1.21.4";
    [JsonProperty("modLoader")]         public ModLoader      ModLoader        { get; set; } = ModLoader.Fabric;
    [JsonProperty("modLoaderVersion")]  public string         ModLoaderVersion { get; set; } = "";
    [JsonProperty("description")]       public string         Description      { get; set; } = "";
    [JsonProperty("author")]            public string         Author           { get; set; } = "";
    [JsonProperty("mods")]              public List<ModPackEntry> Mods         { get; set; } = new();
    [JsonProperty("iconUrl")]           public string         IconUrl          { get; set; } = "";
    [JsonProperty("recommendedMemory")] public int            RecommendedMemory { get; set; } = 4096;
}

public class ModPackEntry
{
    [JsonProperty("projectId")]   public string ProjectId   { get; set; } = "";
    [JsonProperty("name")]        public string Name        { get; set; } = "";
    [JsonProperty("version")]     public string Version     { get; set; } = "";
    [JsonProperty("required")]    public bool   Required    { get; set; } = true;
    [JsonProperty("downloadUrl")] public string DownloadUrl { get; set; } = "";
}