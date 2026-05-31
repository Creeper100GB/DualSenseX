namespace DSX.Core.Enums;

public enum ControllerType
{
    Unknown,
    DualSense,
    DualSenseEdge,
    DualShock4,
    PSVR2Sense,
    AccessController,
    RazerWolverineV2Pro,
    RazerRaijuV3Pro
}

public enum ConnectionType
{
    USB,
    Bluetooth,
    USBWirelessAdapter,
    Unknown
}

public enum EmulationType
{
    None,
    Xbox360,
    DualShock4,
    DualSense
}

public enum BatteryStatus
{
    Unknown,
    Dying,
    Low,
    Medium,
    Full,
    Charging,
    Charged
}

public enum TriggerMode
{
    Off,
    Rigid,
    Pulse,
    RigidA,
    RigidB,
    RigidAB,
    PulseA,
    PulseB,
    PulseAB,
    MachineGun,
    Vibration,
    Resistance,
    Bow,
    Galloping,
    SemiAutomaticWeapon,
    AutomaticWeapon,
    MultiplePositionFeedback,
    SlopeFeedback,
    Vibrate,
    Custom
}

public enum LEDMode
{
    Off,
    StaticColor,
    Rainbow,
    Custom,
    Breathing,
    Strobing,
    Battery
}

public enum RainbowSpeed
{
    Slow,
    Medium,
    Fast,
    Hyper
}

public enum PlayerLEDMode
{
    Off,
    Player1,
    Player2,
    Player3,
    Player4,
    Player5,
    BatteryLevel
}

public enum MuteLEDMode
{
    AlwaysOff,
    AlwaysOn,
    FollowMicMuteState
}

public enum WindowEffect
{
    None,
    Mica,
    Acrylic,
    Tabbed
}

public enum PressType
{
    SinglePress,
    DoublePress,
    TriplePress,
    Hold,
    HoldRelease
}

public enum ActionType
{
    KeyboardKey,
    MouseClick,
    MouseScroll,
    ControllerButtonRemap,
    Macro,
    OpenApplication,
    SwitchProfile,
    ActivateSteamBigPicture,
    VirtualDeviceInput
}

public enum MouseButton
{
    Left,
    Right,
    Middle,
    X1,
    X2
}

public enum MouseScrollDirection
{
    Up,
    Down,
    Left,
    Right
}

public enum StickMappingMode
{
    WASD,
    ZQSD,
    Custom
}

public enum MotionMappingMode
{
    WASD,
    ZQSD,
    Custom
}

public enum VibrationIntensity
{
    Off,
    Weak,
    Medium,
    Strong
}

public enum CurveType
{
    Linear,
    Aggressive,
    Relaxed,
    Custom
}

public enum AudioSourceMode
{
    SoundCapture,
    FilePlayback,
    VDSAudio,
    MixAndMatch
}

public enum HapticsSourceMode
{
    SoundWaves,
    SystemCapture,
    FilePlayback,
    VDSAudio
}

public enum ModConnectionMethod
{
    UDP,
    Textfile
}

public enum NavigationPage
{
    Home,
    MyControllers,
    ControllerMapping,
    AdaptiveTriggers,
    LEDLighting,
    HapticsRumble,
    VirtualDevice,
    InstalledGames,
    Mods,
    HidHide,
    Settings,
    HelpCenter
}
