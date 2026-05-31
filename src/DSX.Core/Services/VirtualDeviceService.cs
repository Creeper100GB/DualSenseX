using System.Collections.Concurrent;
using System.IO;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using DSX.Core.Constants;
using DSX.Core.Interfaces;

namespace DSX.Core.Services;

public sealed class VirtualDeviceService : IVirtualDeviceService, IDisposable
{
    private readonly ConcurrentDictionary<string, IVirtualGamepad> _devices = new();
    private readonly ConcurrentDictionary<string, Xbox360FeedbackReceivedEventArgs> _deviceFeedback = new();
    private ViGEmClient? _client;
    private readonly object _lock = new();
    private volatile bool _disposed;
    private int _deviceCounter;

    public bool IsDSXPlusOwner
    {
        get
        {
            try
            {
                if (!File.Exists(AppConstants.OfflineCacheFilePath))
                    return false;

                var json = File.ReadAllText(AppConstants.OfflineCacheFilePath);
                var cache = System.Text.Json.JsonSerializer.Deserialize<Models.OfflineCache>(json);
                if (cache == null)
                    return false;

                if ((DateTime.Now - cache.LastVerified).TotalDays > AppConstants.OfflineCacheDays)
                    return false;

                return cache.IsDSXPlusOwner;
            }
            catch
            {
                return false;
            }
        }
    }

    public void CreateVirtualDualSense()
    {
        EnsureClient();
        if (_client == null)
            return;

        lock (_lock)
        {
            var device = _client.CreateDualShock4Controller(
                AppConstants.SonyVID, AppConstants.DualSensePID);

            var deviceId = $"DualSense_{Interlocked.Increment(ref _deviceCounter)}";
            device.FeedbackReceived += (sender, args) => { };
            device.AutoSubmitReport = true;
            device.Connect();

            _devices[deviceId] = device;
        }
    }

    public void CreateVirtualXbox360()
    {
        EnsureClient();
        if (_client == null)
            return;

        lock (_lock)
        {
            var device = _client.CreateXbox360Controller();
            var deviceId = $"Xbox360_{Interlocked.Increment(ref _deviceCounter)}";

            device.FeedbackReceived += (sender, args) => { };
            device.AutoSubmitReport = true;
            device.Connect();

            _devices[deviceId] = device;
        }
    }

    public void CreateVirtualDualShock4()
    {
        EnsureClient();
        if (_client == null)
            return;

        lock (_lock)
        {
            var device = _client.CreateDualShock4Controller();
            var deviceId = $"DS4_{Interlocked.Increment(ref _deviceCounter)}";

            device.FeedbackReceived += (sender, args) => { };
            device.AutoSubmitReport = true;
            device.Connect();

            _devices[deviceId] = device;
        }
    }

    public void RemoveVirtualDevice(string deviceId)
    {
        lock (_lock)
        {
            if (_devices.TryRemove(deviceId, out var device))
            {
                try { device.Disconnect(); } catch { }
            }
        }
    }

    public void AddSoundDevice(string virtualDeviceId)
    {
    }

    public void SyncDSXPlus()
    {
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var device in _devices.Values)
        {
            try { device.Disconnect(); } catch { }
        }

        _devices.Clear();
        _client?.Dispose();
    }

    private void EnsureClient()
    {
        if (_client != null)
            return;

        try
        {
            _client = new ViGEmClient();
        }
        catch
        {
        }
    }
}
