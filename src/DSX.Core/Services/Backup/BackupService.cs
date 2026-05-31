using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using DSX.Core.Constants;
using DSX.Core.Interfaces;
using DSX.Core.Models;

namespace DSX.Core.Services.Backup;

public sealed class BackupService : IBackupService
{
    private readonly ConcurrentBag<BackupInfo> _backups = [];
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public IReadOnlyList<BackupInfo> Backups
    {
        get
        {
            lock (_lock)
            {
                return _backups.ToList().AsReadOnly();
            }
        }
    }

    public BackupService()
    {
        Directory.CreateDirectory(AppConstants.BackupsPath);
        Directory.CreateDirectory(AppConstants.ProfilesPath);
        RefreshBackups();
    }

    public void CreateBackup()
    {
        lock (_lock)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"DSX_Backup_{timestamp}.zip";
            var filePath = Path.Combine(AppConstants.BackupsPath, fileName);

            using var zipStream = new FileStream(filePath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            if (Directory.Exists(AppConstants.ProfilesPath))
            {
                foreach (var file in Directory.GetFiles(AppConstants.ProfilesPath, "*.json", SearchOption.AllDirectories))
                {
                    var entryName = Path.Combine("Profiles", Path.GetRelativePath(AppConstants.ProfilesPath, file));
                    archive.CreateEntryFromFile(file, entryName);
                }
            }

            if (File.Exists(AppConstants.SettingsFilePath))
            {
                archive.CreateEntryFromFile(AppConstants.SettingsFilePath, "DSX_Settings.json");
            }

            if (File.Exists(AppConstants.UdpPortFilePath))
            {
                archive.CreateEntryFromFile(AppConstants.UdpPortFilePath, "DSX_UDP_PortNumber.txt");
            }

            var info = new BackupInfo
            {
                FileName = fileName,
                CreatedDate = DateTime.Now,
                SizeBytes = new FileInfo(filePath).Length,
                FilePath = filePath
            };

            _backups.Add(info);
        }
    }

    public void RestoreBackup(string fileName)
    {
        lock (_lock)
        {
            var filePath = Path.Combine(AppConstants.BackupsPath, fileName);
            if (!File.Exists(filePath))
                return;

            using var zipStream = new FileStream(filePath, FileMode.Open);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                string destPath;

                if (entry.FullName.StartsWith("Profiles/"))
                {
                    var relativePath = entry.FullName["Profiles/".Length..];
                    destPath = Path.Combine(AppConstants.ProfilesPath, relativePath);
                }
                else if (entry.FullName == "DSX_Settings.json")
                {
                    destPath = AppConstants.SettingsFilePath;
                }
                else if (entry.FullName == "DSX_UDP_PortNumber.txt")
                {
                    destPath = AppConstants.UdpPortFilePath;
                }
                else
                {
                    continue;
                }

                var dir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                entry.ExtractToFile(destPath, true);
            }
        }
    }

    public void DeleteBackup(string fileName)
    {
        lock (_lock)
        {
            var filePath = Path.Combine(AppConstants.BackupsPath, fileName);
            if (!File.Exists(filePath))
                return;

            File.Delete(filePath);

            var updated = new ConcurrentBag<BackupInfo>(
                _backups.Where(b => b.FileName != fileName));
            
            _backups.Clear();
            foreach (var b in updated)
                _backups.Add(b);
        }
    }

    public void RefreshBackups()
    {
        lock (_lock)
        {
            _backups.Clear();

            if (!Directory.Exists(AppConstants.BackupsPath))
                return;

            foreach (var file in Directory.GetFiles(AppConstants.BackupsPath, "*.zip"))
            {
                var info = new FileInfo(file);
                _backups.Add(new BackupInfo
                {
                    FileName = info.Name,
                    CreatedDate = info.CreationTime,
                    SizeBytes = info.Length,
                    FilePath = info.FullName
                });
            }
        }
    }
}
