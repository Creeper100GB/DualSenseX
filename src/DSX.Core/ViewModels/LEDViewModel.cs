using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSX.Core.Enums;
using DSX.Core.Models;

namespace DSX.Core.ViewModels;

public partial class LEDViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private TouchpadLEDConfig _touchpadLEDConfig = new();

    [ObservableProperty]
    private PlayerLEDConfig _playerLEDConfig = new();

    [ObservableProperty]
    private MuteLEDConfig _muteLEDConfig = new();

    [ObservableProperty]
    private ObservableCollection<LEDMode> _availableLEDModes = new(
        Enum.GetValues<LEDMode>());

    [ObservableProperty]
    private ObservableCollection<PlayerLEDMode> _availablePlayerLEDModes = new(
        Enum.GetValues<PlayerLEDMode>());

    [ObservableProperty]
    private ObservableCollection<RainbowSpeed> _rainbowSpeeds = new(
        Enum.GetValues<RainbowSpeed>());

    [ObservableProperty]
    private LEDMode _selectedTouchpadMode;

    [ObservableProperty]
    private PlayerLEDMode _selectedPlayerMode;

    [ObservableProperty]
    private MuteLEDMode _selectedMuteMode;

    [ObservableProperty]
    private byte _touchpadRed;

    [ObservableProperty]
    private byte _touchpadGreen;

    [ObservableProperty]
    private byte _touchpadBlue;

    [ObservableProperty]
    private RainbowSpeed _selectedRainbowSpeed = RainbowSpeed.Medium;

    [ObservableProperty]
    private double _breathingSpeed = 50;

    [ObservableProperty]
    private double _strobingSpeed = 50;

    [ObservableProperty]
    private byte _batteryLowRed = 255;

    [ObservableProperty]
    private byte _batteryLowGreen;

    [ObservableProperty]
    private byte _batteryLowBlue;

    [ObservableProperty]
    private byte _batteryMedRed = 255;

    [ObservableProperty]
    private byte _batteryMedGreen = 255;

    [ObservableProperty]
    private byte _batteryMedBlue;

    [ObservableProperty]
    private byte _batteryFullRed;

    [ObservableProperty]
    private byte _batteryFullGreen = 255;

    [ObservableProperty]
    private byte _batteryFullBlue;

    [ObservableProperty]
    private bool _flashWhileCharging;

    [ObservableProperty]
    private byte _mutedRed = 255;

    [ObservableProperty]
    private byte _mutedGreen;

    [ObservableProperty]
    private byte _mutedBlue;

    [ObservableProperty]
    private byte _unmutedRed;

    [ObservableProperty]
    private byte _unmutedGreen = 255;

    [ObservableProperty]
    private byte _unmutedBlue;

    [ObservableProperty]
    private string _controllerViewSkin = "Default";

    [ObservableProperty]
    private ObservableCollection<string> _availableSkins = new() { "Default", "Edge", "Custom" };

    [ObservableProperty]
    private bool _isDualSenseEdge;

    public ObservableCollection<MuteLEDMode> AvailableMuteLEDModes { get; } = new(Enum.GetValues<MuteLEDMode>());

    public LEDViewModel(MainViewModel main)
    {
        _main = main;
        _main.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ActiveProfile))
                LoadFromProfile();
            if (e.PropertyName == nameof(MainViewModel.ActiveController))
                UpdateControllerType();
        };
        main.ControllerService.ControllerConnected += (s, e) => AutoApplyLED();
        LoadFromProfile();
        UpdateControllerType();
        AutoApplyLED();
    }

    private void AutoApplyLED()
    {
        if (_main.ControllerService.ActiveController?.IsConnected != true)
            return;

        var touchpadConfig = new TouchpadLEDConfig
        {
            Mode = SelectedTouchpadMode,
            Red = TouchpadRed,
            Green = TouchpadGreen,
            Blue = TouchpadBlue,
            RainbowSpeed = SelectedRainbowSpeed,
            BreathingSpeed = BreathingSpeed,
            StrobingSpeed = StrobingSpeed,
            BatteryLowRed = BatteryLowRed,
            BatteryLowGreen = BatteryLowGreen,
            BatteryLowBlue = BatteryLowBlue,
            BatteryMedRed = BatteryMedRed,
            BatteryMedGreen = BatteryMedGreen,
            BatteryMedBlue = BatteryMedBlue,
            BatteryFullRed = BatteryFullRed,
            BatteryFullGreen = BatteryFullGreen,
            BatteryFullBlue = BatteryFullBlue
        };
        var playerConfig = new PlayerLEDConfig
        {
            Mode = SelectedPlayerMode,
            FlashWhileCharging = FlashWhileCharging
        };
        var muteConfig = new MuteLEDConfig
        {
            Mode = SelectedMuteMode,
            MutedRed = MutedRed,
            MutedGreen = MutedGreen,
            MutedBlue = MutedBlue,
            UnmutedRed = UnmutedRed,
            UnmutedGreen = UnmutedGreen,
            UnmutedBlue = UnmutedBlue
        };

        Task.Run(() =>
        {
            _main.ControllerService.SetLEDConfig(touchpadConfig);
            _main.ControllerService.SetPlayerLEDConfig(playerConfig);
            _main.ControllerService.SetMuteLEDConfig(muteConfig);
        });
    }

    private void UpdateControllerType()
    {
        IsDualSenseEdge = _main.ActiveController?.Type == Enums.ControllerType.DualSenseEdge;
    }

    private void LoadFromProfile()
    {
        var profile = _main.ActiveProfile;
        if (profile == null) return;

        TouchpadLEDConfig = profile.TouchpadLEDConfig;
        PlayerLEDConfig = profile.PlayerLEDConfig;
        MuteLEDConfig = profile.MuteLEDConfig;

        SelectedTouchpadMode = TouchpadLEDConfig.Mode;
        TouchpadRed = TouchpadLEDConfig.Red;
        TouchpadGreen = TouchpadLEDConfig.Green;
        TouchpadBlue = TouchpadLEDConfig.Blue;
        SelectedRainbowSpeed = TouchpadLEDConfig.RainbowSpeed;
        BreathingSpeed = TouchpadLEDConfig.BreathingSpeed;
        StrobingSpeed = TouchpadLEDConfig.StrobingSpeed;
        BatteryLowRed = TouchpadLEDConfig.BatteryLowRed;
        BatteryLowGreen = TouchpadLEDConfig.BatteryLowGreen;
        BatteryLowBlue = TouchpadLEDConfig.BatteryLowBlue;
        BatteryMedRed = TouchpadLEDConfig.BatteryMedRed;
        BatteryMedGreen = TouchpadLEDConfig.BatteryMedGreen;
        BatteryMedBlue = TouchpadLEDConfig.BatteryMedBlue;
        BatteryFullRed = TouchpadLEDConfig.BatteryFullRed;
        BatteryFullGreen = TouchpadLEDConfig.BatteryFullGreen;
        BatteryFullBlue = TouchpadLEDConfig.BatteryFullBlue;

        SelectedPlayerMode = PlayerLEDConfig.Mode;
        FlashWhileCharging = PlayerLEDConfig.FlashWhileCharging;

        SelectedMuteMode = MuteLEDConfig.Mode;
        MutedRed = MuteLEDConfig.MutedRed;
        MutedGreen = MuteLEDConfig.MutedGreen;
        MutedBlue = MuteLEDConfig.MutedBlue;
        UnmutedRed = MuteLEDConfig.UnmutedRed;
        UnmutedGreen = MuteLEDConfig.UnmutedGreen;
        UnmutedBlue = MuteLEDConfig.UnmutedBlue;
    }

    [RelayCommand]
    private void ApplyTouchpadLED()
    {
        var config = new TouchpadLEDConfig
        {
            Mode = SelectedTouchpadMode,
            Red = TouchpadRed,
            Green = TouchpadGreen,
            Blue = TouchpadBlue,
            RainbowSpeed = SelectedRainbowSpeed,
            BreathingSpeed = BreathingSpeed,
            StrobingSpeed = StrobingSpeed,
            BatteryLowRed = BatteryLowRed,
            BatteryLowGreen = BatteryLowGreen,
            BatteryLowBlue = BatteryLowBlue,
            BatteryMedRed = BatteryMedRed,
            BatteryMedGreen = BatteryMedGreen,
            BatteryMedBlue = BatteryMedBlue,
            BatteryFullRed = BatteryFullRed,
            BatteryFullGreen = BatteryFullGreen,
            BatteryFullBlue = BatteryFullBlue
        };
        _main.ControllerService.SetLEDConfig(config);

        var profile = _main.ActiveProfile;
        if (profile != null)
        {
            profile.TouchpadLEDConfig = config;
            _main.ProfileService.UpdateProfile(profile);
        }
    }

    [RelayCommand]
    private void ApplyPlayerLED()
    {
        var config = new PlayerLEDConfig
        {
            Mode = SelectedPlayerMode,
            FlashWhileCharging = FlashWhileCharging
        };
        _main.ControllerService.SetPlayerLEDConfig(config);

        var profile = _main.ActiveProfile;
        if (profile != null)
        {
            profile.PlayerLEDConfig = config;
            _main.ProfileService.UpdateProfile(profile);
        }
    }

    [RelayCommand]
    private void ApplyMuteLED()
    {
        var config = new MuteLEDConfig
        {
            Mode = SelectedMuteMode,
            MutedRed = MutedRed,
            MutedGreen = MutedGreen,
            MutedBlue = MutedBlue,
            UnmutedRed = UnmutedRed,
            UnmutedGreen = UnmutedGreen,
            UnmutedBlue = UnmutedBlue
        };
        _main.ControllerService.SetMuteLEDConfig(config);

        var profile = _main.ActiveProfile;
        if (profile != null)
        {
            profile.MuteLEDConfig = config;
            _main.ProfileService.UpdateProfile(profile);
        }
    }

    [RelayCommand]
    private void SetRainbowSpeed(RainbowSpeed speed)
    {
        SelectedRainbowSpeed = speed;
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        SelectedTouchpadMode = LEDMode.Off;
        TouchpadRed = 0; TouchpadGreen = 0; TouchpadBlue = 0;
        SelectedRainbowSpeed = RainbowSpeed.Medium;
        BreathingSpeed = 50; StrobingSpeed = 50;
        SelectedPlayerMode = PlayerLEDMode.Off;
        FlashWhileCharging = false;
        SelectedMuteMode = MuteLEDMode.FollowMicMuteState;
        MutedRed = 255; MutedGreen = 0; MutedBlue = 0;
        UnmutedRed = 0; UnmutedGreen = 255; UnmutedBlue = 0;

        ApplyTouchpadLED();
        ApplyPlayerLED();
        ApplyMuteLED();
    }
}
