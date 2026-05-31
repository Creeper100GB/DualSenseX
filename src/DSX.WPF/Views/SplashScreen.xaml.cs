using System;
using System.Windows;
using System.Windows.Threading;

namespace DSX.WPF.Views;

public partial class SplashScreen : Window
{
    private readonly DispatcherTimer _progressTimer;
    private readonly DispatcherTimer _dotsTimer;
    private double _progress;
    private int _dotCount;
    private int _step;

    private static readonly string[] LoadingSteps =
    [
        "Loading DSX...",
        "Initializing controller service...",
        "Loading profiles...",
        "Starting UDP server...",
        "Applying settings...",
        "Ready!"
    ];

    public SplashScreen()
    {
        InitializeComponent();

        _progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _progressTimer.Tick += OnProgressTick;

        _dotsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _dotsTimer.Tick += OnDotsTick;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _progressTimer.Start();
        _dotsTimer.Start();
    }

    private void OnProgressTick(object? sender, EventArgs e)
    {
        _progress += 1.8;

        if (_progress >= 100)
        {
            _progress = 100;
            _progressTimer.Stop();
            _dotsTimer.Stop();
            DotsText.Text = "";
            LoadingText.Text = "Ready!";
            return;
        }

        ProgressBar.Width = (_progress / 100.0) * (ProgressBar.Parent as FrameworkElement)!.ActualWidth;

        var stepIndex = (int)(_progress / (100.0 / LoadingSteps.Length));
        if (stepIndex >= LoadingSteps.Length)
            stepIndex = LoadingSteps.Length - 1;

        if (stepIndex != _step)
        {
            _step = stepIndex;
            LoadingText.Text = LoadingSteps[_step];
        }
    }

    private void OnDotsTick(object? sender, EventArgs e)
    {
        _dotCount = (_dotCount + 1) % 4;
        DotsText.Text = new string('.', _dotCount);
    }
}
