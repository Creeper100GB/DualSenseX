using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DSX.Core.Enums;
using DSX.Core.Models;
using DSX.Core.ViewModels;
using DSX.WPF.Helpers;
using DSX.WPF.Views.Pages;
using Hardcodet.Wpf.TaskbarNotification;

namespace DSX.WPF;

public partial class MainWindow : Window
{
    private static Mutex? _mutex;
    private static bool _createdNew;
    private TaskbarIcon? _trayIcon;
    private AppSettings? _settings;
    private DispatcherTimer? _statusBarTimer;

    public MainViewModel? ViewModel { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
    }

    public void Initialize(MainViewModel viewModel, AppSettings settings)
    {
        ViewModel = viewModel;
        _settings = settings;
        DataContext = viewModel;

        SidebarNav.CurrentPage = viewModel.CurrentPage;
        SidebarNav.IsControllerConnected = viewModel.IsControllerConnected;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdateStatusBarConnection(viewModel.IsControllerConnected);
        UpdateStatusBarProfile(viewModel.ActiveProfile);
        UpdateStatusBarBattery(viewModel.ActiveController?.BatteryPercentage ?? -1);

        SetupTrayIcon();
        SetupStatusBarTimer();

        SidebarNav.NavigationChanged += OnNavigationChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.IsControllerConnected):
                    UpdateStatusBarConnection(ViewModel!.IsControllerConnected);
                    SidebarNav.IsControllerConnected = ViewModel.IsControllerConnected;
                    break;
                case nameof(MainViewModel.ActiveProfile):
                    UpdateStatusBarProfile(ViewModel!.ActiveProfile);
                    break;
                case nameof(MainViewModel.ActiveController):
                    if (ViewModel!.ActiveController != null)
                        UpdateStatusBarBattery(ViewModel.ActiveController.BatteryPercentage);
                    break;
                case nameof(MainViewModel.CurrentPage):
                    SidebarNav.CurrentPage = ViewModel!.CurrentPage;
                    NavigateToPage(ViewModel.CurrentPage);
                    break;
            }
        });
    }

    private void SetupStatusBarTimer()
    {
        _statusBarTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _statusBarTimer.Tick += (_, _) =>
        {
            if (ViewModel?.ActiveController != null)
                UpdateStatusBarBattery(ViewModel.ActiveController.BatteryPercentage);
        };
        _statusBarTimer.Start();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "DualSenseX",
            Visibility = Visibility.Visible,
            ContextMenu = CreateTrayContextMenu()
        };

        try
        {
            _trayIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/DSX;component/Resources/Icons/dsx_icon.ico", UriKind.Absolute));
        }
        catch { }

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowWindow();
    }

    private System.Windows.Controls.ContextMenu CreateTrayContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu { Style = (Style)FindResource("DSXContextMenu") };

        var showItem = new System.Windows.Controls.MenuItem
        {
            Header = "Show DSX",
            Style = (Style)FindResource("DSXMenuItem")
        };
        showItem.Click += (_, _) => ShowWindow();
        menu.Items.Add(showItem);

        var separator = new System.Windows.Controls.Separator { Style = (Style)FindResource("DSXSeparator") };
        menu.Items.Add(separator);

        var exitItem = new System.Windows.Controls.MenuItem
        {
            Header = "Exit",
            Style = (Style)FindResource("DSXMenuItem")
        };
        exitItem.Click += (_, _) => ShutdownApplication();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ShutdownApplication()
    {
        _trayIcon?.Dispose();
        Application.Current.Shutdown();
    }

    public static bool EnsureSingleInstance()
    {
        _mutex = new Mutex(true, "DualSenseX_SingleInstance_Mutex", out _createdNew);
        return _createdNew;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            WindowEffectHelper.SetDarkMode(this);
        }
        catch { }

        NavigateToPage(ViewModel?.CurrentPage ?? NavigationPage.Home);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            WindowEffectHelper.SetDarkMode(this);
        }
        catch { }
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        try
        {
            DragMove();
        }
        catch { }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        if (_settings?.MinimizeToTray == true)
        {
            Hide();
        }
        else
        {
            ShutdownApplication();
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeButtonIcon();

        if (WindowState == WindowState.Maximized)
        {
            WindowBorder.Padding = new Thickness(6);
        }
        else
        {
            WindowBorder.Padding = new Thickness(0);
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_settings?.MinimizeToTray == true)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            Cleanup();
        }
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void UpdateMaximizeButtonIcon()
    {
        MaximizeIcon.Text = WindowState == WindowState.Maximized ? "\xE923" : "\xE922";
    }

    private void UpdateStatusBarConnection(bool connected)
    {
        StatusBarConnectionDot.Fill = connected
            ? (SolidColorBrush)FindResource("SuccessBrush")
            : (SolidColorBrush)FindResource("ErrorBrush");

        StatusBarConnectionText.Text = connected ? "Connected" : "Disconnected";
        StatusBarConnectionText.Foreground = connected
            ? (SolidColorBrush)FindResource("TextSecondary")
            : (SolidColorBrush)FindResource("TextMuted");
    }

    private void UpdateStatusBarProfile(UserProfile? profile)
    {
        StatusBarProfileText.Text = profile?.Name ?? "No Profile";
    }

    private void UpdateStatusBarBattery(double percentage)
    {
        if (percentage < 0 || ViewModel?.IsControllerConnected != true)
        {
            StatusBarBatteryText.Text = "--";
            return;
        }

        StatusBarBatteryText.Text = $"{percentage:0}%";
        StatusBarBatteryText.Foreground = percentage switch
        {
            >= 60 => (SolidColorBrush)FindResource("SuccessBrush"),
            >= 30 => (SolidColorBrush)FindResource("WarningBrush"),
            _ => (SolidColorBrush)FindResource("ErrorBrush")
        };
    }

    private void OnNavigationChanged(object sender, RoutedEventArgs e)
    {
        if (SidebarNav.CurrentPage is NavigationPage page)
        {
            ViewModel?.NavigateToCommand.Execute(page);
            NavigateToPage(page);
        }
    }

    private readonly Dictionary<NavigationPage, FrameworkElement> _pageCache = new();

    private void NavigateToPage(NavigationPage page)
    {
        if (!_pageCache.TryGetValue(page, out var pageContent))
        {
            pageContent = CreatePageInstance(page) ?? CreatePlaceholderForPage(page);
            _pageCache[page] = pageContent;

            if (pageContent is UserControl uc && ViewModel != null)
            {
                var pageVm = GetPageViewModel(page);
                if (pageVm != null)
                    uc.DataContext = pageVm;
            }
        }

        PageContent.Content = pageContent;
    }

    private object? GetPageViewModel(NavigationPage page)
    {
        return page switch
        {
            NavigationPage.Home => ViewModel?.HomeViewModel,
            NavigationPage.MyControllers => ViewModel?.MyControllersViewModel,
            NavigationPage.ControllerMapping => ViewModel?.ControllerMappingViewModel,
            NavigationPage.AdaptiveTriggers => ViewModel?.AdaptiveTriggersViewModel,
            NavigationPage.LEDLighting => ViewModel?.LEDViewModel,
            NavigationPage.HapticsRumble => ViewModel?.HapticsRumbleViewModel,
            NavigationPage.VirtualDevice => ViewModel?.VirtualDeviceViewModel,
            NavigationPage.InstalledGames => ViewModel?.InstalledGamesViewModel,
            NavigationPage.Mods => ViewModel?.ModsViewModel,
            NavigationPage.HidHide => ViewModel?.HidHideViewModel,
            NavigationPage.Settings => ViewModel?.SettingsViewModel,
            NavigationPage.HelpCenter => ViewModel?.HelpCenterViewModel,
            _ => null
        };
    }

    private static FrameworkElement? CreatePageInstance(NavigationPage page)
    {
        try
        {
            FrameworkElement? instance = page switch
            {
                NavigationPage.Home => new HomePage(),
                NavigationPage.MyControllers => new MyControllersPage(),
                NavigationPage.ControllerMapping => new ControllerMappingPage(),
                NavigationPage.AdaptiveTriggers => new AdaptiveTriggersPage(),
                NavigationPage.LEDLighting => new LEDPage(),
                NavigationPage.HapticsRumble => new HapticsRumblePage(),
                NavigationPage.VirtualDevice => new VirtualDevicePage(),
                NavigationPage.InstalledGames => new InstalledGamesPage(),
                NavigationPage.Mods => new ModsPage(),
                NavigationPage.HidHide => new HidHidePage(),
                NavigationPage.Settings => new SettingsPage(),
                NavigationPage.HelpCenter => new HelpCenterPage(),
                _ => null
            };
            if (instance != null)
                App.LogError($"Page created OK: {page}");
            return instance;
        }
        catch (Exception ex)
        {
            App.LogError($"FAILED to create page {page}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    private FrameworkElement CreatePlaceholderForPage(NavigationPage page)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleText = new System.Windows.Controls.TextBlock
        {
            Text = GetPageTitle(page),
            Style = (Style)FindResource("DSXHeaderTextBlock"),
            Margin = new Thickness(0, 0, 0, 24)
        };
        Grid.SetRow(titleText, 0);
        grid.Children.Add(titleText);

        var placeholderBorder = new System.Windows.Controls.Border
        {
            Style = (Style)FindResource("CardBorder"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(40, 24, 40, 24)
        };

        var stackPanel = new System.Windows.Controls.StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var iconText = new System.Windows.Controls.TextBlock
        {
            Text = GetPageIcon(page),
            FontFamily = (FontFamily)FindResource("IconFontFamily"),
            FontSize = 48,
            Foreground = (SolidColorBrush)FindResource("TextMuted"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16),
            Opacity = 0.4
        };
        stackPanel.Children.Add(iconText);

        var descText = new System.Windows.Controls.TextBlock
        {
            Text = $"{GetPageTitle(page)} page content will be implemented here.",
            Style = (Style)FindResource("DSXMutedTextBlock"),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        stackPanel.Children.Add(descText);

        placeholderBorder.Child = stackPanel;
        Grid.SetRow(placeholderBorder, 1);
        grid.Children.Add(placeholderBorder);

        return grid;
    }

    private void AnimatePageTransition()
    {
        TransitionOverlay.Opacity = 0;
        TransitionOverlay.IsHitTestVisible = false;
    }

    private static string GetPageTitle(NavigationPage page) => page switch
    {
        NavigationPage.Home => "Home",
        NavigationPage.MyControllers => "My Controllers",
        NavigationPage.ControllerMapping => "Controller Mapping",
        NavigationPage.AdaptiveTriggers => "Adaptive Triggers",
        NavigationPage.LEDLighting => "LED / Lighting",
        NavigationPage.HapticsRumble => "Haptics / Rumble",
        NavigationPage.VirtualDevice => "Virtual Device",
        NavigationPage.InstalledGames => "Installed Games",
        NavigationPage.Mods => "Mods",
        NavigationPage.HidHide => "HidHide",
        NavigationPage.Settings => "Settings",
        NavigationPage.HelpCenter => "Help Center",
        _ => "DSX"
    };

    private static string GetPageIcon(NavigationPage page) => page switch
    {
        NavigationPage.Home => "\uE80F",
        NavigationPage.MyControllers => "\uE960",
        NavigationPage.ControllerMapping => "\uE943",
        NavigationPage.AdaptiveTriggers => "\uE963",
        NavigationPage.LEDLighting => "\uE768",
        NavigationPage.HapticsRumble => "\uE95F",
        NavigationPage.VirtualDevice => "\uE912",
        NavigationPage.InstalledGames => "\uE990",
        NavigationPage.Mods => "\uE902",
        NavigationPage.HidHide => "\uE72E",
        NavigationPage.Settings => "\uE713",
        NavigationPage.HelpCenter => "\uE897",
        _ => "\uE8FD"
    };

    private void Cleanup()
    {
        _statusBarTimer?.Stop();
        _trayIcon?.Dispose();

        if (ViewModel != null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }
}
