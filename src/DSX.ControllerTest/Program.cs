using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DSX.Core.Enums;
using DSX.Core.Interfaces;
using DSX.Core.Models;
using DSX.Core.Services.Controller;
using DSX.Core.Constants;
using HidSharp;

namespace DSX.ControllerTest;

class Program
{
    private static readonly List<TestResult> Results = new();
    private static HidStream? _stream;
    private static HidDevice? _device;
    private static ControllerInputState? _lastInput;
    private static readonly object _ioLock = new();
    private static volatile bool _stopReader;
    private static bool _isBluetooth;

    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== DSX Controller Test Suite v2.0 (Canonical Offsets) ===\n");

        Test("HID Discovery", () =>
        {
            _device = DeviceList.Local.GetHidDevices().FirstOrDefault(d =>
                d.VendorID == DualSenseProtocol.SonyVID &&
                d.ProductID == DualSenseProtocol.DualSensePID);

            if (_device == null)
                throw new Exception("No DualSense found");

            int maxIn = _device.GetMaxInputReportLength();
            int maxOut = _device.GetMaxOutputReportLength();
            string conn = (maxIn >= 78 || maxOut >= 78) ? "Bluetooth" : "USB";
            return $"Found: VID={_device.VendorID:X4} PID={_device.ProductID:X4} In={maxIn} Out={maxOut} ({conn})";
        });

        Test("HID Open", () =>
        {
            if (_device == null || !_device.TryOpen(out var s))
                throw new Exception("Failed to open");
            _stream = s;
            _stream.ReadTimeout = 3000;
            _stream.WriteTimeout = 2000;
            _isBluetooth = _device.GetMaxInputReportLength() >= 78;
            return $"Stream opened ({(_isBluetooth ? "Bluetooth" : "USB")})";
        });

        Test("Input Read (raw)", () =>
        {
            if (_stream == null) throw new Exception("No stream");
            byte[] buf = new byte[_isBluetooth ? 78 : 64];
            int read = _stream.Read(buf);
            if (read <= 0) throw new Exception($"Read {read} bytes");
            string hex = string.Join(" ", buf.Take(16).Select(b => $"{b:X2}"));
            string reportId = buf[0] == 0x31 ? "BT(0x31)" : buf[0] == 0x01 ? "USB(0x01)" : $"0x{buf[0]:X2}";
            return $"{read} bytes, ReportID={reportId}: {hex}";
        });

        var inputThread = new Thread(InputReaderLoop) { IsBackground = true };
        inputThread.Start();

        Console.WriteLine("\n--- LED Tests ---\n");

        Test("LED: Red", () =>
        {
            var report = BuildCanonicalLED(255, 0, 0);
            WriteToStream(report);
            Thread.Sleep(600);
            return "Red — check lightbar";
        });

        Test("LED: Green", () =>
        {
            var report = BuildCanonicalLED(0, 255, 0);
            WriteToStream(report);
            Thread.Sleep(600);
            return "Green — check lightbar";
        });

        Test("LED: Blue", () =>
        {
            var report = BuildCanonicalLED(0, 0, 255);
            WriteToStream(report);
            Thread.Sleep(600);
            return "Blue — check lightbar";
        });

        Test("LED: Purple", () =>
        {
            var report = BuildCanonicalLED(180, 0, 255);
            WriteToStream(report);
            Thread.Sleep(600);
            return "Purple — check lightbar";
        });

        Test("LED: Teal (DSX Accent)", () =>
        {
            var report = BuildCanonicalLED(0, 212, 170);
            WriteToStream(report);
            Thread.Sleep(600);
            return "Teal #00D4AA — check lightbar";
        });

        Console.WriteLine("\n--- Player LED Tests ---\n");

        Test("Player LED: P1", () =>
        {
            var r = BuildCanonicalBase();
            WritePlayerLED(r, DualSenseProtocol.PlayerLEDs.Player1);
            Thread.Sleep(800);
            return "Player 1 — 1 LED should be lit";
        });

        Test("Player LED: P5", () =>
        {
            var r = BuildCanonicalBase();
            WritePlayerLED(r, DualSenseProtocol.PlayerLEDs.Player5);
            Thread.Sleep(800);
            return "Player 5 — all 5 LEDs should be lit";
        });

        Console.WriteLine("\n--- Rumble Tests ---\n");

        Test("Rumble: Left (low freq)", () =>
        {
            var r = BuildCanonicalBase();
            WriteRumble(r, 200, 0);
            Thread.Sleep(500);
            WriteRumble(r, 0, 0);
            return "Left motor 200/255 for 500ms";
        });

        Test("Rumble: Right (high freq)", () =>
        {
            var r = BuildCanonicalBase();
            WriteRumble(r, 0, 200);
            Thread.Sleep(500);
            WriteRumble(r, 0, 0);
            return "Right motor 200/255 for 500ms";
        });

        Test("Rumble: Both", () =>
        {
            var r = BuildCanonicalBase();
            WriteRumble(r, 150, 150);
            Thread.Sleep(400);
            WriteRumble(r, 0, 0);
            return "Both motors 150/255 for 400ms";
        });

        Console.WriteLine("\n--- Trigger Tests ---\n");

        Test("Trigger L2: Rigid (hard stop)", () =>
        {
            var r = BuildCanonicalTrigger();
            SetCanonicalTrigger(r, false, 0x21, 0x02, 0, 255, 255, 255);
            WriteToStream(r);
            Thread.Sleep(2000);
            return "Pull L2 — should feel hard stop";
        });

        Test("Trigger R2: Rigid", () =>
        {
            var r = BuildCanonicalTrigger();
            SetCanonicalTrigger(r, true, 0x21, 0x02, 0, 255, 200, 200);
            WriteToStream(r);
            Thread.Sleep(2000);
            return "Pull R2 — should feel hard stop";
        });

        Test("Trigger L2: Vibration", () =>
        {
            var r = BuildCanonicalTrigger();
            SetCanonicalTrigger(r, false, 0x26, 0x02, 0, 255, 128, 30);
            WriteToStream(r);
            Thread.Sleep(2000);
            return "Pull L2 — should feel vibration";
        });

        Test("Triggers: Reset Off", () =>
        {
            var r = BuildCanonicalTrigger();
            WriteToStream(r);
            Thread.Sleep(300);
            return "Triggers off — should feel normal";
        });

        Console.WriteLine("\n--- Input Verification ---\n");

        Test("Input: Buttons", () =>
        {
            _stopReader = true;
            Thread.Sleep(100);
            if (_lastInput == null) throw new Exception("No input received");
            return $"Cross={_lastInput.Cross} Circle={_lastInput.Circle} " +
                   $"Triangle={_lastInput.Triangle} Square={_lastInput.Square} " +
                   $"L1={_lastInput.L1} R1={_lastInput.R1} " +
                   $"DPad=U:{_lastInput.DPadUp} D:{_lastInput.DPadDown} L:{_lastInput.DPadLeft} R:{_lastInput.DPadRight}";
        });

        Test("Input: Sticks+Triggers", () =>
        {
            if (_lastInput == null) throw new Exception("No input");
            return $"LX={_lastInput.LeftStickX:F2} LY={_lastInput.LeftStickY:F2} " +
                   $"RX={_lastInput.RightStickX:F2} RY={_lastInput.RightStickY:F2} " +
                   $"L2={_lastInput.LeftTrigger:F2} R2={_lastInput.RightTrigger:F2}";
        });

        Test("Input: Gyro+Accel", () =>
        {
            if (_lastInput == null) throw new Exception("No input");
            return $"Gyro=({_lastInput.GyroX:F1},{_lastInput.GyroY:F1},{_lastInput.GyroZ:F1}) " +
                   $"Accel=({_lastInput.AccelX:F1},{_lastInput.AccelY:F1},{_lastInput.AccelZ:F1})";
        });

        Console.WriteLine("\n--- ControllerService Integration ---\n");

        ControllerService? svc = null;
        Test("Service: Start+LED", () =>
        {
            _stopReader = true;
            _stream?.Close();
            _stream?.Dispose();
            _stream = null;
            Thread.Sleep(500);

            svc = new ControllerService();
            svc.StartPolling();
            Thread.Sleep(1500);
            if (svc.ActiveController == null) throw new Exception("No active controller");
            svc.SetLEDConfig(new TouchpadLEDConfig { Mode = LEDMode.StaticColor, Red = 255, Green = 165, Blue = 0 });
            Thread.Sleep(500);
            return $"Service found {svc.ConnectedControllers.Count} controller(s), set LED orange";
        });

        Test("Service: Rumble", () =>
        {
            svc!.SetRumble(80, 40);
            Thread.Sleep(400);
            svc.SetRumble(0, 0);
            return "Rumble via service (80%, 40%)";
        });

        Test("Service: Trigger", () =>
        {
            svc!.SetAdaptiveTrigger(TriggerSide.Left, new TriggerConfig { Mode = TriggerMode.Rigid, StartPosition = 0, Force = 200 });
            Thread.Sleep(1000);
            svc.SetAdaptiveTrigger(TriggerSide.Both, new TriggerConfig { Mode = TriggerMode.Off });
            return "L2 rigid via service then off";
        });

        Test("Service: Cleanup", () =>
        {
            svc!.SetLEDConfig(new TouchpadLEDConfig { Mode = LEDMode.StaticColor, Red = 0, Green = 212, Blue = 170 });
            svc.SetRumble(0, 0);
            Thread.Sleep(300);
            svc.Dispose();
            return "Reset to teal, disposed";
        });

        _stream?.Close();
        _stream?.Dispose();

        int passed = Results.Count(r => r.Passed);
        int failed = Results.Count(r => !r.Passed);

        Console.WriteLine($"\n=== RESULTS: {passed}/{Results.Count} passed, {failed} failed ===\n");
        foreach (var r in Results)
        {
            Console.WriteLine($"  {(r.Passed ? "PASS" : "FAIL")} {r.Name}");
            if (!r.Passed) Console.WriteLine($"       {r.Detail}");
        }

        return failed > 0 ? 1 : 0;
    }

    static void InputReaderLoop()
    {
        byte[] buf = new byte[_isBluetooth ? 78 : 64];
        while (!_stopReader && _stream != null)
        {
            try
            {
                int read;
                lock (_ioLock) { read = _stream.Read(buf); }
                if (read > 0)
                {
                    byte[] data;
                    if (_isBluetooth && read > 2 && buf[0] == 0x31)
                    {
                        data = new byte[read - 2];
                        Buffer.BlockCopy(buf, 2, data, 0, data.Length);
                    }
                    else if (read > 1 && buf[0] == 0x01)
                    {
                        data = new byte[read - 1];
                        Buffer.BlockCopy(buf, 1, data, 0, data.Length);
                    }
                    else
                    {
                        data = new byte[read];
                        Buffer.BlockCopy(buf, 0, data, 0, read);
                    }
                    var state = DualSenseInputParser.Parse(data, ControllerType.DualSense);
                    _lastInput = state;
                }
            }
            catch { break; }
        }
    }

    private static byte _btSeq = 0;

    static byte[] BuildCanonicalBase()
    {
        if (_isBluetooth)
        {
            byte[] r = new byte[DualSenseProtocol.BTOutputReportSize];
            r[0] = DualSenseProtocol.BTOutputReportId;
            r[1] = (byte)((_btSeq++ & 0x0F) << 4);
            r[2] = 0xFF;
            r[3] = 0xF7;
            return r;
        }
        else
        {
            byte[] r = new byte[DualSenseProtocol.USBOutputReportSize];
            r[0] = DualSenseProtocol.USBOutputReportId;
            r[1] = 0xFF;
            r[2] = 0xF7;
            return r;
        }
    }

    static void FinalizeReport(byte[] report)
    {
        if (_isBluetooth)
            DSXCrc32.WriteCrc(report);
    }

    static byte[] BuildCanonicalLED(byte red, byte green, byte blue)
    {
        var r = BuildCanonicalBase();
        int off = _isBluetooth ? 1 : 0;
        r[DualSenseProtocol.Output.LightbarRed + off + 1] = red;
        r[DualSenseProtocol.Output.LightbarGreen + off + 1] = green;
        r[DualSenseProtocol.Output.LightbarBlue + off + 1] = blue;
        r[DualSenseProtocol.Output.ValidFlag2 + off + 1] = DualSenseProtocol.OutputFlags.LightbarSetupControl;
        r[DualSenseProtocol.Output.LightbarSetup + off + 1] = 0x02;
        FinalizeReport(r);
        return r;
    }

    static void WriteToStream(byte[] data)
    {
        if (_stream == null) throw new Exception("No stream");
        lock (_ioLock) { _stream.Write(data); }
    }

    static int HOff => _isBluetooth ? 2 : 1;

    static void WritePlayerLED(byte[] r, byte mask)
    {
        r[DualSenseProtocol.Output.PlayerLedBits + HOff] = mask;
        FinalizeReport(r);
        WriteToStream(r);
    }

    static void WriteRumble(byte[] r, byte left, byte right)
    {
        r[DualSenseProtocol.Output.LeftRumbleMotor + HOff] = left;
        r[DualSenseProtocol.Output.RightRumbleMotor + HOff] = right;
        FinalizeReport(r);
        WriteToStream(r);
    }

    static byte[] BuildCanonicalTrigger()
    {
        var r = BuildCanonicalBase();
        int off = _isBluetooth ? 1 : 0;
        r[DualSenseProtocol.Output.ValidFlag0 + off + 1] |= DualSenseProtocol.OutputFlags.HapticsSelect;
        return r;
    }

    static void SetCanonicalTrigger(byte[] report, bool isRight, byte mode, byte sub, byte p0, byte p1, byte p2, byte p3)
    {
        int off = _isBluetooth ? 1 : 0;
        int offset = isRight ? DualSenseProtocol.Output.RightTriggerStart : DualSenseProtocol.Output.LeftTriggerStart;
        report[offset + off + 1] = mode;
        report[offset + off + 2] = sub;
        report[offset + off + 3] = p0;
        report[offset + off + 4] = p1;
        report[offset + off + 5] = p2;
        report[offset + off + 6] = p3;
        FinalizeReport(report);
    }

    static void Test(string name, Func<string> action)
    {
        Console.Write($"  {name}...");
        try
        {
            var detail = action();
            Results.Add(new TestResult { Name = name, Passed = true, Detail = detail });
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(" PASS");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Results.Add(new TestResult { Name = name, Passed = false, Detail = ex.Message });
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($" FAIL: {ex.Message}");
            Console.ResetColor();
        }
    }
}

class TestResult
{
    public string Name { get; set; } = "";
    public bool Passed { get; set; }
    public string? Detail { get; set; }
}
