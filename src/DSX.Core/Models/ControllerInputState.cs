using DSX.Core.Enums;

namespace DSX.Core.Models;

public class ControllerInputState
{
    public bool Cross { get; set; }
    public bool Circle { get; set; }
    public bool Square { get; set; }
    public bool Triangle { get; set; }
    public bool L1 { get; set; }
    public bool R1 { get; set; }
    public bool L2 { get; set; }
    public bool R2 { get; set; }
    public bool L3 { get; set; }
    public bool R3 { get; set; }
    public bool Share { get; set; }
    public bool Options { get; set; }
    public bool PSButton { get; set; }
    public bool Touchpad { get; set; }
    public bool Mute { get; set; }
    public bool FnLeft { get; set; }
    public bool FnRight { get; set; }
    public bool DPadUp { get; set; }
    public bool DPadDown { get; set; }
    public bool DPadLeft { get; set; }
    public bool DPadRight { get; set; }
    public double LeftStickX { get; set; }
    public double LeftStickY { get; set; }
    public double RightStickX { get; set; }
    public double RightStickY { get; set; }
    public double LeftTrigger { get; set; }
    public double RightTrigger { get; set; }
    public int TouchpadX1 { get; set; }
    public int TouchpadY1 { get; set; }
    public int TouchpadX2 { get; set; }
    public int TouchpadY2 { get; set; }
    public bool TouchpadTouch1 { get; set; }
    public bool TouchpadTouch2 { get; set; }
    public double GyroX { get; set; }
    public double GyroY { get; set; }
    public double GyroZ { get; set; }
    public double AccelX { get; set; }
    public double AccelY { get; set; }
    public double AccelZ { get; set; }
}

public enum ControllerButton
{
    Cross,
    Circle,
    Square,
    Triangle,
    L1,
    R1,
    L2,
    R2,
    L3,
    R3,
    Share,
    Options,
    PSButton,
    Touchpad,
    Mute,
    FnLeft,
    FnRight,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    LeftStickUp,
    LeftStickDown,
    LeftStickLeft,
    LeftStickRight,
    RightStickUp,
    RightStickDown,
    RightStickLeft,
    RightStickRight,
    TouchpadTap,
    TouchpadDoubleTap,
    TouchpadSwipeLeft,
    TouchpadSwipeRight,
    TouchpadSwipeUp,
    TouchpadSwipeDown
}
