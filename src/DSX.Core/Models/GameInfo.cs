using DSX.Core.Enums;

namespace DSX.Core.Models;

public class GameInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string Platform { get; set; } = "Steam";
    public string AssignedProfileId { get; set; } = string.Empty;
    public string AssignedProfileName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

public class ModInfo
{
    public string Id { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ModConnectionMethod ConnectionMethod { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int UdpPort { get; set; } = 6969;
    public bool IsInstalled { get; set; }
}
