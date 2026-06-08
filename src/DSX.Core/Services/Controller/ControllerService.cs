using System.Collections.Concurrent;
using System.IO;
using System.Timers;
using DSX.Core.Constants;
using DSX.Core.Enums;
using DSX.Core.Interfaces;
using DSX.Core.Models;
using HidSharp;
using static DSX.Core.Services.Controller.DualSenseProtocol;
using P = DSX.Core.Services.Controller.DualSenseProtocol;

namespace DSX.Core.Services.Controller;

public sealed class ControllerService : IControllerService, IDisposable
{
    private readonly ConcurrentDictionary<string, ControllerDeviceInfo> _controllers = new();
    private readonly ConcurrentDictionary<string, HidStream> _streams = new();
    private readonly ConcurrentDictionary<string, Thread> _pollThreads = new();
    private readonly ConcurrentDictionary<string, DualSenseOutputReport> _outputReports = new();
    private readonly object _writeLock = new();
    private volatile bool _disposed;
    private volatile string? _activeControllerId;
    private byte _btSequence;
    private LEDMode _activeLEDMode = LEDMode.Off;
    private PlayerLEDMode _activePlayerLEDMode = PlayerLEDMode.Off;
    private RainbowSpeed _rainbowSpeed = RainbowSpeed.Medium;
    private double _rainbowHue;
    private TouchpadLEDConfig? _activeLEDConfig;
    private double _breathingPhase;
    private bool _strobingOn;
    private double _lastBatteryLevel;
    private readonly System.Timers.Timer _ledTimer;

    public static Action<string>? LogAction { get; set; }

    public ControllerService()
    {
        _ledTimer = new System.Timers.Timer(40);
        _ledTimer.Elapsed += OnLEDTimerElapsed;
        _ledTimer.AutoReset = false;
    }

    private static void Log(string message) => LogAction?.Invoke(message);

    public event EventHandler<ControllerDeviceInfo>? ControllerConnected;
    public event EventHandler<string>? ControllerDisconnected;
    public event EventHandler<ControllerInputState>? InputReceived;

    public IReadOnlyList<ControllerDeviceInfo> ConnectedControllers =>
        _controllers.Values.ToList().AsReadOnly();

    public ControllerDeviceInfo? ActiveController =>
        _activeControllerId != null && _controllers.TryGetValue(_activeControllerId, out var c) ? c : null;

    public void StartPolling()
    {
        Log("StartPolling: Scanning for HID devices...");
        int count = 0;

        foreach (var device in DeviceList.Local.GetHidDevices())
        {
            if (!IsSupportedDevice(device.VendorID, device.ProductID))
                continue;

            if (!IsGamepadInterface(device))
                continue;

            count++;
            Log($"StartPolling: Found supported device VID={device.VendorID:X4} PID={device.ProductID:X4} Path={device.DevicePath}");
            TryOpenDevice(device);
        }

        Log($"StartPolling: Found {count} supported device(s), {_controllers.Count} opened successfully.");
        DeviceList.Local.Changed += OnDeviceListChanged;
    }

    public void StopPolling()
    {
        StopLEDTimer();
        DeviceList.Local.Changed -= OnDeviceListChanged;

        foreach (var kvp in _pollThreads.ToList())
            _pollThreads.TryRemove(kvp.Key, out _);

        foreach (var kvp in _streams.ToList())
        {
            if (_streams.TryRemove(kvp.Key, out var stream))
            {
                stream.Close();
                stream.Dispose();
            }
        }

        _controllers.Clear();
        _outputReports.Clear();
    }

    public void ConnectController(string deviceId)
    {
        if (_controllers.ContainsKey(deviceId))
            return;

        foreach (var device in DeviceList.Local.GetHidDevices())
        {
            if (device.DevicePath != deviceId)
                continue;
            TryOpenDevice(device);
            break;
        }
    }

    public void DisconnectController(string deviceId)
    {
        _pollThreads.TryRemove(deviceId, out _);
        _outputReports.TryRemove(deviceId, out _);

        if (_streams.TryRemove(deviceId, out var stream))
        {
            stream.Close();
            stream.Dispose();
        }

        if (_controllers.TryRemove(deviceId, out _))
        {
            if (_activeControllerId == deviceId)
                _activeControllerId = null;
            ControllerDisconnected?.Invoke(this, deviceId);
        }
    }

    public void SetActiveController(string deviceId)
    {
        if (_controllers.ContainsKey(deviceId))
            _activeControllerId = deviceId;
    }

    public void SetAdaptiveTrigger(TriggerSide side, TriggerConfig config)
    {
        if (controllerType() == ControllerType.DualShock4)
            return;

        var report = GetActiveOutputReport();
        if (report == null) { Log($"SetAdaptiveTrigger: no output report (activeId={_activeControllerId})"); return; }

        var triggerData = DualSenseOutputReport.BuildTriggerBytes(config);
        Log($"SetAdaptiveTrigger: side={side} mode={config.Mode} start={config.StartPosition} end={config.EndPosition} force={config.Force} data0={triggerData[0]:X2}");

        if (side == TriggerSide.Both || side == TriggerSide.Left)
            report.SetAdaptiveTrigger(false, triggerData);
        if (side == TriggerSide.Both || side == TriggerSide.Right)
            report.SetAdaptiveTrigger(true, triggerData);

        SendReport();
    }

    public void SendTriggerCalibration()
    {
        var info = ActiveController;
        if (info == null || info.Type == ControllerType.DualShock4) return;

        var report = GetActiveOutputReport();
        if (report == null) return;

        var calibrateData = DualSenseOutputReport.BuildTriggerBytes(new TriggerConfig
        {
            Mode = Enums.TriggerMode.Calibrate,
            StartPosition = 255,
            EndPosition = 255
        });
        report.SetAdaptiveTrigger(false, calibrateData);
        report.SetAdaptiveTrigger(true, calibrateData);
        SendReport();
        Log($"SendTriggerCalibration: calibration sent for {info.Name}");
    }

    public void SetLEDConfig(TouchpadLEDConfig config)
    {
        Log($"SetLEDConfig: R={config.Red} G={config.Green} B={config.Blue} mode={config.Mode}");
        _activeLEDMode = config.Mode;
        _rainbowSpeed = config.RainbowSpeed;
        _activeLEDConfig = config;
        _breathingPhase = 0;
        _strobingOn = true;

        if (config.Mode == LEDMode.Off)
        {
            StopLEDTimer();
            var report = GetActiveOutputReport();
            if (report == null) { Log($"SetLEDConfig: no output report"); return; }
            report.SetLightbar(0, 0, 0);
            SendReport();
            return;
        }

        if (config.Mode == LEDMode.Rainbow)
        {
            _rainbowHue = 0;
            StartLEDTimer();
            return;
        }

        if (config.Mode == LEDMode.Breathing)
        {
            StartLEDTimer();
            return;
        }

        if (config.Mode == LEDMode.Strobing)
        {
            StartLEDTimer();
            return;
        }

        if (config.Mode == LEDMode.Battery)
        {
            StartLEDTimer();
            return;
        }

        StopLEDTimer();
        var rpt = GetActiveOutputReport();
        if (rpt == null) { Log($"SetLEDConfig: no output report"); return; }
        rpt.SetLightbar(config.Red, config.Green, config.Blue);
        SendReport();
        Log($"SetLEDConfig: sent static color report");
    }

    private void StartLEDTimer()
    {
        _ledTimer.Stop();
        _ledTimer.Interval = _activeLEDMode switch
        {
            LEDMode.Rainbow => _rainbowSpeed switch
            {
                RainbowSpeed.Slow => 80,
                RainbowSpeed.Medium => 40,
                RainbowSpeed.Fast => 20,
                RainbowSpeed.Hyper => 10,
                _ => 40
            },
            LEDMode.Breathing => 30,
            LEDMode.Strobing => _activeLEDConfig != null ? Math.Max(20, 120 - _activeLEDConfig.StrobingSpeed) : 60,
            LEDMode.Battery => 2000,
            _ => 40
        };
        _ledTimer.Start();
    }

    private void StopLEDTimer()
    {
        _ledTimer.Stop();
    }

    private void OnLEDTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        var report = GetActiveOutputReport();
        if (report == null) return;

        switch (_activeLEDMode)
        {
            case LEDMode.Rainbow:
                _rainbowHue = (_rainbowHue + 2) % 360;
                var (r, g, b) = HsvToRgb(_rainbowHue, 1.0, 1.0);
                report.SetLightbar(r, g, b);
                break;

            case LEDMode.Breathing:
                if (_activeLEDConfig == null) { StopLEDTimer(); return; }
                var speed = _activeLEDConfig.BreathingSpeed > 0 ? _activeLEDConfig.BreathingSpeed : 50;
                _breathingPhase += speed * 0.003;
                if (_breathingPhase > Math.PI * 2) _breathingPhase -= Math.PI * 2;
                var brightness = (Math.Sin(_breathingPhase) + 1.0) / 2.0;
                report.SetLightbar(
                    (byte)(_activeLEDConfig.Red * brightness),
                    (byte)(_activeLEDConfig.Green * brightness),
                    (byte)(_activeLEDConfig.Blue * brightness));
                break;

            case LEDMode.Strobing:
                if (_activeLEDConfig == null) { StopLEDTimer(); return; }
                _strobingOn = !_strobingOn;
                if (_strobingOn)
                    report.SetLightbar(_activeLEDConfig.Red, _activeLEDConfig.Green, _activeLEDConfig.Blue);
                else
                    report.SetLightbar(0, 0, 0);
                break;

            case LEDMode.Battery:
                if (_activeLEDConfig == null) { StopLEDTimer(); return; }
                var pct = _lastBatteryLevel;
                byte br, bg, bb;
                if (pct < 30)
                {
                    br = _activeLEDConfig.BatteryLowRed;
                    bg = _activeLEDConfig.BatteryLowGreen;
                    bb = _activeLEDConfig.BatteryLowBlue;
                    if (br == 0 && bg == 0 && bb == 0) { br = 255; bg = 0; bb = 0; }
                }
                else if (pct < 60)
                {
                    br = _activeLEDConfig.BatteryMedRed;
                    bg = _activeLEDConfig.BatteryMedGreen;
                    bb = _activeLEDConfig.BatteryMedBlue;
                    if (br == 0 && bg == 0 && bb == 0) { br = 255; bg = 255; bb = 0; }
                }
                else
                {
                    br = _activeLEDConfig.BatteryFullRed;
                    bg = _activeLEDConfig.BatteryFullGreen;
                    bb = _activeLEDConfig.BatteryFullBlue;
                    if (br == 0 && bg == 0 && bb == 0) { br = 0; bg = 0; bb = 255; }
                }
                report.SetLightbar(br, bg, bb);

                if (_activePlayerLEDMode == PlayerLEDMode.BatteryLevel)
                {
                    var mask = BatteryToPlayerMask(pct);
                    report.SetPlayerLEDs(mask);
                }
                break;

            default:
                StopLEDTimer();
                return;
        }

        SendReport();

        try
        {
            _ledTimer.Start();
        }
        catch { }
    }

    private static (byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        double m = v - c;
        double r1, g1, b1;
        if (h < 60) { r1 = c; g1 = x; b1 = 0; }
        else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }
        return ((byte)((r1 + m) * 255), (byte)((g1 + m) * 255), (byte)((b1 + m) * 255));
    }

    public void SetPlayerLEDConfig(PlayerLEDConfig config)
    {
        var report = GetActiveOutputReport();
        if (report == null) return;

        byte mask = config.Mode switch
        {
            PlayerLEDMode.Player1 => P.PlayerLEDs.Player1,
            PlayerLEDMode.Player2 => P.PlayerLEDs.Player2,
            PlayerLEDMode.Player3 => P.PlayerLEDs.Player3,
            PlayerLEDMode.Player4 => P.PlayerLEDs.Player4,
            PlayerLEDMode.Player5 => P.PlayerLEDs.Player5,
            PlayerLEDMode.BatteryLevel => BatteryToPlayerMask(_lastBatteryLevel),
            _ => 0x00
        };

        _activePlayerLEDMode = config.Mode;
        report.SetPlayerLEDs(mask);
        SendReport();
    }

    private static byte BatteryToPlayerMask(double pct)
    {
        return pct switch
        {
            <= 0 => 0x00,
            <= 20 => P.PlayerLEDs.Player1,
            <= 40 => P.PlayerLEDs.Player2,
            <= 60 => P.PlayerLEDs.Player3,
            <= 80 => P.PlayerLEDs.Player4,
            _ => P.PlayerLEDs.Player5
        };
    }

    public void SetMuteLEDConfig(MuteLEDConfig config)
    {
        var report = GetActiveOutputReport();
        if (report == null) return;

        byte mode = config.Mode switch
        {
            MuteLEDMode.AlwaysOff => 0x00,
            MuteLEDMode.AlwaysOn => 0x01,
            MuteLEDMode.FollowMicMuteState => 0x02,
            _ => 0x02
        };

        report.SetMicMuteLed(mode);
        SendReport();
    }

    public void SetRumble(double largeMotor, double smallMotor)
    {
        var report = GetActiveOutputReport();
        if (report == null) { Log($"SetRumble: no output report"); return; }

        report.SetRumble(largeMotor, smallMotor);
        SendReport();
    }

    public void SetMicMute(bool muted)
    {
        var report = GetActiveOutputReport();
        if (report == null) return;

        report.SetMicMuteLed(muted ? (byte)0x01 : (byte)0x00);
        SendReport();
    }

    public void SetSpeakerVolume(byte volume)
    {
        var report = GetActiveOutputReport();
        if (report == null) return;
        report.SetSpeakerVolume(volume);
        SendReport();
    }

    public void SetHeadphoneVolume(byte volume)
    {
        var report = GetActiveOutputReport();
        if (report == null) return;
        report.SetHeadphoneVolume(volume);
        SendReport();
    }

    public void SetMicVolume(byte volume)
    {
        var report = GetActiveOutputReport();
        if (report == null) return;
        report.SetMicVolume(volume);
        SendReport();
    }

    public void RouteAudioToSpeaker()
    {
        var report = GetActiveOutputReport();
        if (report == null) return;
        report.RouteToSpeaker();
        SendReport();
    }

    public void RouteAudioToHeadphones()
    {
        var report = GetActiveOutputReport();
        if (report == null) return;
        report.RouteToHeadphones();
        SendReport();
    }

    public void WriteOutputReport(byte[] data)
    {
        if (_activeControllerId == null) return;
        if (!_streams.TryGetValue(_activeControllerId, out var stream)) return;
        if (!_controllers.TryGetValue(_activeControllerId, out var controller)) return;

        lock (_writeLock)
        {
            try
            {
                byte[] report;
                if (controller.Connection == ConnectionType.Bluetooth)
                {
                    report = new byte[BTOutputReportSize];
                    report[0] = BTOutputReportId;
                    report[1] = (byte)((_btSequence++ & 0x0F) << 4);
                    Buffer.BlockCopy(data, 0, report, 2, Math.Min(data.Length, OutputPayloadSize));
                    DSXCrc32.WriteCrc(report);
                }
                else
                {
                    report = new byte[USBOutputReportSize];
                    report[0] = USBOutputReportId;
                    Buffer.BlockCopy(data, 0, report, 1, Math.Min(data.Length, OutputPayloadSize));
                }

                stream.Write(report);
            }
            catch (IOException) { }
            catch (Exception ex)
            {
                Log($"WriteOutputReport error: {ex.Message}");
            }
        }
    }

    private DualSenseOutputReport? GetActiveOutputReport()
    {
        if (_activeControllerId == null) return null;
        return _outputReports.TryGetValue(_activeControllerId, out var r) ? r : null;
    }

    private int _sendReportCount;

    private void SendReport()
    {
        if (_activeControllerId == null) { Log("SendReport: no active controller"); return; }
        if (!_streams.TryGetValue(_activeControllerId, out var stream)) { Log("SendReport: no stream"); return; }
        if (!_controllers.TryGetValue(_activeControllerId, out var controller)) { Log("SendReport: no controller info"); return; }
        if (!_outputReports.TryGetValue(_activeControllerId, out var outputReport)) { Log("SendReport: no output report"); return; }

        lock (_writeLock)
        {
            try
            {
                byte[] report;
                if (controller.Connection == ConnectionType.Bluetooth)
                {
                    report = outputReport.BuildBTReport(_btSequence++);
                }
                else
                {
                    report = outputReport.BuildUSBReport();
                }

                stream.Write(report);

                int count = System.Threading.Interlocked.Increment(ref _sendReportCount);
                if (count % 500 == 0)
                {
                    Log($"SendReport: #{count} {report.Length}B conn={controller.Connection} ledMode={_activeLEDMode} rgb={report[^4]:X2},{report[^3]:X2},{report[^2]:X2}");
                }
            }
            catch (IOException ex) { Log($"SendReport IO error: {ex.Message}"); }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                int count = System.Threading.Interlocked.CompareExchange(ref _sendReportCount, 0, 0);
                if (count % 200 == 0)
                    Log($"SendReport error: {ex.Message}");
            }
        }
    }

    private void TryOpenDevice(HidDevice device)
    {
        try
        {
            Log($"TryOpenDevice: Attempting to open VID={device.VendorID:X4} PID={device.ProductID:X4}");
            if (!device.TryOpen(out var stream))
            {
                Log($"TryOpenDevice: Failed to open device (TryOpen returned false)");
                return;
            }

            stream.ReadTimeout = Timeout.Infinite;
            stream.WriteTimeout = 500;

            ControllerDeviceInfo info;
            try
            {
                info = BuildDeviceInfo(device);
            }
            catch (Exception bex)
            {
                Log($"TryOpenDevice: BuildDeviceInfo failed: {bex.GetType().Name}: {bex.Message}");
                var type = GetControllerType((ushort)device.VendorID, (ushort)device.ProductID);
                info = new ControllerDeviceInfo
                {
                    DeviceId = device.DevicePath,
                    Name = type.ToString(),
                    Type = type,
                    Connection = DetectConnectionType(device),
                    IsConnected = true,
                    Features = GetFeatures(type)
                };
                for (int i = 0; i < AppConstants.ProfileSlotCount; i++)
                    info.ProfileSlots[i] = new ProfileSlot { SlotIndex = i };
            }

            info.IsConnected = true;
            _controllers[device.DevicePath] = info;
            _streams[device.DevicePath] = stream;
            _outputReports[device.DevicePath] = new DualSenseOutputReport();

            if (_activeControllerId == null)
                _activeControllerId = device.DevicePath;

            Log($"TryOpenDevice: Opened {info.Name} ({info.Type}, {info.Connection}) - Active={_activeControllerId != null}");

            var thread = new Thread(() => PollDevice(device.DevicePath))
            {
                IsBackground = true,
                Name = $"DSX-Poll-{info.Name}"
            };

            _pollThreads[device.DevicePath] = thread;
            thread.Start();

            ControllerConnected?.Invoke(this, info);
            Log($"TryOpenDevice: ControllerConnected event fired for {info.Name}");

            if (info.Features.AdaptiveTriggers)
                SendTriggerCalibration();

            if (info.Connection == ConnectionType.USB)
            {
                RouteAudioToSpeaker();
                SetSpeakerVolume(100);
            }
        }
        catch (Exception ex)
        {
            Log($"TryOpenDevice: Exception - {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void PollDevice(string deviceId)
    {
        if (!_streams.TryGetValue(deviceId, out var stream)) return;
        if (!_controllers.TryGetValue(deviceId, out var info)) return;

        byte[] inputBuffer = new byte[info.Connection == ConnectionType.Bluetooth
            ? BTInputReportSize
            : USBInputReportSize];
        int consecutiveErrors = 0;

        while (!_disposed && _streams.ContainsKey(deviceId))
        {
            try
            {
                int bytesRead = stream.Read(inputBuffer);
                if (bytesRead <= 0)
                {
                    Thread.Sleep(1);
                    continue;
                }

                consecutiveErrors = 0;

                byte[] data;
                if (info.Connection == ConnectionType.Bluetooth && bytesRead > 2)
                {
                    data = new byte[bytesRead - 2];
                    Buffer.BlockCopy(inputBuffer, 2, data, 0, data.Length);
                }
                else if (info.Connection == ConnectionType.USB && bytesRead > 1)
                {
                    data = new byte[bytesRead - 1];
                    Buffer.BlockCopy(inputBuffer, 1, data, 0, data.Length);
                }
                else
                {
                    data = inputBuffer;
                }

                var state = DualSenseInputParser.Parse(data, info.Type);
                InputReceived?.Invoke(this, state);

                info.BatteryPercentage = DualSenseInputParser.ReadBattery(data, info.Connection);
                info.IsCharging = DualSenseInputParser.ReadCharging(data);
                info.BatteryStatus = DetermineBatteryStatus(info.BatteryPercentage, info.IsCharging);
                _lastBatteryLevel = info.BatteryPercentage;
            }
            catch (IOException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                consecutiveErrors++;
                if (consecutiveErrors > 100)
                    break;
                Thread.Sleep(1);
            }
        }

        DisconnectController(deviceId);
    }

    private static BatteryStatus DetermineBatteryStatus(double pct, bool charging)
    {
        if (charging && pct >= 100) return BatteryStatus.Charged;
        if (charging) return BatteryStatus.Charging;
        if (pct <= 5) return BatteryStatus.Dying;
        if (pct <= 20) return BatteryStatus.Low;
        if (pct <= 60) return BatteryStatus.Medium;
        return BatteryStatus.Full;
    }

    private ControllerType controllerType()
    {
        if (_activeControllerId != null && _controllers.TryGetValue(_activeControllerId, out var c))
            return c.Type;
        return ControllerType.Unknown;
    }

    private static bool IsGamepadInterface(HidDevice device)
    {
        var path = device.DevicePath.ToUpperInvariant();
        if (path.Contains("MI_03"))
            return true;
        if (path.Contains("MI_04") || path.Contains("MI_05"))
            return false;
        int maxInput = device.GetMaxInputReportLength();
        int maxOutput = device.GetMaxOutputReportLength();
        if (maxInput >= 78 && maxOutput >= 78)
            return true;
        if (maxInput >= 64 && maxOutput >= 48)
            return true;
        return false;
    }

    private static bool IsSupportedDevice(int vid, int pid)
    {
        if (vid != SonyVID)
            return false;
        return pid is DualSensePID or DualSenseEdgePID or DualShock4V1PID or DualShock4V2PID;
    }

    public static ControllerType GetControllerType(ushort vid, ushort pid)
    {
        if (vid != SonyVID)
            return ControllerType.Unknown;
        return pid switch
        {
            DualSensePID => ControllerType.DualSense,
            DualSenseEdgePID => ControllerType.DualSenseEdge,
            DualShock4V1PID or DualShock4V2PID => ControllerType.DualShock4,
            _ => ControllerType.Unknown
        };
    }

    private static ConnectionType DetectConnectionType(HidDevice device)
    {
        var upper = device.DevicePath.ToUpperInvariant();
        if (upper.Contains("BT_") || upper.Contains("BTH") || upper.Contains("BLUETOOTH"))
            return ConnectionType.Bluetooth;
        if (upper.Contains("00001124") || upper.Contains("00001124-0000-1000-8000-00805F9B34FB"))
            return ConnectionType.Bluetooth;
        if (device.GetMaxInputReportLength() >= 78 || device.GetMaxOutputReportLength() >= 78)
            return ConnectionType.Bluetooth;
        if (upper.Contains("WIRELESS"))
            return ConnectionType.USBWirelessAdapter;
        return ConnectionType.USB;
    }

    private static ControllerFeatures GetFeatures(ControllerType type) => type switch
    {
        ControllerType.DualSense or ControllerType.DualSenseEdge => new ControllerFeatures
        {
            AdaptiveTriggers = true, Haptics = true, Touchpad = true,
            PlayerLED = true, MotionControls = true, MuteButton = true,
            LightBar = true, Gyroscope = true, Microphone = true, AudioJack = true
        },
        ControllerType.DualShock4 => new ControllerFeatures
        {
            AdaptiveTriggers = false, Haptics = false, Touchpad = true,
            PlayerLED = false, MotionControls = true, MuteButton = false,
            LightBar = true, Gyroscope = true, Microphone = false, AudioJack = true
        },
        _ => new ControllerFeatures()
    };

    private static ControllerDeviceInfo BuildDeviceInfo(HidDevice device)
    {
        var type = GetControllerType((ushort)device.VendorID, (ushort)device.ProductID);
        var connection = DetectConnectionType(device);

        var info = new ControllerDeviceInfo
        {
            DeviceId = device.DevicePath,
            Name = device.GetProductName() ?? type.ToString(),
            Type = type,
            Connection = connection,
            MacAddress = device.GetSerialNumber() ?? string.Empty,
            IsConnected = true,
            Features = GetFeatures(type)
        };

        for (int i = 0; i < AppConstants.ProfileSlotCount; i++)
            info.ProfileSlots[i] = new ProfileSlot { SlotIndex = i };

        return info;
    }

    private void OnDeviceListChanged(object? sender, EventArgs e)
    {
        Task.Delay(500).ContinueWith(_ =>
        {
            try
            {
                var current = DeviceList.Local.GetHidDevices()
                    .Where(d => IsSupportedDevice(d.VendorID, d.ProductID) && IsGamepadInterface(d))
                    .Select(d => d.DevicePath)
                    .ToHashSet();

                foreach (var id in _controllers.Keys.ToList())
                {
                    if (!current.Contains(id))
                        DisconnectController(id);
                }

                foreach (var device in DeviceList.Local.GetHidDevices())
                {
                    if (IsSupportedDevice(device.VendorID, device.ProductID)
                        && IsGamepadInterface(device)
                        && !_controllers.ContainsKey(device.DevicePath))
                    {
                        TryOpenDevice(device);
                    }
                }
            }
            catch { }
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _ledTimer.Dispose();
        StopPolling();
    }
}
