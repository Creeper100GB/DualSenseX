using System.Collections.Generic;
using DSX.Core.Enums;

namespace DSX.Core.Models;

public class StickMappingConfig
{
    public bool Enabled { get; set; }
    public StickMappingMode Mode { get; set; } = StickMappingMode.WASD;
    public bool IsEightDirectional { get; set; }
    public string UpKey { get; set; } = "W";
    public string DownKey { get; set; } = "S";
    public string LeftKey { get; set; } = "A";
    public string RightKey { get; set; } = "D";
    public string UpLeftKey { get; set; } = string.Empty;
    public string UpRightKey { get; set; } = string.Empty;
    public string DownLeftKey { get; set; } = string.Empty;
    public string DownRightKey { get; set; } = string.Empty;
    public double Sensitivity { get; set; } = 50;
    public double Deadzone { get; set; } = 10;
    public bool MapToMouse { get; set; }
    public double MouseSensitivity { get; set; } = 50;
    public bool MapToButtons { get; set; }
    public ControllerButton UpButton { get; set; }
    public ControllerButton DownButton { get; set; }
    public ControllerButton LeftButton { get; set; }
    public ControllerButton RightButton { get; set; }
}

public class MotionMappingConfig
{
    public bool Enabled { get; set; }
    public MotionMappingMode Mode { get; set; } = MotionMappingMode.WASD;
    public string UpKey { get; set; } = "W";
    public string DownKey { get; set; } = "S";
    public string LeftKey { get; set; } = "A";
    public string RightKey { get; set; } = "D";
    public bool MapToButtons { get; set; }
    public ControllerButton UpButton { get; set; }
    public ControllerButton DownButton { get; set; }
    public ControllerButton LeftButton { get; set; }
    public ControllerButton RightButton { get; set; }
    public double Sensitivity { get; set; } = 50;
    public double Deadzone { get; set; } = 10;
}

public class DeadzoneConfig
{
    public double LeftStickInner { get; set; }
    public double LeftStickOuter { get; set; } = 100;
    public double RightStickInner { get; set; }
    public double RightStickOuter { get; set; } = 100;
    public double LeftTriggerDeadzone { get; set; }
    public double RightTriggerDeadzone { get; set; }
    public bool ApplySameTriggerSettings { get; set; }
    public CurveType StickCurve { get; set; } = CurveType.Linear;
    public Dictionary<double, double> CustomCurvePoints { get; set; } = new();
}

public class TouchpadGestureConfig
{
    public bool Enabled { get; set; } = true;
    public ButtonAction SingleTapAction { get; set; } = new();
    public ButtonAction DoubleTapAction { get; set; } = new();
    public ButtonAction SwipeLeftAction { get; set; } = new();
    public ButtonAction SwipeRightAction { get; set; } = new();
    public ButtonAction SwipeUpAction { get; set; } = new();
    public ButtonAction SwipeDownAction { get; set; } = new();
    public double ScrollSpeed { get; set; } = 50;
    public bool InvertScroll { get; set; }
    public bool FingerTouchMovementsEnabled { get; set; } = true;
    public int LeftMotorHapticsIntensity { get; set; }
    public int RightMotorHapticsIntensity { get; set; }
}
