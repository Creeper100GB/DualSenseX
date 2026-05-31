using DSX.Core.Interfaces;
using NAudio.CoreAudioApi;

namespace DSX.Core.Services;

public sealed class MicrophoneService : IMicrophoneService, IDisposable
{
    private MMDeviceEnumerator? _deviceEnumerator;
    private MMDevice? _microphoneDevice;
    private readonly object _lock = new();
    private volatile bool _disposed;

    public event EventHandler<bool>? MuteStateChanged;

    public bool IsMuted
    {
        get
        {
            EnsureDevice();
            try
            {
                if (_microphoneDevice == null)
                    return false;

                using var endpoint = _microphoneDevice.AudioEndpointVolume;
                return endpoint?.Mute ?? false;
            }
            catch
            {
                return false;
            }
        }
    }

    public MicrophoneService()
    {
        _deviceEnumerator = new MMDeviceEnumerator();
    }

    public void ToggleMute()
    {
        lock (_lock)
        {
            EnsureDevice();
            if (_microphoneDevice == null)
                return;

            try
            {
                using var endpoint = _microphoneDevice.AudioEndpointVolume;
                if (endpoint == null)
                    return;

                var newState = !endpoint.Mute;
                endpoint.Mute = newState;
                MuteStateChanged?.Invoke(this, newState);
            }
            catch { }
        }
    }

    public void SetMute(bool muted)
    {
        lock (_lock)
        {
            EnsureDevice();
            if (_microphoneDevice == null)
                return;

            try
            {
                using var endpoint = _microphoneDevice.AudioEndpointVolume;
                if (endpoint == null)
                    return;

                endpoint.Mute = muted;
                MuteStateChanged?.Invoke(this, muted);
            }
            catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _microphoneDevice?.Dispose();
        _deviceEnumerator?.Dispose();
    }

    private void EnsureDevice()
    {
        if (_microphoneDevice != null)
            return;

        try
        {
            _deviceEnumerator ??= new MMDeviceEnumerator();
            _microphoneDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        }
        catch
        {
            // No microphone device available
        }
    }
}
