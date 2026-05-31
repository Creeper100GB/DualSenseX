using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSX.Core.Enums;
using DSX.Core.Models;

namespace DSX.Core.ViewModels;

public partial class ControllerMappingViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private EmulationType _emulationType = EmulationType.Xbox360;

    [ObservableProperty]
    private UserProfile? _selectedProfile;

    [ObservableProperty]
    private ObservableCollection<UserProfile> _availableProfiles = new();

    [ObservableProperty]
    private ObservableCollection<ActionListItem> _buttonActions = new();

    [ObservableProperty]
    private ActionListItem? _selectedButtonAction;

    [ObservableProperty]
    private StickMappingConfig _leftStickMapping = new();

    [ObservableProperty]
    private StickMappingConfig _rightStickMapping = new();

    [ObservableProperty]
    private MotionMappingConfig _motionMapping = new();

    [ObservableProperty]
    private DeadzoneConfig _deadzoneConfig = new();

    [ObservableProperty]
    private TouchpadGestureConfig _touchpadGestureConfig = new();

    [ObservableProperty]
    private bool _isButtonSelected;

    [ObservableProperty]
    private string _selectedButtonName = string.Empty;

    public event EventHandler? ImportProfileDialogRequested;
    public event EventHandler<string>? ExportProfileDialogRequested;

    public ControllerMappingViewModel(MainViewModel main)
    {
        _main = main;
        _main.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ActiveProfile))
                LoadFromProfile();
        };
        RefreshProfiles();
        LoadFromProfile();
    }

    partial void OnSelectedProfileChanged(UserProfile? value)
    {
        if (value != null)
            _main.ProfileService.LoadProfile(value.Id);
    }

    partial void OnSelectedButtonActionChanged(ActionListItem? value)
    {
        IsButtonSelected = value != null;
        SelectedButtonName = value?.InputName ?? string.Empty;
    }

    private void LoadFromProfile()
    {
        var profile = _main.ActiveProfile;
        if (profile == null) return;

        EmulationType = profile.EmulationType;
        LeftStickMapping = profile.LeftStickMapping;
        RightStickMapping = profile.RightStickMapping;
        MotionMapping = profile.MotionMapping;
        DeadzoneConfig = profile.DeadzoneConfig;
        TouchpadGestureConfig = profile.TouchpadGestureConfig;

        var actions = new ObservableCollection<ActionListItem>();
        foreach (var kvp in profile.ButtonActions)
        {
            foreach (var action in kvp.Value)
            {
                actions.Add(new ActionListItem
                {
                    InputName = kvp.Key.ToString(),
                    PressType = action.PressType.ToString(),
                    ActionDescription = DescribeAction(action),
                    SourceAction = action
                });
            }
        }
        ButtonActions = actions;
    }

    private static string DescribeAction(ButtonAction action)
    {
        return action.ActionType switch
        {
            ActionType.KeyboardKey => $"Key: {action.KeyValue}",
            ActionType.MouseClick => $"Mouse: {action.MouseButton}",
            ActionType.MouseScroll => $"Scroll: {action.ScrollDirection}",
            ActionType.ControllerButtonRemap => $"Remap: {action.RemapTarget}",
            ActionType.Macro => $"Macro ({action.MacroSteps.Count} steps)",
            ActionType.OpenApplication => $"App: {action.ApplicationPath}",
            ActionType.SwitchProfile => $"Profile: {action.TargetProfileId}",
            ActionType.ActivateSteamBigPicture => "Steam Big Picture",
            ActionType.VirtualDeviceInput => "Virtual Device",
            _ => action.ActionType.ToString()
        };
    }

    private void RefreshProfiles()
    {
        AvailableProfiles = new ObservableCollection<UserProfile>(
            _main.ProfileService.Profiles);
        SelectedProfile = _main.ActiveProfile;
    }

    [RelayCommand]
    private void CreateAction()
    {
    }

    [RelayCommand]
    private void DeleteAction()
    {
        if (SelectedButtonAction?.SourceAction == null) return;
    }

    [RelayCommand]
    private void EditAction()
    {
        if (SelectedButtonAction?.SourceAction == null) return;
    }

    [RelayCommand]
    private void ClearAllButtonSelections()
    {
        SelectedButtonAction = null;
        IsButtonSelected = false;
        SelectedButtonName = string.Empty;
    }

    [RelayCommand]
    private void SetEmulationType()
    {
        var profile = _main.ActiveProfile;
        if (profile == null) return;
        profile.EmulationType = EmulationType;
        _main.ProfileService.UpdateProfile(profile);
    }

    [RelayCommand]
    private void ImportProfile()
    {
        ImportProfileDialogRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ImportProfileFromPath(string filePath)
    {
        _main.ProfileService.ImportProfile(filePath);
        RefreshProfiles();
    }

    [RelayCommand]
    private void ExportProfile()
    {
        if (SelectedProfile == null) return;
        ExportProfileDialogRequested?.Invoke(this, SelectedProfile.Name);
    }

    public void ExportProfileToPath(string filePath)
    {
        if (SelectedProfile == null) return;
        _main.ProfileService.ExportProfile(SelectedProfile.Id, filePath);
    }

    public class ActionListItem
    {
        public string InputName { get; set; } = string.Empty;
        public string PressType { get; set; } = string.Empty;
        public string ActionDescription { get; set; } = string.Empty;
        public ButtonAction SourceAction { get; set; } = null!;
    }
}
