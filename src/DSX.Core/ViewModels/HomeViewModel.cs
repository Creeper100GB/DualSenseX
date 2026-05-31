using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSX.Core.Enums;
using DSX.Core.Models;

namespace DSX.Core.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private DateTime _lastBatteryUpdate = DateTime.MinValue;

    [ObservableProperty]
    private string _controllerName = "No Controller";

    [ObservableProperty]
    private ConnectionType _connectionType;

    [ObservableProperty]
    private double _batteryPercent;

    [ObservableProperty]
    private bool _isCharging;

    [ObservableProperty]
    private string _batteryStatusText = "N/A";

    [ObservableProperty]
    private string _appVersion = "3.1.5.3";

    [ObservableProperty]
    private bool _isDSXPlusOwner;

    [ObservableProperty]
    private bool _isControllerConnected;

    [ObservableProperty]
    private string _macAddress = string.Empty;

    public System.Collections.ObjectModel.ObservableCollection<Notification> Notifications => _main.Notifications;

    public HomeViewModel(MainViewModel main)
    {
        _main = main;
        _main.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ActiveController))
                UpdateControllerInfo();
            if (e.PropertyName == nameof(MainViewModel.IsControllerConnected))
                IsControllerConnected = _main.IsControllerConnected;
            if (e.PropertyName == nameof(MainViewModel.IsDSXPlusOwner))
                IsDSXPlusOwner = _main.IsDSXPlusOwner;
            if (e.PropertyName == nameof(MainViewModel.AppVersion))
                AppVersion = _main.AppVersion;
        };
        _main.ControllerService.InputReceived += (s, state) =>
        {
            if ((DateTime.UtcNow - _lastBatteryUpdate).TotalSeconds >= 2)
            {
                _lastBatteryUpdate = DateTime.UtcNow;
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => UpdateControllerInfo());
            }
        };
        AppVersion = main.AppVersion;
        IsDSXPlusOwner = main.IsDSXPlusOwner;
        IsControllerConnected = main.IsControllerConnected;
        UpdateControllerInfo();
    }

    private void UpdateControllerInfo()
    {
        var controller = _main.ActiveController;
        if (controller == null)
        {
            ControllerName = "No Controller";
            ConnectionType = ConnectionType.Unknown;
            BatteryPercent = 0;
            IsCharging = false;
            BatteryStatusText = "N/A";
            IsControllerConnected = false;
            MacAddress = string.Empty;
            return;
        }

        ControllerName = controller.Name;
        ConnectionType = controller.Connection;
        BatteryPercent = controller.BatteryPercentage;
        IsCharging = controller.IsCharging;
        IsControllerConnected = true;
        MacAddress = controller.MacAddress;
        BatteryStatusText = IsCharging
            ? $"Charging ({BatteryPercent:F0}%)"
            : $"{BatteryPercent:F0}%";
    }

    [RelayCommand]
    private void LaunchLegacyDSX()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://dualsensex.com",
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenDiscord()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://discord.gg/dualsensex",
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenChangelog()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://dualsensex.com/changelog",
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void SyncDSXPlus()
    {
        _main.VirtualDeviceService.SyncDSXPlus();
        IsDSXPlusOwner = _main.IsDSXPlusOwner;
    }

    [RelayCommand]
    private void ActivateSteamBigPicture()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "steam://open/bigpicture",
            UseShellExecute = true
        });
    }
}
