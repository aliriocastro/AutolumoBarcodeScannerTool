using System.Management;
using System.Text.RegularExpressions;

namespace AutolumoBarcodeScannerTool.Hid;

internal sealed record HidDeviceDescriptor(
    string Description,
    string DeviceId,
    string VendorId,
    string ProductId);

internal static class HidDeviceEnumerator
{
    private static readonly Regex VidPidRegex =
        new(@"VID_(?<vid>[0-9A-F]{4})&PID_(?<pid>[0-9A-F]{4})", RegexOptions.IgnoreCase);

    public static IReadOnlyList<HidDeviceDescriptor> Enumerate()
    {
        var list = new List<HidDeviceDescriptor>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT Description, DeviceID FROM Win32_PnPEntity WHERE DeviceID LIKE 'HID%'");

        foreach (var obj in searcher.Get().Cast<ManagementObject>())
        {
            var description = obj["Description"]?.ToString() ?? "";
            var deviceId = obj["DeviceID"]?.ToString() ?? "";
            var match = VidPidRegex.Match(deviceId);
            if (!match.Success) continue;
            var vid = "0x" + match.Groups["vid"].Value.ToUpperInvariant();
            var pid = "0x" + match.Groups["pid"].Value.ToUpperInvariant();
            list.Add(new HidDeviceDescriptor(description, deviceId, vid, pid));
        }
        return list;
    }
}
