using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Text;
using System.Runtime.InteropServices.ComTypes;

namespace AppSwitcher;

public static class ShellHelpers
{
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int dwProcessId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private const int SW_SHOW = 5; private const int SW_RESTORE = 9;
    private const uint SWP_NOSIZE = 0x0001; private const uint SWP_NOMOVE = 0x0002; private const uint SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_TOPMOST = new(-1); private static readonly IntPtr HWND_NOTOPMOST = new(-2);

    public static void ApplyTopmost(Window wpfWindow, bool topmost)
    {
        try
        {
            var h = new WindowInteropHelper(wpfWindow).Handle;
            if (h == IntPtr.Zero) return;
            SetWindowPos(h, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }
        catch { }
    }

    public static void BringToFront(Process process)
    {
        try
        {
            var hWnd = process.MainWindowHandle;
            if (hWnd == IntPtr.Zero)
            {
                hWnd = EnumerateWindowsForProcess(process.Id).FirstOrDefault();
            }
            if (hWnd == IntPtr.Zero) return;

            if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE); else ShowWindow(hWnd, SW_SHOW);
            AllowSetForegroundWindow(-1);
            SetForegroundWindow(hWnd);
            BringWindowToTop(hWnd);
        }
        catch { }
    }

    public static bool BringToFrontWindowHandle(IntPtr hWnd)
    {
        try
        {
            if (hWnd == IntPtr.Zero) return false;
            if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE); else ShowWindow(hWnd, SW_SHOW);
            AllowSetForegroundWindow(-1);
            var ok = SetForegroundWindow(hWnd);
            BringWindowToTop(hWnd);
            return ok;
        }
        catch { return false; }
    }

    public static (IntPtr hWnd, Process? process, string? exePath) GetActiveWindowProcess()
    {
        try
        {
            var h = GetForegroundWindow();
            if (h == IntPtr.Zero) return (IntPtr.Zero, null, null);
            GetWindowThreadProcessId(h, out var pid);
            Process? p = null; try { p = Process.GetProcessById((int)pid); } catch { }
            string? exe = null; try { exe = p?.MainModule?.FileName; } catch { }
            return (h, p, exe);
        }
        catch { return (IntPtr.Zero, null, null); }
    }

    public static string GetWindowTitle(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return string.Empty;
        try
        {
            int len = GetWindowTextLength(hWnd);
            var sb = new StringBuilder(len + 2);
            _ = GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
        catch { return string.Empty; }
    }

    private static IEnumerable<IntPtr> EnumerateWindowsForProcess(int processId)
    {
        var list = new List<IntPtr>();
        EnumWindows((hWnd, l) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == (uint)processId) list.Add(hWnd);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static IEnumerable<(IntPtr hWnd, string title)> EnumerateTopLevelWindowsForProcess(int processId)
    {
        foreach (var h in EnumerateWindowsForProcess(processId))
        {
            var t = GetWindowTitle(h);
            if (!string.IsNullOrWhiteSpace(t)) yield return (h, t);
        }
    }

    public static IntPtr FindWindowForProcessByTitle(int processId, string? titleHint)
    {
        try
        {
            var windows = EnumerateTopLevelWindowsForProcess(processId).ToList();
            if (windows.Count == 0) return IntPtr.Zero;
            if (!string.IsNullOrWhiteSpace(titleHint))
            {
                var exact = windows.FirstOrDefault(w => string.Equals(w.title, titleHint, StringComparison.OrdinalIgnoreCase));
                if (exact.hWnd != IntPtr.Zero) return exact.hWnd;
                var contains = windows.FirstOrDefault(w => w.title.IndexOf(titleHint, StringComparison.OrdinalIgnoreCase) >= 0);
                if (contains.hWnd != IntPtr.Zero) return contains.hWnd;
            }
            return windows[0].hWnd;
        }
        catch { return IntPtr.Zero; }
    }

    public static Process? FindRunningProcessForTarget(string targetPath)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(targetPath);
            var candidates = Process.GetProcessesByName(fileName);
            return candidates.FirstOrDefault();
        }
        catch { return null; }
    }

    public static Process? LaunchProcess(string targetPath, string? args, string? workingDir)
    {
        try
        {
            var psi = new ProcessStartInfo(targetPath)
            {
                Arguments = args ?? string.Empty,
                WorkingDirectory = workingDir ?? Path.GetDirectoryName(targetPath) ?? Environment.CurrentDirectory,
                UseShellExecute = true
            };
            return Process.Start(psi);
        }
        catch { return null; }
    }

    public static BitmapSource? ExtractIconImageSource(string path, int size)
    {
        try
        {
            if (File.Exists(path))
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon == null) return null;
                var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(size, size));
                src.Freeze();
                return src;
            }
        }
        catch { }
        return null;
    }

    public static (string? target, string? args, string? workDir) ResolveShortcutIfNeeded(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath)) return (null, null, null);
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        if (ext != ".lnk") return (inputPath, null, Path.GetDirectoryName(inputPath));
        try
        {
            var shellLink = (IShellLinkW)new ShellLink();
            ((IPersistFile)shellLink).Load(inputPath, 0);
            var fileSb = new StringBuilder(260);
            var data = new WIN32_FIND_DATAW();
            shellLink.GetPath(fileSb, fileSb.Capacity, out data, 0);
            var argsSb = new StringBuilder(512);
            shellLink.GetArguments(argsSb, argsSb.Capacity);
            var workSb = new StringBuilder(260);
            shellLink.GetWorkingDirectory(workSb, workSb.Capacity);
            var target = fileSb.ToString();
            var args = argsSb.ToString();
            var work = workSb.ToString();
            if (string.IsNullOrWhiteSpace(target)) return (inputPath, null, Path.GetDirectoryName(inputPath));
            return (target, string.IsNullOrWhiteSpace(args) ? null : args, string.IsNullOrWhiteSpace(work) ? Path.GetDirectoryName(target) : work);
        }
        catch { return (inputPath, null, Path.GetDirectoryName(inputPath)); }
    }
}

[ComImport]
[Guid("00021401-0000-0000-C000-000000000046")]
internal class ShellLink { }

[ComImport]
[Guid("000214F9-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellLinkW
{
    void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, uint fFlags);
    void GetIDList(out IntPtr ppidl);
    void SetIDList(IntPtr pidl);
    void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    void GetHotkey(out short pwHotkey);
    void SetHotkey(short wHotkey);
    void GetShowCmd(out int piShowCmd);
    void SetShowCmd(int iShowCmd);
    void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
    void Resolve(IntPtr hwnd, uint fFlags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WIN32_FIND_DATAW
{
    public uint dwFileAttributes;
    public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
    public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
    public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
    public uint nFileSizeHigh; public uint nFileSizeLow; public uint dwReserved0; public uint dwReserved1;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string cFileName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string cAlternateFileName;
}


