using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DSX.Core.Constants;
using DSX.Core.Models;
using DSX.Core.Services;
using DSX.Core.Services.Audio;
using DSX.Core.Services.Backup;
using DSX.Core.Services.Controller;
using DSX.Core.Services.HID;
using DSX.Core.Services.HidHide;
using DSX.Core.Services.Localization;
using DSX.Core.Services.Profile;
using DSX.Core.Services.UDP;
using DSX.Core.ViewModels;
using DSX.WPF.Helpers;
using DSX.WPF.Views;
using SplashScreenWindow = DSX.WPF.Views.SplashScreen;

namespace DSX.WPF;

public partial class App : Application
{
    private MainViewModel? _mainViewModel;
    private ControllerService? _controllerService;
    private ProfileService? _profileService;
    private UDPServer? _udpServer;
    private DiscordRichPresence? _discordPresence;
    private SplashScreenWindow? _splashScreen;

    public static AppSettings Settings { get; private set; } = new();

    private void OnStartup(object sender, StartupEventArgs e)
    {
        if (!DSX.WPF.MainWindow.EnsureSingleInstance())
        {
            MessageBox.Show("DualSenseX is already running.", "DSX", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        SetupExceptionHandlers();
        EnsureDirectoryStructure();

        LoadSettings();

        ShowSplashScreen();

        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await InitializeServicesAsync();
                CreateMainWindow();
            }
            catch (Exception ex)
            {
                LogError($"Startup failed: {ex}");
                MessageBox.Show($"Failed to start DualSenseX:\n{ex.Message}", "DSX Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            CloseSplashScreen();
            if (Current.MainWindow is Window w)
            {
                w.Activate();
                w.Focus();
            }
        });
    }

    private void ShowSplashScreen()
    {
        try
        {
            _splashScreen = new SplashScreenWindow();
            _splashScreen.Show();
        }
        catch
        {
            // Splash screen is non-critical
        }
    }

    private void CloseSplashScreen()
    {
        try
        {
            if (_splashScreen != null)
            {
                _splashScreen.Close();
                _splashScreen = null;
            }
        }
        catch { }
    }

    private async Task InitializeServicesAsync()
    {
        _controllerService = new ControllerService();
        ControllerService.LogAction = App.LogError;
        _profileService = new ProfileService();

        var localizationService = new LocalizationService();
        _udpServer = new UDPServer();
        var hidHideService = new HidHideService();
        var audioService = new AudioService(_controllerService);
        var backupService = new BackupService();
        var gameService = new GameService();
        var virtualDeviceService = new VirtualDeviceService();
        var microphoneService = new MicrophoneService();

        _mainViewModel = new MainViewModel(
            _controllerService,
            _profileService,
            localizationService,
            _udpServer,
            hidHideService,
            audioService,
            backupService,
            gameService,
            virtualDeviceService,
            microphoneService);

        _mainViewModel.Initialize();

        if (Settings.AutoConnectOnLaunch)
        {
            try
            {
                _controllerService.StartPolling();
            }
            catch (Exception ex)
            {
                LogError($"Auto-connect failed: {ex.Message}");
            }
        }

        try
        {
            _udpServer.Start(Settings.UdpPort);
        }
        catch (Exception ex)
        {
            LogError($"UDP server start failed: {ex.Message}");
        }

        try
        {
            _discordPresence = new DiscordRichPresence();
            await _discordPresence.InitializeAsync();
        }
        catch (Exception ex)
        {
            LogError($"Discord RPC init failed: {ex.Message}");
        }

        if (Settings.FirstLaunch)
        {
            Settings.FirstLaunch = false;
            SaveSettings();
        }
    }

    private void CreateMainWindow()
    {
        var mainWindow = new DSX.WPF.MainWindow();
        mainWindow.Initialize(_mainViewModel!, Settings);
        Current.MainWindow = mainWindow;
        mainWindow.Show();
        mainWindow.Activate();
        mainWindow.Focus();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        try
        {
            SaveSettings();

            _controllerService?.StopPolling();
            _controllerService?.Dispose();

            _udpServer?.Stop();
            _udpServer?.Dispose();

            _profileService?.Dispose();
            _discordPresence?.Dispose();
        }
        catch (Exception ex)
        {
            LogError($"Exit cleanup error: {ex.Message}");
        }
    }

    public static void LoadSettings()
    {
        try
        {
            if (File.Exists(AppConstants.SettingsFilePath))
            {
                var json = File.ReadAllText(AppConstants.SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                    Settings = settings;
            }
        }
        catch (Exception ex)
        {
            LogError($"Settings load failed: {ex.Message}");
        }
    }

    public static void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppConstants.SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            LogError($"Settings save failed: {ex.Message}");
        }
    }

    private static void EnsureDirectoryStructure()
    {
        try
        {
            Directory.CreateDirectory(AppConstants.LocalAppDataPath);
            Directory.CreateDirectory(AppConstants.ProfilesPath);
            Directory.CreateDirectory(AppConstants.BackupsPath);
            Directory.CreateDirectory(AppConstants.LogsPath);
            Directory.CreateDirectory(AppConstants.LanguagesPath);
        }
        catch (Exception ex)
        {
            LogError($"Directory creation failed: {ex.Message}");
        }
    }

    private void SetupExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogError($"Dispatcher unhandled: {e.Exception}");
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogError($"Unobserved task: {e.Exception?.InnerException}");
        e.SetObserved();
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        LogError($"Domain unhandled: {e.ExceptionObject}");
    }

    public static void LogError(string message)
    {
        try
        {
            var logPath = Path.Combine(AppConstants.LogsPath, "DSX_Log.txt");
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            File.AppendAllText(logPath, $"[{timestamp}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
