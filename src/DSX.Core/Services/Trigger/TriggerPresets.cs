using DSX.Core.Enums;
using DSX.Core.Models;

namespace DSX.Core.Services.Trigger;

public static class TriggerPresets
{
    public static readonly TriggerPresetConfig[] AllPresets =
    [
        new()
        {
            Preset = TriggerPreset.FPS_Shooter,
            DisplayName = "FPS / Shooter",
            Description = "Light resistance for quick trigger pulls, medium force for aiming feel",
            LeftTrigger = new() { Mode = TriggerMode.Resistance, StartPosition = 0, EndPosition = 255, Force = 40 },
            RightTrigger = new() { Mode = TriggerMode.Resistance, StartPosition = 0, EndPosition = 255, Force = 55 }
        },
        new()
        {
            Preset = TriggerPreset.Racing,
            DisplayName = "Racing",
            Description = "Progressive resistance simulating gas/brake pedal feel",
            LeftTrigger = new() { Mode = TriggerMode.SlopeFeedback, StartPosition = 0, EndPosition = 255, Force = 80 },
            RightTrigger = new() { Mode = TriggerMode.SlopeFeedback, StartPosition = 0, EndPosition = 255, Force = 100 }
        },
        new()
        {
            Preset = TriggerPreset.RPG,
            DisplayName = "RPG",
            Description = "Medium resistance for immersive interaction",
            LeftTrigger = new() { Mode = TriggerMode.Rigid, StartPosition = 0, EndPosition = 255, Force = 50 },
            RightTrigger = new() { Mode = TriggerMode.Rigid, StartPosition = 0, EndPosition = 255, Force = 60 }
        },
        new()
        {
            Preset = TriggerPreset.Platformer,
            DisplayName = "Platformer",
            Description = "Light feedback for responsive jumping and actions",
            LeftTrigger = new() { Mode = TriggerMode.Resistance, StartPosition = 0, EndPosition = 200, Force = 30 },
            RightTrigger = new() { Mode = TriggerMode.Resistance, StartPosition = 0, EndPosition = 200, Force = 35 }
        },
        new()
        {
            Preset = TriggerPreset.Sports,
            DisplayName = "Sports",
            Description = "Moderate resistance for varied sports actions",
            LeftTrigger = new() { Mode = TriggerMode.Rigid, StartPosition = 0, EndPosition = 255, Force = 45 },
            RightTrigger = new() { Mode = TriggerMode.Rigid, StartPosition = 0, EndPosition = 255, Force = 55 }
        },
        new()
        {
            Preset = TriggerPreset.Fighting,
            DisplayName = "Fighting",
            Description = "Snappy feedback for punch/kick inputs",
            LeftTrigger = new() { Mode = TriggerMode.Rigid, StartPosition = 0, EndPosition = 180, Force = 70 },
            RightTrigger = new() { Mode = TriggerMode.Rigid, StartPosition = 0, EndPosition = 180, Force = 80 }
        },
        new()
        {
            Preset = TriggerPreset.Stealth,
            DisplayName = "Stealth",
            Description = "Very light resistance for subtle, quiet actions",
            LeftTrigger = new() { Mode = TriggerMode.Resistance, StartPosition = 0, EndPosition = 150, Force = 20 },
            RightTrigger = new() { Mode = TriggerMode.Resistance, StartPosition = 0, EndPosition = 150, Force = 25 }
        },
        new()
        {
            Preset = TriggerPreset.Bow_Hunting,
            DisplayName = "Bow / Hunting",
            Description = "Progressive draw feel with bow string simulation",
            LeftTrigger = new() { Mode = TriggerMode.Bow, StartPosition = 0, EndPosition = 255, Force = 80 },
            RightTrigger = new() { Mode = TriggerMode.Bow, StartPosition = 0, EndPosition = 255, Force = 90 }
        },
        new()
        {
            Preset = TriggerPreset.Racing_Wheel,
            DisplayName = "Racing Wheel",
            Description = "Heavy progressive feedback simulating pedal resistance",
            LeftTrigger = new() { Mode = TriggerMode.SlopeFeedback, StartPosition = 0, EndPosition = 255, Force = 120 },
            RightTrigger = new() { Mode = TriggerMode.SlopeFeedback, StartPosition = 0, EndPosition = 255, Force = 140 }
        },
        new()
        {
            Preset = TriggerPreset.FlightStick,
            DisplayName = "Flight Stick",
            Description = "Smooth resistance for throttle control",
            LeftTrigger = new() { Mode = TriggerMode.Resistance, StartPosition = 0, EndPosition = 255, Force = 60 },
            RightTrigger = new() { Mode = TriggerMode.Resistance, StartPosition = 0, EndPosition = 255, Force = 70 }
        },
        new()
        {
            Preset = TriggerPreset.Gun_Trigger,
            DisplayName = "Gun Trigger",
            Description = "Realistic gun trigger with wall and break point",
            LeftTrigger = new() { Mode = TriggerMode.RigidAB, StartPosition = 0, EndPosition = 255, Force = 80 },
            RightTrigger = new() { Mode = TriggerMode.RigidAB, StartPosition = 0, EndPosition = 200, Force = 100 }
        },
        new()
        {
            Preset = TriggerPreset.Hair_Trigger,
            DisplayName = "Hair Trigger",
            Description = "Minimal resistance for fastest possible trigger response",
            LeftTrigger = new() { Mode = TriggerMode.Resistance, StartPosition = 0, EndPosition = 50, Force = 10 },
            RightTrigger = new() { Mode = TriggerMode.Resistance, StartPosition = 0, EndPosition = 50, Force = 15 }
        },
        new()
        {
            Preset = TriggerPreset.Heavy_Resistance,
            DisplayName = "Heavy Resistance",
            Description = "Maximum resistance for strong feedback",
            LeftTrigger = new() { Mode = TriggerMode.Rigid, StartPosition = 0, EndPosition = 255, Force = 200 },
            RightTrigger = new() { Mode = TriggerMode.Rigid, StartPosition = 0, EndPosition = 255, Force = 220 }
        },
        new()
        {
            Preset = TriggerPreset.Soft_Resistance,
            DisplayName = "Soft Resistance",
            Description = "Gentle, smooth resistance throughout the pull",
            LeftTrigger = new() { Mode = TriggerMode.Resistance, StartPosition = 0, EndPosition = 255, Force = 25 },
            RightTrigger = new() { Mode = TriggerMode.Resistance, StartPosition = 0, EndPosition = 255, Force = 30 }
        },
        new()
        {
            Preset = TriggerPreset.Machine_Gun,
            DisplayName = "Machine Gun",
            Description = "Rapid pulsing feedback simulating automatic fire",
            LeftTrigger = new() { Mode = TriggerMode.Resistance, StartPosition = 0, EndPosition = 255, Force = 30 },
            RightTrigger = new() { Mode = TriggerMode.MachineGun, StartPosition = 0, EndPosition = 255, Force = 100 }
        },
        new()
        {
            Preset = TriggerPreset.Sniper,
            DisplayName = "Sniper",
            Description = "Two-stage trigger with light take-up then heavy break",
            LeftTrigger = new() { Mode = TriggerMode.RigidA, StartPosition = 0, EndPosition = 255, Force = 40 },
            RightTrigger = new() { Mode = TriggerMode.RigidAB, StartPosition = 0, EndPosition = 220, Force = 150 }
        },
        new()
        {
            Preset = TriggerPreset.Shotgun,
            DisplayName = "Shotgun",
            Description = "Heavy pull with pump-action vibration feedback",
            LeftTrigger = new() { Mode = TriggerMode.Galloping, StartPosition = 0, EndPosition = 255, Force = 90 },
            RightTrigger = new() { Mode = TriggerMode.Galloping, StartPosition = 0, EndPosition = 255, Force = 120 }
        },
        new()
        {
            Preset = TriggerPreset.Pistol,
            DisplayName = "Pistol",
            Description = "Short crisp trigger pull with clean break",
            LeftTrigger = new() { Mode = TriggerMode.RigidA, StartPosition = 0, EndPosition = 150, Force = 50 },
            RightTrigger = new() { Mode = TriggerMode.RigidA, StartPosition = 0, EndPosition = 150, Force = 70 }
        }
    ];

    public static TriggerPresetConfig? GetPreset(TriggerPreset preset)
    {
        if (preset == TriggerPreset.Custom) return null;
        return Array.Find(AllPresets, p => p.Preset == preset);
    }
}
