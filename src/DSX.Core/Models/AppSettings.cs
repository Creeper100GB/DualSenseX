using System.Collections.Generic;
using DSX.Core.Enums;

namespace DSX.Core.Models;

public class AppSettings
{
    public string AppVersion { get; set; } = "3.1.5.3";
    public WindowEffect WindowEffect { get; set; } = WindowEffect.Mica;
    public bool DarkMode { get; set; } = true;
    public string Language { get; set; } = "en-US";
    public bool AutoConnectOnLaunch { get; set; } = true;
    public bool KillSteamOnLaunch { get; set; }
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool ToggleMicMuteViaDualSense { get; set; } = true;
    public bool MotionControlsEnabled { get; set; } = true;
    public bool NotificationsControllerConnection { get; set; } = true;
    public bool NotificationsProfileSwitch { get; set; } = true;
    public bool NotificationsBatteryLow { get; set; } = true;
    public string LastActiveProfileId { get; set; } = string.Empty;
    public int UdpPort { get; set; } = 6969;
    public bool LetDSXControlHidHide { get; set; } = true;
    public bool PersistentDeviceHiding { get; set; }
    public bool FirstLaunch { get; set; } = true;
    public bool IsDSXPlusOwner { get; set; }
    public string ControllerViewSkin { get; set; } = "Default";
    public string BackupServerRegion { get; set; } = string.Empty;
    public Dictionary<string, string> PerGameProfiles { get; set; } = new();
    public bool ResetProfileOnGameClose { get; set; }
}

public class OfflineCache
{
    public DateTime LastVerified { get; set; }
    public bool IsDSXPlusOwner { get; set; }
    public string MachineId { get; set; } = string.Empty;
}
