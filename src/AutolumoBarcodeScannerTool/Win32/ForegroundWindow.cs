using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AutolumoBarcodeScannerTool.Core;

namespace AutolumoBarcodeScannerTool.Win32;

internal static class ForegroundWindow
{
    public static ForegroundWindowInfo? GetCurrent()
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
        catch
        {
            return null;
        }

        var titleLen = GetWindowTextLength(hwnd);
        var title = "";
        if (titleLen > 0)
        {
            var sb = new StringBuilder(titleLen + 1);
            _ = GetWindowText(hwnd, sb, sb.Capacity);
            title = sb.ToString();
        }

        return new ForegroundWindowInfo(processName, title);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);
}
