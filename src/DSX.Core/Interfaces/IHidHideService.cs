using System.Collections.Generic;

namespace DSX.Core.Interfaces;

public interface IHidHideService
{
    bool IsDriverInstalled { get; }
    bool LetDSXControl { get; set; }
    bool PersistentHiding { get; set; }
    IReadOnlyList<string> WhitelistedApplications { get; }
    void AddToWhitelist(string applicationPath);
    void RemoveFromWhitelist(string applicationPath);
    void HideController(string deviceId);
    void UnhideController(string deviceId);
    void ApplyPersistentHiding();
    void RefreshDriverStatus();
}
