using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSX.Core.Enums;
using DSX.Core.Models;

namespace DSX.Core.ViewModels;

public partial class HapticsRumbleViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private double _largeMotorIntensity;

    [ObservableProperty]
    private double _smallMotorIntensity;

    [ObservableProperty]
    private bool _audioHapticsEnabled;

    [ObservableProperty]
    private int _audioDelayMs = 5;

    [ObservableProperty]
    private ObservableCollection<string> _availableAudioDevices = new();

    [ObservableProperty]
    private string _selectedAudioDevice = string.Empty;

    [ObservableProperty]
    private double _leftMotorVolume = 50;

    [ObservableProperty]
    private double _rightMotorVolume = 50;

    [ObservableProperty]
    private double _headsetVolume = 50;

    [ObservableProperty]
    private double _speakerVolume = 50;

    [ObservableProperty]
    private bool _syncWithSystemVolume = true;

    [ObservableProperty]
    private bool _showBTHaptics;

    [ObservableProperty]
    private AudioSourceMode _audioSourceMode = AudioSourceMode.SoundCapture;

    [ObservableProperty]
    private HapticsSourceMode _hapticsSourceMode = HapticsSourceMode.SoundWaves;

    [ObservableProperty]
    private double _bTLeftMotorIntensity = 50;

    [ObservableProperty]
    private double _bTRightMotorIntensity = 50;

    [ObservableProperty]
    private int _bTLatencyMs;

    [ObservableProperty]
    private int _bTRSSI;

    [ObservableProperty]
    private string _audioFilePath = string.Empty;

    [ObservableProperty]
    private bool _isBTHapticsRunning;

    [ObservableProperty]
    private string _bTHapticsStatus = "Stopped";

    partial void OnLeftMotorVolumeChanged(double value)
    {
        _main.ControllerService.SetSpeakerVolume((byte)(value * 0x64 / 100.0));
    }

    partial void OnRightMotorVolumeChanged(double value)
    {
        _main.ControllerService.SetHeadphoneVolume((byte)(value * 0x7F / 100.0));
    }

    partial void OnHeadsetVolumeChanged(double value)
    {
        _main.ControllerService.SetHeadphoneVolume((byte)(value * 0x7F / 100.0));
    }

    partial void OnSpeakerVolumeChanged(double value)
    {
        _main.ControllerService.SetSpeakerVolume((byte)(value * 0x64 / 100.0));
    }

    public event EventHandler? BrowseAudioFileDialogRequested;

    public HapticsRumbleViewModel(MainViewModel main)
    {
        _main = main;
        _main.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ActiveProfile))
                LoadFromProfile();
            if (e.PropertyName == nameof(MainViewModel.ActiveController))
                UpdateControllerInfo();
            if (e.PropertyName == nameof(MainViewModel.IsDSXPlusOwner))
                ShowBTHaptics = _main.IsDSXPlusOwner;
        };
        LoadFromProfile();
        RefreshDevices();

        _main.AudioService.BTHapticsDataReady += OnBTHapticsData;
        _main.AudioService.BTHapticsError += OnBTHapticsError;
    }

    private void OnBTHapticsData(object? sender, Services.Audio.HapticsDataEventArgs e)
    {
    }

    private void OnBTHapticsError(object? sender, string error)
    {
        IsBTHapticsRunning = false;
        BTHapticsStatus = $"Error: {error}";
    }

    private void LoadFromProfile()
    {
        var profile = _main.ActiveProfile;
        if (profile == null) return;

        LargeMotorIntensity = profile.RumbleConfig.LargeMotorIntensity;
        SmallMotorIntensity = profile.RumbleConfig.SmallMotorIntensity;
        AudioHapticsEnabled = profile.AudioHapticsConfig.Enabled;
        AudioDelayMs = profile.AudioHapticsConfig.DelayMs;
        LeftMotorVolume = profile.AudioHapticsConfig.LeftMotorVolume;
        RightMotorVolume = profile.AudioHapticsConfig.RightMotorVolume;
        HeadsetVolume = profile.AudioHapticsConfig.HeadsetVolume;
        SpeakerVolume = profile.AudioHapticsConfig.SpeakerVolume;
        SyncWithSystemVolume = profile.AudioHapticsConfig.SyncWithSystemVolume;

        AudioSourceMode = profile.BTHapticsConfig.AudioSourceMode;
        HapticsSourceMode = profile.BTHapticsConfig.HapticsSourceMode;
        BTLeftMotorIntensity = profile.BTHapticsConfig.LeftMotorIntensity;
        BTRightMotorIntensity = profile.BTHapticsConfig.RightMotorIntensity;
        BTLatencyMs = profile.BTHapticsConfig.LatencyCompensationMs;
        AudioFilePath = profile.BTHapticsConfig.AudioFilePath;
    }

    private void UpdateControllerInfo()
    {
        var controller = _main.ActiveController;
        if (controller != null)
        {
            BTRSSI = controller.BTRSSI;
            ShowBTHaptics = _main.IsDSXPlusOwner && controller.Connection == ConnectionType.Bluetooth;
        }
    }

    [RelayCommand]
    private async Task TestRumble()
    {
        _main.ControllerService.SetRumble(LargeMotorIntensity, SmallMotorIntensity);
        await Task.Delay(500);
        _main.ControllerService.SetRumble(0, 0);
    }

    [RelayCommand]
    private void ToggleAudioHaptics()
    {
        AudioHapticsEnabled = !AudioHapticsEnabled;
        if (AudioHapticsEnabled)
            _main.AudioService.StartCapture(SelectedAudioDevice);
        else
            _main.AudioService.StopCapture();
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        AvailableAudioDevices = new ObservableCollection<string>(
            _main.AudioService.AvailableDevices);
        if (string.IsNullOrEmpty(SelectedAudioDevice) && AvailableAudioDevices.Count > 0)
            SelectedAudioDevice = _main.AudioService.DefaultDeviceId;
    }

    [RelayCommand]
    private void StartBTHaptics()
    {
        _main.AudioService.StartBTHaptics(
            audioSource: AudioSourceMode,
            hapticsSource: HapticsSourceMode,
            leftIntensity: BTLeftMotorIntensity,
            rightIntensity: BTRightMotorIntensity,
            latencyCompensation: BTLatencyMs,
            audioFilePath: AudioFilePath);

        IsBTHapticsRunning = true;
        BTHapticsStatus = "Running";
    }

    [RelayCommand]
    private void StopBTHaptics()
    {
        _main.AudioService.StopBTHaptics();
        _main.ControllerService.SetRumble(0, 0);
        IsBTHapticsRunning = false;
        BTHapticsStatus = "Stopped";
    }

    [RelayCommand]
    private void ToggleBTHaptics()
    {
        ShowBTHaptics = !ShowBTHaptics;
    }

    [RelayCommand]
    private void BrowseAudioFile()
    {
        BrowseAudioFileDialogRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetAudioFile(string filePath)
    {
        AudioFilePath = filePath;
    }
}
