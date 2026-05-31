using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text.Json;
using DSX.Core.Constants;
using DSX.Core.Interfaces;

namespace DSX.Core.Services.Localization;

public sealed class LocalizationService : ILocalizationService, IDisposable
{
    private readonly ConcurrentDictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
    private readonly FileSystemWatcher? _watcher;
    private readonly object _lock = new();
    private volatile bool _disposed;
    private string _currentLanguage = "en-US";

    public event EventHandler? LanguageChanged;

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage == value)
                return;

            LoadLanguage(value);
        }
    }

    public IReadOnlyList<string> AvailableLanguages
    {
        get
        {
            var list = new List<string>();

            try
            {
                if (Directory.Exists(AppConstants.LanguagesPath))
                {
                    foreach (var file in Directory.GetFiles(AppConstants.LanguagesPath, "*.json"))
                    {
                        var code = Path.GetFileNameWithoutExtension(file);
                        if (AppConstants.SupportedLanguages.Contains(code))
                            list.Add(code);
                    }
                }
            }
            catch { }

            if (!list.Contains("en-US"))
                list.Insert(0, "en-US");

            foreach (var lang in AppConstants.SupportedLanguages)
            {
                if (!list.Contains(lang))
                    list.Add(lang);
            }

            return list.AsReadOnly();
        }
    }

    public LocalizationService()
    {
        Directory.CreateDirectory(AppConstants.LanguagesPath);
        EnsureDefaultLanguageFiles();
        LoadLanguage("en-US");

        try
        {
            _watcher = new FileSystemWatcher(AppConstants.LanguagesPath, "*.json")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnLanguageFileChanged;
        }
        catch { }
    }

    public string GetString(string key)
    {
        if (_strings.TryGetValue(key, out var value))
            return value;

        if (_currentLanguage != "en-US")
        {
            // Try fallback to en-US key
            return key;
        }

        return key;
    }

    public void LoadLanguage(string languageCode)
    {
        if (!AppConstants.SupportedLanguages.Contains(languageCode))
            languageCode = "en-US";

        lock (_lock)
        {
            _strings.Clear();
            var data = LoadLanguageFile(languageCode);
            foreach (var kvp in data)
                _strings[kvp.Key] = kvp.Value;

            if (languageCode != "en-US")
            {
                var fallback = LoadLanguageFile("en-US");
                foreach (var kvp in fallback)
                {
                    if (!_strings.ContainsKey(kvp.Key))
                        _strings[kvp.Key] = kvp.Value;
                }
            }

            _currentLanguage = languageCode;
        }

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _watcher?.Dispose();
    }

    private Dictionary<string, string> LoadLanguageFile(string languageCode)
    {
        var filePath = Path.Combine(AppConstants.LanguagesPath, $"{languageCode}.json");

        try
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (dict != null)
                    return FlattenDictionary(dict);
            }
        }
        catch { }

        var embedded = LoadEmbeddedResource($"DSX.Core.Resources.Languages.{languageCode}.json");
        if (embedded != null)
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(embedded);
                if (dict != null)
                    return FlattenDictionary(dict);
            }
            catch { }
        }

        return [];
    }

    private static Dictionary<string, string> FlattenDictionary(Dictionary<string, object> dict, string prefix = "")
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in dict)
        {
            var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

            if (kvp.Value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                    result[key] = element.GetString() ?? kvp.Key;
                else if (element.ValueKind == JsonValueKind.Object)
                {
                    var nested = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText());
                    if (nested != null)
                    {
                        foreach (var nestedKvp in FlattenDictionary(nested, key))
                            result[nestedKvp.Key] = nestedKvp.Value;
                    }
                }
            }
            else if (kvp.Value is string s)
            {
                result[key] = s;
            }
        }

        return result;
    }

    private static string? LoadEmbeddedResource(string resourceName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }

    private void EnsureDefaultLanguageFiles()
    {
        try
        {
            var enUsPath = Path.Combine(AppConstants.LanguagesPath, "en-US.json");
            if (!File.Exists(enUsPath))
            {
                var embedded = LoadEmbeddedResource("DSX.Core.Resources.Languages.en-US.json");
                if (embedded != null)
                    File.WriteAllText(enUsPath, embedded);
            }
        }
        catch { }
    }

    private void OnLanguageFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_disposed)
            return;

        var langCode = Path.GetFileNameWithoutExtension(e.FullPath);
        if (langCode == _currentLanguage)
        {
            try
            {
                LoadLanguage(_currentLanguage);
            }
            catch { }
        }
    }
}
