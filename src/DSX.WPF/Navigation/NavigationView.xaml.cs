using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DSX.Core.Enums;

namespace DSX.WPF.Navigation;

public partial class NavigationView : UserControl
{
    private readonly Dictionary<RadioButton, NavigationPage> _navMap = new();
    private readonly Dictionary<NavigationPage, RadioButton> _pageMap = new();

    public static readonly DependencyProperty CurrentPageProperty =
        DependencyProperty.Register(nameof(CurrentPage), typeof(NavigationPage), typeof(NavigationView),
            new PropertyMetadata(NavigationPage.Home, OnCurrentPageChanged));

    public NavigationPage CurrentPage
    {
        get => (NavigationPage)GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public static readonly RoutedEvent NavigationChangedEvent =
        EventManager.RegisterRoutedEvent(nameof(NavigationChanged), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(NavigationView));

    public event RoutedEventHandler NavigationChanged
    {
        add => AddHandler(NavigationChangedEvent, value);
        remove => RemoveHandler(NavigationChangedEvent, value);
    }

    public static readonly DependencyProperty IsControllerConnectedProperty =
        DependencyProperty.Register(nameof(IsControllerConnected), typeof(bool), typeof(NavigationView),
            new PropertyMetadata(false, OnConnectionStateChanged));

    public bool IsControllerConnected
    {
        get => (bool)GetValue(IsControllerConnectedProperty);
        set => SetValue(IsControllerConnectedProperty, value);
    }

    public static readonly DependencyProperty BatteryPercentageProperty =
        DependencyProperty.Register(nameof(BatteryPercentage), typeof(double), typeof(NavigationView),
            new PropertyMetadata(-1.0, OnBatteryChanged));

    public double BatteryPercentage
    {
        get => (double)GetValue(BatteryPercentageProperty);
        set => SetValue(BatteryPercentageProperty, value);
    }

    public static readonly DependencyProperty AppVersionProperty =
        DependencyProperty.Register(nameof(AppVersion), typeof(string), typeof(NavigationView),
            new PropertyMetadata("3.1.5.3"));

    public string AppVersion
    {
        get => (string)GetValue(AppVersionProperty);
        set => SetValue(AppVersionProperty, value);
    }

    public NavigationView()
    {
        InitializeComponent();
        BuildNavigationMap();
        NavHome.IsChecked = true;
    }

    private void BuildNavigationMap()
    {
        _navMap[NavHome] = NavigationPage.Home;
        _navMap[NavMyControllers] = NavigationPage.MyControllers;
        _navMap[NavAdaptiveTriggers] = NavigationPage.AdaptiveTriggers;
        _navMap[NavLEDLighting] = NavigationPage.LEDLighting;
        _navMap[NavHapticsRumble] = NavigationPage.HapticsRumble;
        _navMap[NavVirtualDevice] = NavigationPage.VirtualDevice;
        _navMap[NavInstalledGames] = NavigationPage.InstalledGames;
        _navMap[NavMods] = NavigationPage.Mods;
        _navMap[NavHidHide] = NavigationPage.HidHide;
        _navMap[NavSettings] = NavigationPage.Settings;
        _navMap[NavHelpCenter] = NavigationPage.HelpCenter;

        foreach (var kvp in _navMap)
            _pageMap[kvp.Value] = kvp.Key;
    }

    private void OnNavMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        while (dep != null)
        {
            if (dep is RadioButton rb && _navMap.ContainsKey(rb))
            {
                if (rb.IsChecked != true)
                    rb.IsChecked = true;
                CurrentPage = _navMap[rb];
                RaiseEvent(new RoutedEventArgs(NavigationChangedEvent, CurrentPage));
                break;
            }
            dep = VisualTreeHelper.GetParent(dep);
        }
    }

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && _navMap.TryGetValue(rb, out var page))
        {
            CurrentPage = page;
            RaiseEvent(new RoutedEventArgs(NavigationChangedEvent, page));
        }
    }

    private static void OnCurrentPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (NavigationView)d;
        if (view._pageMap.TryGetValue((NavigationPage)e.NewValue, out var rb))
        {
            rb.IsChecked = true;
        }
    }

    private static void OnConnectionStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (NavigationView)d;
        var connected = (bool)e.NewValue;

        view.ConnectionIndicator.Fill = connected
            ? (SolidColorBrush)view.FindResource("SuccessBrush")
            : (SolidColorBrush)view.FindResource("ErrorBrush");

        view.ConnectionStatusText.Text = connected ? "Controller Connected" : "No Controller";
        view.ConnectionStatusText.Foreground = connected
            ? (SolidColorBrush)view.FindResource("TextSecondary")
            : (SolidColorBrush)view.FindResource("TextMuted");
    }

    private static void OnBatteryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (NavigationView)d;
        var pct = (double)e.NewValue;

        if (pct < 0)
        {
            view.BatteryText.Text = "";
            return;
        }

        view.BatteryText.Text = $"{pct:0}%";

        view.BatteryText.Foreground = pct switch
        {
            >= 60 => (SolidColorBrush)view.FindResource("SuccessBrush"),
            >= 30 => (SolidColorBrush)view.FindResource("WarningBrush"),
            _ => (SolidColorBrush)view.FindResource("ErrorBrush")
        };
    }

    public static List<NavigationItem> GetDefaultNavigationItems()
    {
        return new List<NavigationItem>
        {
            new("\uE80F", "Home", NavigationPage.Home),
            new("\uE960", "My Controllers", NavigationPage.MyControllers),
            new("\uE963", "Adaptive Triggers", NavigationPage.AdaptiveTriggers),
            new("\uE768", "LED / Lighting", NavigationPage.LEDLighting),
            new("\uE95F", "Haptics / Rumble", NavigationPage.HapticsRumble),
            new("\uE912", "Virtual Device", NavigationPage.VirtualDevice),
            new("\uE990", "Installed Games", NavigationPage.InstalledGames),
            new("\uE902", "Mods", NavigationPage.Mods),
            new("\uE72E", "HidHide", NavigationPage.HidHide),
            new("\uE713", "Settings", NavigationPage.Settings),
            new("\uE897", "Help Center", NavigationPage.HelpCenter)
        };
    }
}
