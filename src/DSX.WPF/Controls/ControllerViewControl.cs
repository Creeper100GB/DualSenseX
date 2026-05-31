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
    private Dictionary<string, ControllerButton> _tagToButton = new();
    private Border? _hoveredBorder;

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

    public HashSet<ControllerButton> ActiveButtons
    {
        get => (HashSet<ControllerButton>)GetValue(ActiveButtonsProperty);
        set => SetValue(ActiveButtonsProperty, value);
    }

    public event EventHandler<ControllerButtonEventArgs>? ButtonClicked;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        var canvas = GetTemplateChild("PART_ButtonCanvas") as Canvas;
        if (canvas == null) return;

        _tagToButton = new Dictionary<string, ControllerButton>(StringComparer.OrdinalIgnoreCase)
        {
            ["DPadUp"] = ControllerButton.DPadUp,
            ["DPadDown"] = ControllerButton.DPadDown,
            ["DPadLeft"] = ControllerButton.DPadLeft,
            ["DPadRight"] = ControllerButton.DPadRight,
            ["Triangle"] = ControllerButton.Triangle,
            ["Circle"] = ControllerButton.Circle,
            ["Cross"] = ControllerButton.Cross,
            ["Square"] = ControllerButton.Square,
            ["L1"] = ControllerButton.L1,
            ["R1"] = ControllerButton.R1,
            ["L2"] = ControllerButton.L2,
            ["R2"] = ControllerButton.R2,
            ["Share"] = ControllerButton.Share,
            ["Options"] = ControllerButton.Options,
            ["Touchpad"] = ControllerButton.Touchpad,
            ["PSButton"] = ControllerButton.PSButton,
            ["Mute"] = ControllerButton.Mute,
            ["L3"] = ControllerButton.L3,
            ["R3"] = ControllerButton.R3
        };

        foreach (var child in canvas.Children)
        {
            if (child is Border border && border.Tag is string tag && _tagToButton.TryGetValue(tag, out var btn))
            {
                border.MouseEnter += OnButtonMouseEnter;
                border.MouseLeave += OnButtonMouseLeave;
                border.MouseLeftButtonUp += OnButtonClick;
                border.Background = Brushes.Transparent;
            }
        }

        UpdateHighlights();
    }

    private void OnButtonMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag && _tagToButton.TryGetValue(tag, out var btn))
        {
            if (SelectedButton != btn)
                border.Background = new SolidColorBrush(Color.FromArgb(30, HighlightColor.R, HighlightColor.G, HighlightColor.B));
            _hoveredBorder = border;
        }
    }

    private void OnButtonMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag && _tagToButton.TryGetValue(tag, out var btn))
        {
            if (SelectedButton != btn)
                border.Background = Brushes.Transparent;
            if (_hoveredBorder == border)
                _hoveredBorder = null;
        }
    }

    private void OnButtonClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag && _tagToButton.TryGetValue(tag, out var btn))
        {
            SelectedButton = btn;
            ButtonClicked?.Invoke(this, new ControllerButtonEventArgs(btn));
            e.Handled = true;
        }
    }

    private void UpdateHighlights()
    {
        var canvas = GetTemplateChild("PART_ButtonCanvas") as Canvas;
        if (canvas == null) return;

        var highlightBrush = new SolidColorBrush(Color.FromArgb(60, HighlightColor.R, HighlightColor.G, HighlightColor.B));
        var selectedBrush = new SolidColorBrush(Color.FromArgb(80, HighlightColor.R, HighlightColor.G, HighlightColor.B));
        var activeBrush = new SolidColorBrush(Color.FromArgb(100, HighlightColor.R, HighlightColor.G, HighlightColor.B));

        foreach (var child in canvas.Children)
        {
            if (child is Border border && border.Tag is string tag && _tagToButton.TryGetValue(tag, out var btn))
            {
                var isActive = ActiveButtons?.Contains(btn) == true;
                var isSelected = SelectedButton == btn;

                if (isActive)
                    border.Background = activeBrush;
                else if (isSelected)
                    border.Background = selectedBrush;
                else if (_hoveredBorder != border)
                    border.Background = Brushes.Transparent;

                border.BorderBrush = isSelected ? new SolidColorBrush(HighlightColor) : Brushes.Transparent;
                border.BorderThickness = isSelected ? new Thickness(2) : new Thickness(0);
            }
        }
    }

    private static void OnSelectedButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ControllerViewControl)d).UpdateHighlights();
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
