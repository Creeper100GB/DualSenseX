using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DSX.Core.Enums;

namespace DSX.WPF.Helpers;

public static class WindowEffectHelper
{
    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWA_MICA_EFFECT = 1029;

    private enum WindowCompositionAction
    {
        ACCENT_STATE = 4,
    }

    private enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
        ACCENT_ENABLE_HOSTBACKDROP = 5,
        ACCENT_INVALID_STATE = 6,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttribData
    {
        public WindowCompositionAction Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    public static void ApplyEffect(Window window, WindowEffect effect)
    {
        if (window == null) return;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            window.SourceInitialized += (s, e) =>
            {
                var h = new WindowInteropHelper(window).Handle;
                ApplyEffectInternal(h, effect);
            };
            return;
        }

        ApplyEffectInternal(hwnd, effect);
    }

    private static void ApplyEffectInternal(IntPtr hwnd, WindowEffect effect)
    {
        SetDarkModeHandle(hwnd);

        if (Environment.OSVersion.Version >= new Version(10, 0, 22000, 0))
        {
            var backdropType = effect switch
            {
                WindowEffect.Mica => 2,
                WindowEffect.Acrylic => 3,
                WindowEffect.Tabbed => 4,
                _ => 0
            };
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
        }
        else if (Environment.OSVersion.Version >= new Version(10, 0, 17763, 0))
        {
            if (effect == WindowEffect.Mica)
            {
                var micaValue = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_MICA_EFFECT, ref micaValue, sizeof(int));
            }
            else if (effect == WindowEffect.Acrylic)
            {
                var accent = new AccentPolicy
                {
                    AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                    AccentFlags = 2,
                    GradientColor = 0x01000000
                };
                SetWindowComposition(hwnd, accent);
            }
        }
    }

    public static void SetDarkMode(Window window)
    {
        if (window == null) return;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            window.SourceInitialized += (s, e) =>
            {
                SetDarkModeHandle(new WindowInteropHelper(window).Handle);
            };
            return;
        }

        SetDarkModeHandle(hwnd);
    }

    private static void SetDarkModeHandle(IntPtr hwnd)
    {
        var value = 1;
        try
        {
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }
        catch
        {
            try
            {
                DwmSetWindowAttribute(hwnd, 19, ref value, sizeof(int));
            }
            catch { }
        }
    }

    private static void SetWindowComposition(IntPtr hwnd, AccentPolicy accent)
    {
        var data = new WindowCompositionAttribData
        {
            Attribute = WindowCompositionAction.ACCENT_STATE,
            SizeOfData = Marshal.SizeOf(typeof(AccentPolicy))
        };
        var accentPtr = Marshal.AllocHGlobal(data.SizeOfData);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            data.Data = accentPtr;
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }
}
