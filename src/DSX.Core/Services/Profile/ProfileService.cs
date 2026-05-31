using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DSX.Core.Constants;
using DSX.Core.Interfaces;
using DSX.Core.Models;

namespace DSX.Core.Services.Profile;

public sealed class ProfileService : IProfileService, IDisposable
{
    private readonly ConcurrentDictionary<string, UserProfile> _profiles = new();
    private readonly ConcurrentDictionary<string, ProfileSlot[]> _deviceSlots = new();
    private readonly object _fileLock = new();
    private readonly JsonSerializerOptions _jsonOptions;

    private string? _activeProfileId;
    private FileSystemWatcher? _watcher;
    private volatile bool _disposed;

    public event EventHandler? ProfileChanged;

    public UserProfile? ActiveProfile =>
        _activeProfileId != null && _profiles.TryGetValue(_activeProfileId, out var p) ? p : null;

    public IReadOnlyList<UserProfile> Profiles => _profiles.Values.ToList().AsReadOnly();

    public ProfileService()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        EnsureDirectoryExists();
        LoadAllProfiles();
        SetupWatcher();
    }

    public UserProfile CreateProfile(string name)
    {
        var profile = new UserProfile
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now
        };

        _profiles[profile.Id] = profile;
        SaveProfile(profile);
        ProfileChanged?.Invoke(this, EventArgs.Empty);

        return profile;
    }

    public void DeleteProfile(string profileId)
    {
        if (!_profiles.TryRemove(profileId, out _))
            return;

        var filePath = GetProfileFilePath(profileId);
        lock (_fileLock)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        if (_activeProfileId == profileId)
            _activeProfileId = null;

        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateProfile(UserProfile profile)
    {
        profile.ModifiedDate = DateTime.Now;
        _profiles[profile.Id] = profile;
        SaveProfile(profile);
        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public UserProfile? GetProfile(string profileId)
    {
        return _profiles.TryGetValue(profileId, out var p) ? p : null;
    }

    public void LoadProfile(string profileId)
    {
        if (!_profiles.ContainsKey(profileId))
            return;

        _activeProfileId = profileId;
        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SaveAllProfiles()
    {
        foreach (var profile in _profiles.Values)
            SaveProfile(profile);
    }

    public void ImportProfile(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        lock (_fileLock)
        {
            var json = File.ReadAllText(filePath);
            var profile = JsonSerializer.Deserialize<UserProfile>(json, _jsonOptions);
            if (profile == null)
                return;

            profile.Id = Guid.NewGuid().ToString();
            profile.CreatedDate = DateTime.Now;
            profile.ModifiedDate = DateTime.Now;

            _profiles[profile.Id] = profile;
            SaveProfile(profile);
        }

        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ExportProfile(string profileId, string filePath)
    {
        if (!_profiles.TryGetValue(profileId, out var profile))
            return;

        lock (_fileLock)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(profile, _jsonOptions);
            File.WriteAllText(filePath, json);
        }
    }

    public void AssignProfileToSlot(string deviceId, int slotIndex, string profileId)
    {
        if (slotIndex < 0 || slotIndex >= AppConstants.ProfileSlotCount)
            return;

        if (!_profiles.ContainsKey(profileId))
            return;

        var slots = _deviceSlots.GetOrAdd(deviceId, _ =>
        {
            var s = new ProfileSlot[AppConstants.ProfileSlotCount];
            for (int i = 0; i < s.Length; i++)
                s[i] = new ProfileSlot { SlotIndex = i };
            return s;
        });

        var profile = _profiles[profileId];
        slots[slotIndex].ProfileId = profileId;
        slots[slotIndex].ProfileName = profile.Name;

        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ActivateSlot(string deviceId, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= AppConstants.ProfileSlotCount)
            return;

        if (!_deviceSlots.TryGetValue(deviceId, out var slots))
            return;

        if (string.IsNullOrEmpty(slots[slotIndex].ProfileId))
            return;

        foreach (var slot in slots)
            slot.IsActive = false;

        slots[slotIndex].IsActive = true;
        _activeProfileId = slots[slotIndex].ProfileId;
        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CycleSlot(string deviceId)
    {
        if (!_deviceSlots.TryGetValue(deviceId, out var slots))
            return;

        int currentActive = Array.FindIndex(slots, s => s.IsActive);
        int next = -1;

        for (int i = 1; i <= AppConstants.ProfileSlotCount; i++)
        {
            int idx = (currentActive + i) % AppConstants.ProfileSlotCount;
            if (!string.IsNullOrEmpty(slots[idx].ProfileId))
            {
                next = idx;
                break;
            }
        }

        if (next >= 0)
            ActivateSlot(deviceId, next);
    }

    public ProfileSlot[] GetSlots(string deviceId)
    {
        return _deviceSlots.GetOrAdd(deviceId, _ =>
        {
            var s = new ProfileSlot[AppConstants.ProfileSlotCount];
            for (int i = 0; i < s.Length; i++)
                s[i] = new ProfileSlot { SlotIndex = i };
            return s;
        });
    }

    public void MigrateFromXml(string xmlPath)
    {
        if (!File.Exists(xmlPath))
            return;

        try
        {
            var xmlContent = File.ReadAllText(xmlPath);
            var profile = new UserProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = Path.GetFileNameWithoutExtension(xmlPath) + " (Migrated)",
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now
            };

            _profiles[profile.Id] = profile;
            SaveProfile(profile);
            ProfileChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Migration best-effort
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _watcher?.Dispose();
        SaveAllProfiles();
    }

    private void SaveProfile(UserProfile profile)
    {
        lock (_fileLock)
        {
            var filePath = GetProfileFilePath(profile.Id);
            var json = JsonSerializer.Serialize(profile, _jsonOptions);
            File.WriteAllText(filePath, json);
        }
    }

    private void LoadAllProfiles()
    {
        lock (_fileLock)
        {
            if (!Directory.Exists(AppConstants.ProfilesPath))
                return;

            foreach (var file in Directory.GetFiles(AppConstants.ProfilesPath, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var profile = JsonSerializer.Deserialize<UserProfile>(json, _jsonOptions);
                    if (profile != null && !string.IsNullOrEmpty(profile.Id))
                        _profiles[profile.Id] = profile;
                }
                catch
                {
                    // Skip invalid profiles
                }
            }
        }

        if (_profiles.IsEmpty)
        {
            var defaultProfile = CreateProfile("Default Profile");
            _activeProfileId = defaultProfile.Id;
        }
        else
        {
            _activeProfileId = _profiles.Keys.First();
        }
    }

    private void SetupWatcher()
    {
        try
        {
            if (!Directory.Exists(AppConstants.ProfilesPath))
                return;

            _watcher = new FileSystemWatcher(AppConstants.ProfilesPath, "*.json")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;
        }
        catch
        {
            // Watcher not critical
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_disposed)
            return;

        try
        {
            lock (_fileLock)
            {
                var json = File.ReadAllText(e.FullPath);
                var profile = JsonSerializer.Deserialize<UserProfile>(json, _jsonOptions);
                if (profile != null && !string.IsNullOrEmpty(profile.Id))
                {
                    _profiles[profile.Id] = profile;
                    ProfileChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch
        {
            // File might be in use
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (_disposed)
            return;

        var id = Path.GetFileNameWithoutExtension(e.FullPath);
        if (_profiles.TryRemove(id, out _))
            ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        OnFileDeleted(sender, new FileSystemEventArgs(WatcherChangeTypes.Deleted,
            Path.GetDirectoryName(e.OldFullPath)!, Path.GetFileName(e.OldFullPath)));
        OnFileChanged(sender, new FileSystemEventArgs(WatcherChangeTypes.Created,
            Path.GetDirectoryName(e.FullPath)!, Path.GetFileName(e.FullPath)));
    }

    private static string GetProfileFilePath(string profileId) =>
        Path.Combine(AppConstants.ProfilesPath, $"{profileId}.json");

    private static void EnsureDirectoryExists()
    {
        Directory.CreateDirectory(AppConstants.ProfilesPath);
    }
}
