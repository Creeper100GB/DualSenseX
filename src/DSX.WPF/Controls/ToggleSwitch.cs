using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DSX.WPF.Controls;

public class ToggleSwitch : Control
{
    static ToggleSwitch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(typeof(ToggleSwitch)));
    }

    public static readonly DependencyProperty IsCheckedProperty =
        DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsCheckedChanged));

    public static readonly DependencyProperty CheckedBackgroundProperty =
        DependencyProperty.Register(nameof(CheckedBackground), typeof(Brush), typeof(ToggleSwitch),
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4AA"))));

    public static readonly DependencyProperty UncheckedBackgroundProperty =
        DependencyProperty.Register(nameof(UncheckedBackground), typeof(Brush), typeof(ToggleSwitch),
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"))));

    public static readonly DependencyProperty ThumbBrushProperty =
        DependencyProperty.Register(nameof(ThumbBrush), typeof(Brush), typeof(ToggleSwitch),
            new PropertyMetadata(Brushes.White));

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public Brush CheckedBackground
    {
        get => (Brush)GetValue(CheckedBackgroundProperty);
        set => SetValue(CheckedBackgroundProperty, value);
    }

    public Brush UncheckedBackground
    {
        get => (Brush)GetValue(UncheckedBackgroundProperty);
        set => SetValue(UncheckedBackgroundProperty, value);
    }

    public Brush ThumbBrush
    {
        get => (Brush)GetValue(ThumbBrushProperty);
        set => SetValue(ThumbBrushProperty, value);
    }

    public event RoutedEventHandler? Toggled;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("PART_Root") is FrameworkElement root)
            root.MouseLeftButtonUp += OnRootClick;

        UpdateVisual(false);
    }

    private void OnRootClick(object sender, RoutedEventArgs e)
    {
        IsChecked = !IsChecked;
    }

    private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ts = (ToggleSwitch)d;
        ts.UpdateVisual(true);
        ts.Toggled?.Invoke(ts, new RoutedEventArgs());
    }

    private void UpdateVisual(bool animate)
    {
        if (GetTemplateChild("PART_Thumb") is not Border thumb) return;
        if (GetTemplateChild("PART_Track") is not Border track) return;

        var trackBrush = IsChecked
            ? (CheckedBackground as SolidColorBrush)?.Color ?? Colors.Transparent
            : (UncheckedBackground as SolidColorBrush)?.Color ?? Colors.Transparent;

        if (!animate)
        {
            track.Background = new SolidColorBrush(trackBrush);
            thumb.HorizontalAlignment = IsChecked ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            return;
        }

        var duration = new Duration(TimeSpan.FromMilliseconds(200));
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseInOut };

        var currentBg = (track.Background as SolidColorBrush)?.Color ?? Colors.Transparent;
        var bgAnim = new ColorAnimation(currentBg, trackBrush, duration) { EasingFunction = ease };
        var bgBrush = new SolidColorBrush(currentBg);
        track.Background = bgBrush;
        bgBrush.BeginAnimation(SolidColorBrush.ColorProperty, bgAnim);

        var targetX = IsChecked ? track.ActualWidth - thumb.ActualWidth - 4 : 4;
        if (targetX < 0) targetX = IsChecked ? 20 : 0;

        var tt = thumb.RenderTransform as TranslateTransform ?? new TranslateTransform();
        thumb.RenderTransform = tt;

        var currentX = tt.X;
        tt.BeginAnimation(TranslateTransform.XProperty, null);
        var moveAnim = new DoubleAnimation(currentX, targetX, duration) { EasingFunction = ease };
        tt.BeginAnimation(TranslateTransform.XProperty, moveAnim);
    }
}
