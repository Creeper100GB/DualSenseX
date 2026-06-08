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
    private bool _suppressToggle;

    [ObservableProperty]
    private double _largeMotorIntensity;

    [ObservableProperty]
    private double _smallMotorIntensity;

    [ObservableProperty]
    private bool _audioHapticsEnabled;

    partial void OnAudioHapticsEnabledChanged(bool value)
    {
        if (_suppressToggle) return;

        if (value)
        {
            _main.AudioService.StartCapture(SelectedAudioDevice);
            if (_main.AudioService.SimpleHaptics != null)
            {
                _main.AudioService.SimpleHaptics.Enabled = true;
                UpdateSimpleHapticsConfig();
            }
        }
        else
        {
            if (_main.AudioService.SimpleHaptics != null)
                _main.AudioService.SimpleHaptics.Enabled = false;
            _main.AudioService.StopCapture();
        }
    }

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
    private VolumeSyncMode _volumeSyncMode = VolumeSyncMode.Stereo;

    [ObservableProperty]
    private string _selectedVolumeSyncText = "Stereo";

    [ObservableProperty]
    private bool _showBTHaptics;

    [ObservableProperty]
    private AudioSourceMode _audioSourceMode = AudioSourceMode.SoundCapture;

    [ObservableProperty]
    private HapticsSourceMode _hapticsSourceMode = HapticsSourceMode.SoundWaves;

    [ObservableProperty]
    private string _selectedAudioSourceText = "Sound Capture";

    [ObservableProperty]
    private string _selectedHapticsSourceText = "Sound Waves";

    partial void OnSelectedAudioSourceTextChanged(string value)
    {
        AudioSourceMode = value switch
        {
            "Sound Capture" => AudioSourceMode.SoundCapture,
            "File Playback" => AudioSourceMode.FilePlayback,
            "vDS Audio" => AudioSourceMode.VDSAudio,
            "Mix and Match" => AudioSourceMode.MixAndMatch,
            _ => AudioSourceMode.SoundCapture
        };
    }

    partial void OnSelectedHapticsSourceTextChanged(string value)
    {
        HapticsSourceMode = value switch
        {
            "Sound Waves" => HapticsSourceMode.SoundWaves,
            "System Capture" => HapticsSourceMode.SystemCapture,
            "File Playback" => HapticsSourceMode.FilePlayback,
            "vDS Audio" => HapticsSourceMode.VDSAudio,
            _ => HapticsSourceMode.SoundWaves
        };
    }

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

    [ObservableProperty]
    private float _lastSubBassEnergy;

    [ObservableProperty]
    private float _lastBassEnergy;

    [ObservableProperty]
    private float _lastMidEnergy;

    [ObservableProperty]
    private float _lastBeatConfidence;

    [ObservableProperty]
    private byte _lastLeftMotor;

    [ObservableProperty]
    private byte _lastRightMotor;

    partial void OnSelectedVolumeSyncTextChanged(string value)
    {
        VolumeSyncMode = value switch
        {
            "Stereo" => VolumeSyncMode.Stereo,
            "Mono" => VolumeSyncMode.Mono,
            "Left Only" => VolumeSyncMode.LeftOnly,
            "Right Only" => VolumeSyncMode.RightOnly,
            "Swap" => VolumeSyncMode.Swap,
            _ => VolumeSyncMode.Stereo
        };
        UpdateSimpleHapticsConfig();
    }

    partial void OnLeftMotorVolumeChanged(double value)
    {
        try { _main.ControllerService.SetSpeakerVolume((byte)(value * 0x64 / 100.0)); }
        catch { }
        UpdateSimpleHapticsConfig();
    }

    partial void OnRightMotorVolumeChanged(double value)
    {
        try { _main.ControllerService.SetHeadphoneVolume((byte)(value * 0x7F / 100.0)); }
        catch { }
        UpdateSimpleHapticsConfig();
    }

    partial void OnHeadsetVolumeChanged(double value)
    {
        try { _main.ControllerService.SetHeadphoneVolume((byte)(value * 0x7F / 100.0)); }
        catch { }
    }

    partial void OnSpeakerVolumeChanged(double value)
    {
        try { _main.ControllerService.SetSpeakerVolume((byte)(value * 0x64 / 100.0)); }
        catch { }
    }

    private void UpdateSimpleHapticsConfig()
    {
        var pipeline = _main.AudioService.SimpleHaptics;
        if (pipeline == null) return;
        pipeline.LeftMotorVolume = LeftMotorVolume;
        pipeline.RightMotorVolume = RightMotorVolume;
        pipeline.SyncMode = VolumeSyncMode;
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

    private int _uiUpdateCounter;

    private void OnBTHapticsData(object? sender, Services.Audio.HapticsDataEventArgs e)
    {
        int count = System.Threading.Interlocked.Increment(ref _uiUpdateCounter);
        if (count % 10 != 0)
            return;

        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            LastLeftMotor = e.LeftMotor;
            LastRightMotor = e.RightMotor;
            LastSubBassEnergy = e.SubBassEnergy;
            LastBassEnergy = e.BassEnergy;
            LastMidEnergy = e.MidFreqEnergy;
            LastBeatConfidence = e.BeatConfidence;
        });
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

        _suppressToggle = true;

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

        VolumeSyncMode = profile.AudioHapticsConfig.VolumeSyncMode;
        SelectedVolumeSyncText = VolumeSyncMode switch
        {
            Enums.VolumeSyncMode.Stereo => "Stereo",
            Enums.VolumeSyncMode.Mono => "Mono",
            Enums.VolumeSyncMode.LeftOnly => "Left Only",
            Enums.VolumeSyncMode.RightOnly => "Right Only",
            Enums.VolumeSyncMode.Swap => "Swap",
            _ => "Stereo"
        };
        UpdateSimpleHapticsConfig();
        _suppressToggle = false;
    }

    private void UpdateControllerInfo()
    {
        var controller = _main.ActiveController;
        if (controller != null)
        {
            BTRSSI = controller.BTRSSI;
            ShowBTHaptics = _main.IsDSXPlusOwner;
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
