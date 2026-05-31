using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using DSX.Core.Interfaces;
using DSX.Core.Models;
using Microsoft.Win32;

namespace DSX.Core.Services;

public sealed class GameService : IGameService
{
    private readonly ConcurrentDictionary<string, GameInfo> _games = new();
    private readonly object _lock = new();

    public event EventHandler? GamesUpdated;

    public IReadOnlyList<GameInfo> InstalledGames => _games.Values.ToList().AsReadOnly();

    public void ScanGames()
    {
        lock (_lock)
        {
            _games.Clear();

            var steamPath = GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamPath))
                return;

            var libraryPaths = GetSteamLibraryPaths(steamPath);

            foreach (var libraryPath in libraryPaths)
            {
                var steamappsPath = Path.Combine(libraryPath, "steamapps");
                if (!Directory.Exists(steamappsPath))
                    continue;

                foreach (var acfFile in Directory.GetFiles(steamappsPath, "appmanifest_*.acf"))
                {
                    var game = ParseAppManifest(acfFile);
                    if (game != null)
                        _games[game.Id] = game;
                }
            }

            GamesUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public void AssignProfile(string gameId, string profileId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return;

        game.AssignedProfileId = profileId;
        GamesUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveProfile(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return;

        game.AssignedProfileId = string.Empty;
        game.AssignedProfileName = string.Empty;
        GamesUpdated?.Invoke(this, EventArgs.Empty);
    }

    private static string? GetSteamInstallPath()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            var path = key?.GetValue("InstallPath") as string;
            if (path != null && Directory.Exists(path))
                return path;
        }
        catch { }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
            var path = key?.GetValue("InstallPath") as string;
            if (path != null && Directory.Exists(path))
                return path;
        }
        catch { }

        return null;
    }

    private static List<string> GetSteamLibraryPaths(string steamPath)
    {
        var paths = new List<string> { steamPath };

        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath))
            return paths;

        try
        {
            var content = File.ReadAllText(vdfPath);
            var regex = new Regex(@"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase);

            foreach (Match match in regex.Matches(content))
            {
                var libPath = match.Groups[1].Value.Replace(@"\\", @"\");
                if (Directory.Exists(libPath) && !paths.Contains(libPath))
                    paths.Add(libPath);
            }
        }
        catch { }

        return paths;
    }

    private static GameInfo? ParseAppManifest(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);

            var appIdMatch = Regex.Match(Path.GetFileName(filePath), @"appmanifest_(\d+)\.acf");
            if (!appIdMatch.Success)
                return null;

            var appId = appIdMatch.Groups[1].Value;

            var nameMatch = Regex.Match(content, @"""name""\s+""([^""]+)""");
            if (!nameMatch.Success)
                return null;

            var name = nameMatch.Groups[1].Value;

            var installDirMatch = Regex.Match(content, @"""installdir""\s+""([^""]+)""");
            var installDir = installDirMatch.Success ? installDirMatch.Groups[1].Value : string.Empty;

            var manifestDir = Path.GetDirectoryName(filePath);
            var installPath = !string.IsNullOrEmpty(manifestDir) && !string.IsNullOrEmpty(installDir)
                ? Path.Combine(manifestDir, "common", installDir)
                : string.Empty;

            return new GameInfo
            {
                Id = appId,
                Name = name,
                InstallPath = installPath,
                Platform = "Steam"
            };
        }
        catch
        {
            return null;
        }
    }
}
