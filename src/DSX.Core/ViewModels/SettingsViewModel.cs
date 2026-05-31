using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSX.Core.Enums;
using DSX.Core.Models;

namespace DSX.Core.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private WindowEffect _windowEffect = WindowEffect.Mica;

    [ObservableProperty]
    private bool _darkMode = true;

    [ObservableProperty]
    private string _language = "en-US";

    [ObservableProperty]
    private ObservableCollection<string> _availableLanguages = new();

    [ObservableProperty]
    private ObservableCollection<WindowEffect> _availableWindowEffects = new(
        Enum.GetValues<WindowEffect>());

    [ObservableProperty]
    private bool _autoConnectOnLaunch = true;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private ObservableCollection<BackupInfo> _backups = new();

    [ObservableProperty]
    private string _appVersion = "3.1.5.3";

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
        AvailableLanguages = new ObservableCollection<string>(
            _main.LocalizationService.AvailableLanguages);
        AppVersion = main.AppVersion;
        LoadBackups();
    }

    private void LoadBackups()
    {
        _main.BackupService.RefreshBackups();
        Backups = new ObservableCollection<BackupInfo>(
            _main.BackupService.Backups);
    }

    [RelayCommand]
    private void ChangeWindowEffect()
    {
    }

    [RelayCommand]
    private void ChangeLanguage()
    {
        _main.LocalizationService.LoadLanguage(Language);
    }

    [RelayCommand]
    private void ToggleDarkMode()
    {
        DarkMode = !DarkMode;
    }

    [RelayCommand]
    private void CreateBackup()
    {
        _main.BackupService.CreateBackup();
        LoadBackups();
    }

    [RelayCommand]
    private void RestoreBackup()
    {
    }

    [RelayCommand]
    private void DeleteBackup()
    {
    }

    [RelayCommand]
    private void ResetSettings()
    {
        WindowEffect = WindowEffect.Mica;
        DarkMode = true;
        Language = "en-US";
        AutoConnectOnLaunch = true;
        StartWithWindows = false;
        MinimizeToTray = true;
    }
}
