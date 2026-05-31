using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSX.Core.Enums;
using DSX.Core.Models;

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
    private int _startPosition;

    [ObservableProperty]
    private int _endPosition = 255;

    [ObservableProperty]
    private int _force = 50;

    [ObservableProperty]
    private int _amplitude = 50;

    [ObservableProperty]
    private int _frequency = 50;

    [ObservableProperty]
    private int[] _customBytes = new int[7];

    [ObservableProperty]
    private bool _showDS4Warning;

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
    }

    partial void OnSelectedRightTriggerModeChanged(TriggerMode value)
    {
        RightTriggerConfig.Mode = value;
    }

    private void LoadFromProfile()
    {
        var profile = _main.ActiveProfile;
        if (profile == null) return;

        LeftTriggerConfig = profile.LeftTriggerConfig;
        RightTriggerConfig = profile.RightTriggerConfig;
        SelectedLeftTriggerMode = LeftTriggerConfig.Mode;
        SelectedRightTriggerMode = RightTriggerConfig.Mode;
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
            StartPosition = StartPosition,
            EndPosition = EndPosition,
            Force = Force,
            Amplitude = Amplitude,
            Frequency = Frequency,
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
            StartPosition = StartPosition,
            EndPosition = EndPosition,
            Force = Force,
            Amplitude = Amplitude,
            Frequency = Frequency,
            CustomBytes = CustomBytes
        };
        _main.ControllerService.SetAdaptiveTrigger(Interfaces.TriggerSide.Right, RightTriggerConfig);
    }

    [RelayCommand]
    private void ExportToClipboard()
    {
        var data = $"Left={SelectedLeftTriggerMode},{StartPosition},{EndPosition},{Force},{Amplitude},{Frequency}|Right={SelectedRightTriggerMode},{StartPosition},{EndPosition},{Force},{Amplitude},{Frequency}";
        ExportRequested?.Invoke(this, data);
    }

    [RelayCommand]
    private void ExportForTextfile()
    {
        var data = $"Left={SelectedLeftTriggerMode},{StartPosition},{EndPosition},{Force},{Amplitude},{Frequency}|Right={SelectedRightTriggerMode},{StartPosition},{EndPosition},{Force},{Amplitude},{Frequency}";
        ExportRequested?.Invoke(this, data);
    }

    [RelayCommand]
    private void ExportForUDP()
    {
        var data = System.Text.Encoding.UTF8.GetBytes(
            $"Left={SelectedLeftTriggerMode},{StartPosition},{EndPosition},{Force},{Amplitude},{Frequency}|Right={SelectedRightTriggerMode},{StartPosition},{EndPosition},{Force},{Amplitude},{Frequency}");
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
                StartPosition = int.TryParse(values[1], out var sp) ? sp : StartPosition;
                EndPosition = int.TryParse(values[2], out var ep) ? ep : EndPosition;
                Force = int.TryParse(values[3], out var f) ? f : Force;
                Amplitude = int.TryParse(values[4], out var a) ? a : Amplitude;
                Frequency = int.TryParse(values[5], out var fr) ? fr : Frequency;
            }
        }
    }
}
