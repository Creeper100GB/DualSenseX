using DSX.Core.Enums;
using DSX.Core.Models;
using static DSX.Core.Services.Controller.DualSenseProtocol.Input;

namespace DSX.Core.Services.Controller;

public static class DualSenseInputParser
{
    public static ControllerInputState Parse(byte[] data, ControllerType type)
    {
        var state = new ControllerInputState();

        if (data.Length < 12)
            return state;

        ParseSticksAndTriggers(data, state);
        ParseButtons(data, type, state);
        ParseGyro(data, state);
        ParseTouchpad(data, state);
        ParseDpad(data, state);

        return state;
    }

    private static void ParseSticksAndTriggers(byte[] data, ControllerInputState state)
    {
        if (data.Length <= R2Analog) return;

        state.LeftStickX = (data[LeftStickX] - 128) / 128.0;
        state.LeftStickY = (data[LeftStickY] - 128) / 128.0;
        state.RightStickX = (data[RightStickX] - 128) / 128.0;
        state.RightStickY = (data[RightStickY] - 128) / 128.0;
        state.LeftTrigger = data[L2Analog] / 255.0;
        state.RightTrigger = data[R2Analog] / 255.0;
    }

    private static void ParseButtons(byte[] data, ControllerType type, ControllerInputState state)
    {
        if (data.Length <= Buttons2) return;

        byte b0 = data[Buttons0];
        byte b1 = data[Buttons1];
        byte b2 = data[Buttons2];

        state.Triangle = (b0 & 0x80) != 0;
        state.Circle = (b0 & 0x40) != 0;
        state.Cross = (b0 & 0x20) != 0;
        state.Square = (b0 & 0x10) != 0;

        state.L1 = (b1 & 0x01) != 0;
        state.R1 = (b1 & 0x02) != 0;

        if (type == ControllerType.DualSense || type == ControllerType.DualSenseEdge)
        {
            state.L2 = (b1 & 0x04) != 0;
            state.R2 = (b1 & 0x08) != 0;
            state.Share = (b1 & 0x10) != 0;
            state.Options = (b1 & 0x20) != 0;
            state.L3 = (b1 & 0x40) != 0;
            state.R3 = (b1 & 0x80) != 0;

            state.PSButton = (b2 & 0x01) != 0;
            state.Touchpad = (b2 & 0x02) != 0;
            state.Mute = (b2 & 0x04) != 0;

            if (type == ControllerType.DualSenseEdge && data.Length > Buttons3)
            {
                state.FnLeft = (b2 & 0x08) != 0;
                state.FnRight = (b2 & 0x10) != 0;
            }
        }
        else
        {
            state.Share = (b1 & 0x10) != 0;
            state.Options = (b1 & 0x20) != 0;
            state.L3 = (b1 & 0x40) != 0;
            state.R3 = (b1 & 0x80) != 0;
            state.L2 = (b1 & 0x04) != 0;
            state.R2 = (b1 & 0x08) != 0;

            state.PSButton = (b2 & 0x01) != 0;
            state.Touchpad = (b2 & 0x02) != 0;
        }
    }

    private static void ParseDpad(byte[] data, ControllerInputState state)
    {
        if (data.Length <= Buttons0) return;
        int dpad = data[Buttons0] & 0x0F;
        state.DPadUp = dpad is 0 or 1 or 7;
        state.DPadRight = dpad is 2 or 3 or 1;
        state.DPadDown = dpad is 4 or 5 or 3;
        state.DPadLeft = dpad is 6 or 7 or 5;
    }

    private static void ParseGyro(byte[] data, ControllerInputState state)
    {
        if (data.Length <= AccelZ + 1) return;

        state.GyroX = BitConverter.ToInt16(data, GyroPitch) / 1024.0;
        state.GyroY = BitConverter.ToInt16(data, GyroYaw) / 1024.0;
        state.GyroZ = BitConverter.ToInt16(data, GyroRoll) / 1024.0;
        state.AccelX = BitConverter.ToInt16(data, AccelX) / 1024.0;
        state.AccelY = BitConverter.ToInt16(data, AccelY) / 1024.0;
        state.AccelZ = BitConverter.ToInt16(data, AccelZ) / 1024.0;
    }

    private static void ParseTouchpad(byte[] data, ControllerInputState state)
    {
        if (data.Length <= Touch1YHigh) return;

        byte t1id = data[Touch1Id];
        state.TouchpadTouch1 = (t1id & 0x80) == 0;
        state.TouchpadX1 = ((data[Touch1XY] & 0x0F) << 8) | data[Touch1XLow];
        state.TouchpadY1 = (data[Touch1YHigh] << 4) | ((data[Touch1XY] & 0xF0) >> 4);

        if (data.Length > Touch2YHigh)
        {
            byte t2id = data[Touch2Id];
            state.TouchpadTouch2 = (t2id & 0x80) == 0;
            state.TouchpadX2 = ((data[Touch2XY] & 0x0F) << 8) | data[Touch2XLow];
            state.TouchpadY2 = (data[Touch2YHigh] << 4) | ((data[Touch2XY] & 0xF0) >> 4);
        }
    }

    public static double ReadBattery(byte[] data, ConnectionType connection)
    {
        int offset = Battery;
        if (data.Length <= offset) return 0;

        byte raw = data[offset];
        int level = raw & 0x0F;
        bool charging = (raw & 0x10) != 0;

        if (charging && level >= 10) return 100;
        if (connection == ConnectionType.Bluetooth)
        {
            return level switch
            {
                0 => 0, 1 => 5, 2 => 10, 3 => 25,
                4 => 50, 5 => 75, 6 => 90,
                >= 7 => 100, _ => 0
            };
        }

        return Math.Min(level * 10, 100);
    }

    public static bool ReadCharging(byte[] data)
    {
        if (data.Length <= Battery) return false;
        return (data[Battery] & 0x10) != 0;
    }
}
