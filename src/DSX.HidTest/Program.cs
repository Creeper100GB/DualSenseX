using System;
using System.Linq;
using HidSharp;

var devices = DeviceList.Local.GetHidDevices()
    .Where(d => d.VendorID == 0x054C && d.ProductID == 0x0CE6)
    .ToList();

Console.WriteLine($"Found {devices.Count} DualSense HID device(s):\n");

foreach (var d in devices)
{
    Console.WriteLine($"Path: {d.DevicePath}");
    Console.WriteLine($"  MaxInputReportLength:  {d.GetMaxInputReportLength()}");
    Console.WriteLine($"  MaxOutputReportLength: {d.GetMaxOutputReportLength()}");
    Console.WriteLine($"  Has MI_03: {d.DevicePath.ToUpperInvariant().Contains("MI_03")}");
    Console.WriteLine($"  Has MI_04: {d.DevicePath.ToUpperInvariant().Contains("MI_04")}");
    Console.WriteLine($"  Has BTHENUM: {d.DevicePath.ToUpperInvariant().Contains("BTHENUM")}");
    Console.WriteLine($"  Has HID: {d.DevicePath.ToUpperInvariant().Contains("HID#")}");
    
    string connType = "Unknown";
    var upper = d.DevicePath.ToUpperInvariant();
    if (upper.Contains("BT_") || upper.Contains("BTH") || upper.Contains("BTHENUM") || upper.Contains("BLUETOOTH"))
        connType = "Bluetooth";
    else if (upper.Contains("WIRELESS"))
        connType = "USB Wireless Adapter";
    else
        connType = "USB";
    Console.WriteLine($"  Connection: {connType}");
    Console.WriteLine();
}
