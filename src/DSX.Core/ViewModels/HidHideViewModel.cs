using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSX.Core.Enums;
using DSX.Core.Models;

namespace DSX.Core.ViewModels;

public partial class HidHideViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private bool _isDriverInstalled;

    [ObservableProperty]
    private bool _letDSXControl = true;

    [ObservableProperty]
    private bool _persistentHiding;

    [ObservableProperty]
    private ObservableCollection<string> _whitelistedApps = new();

    [ObservableProperty]
    private string _driverStatusText = "Unknown";

    public event EventHandler? AddToWhitelistDialogRequested;

    public HidHideViewModel(MainViewModel main)
    {
        _main = main;
        RefreshDriverStatus();
    }

    [RelayCommand]
    private void RefreshDriverStatus()
    {
        _main.HidHideService.RefreshDriverStatus();
        IsDriverInstalled = _main.HidHideService.IsDriverInstalled;
        LetDSXControl = _main.HidHideService.LetDSXControl;
        PersistentHiding = _main.HidHideService.PersistentHiding;
        WhitelistedApps = new ObservableCollection<string>(
            _main.HidHideService.WhitelistedApplications);
        DriverStatusText = IsDriverInstalled ? "Installed" : "Not Installed";
    }

    [RelayCommand]
    private void AddToWhitelist()
    {
        AddToWhitelistDialogRequested?.Invoke(this, EventArgs.Empty);
    }

    public void AddWhitelistedApp(string applicationPath)
    {
        _main.HidHideService.AddToWhitelist(applicationPath);
        WhitelistedApps.Add(applicationPath);
    }

    [RelayCommand]
    private void RemoveFromWhitelist()
    {
    }

    [RelayCommand]
    private void ToggleDSXControl()
    {
        LetDSXControl = !LetDSXControl;
        _main.HidHideService.LetDSXControl = LetDSXControl;
    }

    [RelayCommand]
    private void TogglePersistentHiding()
    {
        PersistentHiding = !PersistentHiding;
        _main.HidHideService.PersistentHiding = PersistentHiding;
        if (PersistentHiding)
            _main.HidHideService.ApplyPersistentHiding();
    }
}
