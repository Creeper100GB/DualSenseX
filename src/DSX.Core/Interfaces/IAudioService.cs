using System.Collections.Generic;
using DSX.Core.Enums;
using DSX.Core.Services.Audio;

namespace DSX.Core.Interfaces;

public interface IAudioService
{
    event EventHandler<float[]>? AudioDataCaptured;
    event EventHandler<HapticsDataEventArgs>? BTHapticsDataReady;
    event EventHandler<string>? BTHapticsError;

    IReadOnlyList<string> AvailableDevices { get; }
    string DefaultDeviceId { get; }
    bool IsBTHapticsRunning { get; }
    string BTHapticsStatus { get; }
    Services.Audio.SimpleHapticsPipeline? SimpleHaptics { get; }

    void StartCapture(string deviceId);
    void StopCapture();
    void SetVolume(double leftMotor, double rightMotor, double headset, double speaker);
    void PlayFile(string filePath);
    void StopPlayback();

    void StartBTHaptics(
        AudioSourceMode audioSource = AudioSourceMode.SoundCapture,
        HapticsSourceMode hapticsSource = HapticsSourceMode.SoundWaves,
        double leftIntensity = 50.0,
        double rightIntensity = 50.0,
        int latencyCompensation = 0,
        string? audioFilePath = null);
    void StopBTHaptics();
    void ConfigureBTHaptics(
        double? leftIntensity = null,
        double? rightIntensity = null,
        HapticsSourceMode? hapticsSource = null,
        int? latencyCompensation = null);
    void FeedVdsAudio(float[] samples, int channels, int sampleRate);

    void Dispose();
}
