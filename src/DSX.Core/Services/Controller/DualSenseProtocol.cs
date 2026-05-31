namespace DSX.Core.Services.Controller;

public static class DualSenseProtocol
{
    public const int USBInputReportSize = 64;
    public const int BTInputReportSize = 78;
    public const int USBOutputReportSize = 48;
    public const int BTOutputReportSize = 78;
    public const int OutputPayloadSize = 47;

    public const byte USBInputReportId = 0x01;
    public const byte USBOutputReportId = 0x02;
    public const byte BTInputReportId = 0x31;
    public const byte BTOutputReportId = 0x31;
    public const byte BTOutputTag = 0x02;

    public const ushort SonyVID = 0x054C;
    public const ushort DualSensePID = 0x0CE6;
    public const ushort DualSenseEdgePID = 0x0DF2;
    public const ushort DualShock4V1PID = 0x05C4;
    public const ushort DualShock4V2PID = 0x09CC;

    public const string DualSenseMI = "mi_03";

    public static class OutputFlags
    {
        public const byte CompatibleVibration = 0x01;
        public const byte HapticsSelect = 0x02;
        public const byte SpeakerVolumeEnable = 0x20;
        public const byte MicVolumeEnable = 0x40;
        public const byte AudioControlEnable = 0x80;
        public const byte AllFlag0 = 0xFF;

        public const byte MicMuteLedControl = 0x01;
        public const byte PowerSaveControl = 0x02;
        public const byte LightbarControl = 0x04;
        public const byte ReleaseLeds = 0x08;
        public const byte PlayerIndicatorControl = 0x10;
        public const byte AudioControl2Enable = 0x80;
        public const byte AllFlag1 = 0xFF;

        public const byte LightbarSetupControl = 0x02;
        public const byte CompatibleVibration2 = 0x04;
    }

    public static class Output
    {
        public const int ValidFlag0 = 0;
        public const int ValidFlag1 = 1;
        public const int RightRumbleMotor = 2;
        public const int LeftRumbleMotor = 3;
        public const int HeadphoneVolume = 4;
        public const int SpeakerVolume = 5;
        public const int MicVolume = 6;
        public const int AudioControl = 7;
        public const int MicMuteLed = 8;
        public const int PowerSaveControl = 9;
        public const int RightTriggerStart = 10;
        public const int RightTriggerEnd = 20;
        public const int LeftTriggerStart = 21;
        public const int LeftTriggerEnd = 31;
        public const int Reserved32 = 32;
        public const int Reserved33 = 33;
        public const int Reserved34 = 34;
        public const int Reserved35 = 35;
        public const int Reserved36 = 36;
        public const int AudioControl2 = 37;
        public const int Reserved38 = 38;
        public const int Reserved39 = 39;
        public const int ValidFlag2 = 40;
        public const int LightbarSetup = 41;
        public const int LedBrightness = 42;
        public const int PlayerLedBits = 43;
        public const int LightbarRed = 44;
        public const int LightbarGreen = 45;
        public const int LightbarBlue = 46;
    }

    public static class AudioRouting
    {
        public const byte HeadphoneLR = 0x00;
        public const byte HeadphoneLL = 0x10;
        public const byte SpeakerR = 0x30;
    }

    public static class SpeakerPreampGain
    {
        public const byte Minus6dB = 0x00;
        public const byte Zero = 0x01;
        public const byte Plus6dB = 0x02;
    }

    public static class Input
    {
        public const int LeftStickX = 0;
        public const int LeftStickY = 1;
        public const int RightStickX = 2;
        public const int RightStickY = 3;
        public const int L2Analog = 4;
        public const int R2Analog = 5;
        public const int SequenceNumber = 6;
        public const int Buttons0 = 7;
        public const int Buttons1 = 8;
        public const int Buttons2 = 9;
        public const int Buttons3 = 10;
        public const int Timestamp = 11;
        public const int GyroPitch = 15;
        public const int GyroYaw = 17;
        public const int GyroRoll = 19;
        public const int AccelX = 21;
        public const int AccelY = 23;
        public const int AccelZ = 25;
        public const int SensorTimestamp = 27;
        public const int Touch1Id = 32;
        public const int Touch1XLow = 33;
        public const int Touch1XY = 34;
        public const int Touch1YHigh = 35;
        public const int Touch2Id = 36;
        public const int Touch2XLow = 37;
        public const int Touch2XY = 38;
        public const int Touch2YHigh = 39;
        public const int TouchPacketCounter = 40;
        public const int R2FeedbackState = 41;
        public const int L2FeedbackState = 42;
        public const int Battery = 52;
    }

    public static class TriggerMode
    {
        public const byte Off = 0x00;
        public const byte Resistance = 0x01;
        public const byte Section = 0x02;
        public const byte ResistanceAndFeedback = 0x21;
        public const byte ResistanceAndVibration = 0x25;
        public const byte SectionAndVibration = 0x26;
        public const byte Calibrate = 0xFC;
    }

    public static class PlayerLEDs
    {
        public const byte Player1 = 0x04;
        public const byte Player2 = 0x06;
        public const byte Player3 = 0x07;
        public const byte Player4 = 0x0E;
        public const byte Player5 = 0x1F;
        public const byte FadeDisable = 0x20;
    }
}
