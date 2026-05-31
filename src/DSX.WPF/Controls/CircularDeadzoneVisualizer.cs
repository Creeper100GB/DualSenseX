using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DSX.WPF.Controls;

public class CircularDeadzoneVisualizer : Control
{
    private double _stickX;
    private double _stickY;

    static CircularDeadzoneVisualizer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CircularDeadzoneVisualizer),
            new FrameworkPropertyMetadata(typeof(CircularDeadzoneVisualizer)));
    }

    public static readonly DependencyProperty InnerDeadzoneProperty =
        DependencyProperty.Register(nameof(InnerDeadzone), typeof(double), typeof(CircularDeadzoneVisualizer),
            new PropertyMetadata(0.1, OnVisualPropertyChanged));

    public static readonly DependencyProperty OuterDeadzoneProperty =
        DependencyProperty.Register(nameof(OuterDeadzone), typeof(double), typeof(CircularDeadzoneVisualizer),
            new PropertyMetadata(0.95, OnVisualPropertyChanged));

    public static readonly DependencyProperty StickXProperty =
        DependencyProperty.Register(nameof(StickX), typeof(double), typeof(CircularDeadzoneVisualizer),
            new PropertyMetadata(0.0, OnPositionChanged));

    public static readonly DependencyProperty StickYProperty =
        DependencyProperty.Register(nameof(StickY), typeof(double), typeof(CircularDeadzoneVisualizer),
            new PropertyMetadata(0.0, OnPositionChanged));

    public static readonly DependencyProperty DeadzoneColorProperty =
        DependencyProperty.Register(nameof(DeadzoneColor), typeof(Color), typeof(CircularDeadzoneVisualizer),
            new PropertyMetadata(Colors.Red));

    public static readonly DependencyProperty RangeColorProperty =
        DependencyProperty.Register(nameof(RangeColor), typeof(Color), typeof(CircularDeadzoneVisualizer),
            new PropertyMetadata(Color.FromRgb(0x1A, 0x1A, 0x1A)));

    public static readonly DependencyProperty StickDotColorProperty =
        DependencyProperty.Register(nameof(StickDotColor), typeof(Color), typeof(CircularDeadzoneVisualizer),
            new PropertyMetadata(Color.FromRgb(0x00, 0xD4, 0xAA)));

    public static readonly DependencyProperty OuterRingColorProperty =
        DependencyProperty.Register(nameof(OuterRingColor), typeof(Color), typeof(CircularDeadzoneVisualizer),
            new PropertyMetadata(Color.FromRgb(0x33, 0x33, 0x33)));

    public double InnerDeadzone
    {
        get => (double)GetValue(InnerDeadzoneProperty);
        set => SetValue(InnerDeadzoneProperty, value);
    }

    public double OuterDeadzone
    {
        get => (double)GetValue(OuterDeadzoneProperty);
        set => SetValue(OuterDeadzoneProperty, value);
    }

    public double StickX
    {
        get => (double)GetValue(StickXProperty);
        set => SetValue(StickXProperty, value);
    }

    public double StickY
    {
        get => (double)GetValue(StickYProperty);
        set => SetValue(StickYProperty, value);
    }

    public Color DeadzoneColor
    {
        get => (Color)GetValue(DeadzoneColorProperty);
        set => SetValue(DeadzoneColorProperty, value);
    }

    public Color RangeColor
    {
        get => (Color)GetValue(RangeColorProperty);
        set => SetValue(RangeColorProperty, value);
    }

    public Color StickDotColor
    {
        get => (Color)GetValue(StickDotColorProperty);
        set => SetValue(StickDotColorProperty, value);
    }

    public Color OuterRingColor
    {
        get => (Color)GetValue(OuterRingColorProperty);
        set => SetValue(OuterRingColorProperty, value);
    }

    public void UpdateStickPosition(double x, double y)
    {
        _stickX = Math.Clamp(x, -1.0, 1.0);
        _stickY = Math.Clamp(y, -1.0, 1.0);
        InvalidateVisual();
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((CircularDeadzoneVisualizer)d).InvalidateVisual();
    }

    private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (CircularDeadzoneVisualizer)d;
        c._stickX = c.StickX;
        c._stickY = c.StickY;
        c.InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        var center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
        var radius = size / 2.0 - 2;

        dc.DrawEllipse(new SolidColorBrush(RangeColor), null, center, radius, radius);

        var outerR = radius * Math.Clamp(OuterDeadzone, 0, 1);
        var outerPen = new Pen(new SolidColorBrush(OuterRingColor), 1.5) { DashStyle = DashStyles.Dash };
        dc.DrawEllipse(null, outerPen, center, outerR, outerR);

        var innerR = radius * Math.Clamp(InnerDeadzone, 0, 1);
        var innerBrush = new SolidColorBrush(Color.FromArgb(40, DeadzoneColor.R, DeadzoneColor.G, DeadzoneColor.B));
        var innerPen = new Pen(new SolidColorBrush(DeadzoneColor), 1);
        dc.DrawEllipse(innerBrush, innerPen, center, innerR, innerR);

        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), 0.5),
            new Point(center.X - radius, center.Y), new Point(center.X + radius, center.Y));
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), 0.5),
            new Point(center.X, center.Y - radius), new Point(center.X, center.Y + radius));

        var stickRadius = Math.Max(4, radius * 0.04);
        var dotX = center.X + _stickX * radius;
        var dotY = center.Y - _stickY * radius;
        var dotBrush = new SolidColorBrush(StickDotColor);

        dc.DrawEllipse(null,
            new Pen(new SolidColorBrush(Color.FromArgb(60, StickDotColor.R, StickDotColor.G, StickDotColor.B)), 1),
            new Point(dotX, dotY), stickRadius * 3, stickRadius * 3);

        dc.DrawEllipse(dotBrush, null, new Point(dotX, dotY), stickRadius, stickRadius);

        var outerLabel = new FormattedText($"{OuterDeadzone:P0}",
            CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 9, new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(outerLabel, new Point(center.X + outerR + 4, center.Y - outerLabel.Height / 2));

        var innerLabel = new FormattedText($"{InnerDeadzone:P0}",
            CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 9, new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(innerLabel, new Point(center.X + innerR + 4, center.Y + 6));
    }
}
