using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSX.Core.Enums;
using DSX.Core.Models;

namespace DSX.Core.ViewModels;

public partial class VirtualDeviceViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private bool _isDSXPlusOwner;

    [ObservableProperty]
    private int _selectedTab;

    [ObservableProperty]
    private bool _hasVirtualDualSense;

    [ObservableProperty]
    private bool _hasVirtualXbox360;

    [ObservableProperty]
    private bool _hasVirtualDS4;

    public VirtualDeviceViewModel(MainViewModel main)
    {
        _main = main;
        _main.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsDSXPlusOwner))
                IsDSXPlusOwner = _main.IsDSXPlusOwner;
        };
        IsDSXPlusOwner = main.IsDSXPlusOwner;
    }

    [RelayCommand]
    private void CreateVirtualDualSense()
    {
        _main.VirtualDeviceService.CreateVirtualDualSense();
        HasVirtualDualSense = true;
    }

    [RelayCommand]
    private void CreateVirtualXbox360()
    {
        _main.VirtualDeviceService.CreateVirtualXbox360();
        HasVirtualXbox360 = true;
    }

    [RelayCommand]
    private void CreateVirtualDS4()
    {
        _main.VirtualDeviceService.CreateVirtualDualShock4();
        HasVirtualDS4 = true;
    }

    [RelayCommand]
    private void RemoveVirtualDevice()
    {
    }

    [RelayCommand]
    private void AddSoundDevice()
    {
    }

    [RelayCommand]
    private void SyncDSXPlus()
    {
        _main.VirtualDeviceService.SyncDSXPlus();
        IsDSXPlusOwner = _main.IsDSXPlusOwner;
    }
}
