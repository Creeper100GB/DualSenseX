using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSX.Core.Enums;
using DSX.Core.Models;
using DSX.Core.Services.Trigger;

namespace DSX.Core.ViewModels;

public partial class AdaptiveTriggersViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private TriggerConfig _leftTriggerConfig = new();

    [ObservableProperty]
    private TriggerConfig _rightTriggerConfig = new();

    [ObservableProperty]
    private ObservableCollection<TriggerMode> _availableTriggerModes = new(
        Enum.GetValues<TriggerMode>());

    [ObservableProperty]
    private TriggerMode _selectedLeftTriggerMode;

    [ObservableProperty]
    private TriggerMode _selectedRightTriggerMode;

    [ObservableProperty]
    private int _leftStartPosition;

    [ObservableProperty]
    private int _leftEndPosition = 255;

    [ObservableProperty]
    private int _leftForce = 50;

    [ObservableProperty]
    private int _leftAmplitude = 50;

    [ObservableProperty]
    private int _leftFrequency = 50;

    [ObservableProperty]
    private int _rightStartPosition;

    [ObservableProperty]
    private int _rightEndPosition = 255;

    [ObservableProperty]
    private int _rightForce = 50;

    [ObservableProperty]
    private int _rightAmplitude = 50;

    [ObservableProperty]
    private int _rightFrequency = 50;

    [ObservableProperty]
    private int[] _customBytes = new int[7];

    [ObservableProperty]
    private bool _showDS4Warning;

    [ObservableProperty]
    private TriggerPreset _selectedPreset = TriggerPreset.Custom;

    [ObservableProperty]
    private string _presetDescription = string.Empty;

    [ObservableProperty]
    private TriggerPresetConfig? _selectedPresetFromList;

    public ObservableCollection<TriggerPresetConfig> AvailablePresets { get; } =
        new(TriggerPresets.AllPresets);

    public event EventHandler<string>? ExportRequested;
    public event EventHandler<string>? ImportRequested;

    public AdaptiveTriggersViewModel(MainViewModel main)
    {
        _main = main;
        _main.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ActiveProfile))
                LoadFromProfile();
            if (e.PropertyName == nameof(MainViewModel.ActiveController))
                UpdateControllerWarning();
        };
        LoadFromProfile();
        UpdateControllerWarning();
    }

    partial void OnSelectedLeftTriggerModeChanged(TriggerMode value)
    {
        LeftTriggerConfig.Mode = value;
        SelectedPreset = TriggerPreset.Custom;
    }

    partial void OnSelectedRightTriggerModeChanged(TriggerMode value)
    {
        RightTriggerConfig.Mode = value;
        SelectedPreset = TriggerPreset.Custom;
    }

    partial void OnSelectedPresetChanged(TriggerPreset value)
    {
        if (value == TriggerPreset.Custom) return;

        var preset = TriggerPresets.GetPreset(value);
        if (preset == null) return;

        PresetDescription = preset.Description;

        SelectedLeftTriggerMode = preset.LeftTrigger.Mode;
        SelectedRightTriggerMode = preset.RightTrigger.Mode;
        LeftStartPosition = preset.LeftTrigger.StartPosition;
        LeftEndPosition = preset.LeftTrigger.EndPosition;
        LeftForce = preset.LeftTrigger.Force;
        LeftAmplitude = preset.LeftTrigger.Amplitude;
        LeftFrequency = preset.LeftTrigger.Frequency;
        RightStartPosition = preset.RightTrigger.StartPosition;
        RightEndPosition = preset.RightTrigger.EndPosition;
        RightForce = preset.RightTrigger.Force;
        RightAmplitude = preset.RightTrigger.Amplitude;
        RightFrequency = preset.RightTrigger.Frequency;
    }

    partial void OnSelectedPresetFromListChanged(TriggerPresetConfig? value)
    {
        if (value == null) return;
        SelectedPreset = value.Preset;
    }

    [RelayCommand]
    private void ApplyPreset()
    {
        if (SelectedPreset == TriggerPreset.Custom)
        {
            ApplyLeftTrigger();
            ApplyRightTrigger();
            return;
        }

        var preset = TriggerPresets.GetPreset(SelectedPreset);
        if (preset == null) return;

        LeftTriggerConfig = new TriggerConfig
        {
            Mode = preset.LeftTrigger.Mode,
            StartPosition = preset.LeftTrigger.StartPosition,
            EndPosition = preset.LeftTrigger.EndPosition,
            Force = preset.LeftTrigger.Force,
            Amplitude = preset.LeftTrigger.Amplitude,
            Frequency = preset.LeftTrigger.Frequency
        };

        RightTriggerConfig = new TriggerConfig
        {
            Mode = preset.RightTrigger.Mode,
            StartPosition = preset.RightTrigger.StartPosition,
            EndPosition = preset.RightTrigger.EndPosition,
            Force = preset.RightTrigger.Force,
            Amplitude = preset.RightTrigger.Amplitude,
            Frequency = preset.RightTrigger.Frequency
        };

        _main.ControllerService.SetAdaptiveTrigger(Interfaces.TriggerSide.Left, LeftTriggerConfig);
        _main.ControllerService.SetAdaptiveTrigger(Interfaces.TriggerSide.Right, RightTriggerConfig);
    }

    private void LoadFromProfile()
    {
        var profile = _main.ActiveProfile;
        if (profile == null) return;

        LeftTriggerConfig = profile.LeftTriggerConfig;
        RightTriggerConfig = profile.RightTriggerConfig;
        SelectedLeftTriggerMode = LeftTriggerConfig.Mode;
        SelectedRightTriggerMode = RightTriggerConfig.Mode;

        LeftStartPosition = LeftTriggerConfig.StartPosition;
        LeftEndPosition = LeftTriggerConfig.EndPosition;
        LeftForce = LeftTriggerConfig.Force;
        LeftAmplitude = LeftTriggerConfig.Amplitude;
        LeftFrequency = LeftTriggerConfig.Frequency;

        RightStartPosition = RightTriggerConfig.StartPosition;
        RightEndPosition = RightTriggerConfig.EndPosition;
        RightForce = RightTriggerConfig.Force;
        RightAmplitude = RightTriggerConfig.Amplitude;
        RightFrequency = RightTriggerConfig.Frequency;
    }

    private void UpdateControllerWarning()
    {
        ShowDS4Warning = _main.ActiveController?.Type == ControllerType.DualShock4;
    }

    [RelayCommand]
    private void ApplyLeftTrigger()
    {
        LeftTriggerConfig = new TriggerConfig
        {
            Mode = SelectedLeftTriggerMode,
            StartPosition = LeftStartPosition,
            EndPosition = LeftEndPosition,
            Force = LeftForce,
            Amplitude = LeftAmplitude,
            Frequency = LeftFrequency,
            CustomBytes = CustomBytes
        };
        _main.ControllerService.SetAdaptiveTrigger(Interfaces.TriggerSide.Left, LeftTriggerConfig);
    }

    [RelayCommand]
    private void ApplyRightTrigger()
    {
        RightTriggerConfig = new TriggerConfig
        {
            Mode = SelectedRightTriggerMode,
            StartPosition = RightStartPosition,
            EndPosition = RightEndPosition,
            Force = RightForce,
            Amplitude = RightAmplitude,
            Frequency = RightFrequency,
            CustomBytes = CustomBytes
        };
        _main.ControllerService.SetAdaptiveTrigger(Interfaces.TriggerSide.Right, RightTriggerConfig);
    }

    [RelayCommand]
    private void ExportToClipboard()
    {
        var data = $"Left={SelectedLeftTriggerMode},{LeftStartPosition},{LeftEndPosition},{LeftForce},{LeftAmplitude},{LeftFrequency}|Right={SelectedRightTriggerMode},{RightStartPosition},{RightEndPosition},{RightForce},{RightAmplitude},{RightFrequency}";
        ExportRequested?.Invoke(this, data);
    }

    [RelayCommand]
    private void ExportForTextfile()
    {
        var data = $"Left={SelectedLeftTriggerMode},{LeftStartPosition},{LeftEndPosition},{LeftForce},{LeftAmplitude},{LeftFrequency}|Right={SelectedRightTriggerMode},{RightStartPosition},{RightEndPosition},{RightForce},{RightAmplitude},{RightFrequency}";
        ExportRequested?.Invoke(this, data);
    }

    [RelayCommand]
    private void ExportForUDP()
    {
        var data = System.Text.Encoding.UTF8.GetBytes(
            $"Left={SelectedLeftTriggerMode},{LeftStartPosition},{LeftEndPosition},{LeftForce},{LeftAmplitude},{LeftFrequency}|Right={SelectedRightTriggerMode},{RightStartPosition},{RightEndPosition},{RightForce},{RightAmplitude},{RightFrequency}");
        _main.UdpServer.Send(data, "127.0.0.1", 6969);
    }

    [RelayCommand]
    private void ImportFromClipboard()
    {
        ImportRequested?.Invoke(this, "clipboard");
    }

    [RelayCommand]
    private void ImportFromFile()
    {
        ImportRequested?.Invoke(this, "file");
    }

    public void ParseTriggerString(string data)
    {
        var parts = data.Split('|');
        foreach (var part in parts)
        {
            var tokens = part.Split('=');
            if (tokens.Length != 2) continue;
            var values = tokens[1].Split(',');
            if (values.Length < 6) continue;

            if (tokens[0] == "Left" && Enum.TryParse<TriggerMode>(values[0], out var leftMode))
            {
                SelectedLeftTriggerMode = leftMode;
                LeftStartPosition = int.TryParse(values[1], out var sp) ? sp : LeftStartPosition;
                LeftEndPosition = int.TryParse(values[2], out var ep) ? ep : LeftEndPosition;
                LeftForce = int.TryParse(values[3], out var f) ? f : LeftForce;
                LeftAmplitude = int.TryParse(values[4], out var a) ? a : LeftAmplitude;
                LeftFrequency = int.TryParse(values[5], out var fr) ? fr : LeftFrequency;
            }
            else if (tokens[0] == "Right" && Enum.TryParse<TriggerMode>(values[0], out var rightMode))
            {
                SelectedRightTriggerMode = rightMode;
                RightStartPosition = int.TryParse(values[1], out var sp) ? sp : RightStartPosition;
                RightEndPosition = int.TryParse(values[2], out var ep) ? ep : RightEndPosition;
                RightForce = int.TryParse(values[3], out var f) ? f : RightForce;
                RightAmplitude = int.TryParse(values[4], out var a) ? a : RightAmplitude;
                RightFrequency = int.TryParse(values[5], out var fr) ? fr : RightFrequency;
            }
        }
    }
}
