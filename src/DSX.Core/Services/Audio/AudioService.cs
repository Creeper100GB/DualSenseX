using System.Collections.Concurrent;
using System.IO;
using DSX.Core.Enums;
using DSX.Core.Interfaces;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DSX.Core.Services.Audio;

public sealed class AudioService : IAudioService, IDisposable
{
    private readonly IControllerService? _controllerService;
    private MMDeviceEnumerator? _deviceEnumerator;
    private WasapiLoopbackCapture? _capture;
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _fileReader;
    private readonly ConcurrentDictionary<string, string> _devices = new();
    private readonly object _captureLock = new();
    private volatile bool _disposed;

    private readonly object _btHapticsLock = new();
    private BTHapticsPipeline? _btHaptics;
    private SimpleHapticsPipeline? _simpleHaptics;

    private int _hapticsLogCounter;

    public event EventHandler<float[]>? AudioDataCaptured;
    public event EventHandler<HapticsDataEventArgs>? BTHapticsDataReady;
    public event EventHandler<string>? BTHapticsError;

    public BTHapticsPipeline? BTHaptics => _btHaptics;
    public SimpleHapticsPipeline? SimpleHaptics => _simpleHaptics;

    public IReadOnlyList<string> AvailableDevices
    {
        get
        {
            RefreshDevices();
            return _devices.Keys.ToList().AsReadOnly();
        }
    }

    public string DefaultDeviceId
    {
        get
        {
            EnsureEnumerator();
            try
            {
                var defaultDevice = _deviceEnumerator!.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return defaultDevice?.ID ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public AudioService(IControllerService? controllerService = null)
    {
        _controllerService = controllerService;
        _deviceEnumerator = new MMDeviceEnumerator();
        if (controllerService != null)
            _simpleHaptics = new SimpleHapticsPipeline(controllerService);
    }

    public void StartBTHaptics(
        AudioSourceMode audioSource = AudioSourceMode.SoundCapture,
        HapticsSourceMode hapticsSource = HapticsSourceMode.SoundWaves,
        double leftIntensity = 50.0,
        double rightIntensity = 50.0,
        int latencyCompensation = 0,
        string? audioFilePath = null)
    {
        lock (_btHapticsLock)
        {
            StopBTHaptics();

            _btHaptics = new BTHapticsPipeline
            {
                AudioSourceMode = audioSource,
                HapticsSourceMode = hapticsSource,
                LeftMotorIntensity = leftIntensity,
                RightMotorIntensity = rightIntensity,
                LatencyCompensationMs = latencyCompensation,
                AudioFilePath = audioFilePath ?? string.Empty
            };

            _btHaptics.HapticsDataReady += OnBTHapticsDataReady;
            _btHaptics.ErrorOccurred += OnBTHapticsError;

            try
            {
                _btHaptics.Start();
            }
            catch (Exception ex)
            {
                BTHapticsError?.Invoke(this, $"Start failed: {ex.Message}");
                _btHaptics.HapticsDataReady -= OnBTHapticsDataReady;
                _btHaptics.ErrorOccurred -= OnBTHapticsError;
                _btHaptics.Dispose();
                _btHaptics = null;
            }
        }
    }

    public void StopBTHaptics()
    {
        lock (_btHapticsLock)
        {
            if (_btHaptics == null) return;

            _btHaptics.HapticsDataReady -= OnBTHapticsDataReady;
            _btHaptics.ErrorOccurred -= OnBTHapticsError;

            try
            {
                _btHaptics.Stop();
            }
            catch { }

            _btHaptics.Dispose();
            _btHaptics = null;
        }

        try
        {
            _controllerService?.SetRumble(0, 0);
        }
        catch { }
    }

    public void ConfigureBTHaptics(
        double? leftIntensity = null,
        double? rightIntensity = null,
        HapticsSourceMode? hapticsSource = null,
        int? latencyCompensation = null)
    {
        lock (_btHapticsLock)
        {
            if (_btHaptics == null) return;

            if (leftIntensity.HasValue)
                _btHaptics.LeftMotorIntensity = leftIntensity.Value;
            if (rightIntensity.HasValue)
                _btHaptics.RightMotorIntensity = rightIntensity.Value;
            if (hapticsSource.HasValue)
                _btHaptics.HapticsSourceMode = hapticsSource.Value;
            if (latencyCompensation.HasValue)
                _btHaptics.LatencyCompensationMs = latencyCompensation.Value;
        }
    }

    public bool IsBTHapticsRunning
    {
        get
        {
            lock (_btHapticsLock)
            {
                return _btHaptics?.IsRunning ?? false;
            }
        }
    }

    public string BTHapticsStatus
    {
        get
        {
            lock (_btHapticsLock)
            {
                return _btHaptics?.StatusText ?? "Stopped";
            }
        }
    }

    public void FeedVdsAudio(float[] samples, int channels, int sampleRate)
    {
        lock (_btHapticsLock)
        {
            _btHaptics?.FeedExternalAudio(samples, channels, sampleRate);
        }
    }

    private void OnBTHapticsDataReady(object? sender, HapticsDataEventArgs e)
    {
        BTHapticsDataReady?.Invoke(this, e);

        int count = System.Threading.Interlocked.Increment(ref _hapticsLogCounter);
        if (count % 200 == 0)
        {
            double leftPct = e.LeftMotor / 255.0 * 100.0;
            double rightPct = e.RightMotor / 255.0 * 100.0;

            DSX.Core.Services.Controller.ControllerService.LogAction?
                .Invoke($"[Haptics] L{e.LeftMotor}/R{e.RightMotor} ({leftPct:F0}/{rightPct:F0}%) " +
                        $"sub={e.SubBassEnergy:F3} bass={e.BassEnergy:F3} beat={e.BeatConfidence:F2}");
        }

        if (e.LeftMotor > 1 || e.RightMotor > 1)
        {
            double leftPct = e.LeftMotor / 255.0 * 100.0;
            double rightPct = e.RightMotor / 255.0 * 100.0;

            try
            {
                _controllerService?.SetRumble(leftPct, rightPct);
            }
            catch (Exception ex)
            {
                if (count % 500 == 0)
                {
                    DSX.Core.Services.Controller.ControllerService.LogAction?
                        .Invoke($"[Haptics] SetRumble error: {ex.Message}");
                }
            }
        }
        else
        {
            try
            {
                _controllerService?.SetRumble(0, 0);
            }
            catch { }
        }
    }

    private void OnBTHapticsError(object? sender, string error)
    {
        var log = DSX.Core.Services.Controller.ControllerService.LogAction;
        if (log != null)
            log($"[Haptics] Error: {error}");
        BTHapticsError?.Invoke(this, error);
    }

    public void StartCapture(string deviceId)
    {
        lock (_captureLock)
        {
            StopCaptureInternal();

            EnsureEnumerator();

            MMDevice? device;
            if (string.IsNullOrEmpty(deviceId))
            {
                device = _deviceEnumerator!.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            else
            {
                try
                {
                    device = _deviceEnumerator!.GetDevice(deviceId);
                }
                catch
                {
                    device = _deviceEnumerator!.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                }
            }

            if (device == null)
                return;

            _capture = new WasapiLoopbackCapture(device);
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
            _capture.StartRecording();
        }
    }

    public void StopCapture()
    {
        lock (_captureLock)
        {
            StopCaptureInternal();
        }
    }

    public void SetVolume(double leftMotor, double rightMotor, double headset, double speaker)
    {
    }

    public void PlayFile(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        StopPlayback();

        try
        {
            _fileReader = new AudioFileReader(filePath);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_fileReader);
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            _waveOut.Play();
        }
        catch
        {
            _fileReader?.Dispose();
            _fileReader = null;
            _waveOut?.Dispose();
            _waveOut = null;
        }
    }

    public void StopPlayback()
    {
        try
        {
            _waveOut?.Stop();
        }
        catch { }

        _waveOut?.Dispose();
        _waveOut = null;
        _fileReader?.Dispose();
        _fileReader = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopCaptureInternal();
        StopPlayback();

        lock (_btHapticsLock)
        {
            if (_btHaptics != null)
            {
                _btHaptics.HapticsDataReady -= OnBTHapticsDataReady;
                _btHaptics.ErrorOccurred -= OnBTHapticsError;
                try { _btHaptics.Stop(); } catch { }
                _btHaptics.Dispose();
                _btHaptics = null;
            }
        }

        _simpleHaptics?.Stop();
        _deviceEnumerator?.Dispose();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_disposed)
            return;

        try
        {
            int bytesPerSample = _capture?.WaveFormat.BitsPerSample / 8 ?? 4;
            int channelCount = _capture?.WaveFormat.Channels ?? 2;
            int sampleCount = e.BytesRecorded / (bytesPerSample * channelCount);

            if (sampleCount == 0)
                return;

            _simpleHaptics?.Process(e.Buffer, e.BytesRecorded, bytesPerSample, channelCount);

            float[] monoSamples = new float[sampleCount];

            if (bytesPerSample == 4 && _capture?.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    float sum = 0;
                    for (int ch = 0; ch < channelCount; ch++)
                    {
                        int offset = (i * channelCount + ch) * 4;
                        if (offset + 4 <= e.Buffer.Length)
                        {
                            float sample = BitConverter.ToSingle(e.Buffer, offset);
                            sum += sample;
                        }
                    }
                    monoSamples[i] = Math.Clamp(sum / channelCount, -1f, 1f);
                }
            }
            else if (bytesPerSample == 2)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    float sum = 0;
                    for (int ch = 0; ch < channelCount; ch++)
                    {
                        int offset = (i * channelCount + ch) * 2;
                        if (offset + 2 <= e.Buffer.Length)
                        {
                            short sample = BitConverter.ToInt16(e.Buffer, offset);
                            sum += sample / 32768f;
                        }
                    }
                    monoSamples[i] = Math.Clamp(sum / channelCount, -1f, 1f);
                }
            }

            AudioDataCaptured?.Invoke(this, monoSamples);
        }
        catch
        {
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (!_disposed)
        {
            try
            {
                _capture?.StartRecording();
            }
            catch { }
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        _fileReader?.Dispose();
        _fileReader = null;
    }

    private void StopCaptureInternal()
    {
        if (_capture != null)
        {
            try
            {
                _capture.DataAvailable -= OnDataAvailable;
                _capture.RecordingStopped -= OnRecordingStopped;
                _capture.StopRecording();
            }
            catch { }

            _capture.Dispose();
            _capture = null;
        }
    }

    private void RefreshDevices()
    {
        EnsureEnumerator();
        _devices.Clear();

        try
        {
            foreach (var device in _deviceEnumerator!.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                _devices[device.ID] = device.FriendlyName;
                device.Dispose();
            }
        }
        catch { }
    }

    private void EnsureEnumerator()
    {
        _deviceEnumerator ??= new MMDeviceEnumerator();
    }
}
