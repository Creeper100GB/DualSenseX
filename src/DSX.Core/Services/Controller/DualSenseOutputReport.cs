using System;
using DSX.Core.Enums;
using DSX.Core.Models;
using static DSX.Core.Services.Controller.DualSenseProtocol;
using static DSX.Core.Services.Controller.DualSenseProtocol.Output;
using static DSX.Core.Services.Controller.DualSenseProtocol.OutputFlags;
using static DSX.Core.Services.Controller.DualSenseProtocol.TriggerMode;

namespace DSX.Core.Services.Controller;

public sealed class DualSenseOutputReport
{
    private readonly byte[] _state = new byte[OutputPayloadSize];
    private readonly object _lock = new();
    private bool _lightbarInitialized;

    public void SetRumble(double leftMotor, double rightMotor)
    {
        lock (_lock)
        {
            _state[ValidFlag0] |= CompatibleVibration;
            _state[LeftRumbleMotor] = (byte)(Math.Clamp(leftMotor, 0, 100) * 2.55);
            _state[RightRumbleMotor] = (byte)(Math.Clamp(rightMotor, 0, 100) * 2.55);
        }
    }

    public void SetLightbar(byte r, byte g, byte b)
    {
        lock (_lock)
        {
            _state[ValidFlag1] |= LightbarControl | PlayerIndicatorControl;
            if (!_lightbarInitialized)
            {
                _state[ValidFlag2] |= LightbarSetupControl;
                _state[LightbarSetup] = 0x02;
                _lightbarInitialized = true;
            }
            _state[LightbarRed] = r;
            _state[LightbarGreen] = g;
            _state[LightbarBlue] = b;
        }
    }

    public void SetPlayerLEDs(byte mask)
    {
        lock (_lock)
        {
            _state[ValidFlag1] |= PlayerIndicatorControl;
            _state[PlayerLedBits] = mask;
        }
    }

    public void SetMicMuteLed(byte mode)
    {
        lock (_lock)
        {
            _state[ValidFlag1] |= MicMuteLedControl;
            _state[MicMuteLed] = mode;
        }
    }

    public void SetAdaptiveTrigger(bool isRight, byte[] triggerData)
    {
        lock (_lock)
        {
            _state[ValidFlag0] |= HapticsSelect;
            int offset = isRight ? RightTriggerStart : LeftTriggerStart;
            int len = Math.Min(triggerData.Length, 11);
            Buffer.BlockCopy(triggerData, 0, _state, offset, len);
        }
    }

    public void SetTriggerOff(bool isRight)
    {
        lock (_lock)
        {
            int offset = isRight ? RightTriggerStart : LeftTriggerStart;
            for (int i = 0; i < 11; i++)
                _state[offset + i] = 0;
        }
    }

    public void ResetAll()
    {
        lock (_lock)
        {
            Array.Clear(_state, 0, _state.Length);
            _lightbarInitialized = false;
        }
    }

    public void SetSpeakerVolume(byte volume)
    {
        lock (_lock)
        {
            _state[ValidFlag0] |= SpeakerVolumeEnable;
            _state[SpeakerVolume] = Math.Clamp(volume, (byte)0x00, (byte)0x64);
        }
    }

    public void SetHeadphoneVolume(byte volume)
    {
        lock (_lock)
        {
            _state[ValidFlag0] |= AudioControlEnable;
            _state[HeadphoneVolume] = Math.Clamp(volume, (byte)0x00, (byte)0x7F);
        }
    }

    public void SetMicVolume(byte volume)
    {
        lock (_lock)
        {
            _state[ValidFlag0] |= MicVolumeEnable;
            _state[MicVolume] = Math.Clamp(volume, (byte)0x00, (byte)0x40);
        }
    }

    public void SetAudioRouting(byte routing)
    {
        lock (_lock)
        {
            _state[ValidFlag0] |= AudioControlEnable;
            _state[AudioControl] = routing;
        }
    }

    public void SetSpeakerPreampGain(byte gain)
    {
        lock (_lock)
        {
            _state[ValidFlag1] |= AudioControl2Enable;
            _state[AudioControl2] = (byte)(gain & 0x07);
        }
    }

    public void RouteToSpeaker()
    {
        lock (_lock)
        {
            _state[ValidFlag0] |= AudioControlEnable | SpeakerVolumeEnable;
            _state[AudioControl] = 0x30;
            _state[SpeakerVolume] = 0x64;
            _state[ValidFlag1] |= AudioControl2Enable;
            _state[AudioControl2] = 0x02;
        }
    }

    public void RouteToHeadphones()
    {
        lock (_lock)
        {
            _state[ValidFlag0] |= AudioControlEnable;
            _state[AudioControl] = 0x00;
        }
    }

    public byte[] BuildUSBReport()
    {
        lock (_lock)
        {
            var report = new byte[USBOutputReportSize];
            report[0] = USBOutputReportId;
            Buffer.BlockCopy(_state, 0, report, 1, OutputPayloadSize);
            return report;
        }
    }

    public byte[] BuildBTReport(byte sequence)
    {
        lock (_lock)
        {
            var report = new byte[BTOutputReportSize];
            report[0] = BTOutputReportId;
            report[1] = (byte)(((sequence & 0x0F) << 4) | BTOutputTag);
            Buffer.BlockCopy(_state, 0, report, 2, OutputPayloadSize);
            DSXCrc32.WriteCrc(report);
            return report;
        }
    }

    public static byte[] BuildTriggerBytes(TriggerConfig config)
    {
        return config.Mode switch
        {
            Enums.TriggerMode.Rigid => BuildRigidTrigger(config),
            Enums.TriggerMode.RigidA => BuildRigidATrigger(config),
            Enums.TriggerMode.RigidB => BuildRigidBTrigger(config),
            Enums.TriggerMode.RigidAB => BuildRigidABTrigger(config),
            Enums.TriggerMode.Pulse => BuildPulseTrigger(config),
            Enums.TriggerMode.PulseA => BuildPulseATrigger(config),
            Enums.TriggerMode.PulseB => BuildPulseBTrigger(config),
            Enums.TriggerMode.PulseAB => BuildPulseABTrigger(config),
            Enums.TriggerMode.MachineGun => BuildMachineGunTrigger(config),
            Enums.TriggerMode.Vibration => BuildVibrationTrigger(config),
            Enums.TriggerMode.Resistance => BuildResistanceTrigger(config),
            Enums.TriggerMode.Bow => BuildBowTrigger(config),
            Enums.TriggerMode.Galloping => BuildGallopingTrigger(config),
            Enums.TriggerMode.SemiAutomaticWeapon => BuildSemiAutoWeaponTrigger(config),
            Enums.TriggerMode.AutomaticWeapon => BuildAutoWeaponTrigger(config),
            Enums.TriggerMode.MultiplePositionFeedback => BuildMultiPosTrigger(config),
            Enums.TriggerMode.SlopeFeedback => BuildSlopeFeedbackTrigger(config),
            Enums.TriggerMode.Vibrate => BuildVibrateTrigger(config),
            Enums.TriggerMode.Custom => BuildCustomTrigger(config),
            _ => BuildOffTrigger()
        };
    }

    private static byte[] BuildOffTrigger() => new byte[11];

    private static byte[] BuildRigidTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndFeedback;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)c.Force;
        d[5] = (byte)c.Force;
        return d;
    }

    private static byte[] BuildRigidATrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndFeedback;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)c.Force;
        d[5] = 0x00;
        return d;
    }

    private static byte[] BuildRigidBTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndFeedback;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = 0x00;
        d[5] = (byte)c.Force;
        return d;
    }

    private static byte[] BuildRigidABTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndFeedback;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)(c.Force / 2);
        d[5] = (byte)(c.Force / 2);
        return d;
    }

    private static byte[] BuildPulseTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndVibration;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)c.Force;
        d[5] = (byte)(c.Amplitude & 0x7F);
        d[6] = (byte)(c.Frequency & 0x7F);
        return d;
    }

    private static byte[] BuildPulseATrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndVibration;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)c.Force;
        d[5] = (byte)(c.Amplitude & 0x7F);
        d[6] = 0x00;
        return d;
    }

    private static byte[] BuildPulseBTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndVibration;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)c.Force;
        d[5] = 0x00;
        d[6] = (byte)(c.Frequency & 0x7F);
        return d;
    }

    private static byte[] BuildPulseABTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndVibration;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)c.Force;
        d[5] = (byte)(c.Amplitude / 2 & 0x7F);
        d[6] = (byte)(c.Frequency / 2 & 0x7F);
        return d;
    }

    private static byte[] BuildMachineGunTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndFeedback;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)c.Force;
        d[5] = (byte)c.Force;
        d[6] = 6;
        d[7] = (byte)(c.Force / 3);
        d[8] = 80;
        return d;
    }

    private static byte[] BuildVibrationTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = SectionAndVibration;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)c.Force;
        d[5] = (byte)(c.Amplitude & 0x7F);
        d[6] = (byte)(c.Frequency & 0x7F);
        return d;
    }

    private static byte[] BuildResistanceTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = Resistance;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)c.Force;
        return d;
    }

    private static byte[] BuildBowTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndFeedback;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)c.Force;
        d[5] = (byte)(c.Force * 2);
        d[6] = 6;
        d[7] = (byte)(c.Force / 2);
        d[8] = 100;
        return d;
    }

    private static byte[] BuildGallopingTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndFeedback;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)c.Force;
        d[5] = (byte)(c.Force / 2);
        d[6] = 4;
        d[7] = (byte)(c.Force / 2);
        d[8] = 60;
        return d;
    }

    private static byte[] BuildSemiAutoWeaponTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndFeedback;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)c.Force;
        d[5] = (byte)c.Force;
        d[6] = 10;
        d[7] = (byte)(c.Force / 2);
        d[8] = 50;
        return d;
    }

    private static byte[] BuildAutoWeaponTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndFeedback;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)c.Force;
        d[5] = (byte)c.Force;
        d[6] = 5;
        d[7] = (byte)(c.Force / 3);
        d[8] = 80;
        return d;
    }

    private static byte[] BuildSlopeFeedbackTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndFeedback;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)(c.Force / 3);
        d[5] = (byte)(c.Force * 2);
        return d;
    }

    private static byte[] BuildVibrateTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndVibration;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        d[4] = (byte)c.Force;
        d[5] = (byte)(c.Amplitude & 0x7F);
        d[6] = (byte)(c.Frequency & 0x7F);
        return d;
    }

    private static byte[] BuildMultiPosTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        d[0] = ResistanceAndFeedback;
        d[1] = 0x02;
        d[2] = (byte)c.StartPosition;
        d[3] = (byte)c.EndPosition;
        for (int i = 0; i < Math.Min(c.MultiplePositionForces.Length, 7); i++)
            d[4 + i] = (byte)c.MultiplePositionForces[i];
        return d;
    }

    private static byte[] BuildCustomTrigger(TriggerConfig c)
    {
        var d = new byte[11];
        for (int i = 0; i < Math.Min(c.CustomBytes.Length, 11); i++)
            d[i] = (byte)c.CustomBytes[i];
        return d;
    }
}
