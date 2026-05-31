using System.IO;

namespace DSX.Core.Constants;

public static class AppConstants
{
    public const string AppDataFolder = "DSX";
    public const string SettingsFileName = "DSX_Settings.json";
    public const string UdpPortFileName = "DSX_UDP_PortNumber.txt";
    public const string OfflineCacheFileName = "DSX_OfflineCache.json";
    public const string ProfilesFolderName = "Profiles";
    public const string BackupsFolderName = "Backups";
    public const string LogsFolderName = "Logs";
    public const string LanguagesFolderName = "Languages";

    public const int DefaultUDPPort = 6969;
    public const int OfflineCacheDays = 28;
    public const int LauncherTimeoutMinutes = 1;

    public const int ProfileSlotCount = 4;

    public const ushort SonyVID = 0x054C;
    public const ushort DualSensePID = 0x0CE6;
    public const ushort DualSenseEdgePID = 0x0DF2;
    public const ushort DualShock4V1PID = 0x05C4;
    public const ushort DualShock4V2PID = 0x09CC;

    public const int DualSenseUSBReportSize = 64;
    public const int DualSenseBTReportSize = 78;
    public const int DualShock4ReportSize = 64;

    public const string HidHideRegistryPath = @"SOFTWARE\HidHide";
    public const string HidHideWhitelistPath = @"SOFTWARE\HidHide\Whitelist";
    public const string HidHideConfigPath = @"SOFTWARE\HidHide\Config";

    public static readonly string[] SupportedLanguages =
    [
        "en-US",
        "ar-SA",
        "bg-BG",
        "cs-CZ",
        "da-DK",
        "de-DE",
        "el-GR",
        "es-ES",
        "fi-FI",
        "fr-FR",
        "he-IL",
        "hu-HU",
        "it-IT",
        "ja-JP",
        "ko-KR",
        "nl-NL",
        "no-NO",
        "pl-PL",
        "pt-BR",
        "pt-PT",
        "ro-RO",
        "ru-RU",
        "sk-SK",
        "sv-SE",
        "th-TH",
        "zh-CN",
        "zh-TW"
    ];

    public static string LocalAppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppDataFolder);

    public static string ProfilesPath => Path.Combine(LocalAppDataPath, ProfilesFolderName);
    public static string BackupsPath => Path.Combine(LocalAppDataPath, BackupsFolderName);
    public static string LogsPath => Path.Combine(LocalAppDataPath, LogsFolderName);
    public static string LanguagesPath => Path.Combine(LocalAppDataPath, LanguagesFolderName);
    public static string SettingsFilePath => Path.Combine(LocalAppDataPath, SettingsFileName);
    public static string UdpPortFilePath => Path.Combine(LocalAppDataPath, UdpPortFileName);
    public static string OfflineCacheFilePath => Path.Combine(LocalAppDataPath, OfflineCacheFileName);
}
