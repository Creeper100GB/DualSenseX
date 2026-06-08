using DSX.Core.Enums;

namespace DSX.Core.Models;

public class TriggerConfig
{
    public TriggerMode Mode { get; set; } = TriggerMode.Off;
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }
    public int Force { get; set; }
    public int Amplitude { get; set; }
    public int Frequency { get; set; }
    public int[] CustomBytes { get; set; } = new int[7];
    public int[] MultiplePositionForces { get; set; } = new int[10];
}

public class TriggerPresetConfig
{
    public TriggerPreset Preset { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public TriggerConfig LeftTrigger { get; init; } = new();
    public TriggerConfig RightTrigger { get; init; } = new();
}

public class TouchpadLEDConfig
{
    public LEDMode Mode { get; set; } = LEDMode.Off;
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }
    public RainbowSpeed RainbowSpeed { get; set; } = RainbowSpeed.Medium;
    public double BreathingSpeed { get; set; } = 50;
    public double StrobingSpeed { get; set; } = 50;
    public byte BatteryLowRed { get; set; } = 255;
    public byte BatteryLowGreen { get; set; }
    public byte BatteryLowBlue { get; set; }
    public byte BatteryMedRed { get; set; } = 255;
    public byte BatteryMedGreen { get; set; } = 255;
    public byte BatteryMedBlue { get; set; }
    public byte BatteryFullRed { get; set; }
    public byte BatteryFullGreen { get; set; } = 255;
    public byte BatteryFullBlue { get; set; }
}

public class PlayerLEDConfig
{
    public PlayerLEDMode Mode { get; set; } = PlayerLEDMode.Off;
    public bool FlashWhileCharging { get; set; }
}

public class MuteLEDConfig
{
    public MuteLEDMode Mode { get; set; } = MuteLEDMode.FollowMicMuteState;
    public byte MutedRed { get; set; } = 255;
    public byte MutedGreen { get; set; }
    public byte MutedBlue { get; set; }
    public byte UnmutedRed { get; set; }
    public byte UnmutedGreen { get; set; } = 255;
    public byte UnmutedBlue { get; set; }
}

public class RumbleConfig
{
    public double LargeMotorIntensity { get; set; }
    public double SmallMotorIntensity { get; set; }
}

public class AudioHapticsConfig
{
    public bool Enabled { get; set; }
    public int DelayMs { get; set; } = 5;
    public double LeftMotorVolume { get; set; } = 50;
    public double RightMotorVolume { get; set; } = 50;
    public double HeadsetVolume { get; set; } = 50;
    public double SpeakerVolume { get; set; } = 50;
    public bool SyncWithSystemVolume { get; set; } = true;
    public VolumeSyncMode VolumeSyncMode { get; set; } = VolumeSyncMode.Stereo;
    public string SelectedDeviceId { get; set; } = string.Empty;
}

public class BTHapticsConfig
{
    public AudioSourceMode AudioSourceMode { get; set; } = AudioSourceMode.SoundCapture;
    public HapticsSourceMode HapticsSourceMode { get; set; } = HapticsSourceMode.SoundWaves;
    public double LeftMotorIntensity { get; set; } = 50;
    public double RightMotorIntensity { get; set; } = 50;
    public int LatencyCompensationMs { get; set; }
    public string AudioFilePath { get; set; } = string.Empty;
    public string HapticsPreset { get; set; } = "Balanced";
}

public static class HapticsPresets
{
    public const string Balanced = "Balanced";
    public const string BassHeavy = "Bass Heavy";
    public const string Subtle = "Subtle";
    public const string Intense = "Intense";
    public const string MusicLive = "Music Live";

    public static (double left, double right, HapticsSourceMode mode) GetPreset(string preset) => preset switch
    {
        BassHeavy => (80, 60, HapticsSourceMode.SoundWaves),
        Subtle => (30, 30, HapticsSourceMode.SoundWaves),
        Intense => (100, 100, HapticsSourceMode.SystemCapture),
        MusicLive => (70, 80, HapticsSourceMode.SoundWaves),
        _ => (50, 50, HapticsSourceMode.SoundWaves)
    };
}
