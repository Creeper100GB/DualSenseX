using System.Collections.Generic;
using System.Text.Json;
using DSX.Core.Constants;
using DSX.Core.Interfaces;
using Microsoft.Win32;

namespace DSX.Core.Services.HidHide;

public sealed class HidHideService : IHidHideService
{
    private readonly HashSet<string> _hiddenDevices = new();
    private readonly object _registryLock = new();

    public bool IsDriverInstalled
    {
        get
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(AppConstants.HidHideRegistryPath);
                return key != null;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool LetDSXControl { get; set; } = true;

    public bool PersistentHiding { get; set; }

    public IReadOnlyList<string> WhitelistedApplications
    {
        get
        {
            var list = new List<string>();
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(AppConstants.HidHideWhitelistPath);
                if (key != null)
                {
                    foreach (var name in key.GetValueNames())
                    {
                        var value = key.GetValue(name);
                        if (value is string s && !string.IsNullOrEmpty(s))
                            list.Add(s);
                    }
                }
            }
            catch { }
            return list.AsReadOnly();
        }
    }

    public void AddToWhitelist(string applicationPath)
    {
        if (!IsDriverInstalled)
            return;

        lock (_registryLock)
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(AppConstants.HidHideWhitelistPath);
                key?.SetValue(applicationPath, applicationPath);
            }
            catch
            {
                // Requires admin privileges
            }
        }
    }

    public void RemoveFromWhitelist(string applicationPath)
    {
        if (!IsDriverInstalled)
            return;

        lock (_registryLock)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(AppConstants.HidHideWhitelistPath, true);
                key?.DeleteValue(applicationPath, false);
            }
            catch { }
        }
    }

    public void HideController(string deviceId)
    {
        if (!IsDriverInstalled)
            return;

        lock (_registryLock)
        {
            _hiddenDevices.Add(deviceId);

            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(AppConstants.HidHideConfigPath);
                var existing = GetHiddenDevicesFromRegistry();
                var updated = existing.Append(deviceId).Distinct().ToList();
                key?.SetValue("HiddenInstances", JsonSerializer.Serialize(updated));
            }
            catch { }
        }
    }

    public void UnhideController(string deviceId)
    {
        lock (_registryLock)
        {
            _hiddenDevices.Remove(deviceId);

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(AppConstants.HidHideConfigPath, true);
                if (key == null) return;

                var existing = GetHiddenDevicesFromRegistry();
                var updated = existing.Where(d => d != deviceId).ToList();

                if (updated.Count > 0)
                    key.SetValue("HiddenInstances", JsonSerializer.Serialize(updated));
                else
                    key.DeleteValue("HiddenInstances", false);
            }
            catch { }
        }
    }

    public void ApplyPersistentHiding()
    {
        if (!PersistentHiding || !IsDriverInstalled)
            return;

        foreach (var deviceId in _hiddenDevices)
        {
            try
            {
                lock (_registryLock)
                {
                    using var key = Registry.LocalMachine.CreateSubKey(AppConstants.HidHideConfigPath);
                    var existing = GetHiddenDevicesFromRegistry();
                    if (!existing.Contains(deviceId))
                    {
                        var updated = existing.Append(deviceId).ToList();
                        key?.SetValue("HiddenInstances", JsonSerializer.Serialize(updated));
                    }
                }
            }
            catch { }
        }
    }

    public void RefreshDriverStatus()
    {
        lock (_registryLock)
        {
            var hidden = GetHiddenDevicesFromRegistry();
            _hiddenDevices.Clear();
            foreach (var d in hidden)
                _hiddenDevices.Add(d);
        }
    }

    private static List<string> GetHiddenDevicesFromRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(AppConstants.HidHideConfigPath);
            if (key?.GetValue("HiddenInstances") is string json)
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch { }
        return [];
    }
}
