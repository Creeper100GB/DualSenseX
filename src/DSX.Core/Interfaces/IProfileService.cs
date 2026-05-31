using System.Collections.Generic;
using DSX.Core.Models;

namespace DSX.Core.Interfaces;

public interface IProfileService
{
    event EventHandler? ProfileChanged;
    UserProfile? ActiveProfile { get; }
    IReadOnlyList<UserProfile> Profiles { get; }
    UserProfile CreateProfile(string name);
    void DeleteProfile(string profileId);
    void UpdateProfile(UserProfile profile);
    UserProfile? GetProfile(string profileId);
    void LoadProfile(string profileId);
    void SaveAllProfiles();
    void ImportProfile(string filePath);
    void ExportProfile(string profileId, string filePath);
    void AssignProfileToSlot(string deviceId, int slotIndex, string profileId);
    void ActivateSlot(string deviceId, int slotIndex);
    void CycleSlot(string deviceId);
    ProfileSlot[] GetSlots(string deviceId);
    void MigrateFromXml(string xmlPath);
}
