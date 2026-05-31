using DSX.Core.Enums;

namespace DSX.Core.Models;

public class DSXPacket
{
    public DSXInstruction[] Instructions { get; set; } = [];
}

public class DSXInstruction
{
    public int Type { get; set; }
    public int[] Parameters { get; set; } = [];
}

public class Notification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsRead { get; set; }
}

public class BackupInfo
{
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public long SizeBytes { get; set; }
    public string FilePath { get; set; } = string.Empty;
}

public class EdgeOnBoardProfile
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public EdgeButtonAssignments ButtonAssignments { get; set; } = new();
    public EdgeStickConfig LeftStickConfig { get; set; } = new();
    public EdgeStickConfig RightStickConfig { get; set; } = new();
    public double TriggerDeadzoneL2 { get; set; }
    public double TriggerDeadzoneR2 { get; set; }
    public bool ApplySameTriggerSettings { get; set; }
    public VibrationIntensity VibrationIntensity { get; set; } = VibrationIntensity.Strong;
    public VibrationIntensity TriggerEffectIntensity { get; set; } = VibrationIntensity.Strong;
    public bool DisableProfileSwitching { get; set; }
}

public class EdgeButtonAssignments
{
    public bool CircleEnabled { get; set; } = true;
    public bool CrossEnabled { get; set; } = true;
    public bool TouchpadFingerTouch { get; set; } = true;
    public bool LeftStickEnabled { get; set; } = true;
    public bool RightStickEnabled { get; set; } = true;
    public bool SwapSticks { get; set; }
}

public class EdgeStickConfig
{
    public CurveType CurveType { get; set; } = CurveType.Linear;
    public double Deadzone { get; set; }
    public Dictionary<double, double> CustomCurvePoints { get; set; } = new();
}
