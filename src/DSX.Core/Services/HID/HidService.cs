using System.Collections.Generic;
using DSX.Core.Constants;
using DSX.Core.Enums;
using DSX.Core.Services.Controller;
using HidSharp;

namespace DSX.Core.Services.HID;

public static class HidService
{
    public static IReadOnlyList<HidDevice> EnumerateDevices()
    {
        return DeviceList.Local.GetHidDevices().ToList().AsReadOnly();
    }

    public static IReadOnlyList<HidDevice> EnumerateDevices(ushort vendorId, ushort productId)
    {
        return DeviceList.Local.GetHidDevices()
            .Where(d => d.VendorID == vendorId && d.ProductID == productId)
            .ToList().AsReadOnly();
    }

    public static IReadOnlyList<HidDevice> EnumerateControllers()
    {
        return DeviceList.Local.GetHidDevices()
            .Where(d => ControllerService.GetControllerType((ushort)d.VendorID, (ushort)d.ProductID) != ControllerType.Unknown)
            .ToList().AsReadOnly();
    }

    public static HidDevice? FindDevice(string devicePath)
    {
        return DeviceList.Local.GetHidDevices()
            .FirstOrDefault(d => d.DevicePath == devicePath);
    }

    public static bool TryOpenRead(HidDevice device, out HidStream? stream)
    {
        return device.TryOpen(out stream);
    }

    public static byte[] ReadReport(HidStream stream, int timeoutMs = 1000)
    {
        var temp = stream.ReadTimeout;
        stream.ReadTimeout = timeoutMs;
        try
        {
            return stream.Read();
        }
        finally
        {
            stream.ReadTimeout = temp;
        }
    }

    public static void WriteReport(HidStream stream, byte[] data)
    {
        stream.Write(data);
    }

    public static void WriteFeatureReport(HidStream stream, byte[] data)
    {
        stream.SetFeature(data);
    }

    public static byte[] ReadFeatureReport(HidStream stream, byte[] buffer)
    {
        stream.GetFeature(buffer);
        return buffer;
    }

    public static string GetManufacturer(HidDevice device)
    {
        return device.GetManufacturer() ?? "Unknown";
    }

    public static string GetProductName(HidDevice device)
    {
        return device.GetProductName() ?? "Unknown Device";
    }

    public static string GetSerialNumber(HidDevice device)
    {
        return device.GetSerialNumber() ?? string.Empty;
    }

    public static string GetDeviceDescription(HidDevice device)
    {
        return $"{GetManufacturer(device)} {GetProductName(device)} " +
               $"(VID:{device.VendorID:X4} PID:{device.ProductID:X4})";
    }

    public static int GetMaxInputReportLength(HidDevice device) => device.GetMaxInputReportLength();
    public static int GetMaxOutputReportLength(HidDevice device) => device.GetMaxOutputReportLength();
    public static int GetMaxFeatureReportLength(HidDevice device) => device.GetMaxFeatureReportLength();
}
