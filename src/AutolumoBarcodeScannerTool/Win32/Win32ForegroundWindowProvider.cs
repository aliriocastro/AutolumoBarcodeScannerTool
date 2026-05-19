using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AutolumoBarcodeScannerTool.Core.Sinks;

namespace AutolumoBarcodeScannerTool.Win32;

internal sealed class Win32ForegroundWindowProvider : IForegroundWindowProvider
{
    public ForegroundWindowInfo? GetCurrent()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        _ = GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0) return null;

        string processName;
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            processName = proc.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }

        var title = new StringBuilder(512);
        _ = GetWindowText(hwnd, title, title.Capacity);
        return new ForegroundWindowInfo(processName, title.ToString());
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
}
