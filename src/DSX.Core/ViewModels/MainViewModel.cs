using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSX.Core.Interfaces;
using DSX.Core.Models;
using DSX.Core.Enums;

namespace DSX.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;
    private readonly IProfileService _profileService;
    private readonly ILocalizationService _localizationService;
    private readonly IUDPServer _udpServer;
    private readonly IHidHideService _hidHideService;
    private readonly IAudioService _audioService;
    private readonly IBackupService _backupService;
    private readonly IGameService _gameService;
    private readonly IVirtualDeviceService _virtualDeviceService;
    private readonly IMicrophoneService _microphoneService;

    [ObservableProperty]
    private NavigationPage _currentPage = NavigationPage.Home;

    [ObservableProperty]
    private ControllerDeviceInfo? _activeController;

    [ObservableProperty]
    private UserProfile? _activeProfile;

    [ObservableProperty]
    private bool _isControllerConnected;

    [ObservableProperty]
    private bool _isDSXPlusOwner;

    [ObservableProperty]
    private string _appVersion = "3.1.5.3";

    [ObservableProperty]
    private ObservableCollection<ControllerDeviceInfo> _connectedControllers = new();

    [ObservableProperty]
    private ObservableCollection<Notification> _notifications = new();

    [ObservableProperty]
    private int _unreadNotificationCount;

    [ObservableProperty]
    private bool _isFirstLaunch;

    public HomeViewModel HomeViewModel { get; }
    public MyControllersViewModel MyControllersViewModel { get; }
    public ControllerMappingViewModel ControllerMappingViewModel { get; }
    public AdaptiveTriggersViewModel AdaptiveTriggersViewModel { get; }
    public LEDViewModel LEDViewModel { get; }
    public HapticsRumbleViewModel HapticsRumbleViewModel { get; }
    public VirtualDeviceViewModel VirtualDeviceViewModel { get; }
    public InstalledGamesViewModel InstalledGamesViewModel { get; }
    public ModsViewModel ModsViewModel { get; }
    public HidHideViewModel HidHideViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public HelpCenterViewModel HelpCenterViewModel { get; }

    public MainViewModel(
        IControllerService controllerService,
        IProfileService profileService,
        ILocalizationService localizationService,
        IUDPServer udpServer,
        IHidHideService hidHideService,
        IAudioService audioService,
        IBackupService backupService,
        IGameService gameService,
        IVirtualDeviceService virtualDeviceService,
        IMicrophoneService microphoneService)
    {
        _controllerService = controllerService;
        _profileService = profileService;
        _localizationService = localizationService;
        _udpServer = udpServer;
        _hidHideService = hidHideService;
        _audioService = audioService;
        _backupService = backupService;
        _gameService = gameService;
        _virtualDeviceService = virtualDeviceService;
        _microphoneService = microphoneService;

        HomeViewModel = new HomeViewModel(this);
        MyControllersViewModel = new MyControllersViewModel(this);
        ControllerMappingViewModel = new ControllerMappingViewModel(this);
        AdaptiveTriggersViewModel = new AdaptiveTriggersViewModel(this);
        LEDViewModel = new LEDViewModel(this);
        HapticsRumbleViewModel = new HapticsRumbleViewModel(this);
        VirtualDeviceViewModel = new VirtualDeviceViewModel(this);
        InstalledGamesViewModel = new InstalledGamesViewModel(this);
        ModsViewModel = new ModsViewModel(this);
        HidHideViewModel = new HidHideViewModel(this);
        SettingsViewModel = new SettingsViewModel(this);
        HelpCenterViewModel = new HelpCenterViewModel(this);

        _controllerService.ControllerConnected += OnControllerConnected;
        _controllerService.ControllerDisconnected += OnControllerDisconnected;
        _profileService.ProfileChanged += OnProfileChanged;
    }

    public IControllerService ControllerService => _controllerService;
    public IProfileService ProfileService => _profileService;
    public ILocalizationService LocalizationService => _localizationService;
    public IUDPServer UdpServer => _udpServer;
    public IHidHideService HidHideService => _hidHideService;
    public IAudioService AudioService => _audioService;
    public IBackupService BackupService => _backupService;
    public IGameService GameService => _gameService;
    public IVirtualDeviceService VirtualDeviceService => _virtualDeviceService;
    public IMicrophoneService MicrophoneService => _microphoneService;

    [RelayCommand]
    private void NavigateTo(NavigationPage page) => CurrentPage = page;

    private void OnControllerConnected(object? sender, ControllerDeviceInfo info)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (!ConnectedControllers.Any(c => c.DeviceId == info.DeviceId))
                ConnectedControllers.Add(info);
            ActiveController = info;
            IsControllerConnected = true;
        });
    }

    private void OnControllerDisconnected(object? sender, string deviceId)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var existing = ConnectedControllers.FirstOrDefault(c => c.DeviceId == deviceId);
            if (existing != null)
                ConnectedControllers.Remove(existing);
            if (ActiveController?.DeviceId == deviceId)
            {
                ActiveController = ConnectedControllers.FirstOrDefault();
                IsControllerConnected = ActiveController != null;
            }
        });
    }

    private void OnProfileChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ActiveProfile = _profileService.ActiveProfile;
        });
    }

    public void AddNotification(string title, string message)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Notifications.Insert(0, new Notification { Title = title, Message = message });
            UnreadNotificationCount = Notifications.Count(n => !n.IsRead);
        });
    }

    public void Initialize()
    {
        if (_controllerService.ConnectedControllers.Count > 0)
        {
            ActiveController = _controllerService.ActiveController;
            IsControllerConnected = true;
            ConnectedControllers = new ObservableCollection<ControllerDeviceInfo>(
                _controllerService.ConnectedControllers);
        }

        ActiveProfile = _profileService.ActiveProfile;
        IsDSXPlusOwner = _virtualDeviceService.IsDSXPlusOwner;
    }
}
