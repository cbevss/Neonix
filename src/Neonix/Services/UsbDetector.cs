using System.Collections.Generic;
using System.Linq;
using HidSharp;

namespace Neonix.Services
{
    public record UsbDevice(string Vendor, string Product, int VendorId, int ProductId);

    /// <summary>
    /// Native USB scan (no OpenRGB needed). Finds HID devices from RGB / cooling vendors so
    /// Neonix can show what's plugged in – AIOs, lighting controllers, peripherals.
    /// </summary>
    public static class UsbDetector
    {
        static readonly Dictionary<int, string> KnownVendors = new()
        {
            [0x1B1C] = "Corsair",
            [0x1E71] = "NZXT",
            [0x0B05] = "ASUS",
            [0x1462] = "MSI",
            [0x048D] = "ITE (Gigabyte RGB Fusion)",
            [0x1044] = "Gigabyte",
            [0x0CF2] = "Lian Li",
            [0x2516] = "Cooler Master",
            [0x1532] = "Razer",
            [0x046D] = "Logitech",
            [0x1038] = "SteelSeries",
            [0x264A] = "Thermaltake",
            [0x0951] = "HyperX",
            [0x03F0] = "HyperX (HP)",
            [0x1A86] = "QinHeng (common in ARGB controllers)",
            [0x16D0] = "MCS (common in ARGB controllers)",
            [0x0416] = "Winbond (common in AIO pumps)",
        };

        public static List<UsbDevice> Scan()
        {
            var result = new List<UsbDevice>();
            try
            {
                foreach (var d in DeviceList.Local.GetHidDevices())
                {
                    if (!KnownVendors.TryGetValue(d.VendorID, out var vendor)) continue;
                    string product;
                    try { product = d.GetProductName(); } catch { product = "Unknown device"; }
                    result.Add(new UsbDevice(vendor, product, d.VendorID, d.ProductID));
                }
            }
            catch { /* HID enumeration failed – return what we have */ }

            // One HID device often exposes several interfaces; show each product once.
            return result
                .GroupBy(x => (x.VendorId, x.ProductId))
                .Select(g => g.First())
                .OrderBy(x => x.Vendor)
                .ToList();
        }
    }
}
