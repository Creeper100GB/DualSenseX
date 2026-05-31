using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSX.Core.Enums;
using DSX.Core.Models;

namespace DSX.Core.ViewModels;

public partial class ModsViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private ObservableCollection<ModInfo> _availableMods = new();

    [ObservableProperty]
    private ModInfo? _selectedMod;

    [ObservableProperty]
    private int _udpPort = 6969;

    [ObservableProperty]
    private ObservableCollection<string> _supportedGames = new();

    public event EventHandler? AddModDialogRequested;

    public ModsViewModel(MainViewModel main)
    {
        _main = main;
    }

    [RelayCommand]
    private void AddMod()
    {
        AddModDialogRequested?.Invoke(this, EventArgs.Empty);
    }

    public void AddModFromFile(string filePath)
    {
        AvailableMods.Add(new ModInfo
        {
            Id = Guid.NewGuid().ToString(),
            FilePath = filePath,
            IsInstalled = true
        });
    }

    [RelayCommand]
    private void RemoveMod()
    {
        if (SelectedMod == null) return;
        AvailableMods.Remove(SelectedMod);
        SelectedMod = null;
    }

    [RelayCommand]
    private void SaveUdpPort()
    {
        _main.UdpServer.Stop();
        _main.UdpServer.Start(UdpPort);
    }

    [RelayCommand]
    private void OpenModRepository()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://dualsensex.com/mods",
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void RefreshMods()
    {
    }
}
