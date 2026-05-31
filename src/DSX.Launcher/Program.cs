using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using DSX.Core.Constants;
using DSX.Core.Models;

namespace DSX.Launcher;

class Program
{
    const int SteamAppId = 1812620;
    const int ShutdownTimeoutSeconds = 60;

    static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppConstants.AppDataFolder);
    static readonly string SettingsPath = Path.Combine(AppDataPath, AppConstants.SettingsFileName);
    static readonly string OfflineCachePath = Path.Combine(AppDataPath, AppConstants.OfflineCacheFileName);
    static readonly string LogDirectory = Path.Combine(AppDataPath, AppConstants.LogsFolderName);
    static readonly string LogPath = Path.Combine(LogDirectory, "DSX_Launcher_Log.txt");

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    static readonly CancellationTokenSource Cts = new();

    static async Task<int> Main(string[] args)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Cts.Cancel();
        };

        try
        {
            Directory.CreateDirectory(AppDataPath);
            Directory.CreateDirectory(LogDirectory);

            Log("=== DSX Launcher Started ===");

            bool verified = VerifySteamOwnership();

            if (!verified)
            {
                OfflineCache? cache = LoadOfflineCache();
                if (cache != null && IsCacheValid(cache))
                {
                    Log($"Offline cache valid. Last verified: {cache.LastVerified:yyyy-MM-dd HH:mm:ss}");
                    verified = true;
                }
                else
                {
                    Log("Steam ownership could not be verified and no valid offline cache exists.");
                    return 1;
                }
            }

            if (verified)
            {
                SaveOfflineCache(new OfflineCache
                {
                    LastVerified = DateTime.UtcNow,
                    IsDSXPlusOwner = true,
                    MachineId = GetMachineId()
                });
            }

            bool vigemInstalled = CheckViGEmBusDriver();
            bool hidhideInstalled = CheckHidHideDriver();

            Log($"ViGEmBus driver installed: {vigemInstalled}");
            Log($"HidHide driver installed: {hidhideInstalled}");

            LaunchMainApp();

            Log("Main app launched. Launcher will stay alive for monitoring.");
            await WaitForShutdown();

            Log("=== DSX Launcher Exiting ===");
            return 0;
        }
        catch (Exception ex)
        {
            Log($"FATAL: {ex.Message}\n{ex.StackTrace}");
            return 2;
        }
    }

    static bool VerifySteamOwnership()
    {
        try
        {
            string? steamPath = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
                "InstallPath",
                null) as string;

            if (string.IsNullOrEmpty(steamPath))
            {
                steamPath = Registry.GetValue(
                    @"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam",
                    "SteamPath",
                    null) as string;
            }

            if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
            {
                Log("Steam installation not found in registry.");
                return false;
            }

            Log($"Steam found at: {steamPath}");

            string steamExe = Path.Combine(steamPath, "steam.exe");
            if (!File.Exists(steamExe))
            {
                Log("steam.exe not found.");
                return false;
            }

            string? runningSteam = Process.GetProcessesByName("steam")
                .FirstOrDefault()?.MainModule?.FileName;

            if (runningSteam == null)
            {
                Log("Steam is not running. Attempting to start Steam...");
                try
                {
                    var steamProc = Process.Start(new ProcessStartInfo
                    {
                        FileName = steamExe,
                        UseShellExecute = false
                    });
                    if (steamProc != null)
                    {
                        Log($"Steam started (PID: {steamProc.Id}). Waiting for initialization...");
                        Thread.Sleep(5000);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Failed to start Steam: {ex.Message}");
                    return false;
                }
            }
            else
            {
                Log("Steam is running.");
            }

            string loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
            if (!File.Exists(loginUsersPath))
            {
                Log("loginusers.vdf not found.");
                return false;
            }

            string libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFoldersPath))
            {
                Log("libraryfolders.vdf not found.");
                return false;
            }

            string appManifest = Path.Combine(steamPath, "steamapps", $"appmanifest_{SteamAppId}.acf");
            if (File.Exists(appManifest))
            {
                Log($"Found app manifest for DSX (App ID {SteamAppId}).");
                return true;
            }

            string libraryContent = File.ReadAllText(libraryFoldersPath);
            string[] lines = libraryContent.Split('\n');
            foreach (string line in lines)
            {
                int pathIdx = line.IndexOf("\"path\"", StringComparison.OrdinalIgnoreCase);
                if (pathIdx < 0) continue;

                string trimmed = line.Substring(pathIdx);
                int firstQuote = trimmed.IndexOf('"', 6);
                if (firstQuote < 0) continue;
                int secondQuote = trimmed.IndexOf('"', firstQuote + 1);
                if (secondQuote < 0) continue;

                string libPath = trimmed.Substring(firstQuote + 1, secondQuote - firstQuote - 1)
                    .Replace(@"\\", @"\")
                    .Trim();

                if (string.IsNullOrEmpty(libPath)) continue;

                string manifestPath = Path.Combine(libPath, "steamapps", $"appmanifest_{SteamAppId}.acf");
                if (File.Exists(manifestPath))
                {
                    Log($"Found app manifest in library: {libPath}");
                    return true;
                }
            }

            Log($"DSX (App ID {SteamAppId}) not found in any Steam library.");
            return false;
        }
        catch (Exception ex)
        {
            Log($"Steam verification error: {ex.Message}");
            return false;
        }
    }

    static OfflineCache? LoadOfflineCache()
    {
        try
        {
            if (!File.Exists(OfflineCachePath))
            {
                Log("No offline cache file found.");
                return null;
            }

            string json = File.ReadAllText(OfflineCachePath);
            var cache = JsonSerializer.Deserialize<OfflineCache>(json);

            if (cache == null)
            {
                Log("Failed to deserialize offline cache.");
                return null;
            }

            string currentMachineId = GetMachineId();
            if (!string.IsNullOrEmpty(cache.MachineId) && cache.MachineId != currentMachineId)
            {
                Log("Offline cache machine ID mismatch. Cache invalidated.");
                return null;
            }

            return cache;
        }
        catch (Exception ex)
        {
            Log($"Error loading offline cache: {ex.Message}");
            return null;
        }
    }

    static void SaveOfflineCache(OfflineCache cache)
    {
        try
        {
            Directory.CreateDirectory(AppDataPath);
            string json = JsonSerializer.Serialize(cache, JsonOptions);
            File.WriteAllText(OfflineCachePath, json);
            Log($"Offline cache saved. Valid until: {cache.LastVerified.AddDays(AppConstants.OfflineCacheDays):yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            Log($"Error saving offline cache: {ex.Message}");
        }
    }

    static bool IsCacheValid(OfflineCache cache)
    {
        if (cache.LastVerified == default)
            return false;

        TimeSpan elapsed = DateTime.UtcNow - cache.LastVerified;
        if (elapsed.TotalDays > AppConstants.OfflineCacheDays)
        {
            Log($"Offline cache expired. {elapsed.TotalDays:F1} days old (max {AppConstants.OfflineCacheDays}).");
            return false;
        }

        return true;
    }

    static bool CheckViGEmBusDriver()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\ViGEmBus");
            if (key != null)
            {
                object? imagePath = key.GetValue("ImagePath");
                if (imagePath != null)
                {
                    Log($"ViGEmBus driver found: {imagePath}");
                    return true;
                }
            }

            using var setupKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpLockdownFiles");
            string? programData = Environment.GetEnvironmentVariable("ProgramData");
            if (programData != null)
            {
                string vigemInfPath = Path.Combine(programData, "ViGEmBus", "ViGEmBus Driver");
                if (Directory.Exists(vigemInfPath))
                {
                    Log("ViGEmBus driver files found in ProgramData.");
                    return true;
                }
            }

            using var uninstallKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstallKey != null)
            {
                foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                {
                    using var subKey = uninstallKey.OpenSubKey(subKeyName);
                    string? displayName = subKey?.GetValue("DisplayName") as string;
                    if (displayName != null && displayName.Contains("ViGEmBus", StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"ViGEmBus found in installed programs: {displayName}");
                        return true;
                    }
                }
            }

            Log("ViGEmBus driver not found.");
            return false;
        }
        catch (Exception ex)
        {
            Log($"Error checking ViGEmBus driver: {ex.Message}");
            return false;
        }
    }

    static bool CheckHidHideDriver()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(AppConstants.HidHideRegistryPath);
            if (key != null)
            {
                Log("HidHide registry key found.");
                return true;
            }

            using var configKey = Registry.LocalMachine.OpenSubKey(AppConstants.HidHideConfigPath);
            if (configKey != null)
            {
                object? whitelist = configKey.GetValue("Whitelist");
                if (whitelist != null)
                {
                    Log("HidHide configuration found.");
                    return true;
                }
            }

            using var uninstallKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstallKey != null)
            {
                foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                {
                    using var subKey = uninstallKey.OpenSubKey(subKeyName);
                    string? displayName = subKey?.GetValue("DisplayName") as string;
                    if (displayName != null && displayName.Contains("HidHide", StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"HidHide found in installed programs: {displayName}");
                        return true;
                    }
                }
            }

            using var serviceKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\HidHide");
            if (serviceKey != null)
            {
                Log("HidHide service found.");
                return true;
            }

            Log("HidHide driver not found.");
            return false;
        }
        catch (Exception ex)
        {
            Log($"Error checking HidHide driver: {ex.Message}");
            return false;
        }
    }

    static void LaunchMainApp()
    {
        string currentDir = AppDomain.CurrentDomain.BaseDirectory;
        string mainAppPath = Path.Combine(currentDir, "DSX.exe");

        if (!File.Exists(mainAppPath))
        {
            string? parentDir = Directory.GetParent(currentDir)?.FullName;
            if (parentDir != null)
            {
                mainAppPath = Path.Combine(parentDir, "DSX.exe");
            }
        }

        if (!File.Exists(mainAppPath))
        {
            Log($"DSX.exe not found. Searched: {AppDomain.CurrentDomain.BaseDirectory}");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = mainAppPath,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(mainAppPath)!
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                Log($"DSX.exe launched successfully (PID: {process.Id}).");
            }
            else
            {
                Log("Failed to start DSX.exe - Process.Start returned null.");
            }
        }
        catch (Exception ex)
        {
            Log($"Error launching DSX.exe: {ex.Message}");
        }
    }

    static async Task WaitForShutdown()
    {
        Log($"Launcher will auto-close in {ShutdownTimeoutSeconds} seconds.");

        for (int i = ShutdownTimeoutSeconds; i > 0; i--)
        {
            if (Cts.Token.IsCancellationRequested)
                break;

            if (i % 15 == 0 || i <= 5)
                Log($"Auto-close in {i} seconds...");

            await Task.Delay(1000, Cts.Token).ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnCanceled);
        }
    }

    static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            File.AppendAllText(LogPath, logEntry);
        }
        catch
        {
        }
    }

    static string GetMachineId()
    {
        try
        {
            string? machineGuid = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography",
                "MachineGuid",
                null) as string;

            if (!string.IsNullOrEmpty(machineGuid))
                return machineGuid;

            return Environment.MachineName;
        }
        catch
        {
            return Environment.MachineName;
        }
    }
}
