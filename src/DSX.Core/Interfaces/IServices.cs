using System.Collections.Generic;
using DSX.Core.Models;

namespace DSX.Core.Interfaces;

public interface IBackupService
{
    IReadOnlyList<BackupInfo> Backups { get; }
    void CreateBackup();
    void RestoreBackup(string fileName);
    void DeleteBackup(string fileName);
    void RefreshBackups();
}

public interface IGameService
{
    event EventHandler? GamesUpdated;
    IReadOnlyList<GameInfo> InstalledGames { get; }
    void ScanGames();
    void AssignProfile(string gameId, string profileId);
    void RemoveProfile(string gameId);
}

public interface IVirtualDeviceService
{
    bool IsDSXPlusOwner { get; }
    void CreateVirtualDualSense();
    void CreateVirtualXbox360();
    void CreateVirtualDualShock4();
    void RemoveVirtualDevice(string deviceId);
    void AddSoundDevice(string virtualDeviceId);
    void SyncDSXPlus();
}

public interface IMicrophoneService
{
    bool IsMuted { get; }
    event EventHandler<bool>? MuteStateChanged;
    void ToggleMute();
    void SetMute(bool muted);
}
