using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSX.Core.Enums;
using DSX.Core.Models;

namespace DSX.Core.ViewModels;

public partial class MyControllersViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private ControllerDeviceInfo? _selectedController;

    [ObservableProperty]
    private string _controllerName = string.Empty;

    [ObservableProperty]
    private ConnectionType _connectionType;

    [ObservableProperty]
    private string _firmwareVersion = string.Empty;

    [ObservableProperty]
    private string _buildDate = string.Empty;

    [ObservableProperty]
    private string _macAddress = string.Empty;

    [ObservableProperty]
    private double _batteryPercent;

    [ObservableProperty]
    private bool _isCharging;

    [ObservableProperty]
    private int _bTRSSI;

    [ObservableProperty]
    private string _connectionIcon = "USB";

    [ObservableProperty]
    private ObservableCollection<FeatureItem> _deviceFeatures = new();

    [ObservableProperty]
    private ObservableCollection<ProfileSlotViewModel> _profileSlots = new();

    [ObservableProperty]
    private bool _autoConnectOnLaunch = true;

    [ObservableProperty]
    private bool _killSteamOnLaunch;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _toggleMicMute = true;

    [ObservableProperty]
    private bool _motionEnabled = true;

    [ObservableProperty]
    private bool _notifyConnection = true;

    [ObservableProperty]
    private bool _notifyProfileSwitch = true;

    [ObservableProperty]
    private bool _notifyBatteryLow = true;

    [ObservableProperty]
    private bool _isDualSenseEdge;

    [ObservableProperty]
    private ObservableCollection<EdgeOnBoardProfile> _edgeProfiles = new();

    public event EventHandler<string>? CopyMacRequested;

    public MyControllersViewModel(MainViewModel main)
    {
        _main = main;
        _main.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ActiveController))
                UpdateControllerDetails();
        };
        UpdateControllerDetails();
    }

    partial void OnSelectedControllerChanged(ControllerDeviceInfo? value)
    {
        UpdateFromController(value);
    }

    private void UpdateControllerDetails()
    {
        var controller = _main.ActiveController;
        if (controller != null)
            UpdateFromController(controller);
    }

    private void UpdateFromController(ControllerDeviceInfo? controller)
    {
        if (controller == null) return;

        ControllerName = controller.Name;
        ConnectionType = controller.Connection;
        FirmwareVersion = controller.FirmwareVersion;
        BuildDate = controller.BuildDate;
        MacAddress = controller.MacAddress;
        BatteryPercent = controller.BatteryPercentage;
        IsCharging = controller.IsCharging;
        BTRSSI = controller.BTRSSI;
        ConnectionIcon = controller.Connection switch
        {
            ConnectionType.Bluetooth => "BT",
            ConnectionType.USBWirelessAdapter => "Wireless",
            _ => "USB"
        };
        IsDualSenseEdge = controller.Type == ControllerType.DualSenseEdge;

        DeviceFeatures = new ObservableCollection<FeatureItem>
        {
            new("Adaptive Triggers", controller.Features.AdaptiveTriggers),
            new("Haptics", controller.Features.Haptics),
            new("Touchpad", controller.Features.Touchpad),
            new("Player LED", controller.Features.PlayerLED),
            new("Motion Controls", controller.Features.MotionControls),
            new("Mute Button", controller.Features.MuteButton),
            new("Light Bar", controller.Features.LightBar),
            new("Gyroscope", controller.Features.Gyroscope),
            new("Microphone", controller.Features.Microphone),
            new("Audio Jack", controller.Features.AudioJack)
        };

        ProfileSlots = new ObservableCollection<ProfileSlotViewModel>(
            controller.ProfileSlots.Select((s, i) => new ProfileSlotViewModel
            {
                SlotIndex = i,
                ProfileId = s?.ProfileId ?? string.Empty,
                ProfileName = s?.ProfileName ?? "Empty",
                IsActive = s?.IsActive ?? false
            }));

        if (IsDualSenseEdge)
        {
            EdgeProfiles = new ObservableCollection<EdgeOnBoardProfile>(
                _main.ProfileService.GetSlots(controller.DeviceId)
                    .Where(s => !string.IsNullOrEmpty(s.ProfileId))
                    .Select(s =>
                    {
                        var p = _main.ProfileService.GetProfile(s.ProfileId);
                        return new EdgeOnBoardProfile
                        {
                            Index = s.SlotIndex,
                            Name = s.ProfileName,
                            LastModified = DateTime.Now
                        };
                    }));
        }
    }

    [RelayCommand]
    private void CopyMacAddress()
    {
        if (!string.IsNullOrEmpty(MacAddress))
            CopyMacRequested?.Invoke(this, MacAddress);
    }

    [RelayCommand]
    private void ToggleAutoConnect()
    {
        AutoConnectOnLaunch = !AutoConnectOnLaunch;
    }

    [RelayCommand]
    private void ToggleStartWithWindows()
    {
        StartWithWindows = !StartWithWindows;
    }

    [RelayCommand]
    private void AssignProfileToSlot()
    {
        var controller = _main.ActiveController;
        if (controller == null) return;
    }

    [RelayCommand]
    private void CycleSlot()
    {
        var controller = _main.ActiveController;
        if (controller == null) return;
        _main.ProfileService.CycleSlot(controller.DeviceId);
        UpdateFromController(controller);
    }

    [RelayCommand]
    private void ActivateSlot()
    {
        var controller = _main.ActiveController;
        if (controller == null || SelectedController == null) return;
    }

    public class FeatureItem
    {
        public string Name { get; set; } = string.Empty;
        public bool Supported { get; set; }
        public FeatureItem(string name, bool supported)
        {
            Name = name;
            Supported = supported;
        }
    }
}

public class ProfileSlotViewModel
{
    public int SlotIndex { get; set; }
    public string ProfileId { get; set; } = string.Empty;
    public string ProfileName { get; set; } = "Empty";
    public bool IsActive { get; set; }
    public string ActivateKey { get; set; } = string.Empty;
}
