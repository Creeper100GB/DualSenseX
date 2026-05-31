using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DSX.Core.Models;

namespace DSX.WPF.Controls;

public class ControllerViewControl : Control
{
    private readonly Dictionary<ControllerButton, Rect> _buttonRegions = new();
    private ControllerButton? _hoveredButton;

    static ControllerViewControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ControllerViewControl),
            new FrameworkPropertyMetadata(typeof(ControllerViewControl)));
    }

    public static readonly DependencyProperty SelectedButtonProperty =
        DependencyProperty.Register(nameof(SelectedButton), typeof(ControllerButton?), typeof(ControllerViewControl),
            new PropertyMetadata(null, OnSelectedButtonChanged));

    public static readonly DependencyProperty HighlightColorProperty =
        DependencyProperty.Register(nameof(HighlightColor), typeof(Color), typeof(ControllerViewControl),
            new PropertyMetadata(Color.FromRgb(0x00, 0xD4, 0xAA)));

    public static readonly DependencyProperty ControllerBodyColorProperty =
        DependencyProperty.Register(nameof(ControllerBodyColor), typeof(Color), typeof(ControllerViewControl),
            new PropertyMetadata(Color.FromRgb(0x22, 0x22, 0x22)));

    public static readonly DependencyProperty ActiveButtonsProperty =
        DependencyProperty.Register(nameof(ActiveButtons), typeof(HashSet<ControllerButton>), typeof(ControllerViewControl),
            new PropertyMetadata(new HashSet<ControllerButton>()));

    public ControllerButton? SelectedButton
    {
        get => (ControllerButton?)GetValue(SelectedButtonProperty);
        set => SetValue(SelectedButtonProperty, value);
    }

    public Color HighlightColor
    {
        get => (Color)GetValue(HighlightColorProperty);
        set => SetValue(HighlightColorProperty, value);
    }

    public Color ControllerBodyColor
    {
        get => (Color)GetValue(ControllerBodyColorProperty);
        set => SetValue(ControllerBodyColorProperty, value);
    }

    public HashSet<ControllerButton> ActiveButtons
    {
        get => (HashSet<ControllerButton>)GetValue(ActiveButtonsProperty);
        set => SetValue(ActiveButtonsProperty, value);
    }

    public event EventHandler<ControllerButtonEventArgs>? ButtonClicked;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        _buttonRegions.Clear();

        var bodyBrush = new SolidColorBrush(ControllerBodyColor);
        var bodyPen = new Pen(new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)), 1.5);
        var highlightBrush = new SolidColorBrush(Color.FromArgb(60, HighlightColor.R, HighlightColor.G, HighlightColor.B));
        var highlightPen = new Pen(new SolidColorBrush(HighlightColor), 2);
        var textBrush = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
        var activeBrush = new SolidColorBrush(Color.FromArgb(100, HighlightColor.R, HighlightColor.G, HighlightColor.B));
        var hoverBrush = new SolidColorBrush(Color.FromArgb(30, HighlightColor.R, HighlightColor.G, HighlightColor.B));

        var cx = w / 2;
        var cy = h / 2;

        var bodyW = w * 0.85;
        var bodyH = h * 0.75;
        var bodyRect = new Rect(cx - bodyW / 2, cy - bodyH / 2, bodyW, bodyH);

        var bodyPath = new PathGeometry();
        var halfW = bodyW / 2;
        var halfH = bodyH / 2;
        var gripOffset = bodyH * 0.2;

        var leftGrip = new Rect(bodyRect.Left - 10, bodyRect.Bottom - gripOffset * 2, gripOffset * 1.5, gripOffset * 2.5);
        var rightGrip = new Rect(bodyRect.Right - gripOffset * 1.5 + 10, bodyRect.Bottom - gripOffset * 2, gripOffset * 1.5, gripOffset * 2.5);

        dc.DrawRoundedRectangle(bodyBrush, bodyPen, bodyRect, 30, 30);

        dc.DrawRoundedRectangle(bodyBrush, bodyPen, leftGrip, 15, 15);
        dc.DrawRoundedRectangle(bodyBrush, bodyPen, rightGrip, 15, 15);

        DrawButtonRegion(dc, ControllerButton.DPadUp, cx - bodyW * 0.25, cy - bodyH * 0.15, 20, 12,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);
        DrawButtonRegion(dc, ControllerButton.DPadDown, cx - bodyW * 0.25, cy + bodyH * 0.02, 20, 12,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);
        DrawButtonRegion(dc, ControllerButton.DPadLeft, cx - bodyW * 0.25 - 14, cy - bodyH * 0.06, 12, 20,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);
        DrawButtonRegion(dc, ControllerButton.DPadRight, cx - bodyW * 0.25 + 14, cy - bodyH * 0.06, 12, 20,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);

        DrawButtonRegion(dc, ControllerButton.Triangle, cx + bodyW * 0.22, cy - bodyH * 0.16, 18, 18,
            new SolidColorBrush(Color.FromRgb(0x00, 0xB4, 0xD8)), highlightBrush, activeBrush, hoverBrush, highlightPen);
        DrawButtonRegion(dc, ControllerButton.Circle, cx + bodyW * 0.32, cy - bodyH * 0.06, 18, 18,
            new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x6E)), highlightBrush, activeBrush, hoverBrush, highlightPen);
        DrawButtonRegion(dc, ControllerButton.Cross, cx + bodyW * 0.22, cy + bodyH * 0.04, 18, 18,
            new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xAA)), highlightBrush, activeBrush, hoverBrush, highlightPen);
        DrawButtonRegion(dc, ControllerButton.Square, cx + bodyW * 0.12, cy - bodyH * 0.06, 18, 18,
            new SolidColorBrush(Color.FromRgb(0xB5, 0x37, 0xF2)), highlightBrush, activeBrush, hoverBrush, highlightPen);

        DrawButtonRegion(dc, ControllerButton.L1, bodyRect.Left + 40, bodyRect.Top - 4, 50, 12,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);
        DrawButtonRegion(dc, ControllerButton.R1, bodyRect.Right - 90, bodyRect.Top - 4, 50, 12,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);

        DrawButtonRegion(dc, ControllerButton.L2, bodyRect.Left + 40, bodyRect.Top - 18, 50, 12,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);
        DrawButtonRegion(dc, ControllerButton.R2, bodyRect.Right - 90, bodyRect.Top - 18, 50, 12,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);

        DrawButtonRegion(dc, ControllerButton.Share, cx - 35, cy - bodyH * 0.12, 22, 14,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);
        DrawButtonRegion(dc, ControllerButton.Touchpad, cx - 30, cy - bodyH * 0.02, 60, 25,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);
        DrawButtonRegion(dc, ControllerButton.Options, cx + 15, cy - bodyH * 0.12, 22, 14,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);
        DrawButtonRegion(dc, ControllerButton.PSButton, cx, cy + bodyH * 0.15, 16, 16,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);
        DrawButtonRegion(dc, ControllerButton.Mute, cx, cy + bodyH * 0.02, 12, 12,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);

        DrawButtonRegion(dc, ControllerButton.L3, cx - bodyW * 0.12, cy + bodyH * 0.02, 24, 24,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);
        DrawButtonRegion(dc, ControllerButton.R3, cx + bodyW * 0.12, cy + bodyH * 0.12, 24, 24,
            textBrush, highlightBrush, activeBrush, hoverBrush, highlightPen);
    }

    private void DrawButtonRegion(DrawingContext dc, ControllerButton button,
        double x, double y, double bw, double bh,
        Brush labelBrush, Brush highlightBrush, Brush activeBrush, Brush hoverBrush, Pen highlightPen)
    {
        var rect = new Rect(x, y, bw, bh);
        _buttonRegions[button] = rect;

        var isActive = ActiveButtons.Contains(button);
        var isSelected = SelectedButton == button;
        var isHovered = _hoveredButton == button;

        dc.DrawRoundedRectangle(
            isActive ? activeBrush : (isHovered ? hoverBrush : Brushes.Transparent),
            isSelected ? highlightPen : new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 0.5),
            rect, 4, 4);

        if (isSelected)
            dc.DrawRoundedRectangle(null, highlightPen, rect, 4, 4);

        var label = GetButtonLabel(button);
        var formatted = new FormattedText(label,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 8, labelBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        { TextAlignment = TextAlignment.Center };

        dc.DrawText(formatted, new Point(x + bw / 2 - formatted.Width / 2, y + bh / 2 - formatted.Height / 2));
    }

    private static string GetButtonLabel(ControllerButton btn) => btn switch
    {
        ControllerButton.Cross => "X",
        ControllerButton.Circle => "O",
        ControllerButton.Square => "[ ]",
        ControllerButton.Triangle => "T",
        ControllerButton.L1 or ControllerButton.R1 or ControllerButton.L2 or ControllerButton.R2
            => btn.ToString(),
        ControllerButton.L3 => "L3",
        ControllerButton.R3 => "R3",
        ControllerButton.Share => "S",
        ControllerButton.Options => "O",
        ControllerButton.PSButton => "PS",
        ControllerButton.Touchpad => "TP",
        ControllerButton.Mute => "M",
        ControllerButton.DPadUp => "^",
        ControllerButton.DPadDown => "v",
        ControllerButton.DPadLeft => "<",
        ControllerButton.DPadRight => ">",
        _ => "?"
    };

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = e.GetPosition(this);
        var newHovered = HitTestButton(pos);
        if (newHovered != _hoveredButton)
        {
            _hoveredButton = newHovered;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredButton = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        var pos = e.GetPosition(this);
        var hit = HitTestButton(pos);
        if (hit.HasValue)
        {
            SelectedButton = hit.Value;
            ButtonClicked?.Invoke(this, new ControllerButtonEventArgs(hit.Value));
        }
    }

    private ControllerButton? HitTestButton(Point pos)
    {
        foreach (var kvp in _buttonRegions)
        {
            if (kvp.Value.Contains(pos))
                return kvp.Key;
        }
        return null;
    }

    private static void OnSelectedButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ControllerViewControl)d).InvalidateVisual();
    }
}

public class ControllerButtonEventArgs : EventArgs
{
    public ControllerButton Button { get; }

    public ControllerButtonEventArgs(ControllerButton button)
    {
        Button = button;
    }
}
