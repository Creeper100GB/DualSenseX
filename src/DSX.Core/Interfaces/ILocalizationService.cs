using System.Collections.Generic;
using DSX.Core.Models;

namespace DSX.Core.Interfaces;

public interface ILocalizationService
{
    string CurrentLanguage { get; set; }
    IReadOnlyList<string> AvailableLanguages { get; }
    string GetString(string key);
    void LoadLanguage(string languageCode);
    event EventHandler? LanguageChanged;
}
