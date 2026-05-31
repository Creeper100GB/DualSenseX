using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSX.Core.Enums;
using DSX.Core.Models;

namespace DSX.Core.ViewModels;

public partial class InstalledGamesViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private ObservableCollection<GameInfo> _installedGames = new();

    [ObservableProperty]
    private GameInfo? _selectedGame;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _assignedProfileId = string.Empty;

    [ObservableProperty]
    private bool _resetProfileOnGameClose;

    public ObservableCollection<GameInfo> FilteredGames =>
        string.IsNullOrWhiteSpace(SearchText)
            ? InstalledGames
            : new ObservableCollection<GameInfo>(
                InstalledGames.Where(g =>
                    g.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));

    public InstalledGamesViewModel(MainViewModel main)
    {
        _main = main;
        _main.GameService.GamesUpdated += OnGamesUpdated;
        LoadGames();
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredGames));
    }

    partial void OnSelectedGameChanged(GameInfo? value)
    {
        AssignedProfileId = value?.AssignedProfileId ?? string.Empty;
    }

    private void LoadGames()
    {
        InstalledGames = new ObservableCollection<GameInfo>(
            _main.GameService.InstalledGames);
        OnPropertyChanged(nameof(FilteredGames));
    }

    private void OnGamesUpdated(object? sender, EventArgs e)
    {
        LoadGames();
    }

    [RelayCommand]
    private void ScanGames()
    {
        _main.GameService.ScanGames();
    }

    [RelayCommand]
    private void AssignProfile()
    {
        if (SelectedGame == null || string.IsNullOrEmpty(AssignedProfileId)) return;
        _main.GameService.AssignProfile(SelectedGame.Id, AssignedProfileId);
    }

    [RelayCommand]
    private void RemoveProfile()
    {
        if (SelectedGame == null) return;
        _main.GameService.RemoveProfile(SelectedGame.Id);
        AssignedProfileId = string.Empty;
    }

    [RelayCommand]
    private void SearchGames()
    {
        OnPropertyChanged(nameof(FilteredGames));
    }
}
