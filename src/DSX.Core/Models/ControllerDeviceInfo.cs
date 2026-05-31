using System.Collections.Generic;
using DSX.Core.Enums;

namespace DSX.Core.Models;

public class ControllerDeviceInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ControllerType Type { get; set; }
    public ConnectionType Connection { get; set; }
    public string FirmwareVersion { get; set; } = string.Empty;
    public string BuildDate { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public double BatteryPercentage { get; set; }
    public BatteryStatus BatteryStatus { get; set; }
    public bool IsCharging { get; set; }
    public int BTRSSI { get; set; }
    public bool IsConnected { get; set; }
    public ControllerFeatures Features { get; set; } = new();
    public ProfileSlot[] ProfileSlots { get; set; } = new ProfileSlot[4];
}

public class ControllerFeatures
{
    public bool AdaptiveTriggers { get; set; }
    public bool Haptics { get; set; }
    public bool Touchpad { get; set; }
    public bool PlayerLED { get; set; }
    public bool MotionControls { get; set; }
    public bool MuteButton { get; set; }
    public bool LightBar { get; set; }
    public bool Gyroscope { get; set; }
    public bool Microphone { get; set; }
    public bool AudioJack { get; set; }
}
