// src/HuntLoader/Core/Constants.cs
using System;
using System.IO;

namespace HuntLoader.Core;

public static class Constants
{
    public const string LauncherName    = "Hunt Loader";
    public const string LauncherVersion = "2.0.0"; 
    public const string LauncherAuthor  = "HuntTeam";
    public const string DiscordUrl      = "https://discord.gg/UTe3vvSrq4";
    public const string WebsiteUrl      = "";

    public static readonly string AppDataRoot =
        Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData), ".huntloader");

    public static readonly string ProfilesDir  = Path.Combine(AppDataRoot, "profiles");
    public static readonly string VersionsDir  = Path.Combine(AppDataRoot, "versions");
    public static readonly string JavaDir      = Path.Combine(AppDataRoot, "java");
    public static readonly string LogsDir      = Path.Combine(AppDataRoot, "logs");
    public static readonly string ConfigFile   = Path.Combine(AppDataRoot, "config.json");
    public static readonly string AccountsFile = Path.Combine(AppDataRoot, "accounts.json");
    public static readonly string AssetsDir    = Path.Combine(AppDataRoot, "assets");
    public static readonly string LibrariesDir = Path.Combine(AppDataRoot, "libraries");
    public static readonly string TempDir      = Path.Combine(AppDataRoot, "temp");

    public const string VersionManifestUrl =
        "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";
    public const string AssetsBaseUrl =
        "https://resources.download.minecraft.net/";
    public const string LibrariesBaseUrl =
        "https://libraries.minecraft.net/";

    public const string MsClientId    = "00000000402b5328";
    public const string MsRedirectUri =
        "https://login.microsoftonline.com/common/oauth2/nativeclient";
    public const string XboxAuthUrl   =
        "https://user.auth.xboxlive.com/user/authenticate";
    public const string XstsAuthUrl   =
        "https://xsts.auth.xboxlive.com/xsts/authorize";
    public const string McAuthUrl     =
        "https://api.minecraftservices.com/authentication/login_with_xbox";
    public const string McProfileUrl  =
        "https://api.minecraftservices.com/minecraft/profile";

    public static readonly string[] FeaturedVersions =
    {
        "1.21.4", "1.21.3", "1.21.1", "1.20.4",
        "1.20.1", "1.19.4", "1.18.2", "1.17.1",
        "1.16.5", "1.12.2", "1.8.9"
    };

    public const string JavaDownloadUrl =
        "https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jdk/hotspot/normal/eclipse";
    public const int DefaultJavaHeapMin = 512;
    public const int DefaultJavaHeapMax = 2048;

    public const string DefaultTheme    = "Dark";
    public const string DefaultAccent   = "#FF6B35";
    public const string SecondaryColor  = "#1A1A2E";
    public const string BackgroundColor = "#0F0F1A";
}