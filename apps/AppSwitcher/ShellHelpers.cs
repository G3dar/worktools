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
using System.Windows.Automation;
using System.Net.Http;
using System.Text.Json;

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
    public static bool IsWindowValid(IntPtr hWnd) => hWnd != IntPtr.Zero && IsWindow(hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private const int SW_SHOW = 5; private const int SW_RESTORE = 9;
    private const uint SWP_NOSIZE = 0x0001; private const uint SWP_NOMOVE = 0x0002; private const uint SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_TOPMOST = new(-1); private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private const uint GA_ROOTOWNER = 3;
    private const uint GW_OWNER = 4;

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

    public static int? GetProcessIdForWindow(IntPtr hWnd)
    {
        try
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return null;
            GetWindowThreadProcessId(hWnd, out var pid);
            return (int)pid;
        }
        catch { return null; }
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

    public static IntPtr FindWindowByTitleAcrossProcesses(string? titleHint)
    {
        if (string.IsNullOrWhiteSpace(titleHint)) return IntPtr.Zero;
        try
        {
            var handles = new List<(IntPtr hWnd, string title)>();
            EnumWindows((h, l) => { var t = GetWindowTitle(h); if (!string.IsNullOrWhiteSpace(t)) handles.Add((h, t)); return true; }, IntPtr.Zero);
            var exact = handles.FirstOrDefault(w => string.Equals(w.title, titleHint, StringComparison.OrdinalIgnoreCase));
            if (exact.hWnd != IntPtr.Zero) return exact.hWnd;
            var contains = handles.FirstOrDefault(w => w.title.IndexOf(titleHint, StringComparison.OrdinalIgnoreCase) >= 0);
            return contains.hWnd;
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

    public static BitmapSource? TryGetFaviconFromChromeWindow(IntPtr hWnd, int size, out string? url)
    {
        url = null;
        try
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return null;
            // Chrome embeds page title and sometimes URL in window text; best-effort parse http(s) patterns in title
            // Prefer reading URL via UI Automation from the address bar
            url = TryGetBrowserUrlViaUIA(hWnd);
            if (string.IsNullOrWhiteSpace(url))
            {
                var title = GetWindowTitle(hWnd);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(title, @"https?://[^\s]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) url = m.Value;
                }
            }
            // If URL not found, try DevTools (requires Chrome started with --remote-debugging-port=9222)
            if (string.IsNullOrWhiteSpace(url))
            {
                url = TryGetChromeUrlViaDevTools();
            }
            // If URL still not found, still try to read favicon from current process cache via shell (best-effort):
            if (url == null) return null;
            return TryDownloadFaviconForUrl(url, size);
        }
        catch { return null; }
    }

    private static string? TryGetBrowserUrlViaUIA(IntPtr hWnd)
    {
        try
        {
            var root = AutomationElement.FromHandle(hWnd);
            if (root == null) return null;
            // Prefer specific known address bar identifiers
            var candidates = new List<System.Windows.Automation.Condition>
            {
                new System.Windows.Automation.PropertyCondition(AutomationElement.AutomationIdProperty, "address and search bar"), // Chrome (en-US)
                new System.Windows.Automation.PropertyCondition(AutomationElement.NameProperty, "Address and search bar"),
                new System.Windows.Automation.PropertyCondition(AutomationElement.AutomationIdProperty, "url bar"), // Edge
                new System.Windows.Automation.PropertyCondition(AutomationElement.NameProperty, "Search or enter web address"),
                new System.Windows.Automation.PropertyCondition(AutomationElement.AutomationIdProperty, "Omnibox"),
            };
            foreach (var cond in candidates)
            {
                var el = root.FindFirst(TreeScope.Subtree, new System.Windows.Automation.AndCondition(
                    new System.Windows.Automation.PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                    cond));
                if (el != null && el.TryGetCurrentPattern(ValuePattern.Pattern, out var p1))
                {
                    var val = ((ValuePattern)p1).Current.Value;
                    if (!string.IsNullOrWhiteSpace(val) && (val.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || val.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                        return val;
                }
            }
            // Fallback: any edit containing a URL-looking value
            var edits = root.FindAll(TreeScope.Subtree, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            foreach (AutomationElement e in edits)
            {
                try
                {
                    if (!e.TryGetCurrentPattern(ValuePattern.Pattern, out var p)) continue;
                    var val = ((ValuePattern)p).Current.Value;
                    if (string.IsNullOrWhiteSpace(val)) continue;
                    if (val.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || val.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        return val;
                    }
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    public static BitmapSource? TryDownloadFaviconForUrl(string url, int size)
    {
        try
        {
            var host = new Uri(url).Host;
            // Try Google service first, then direct /favicon.ico
            var faviconApi = $"https://www.google.com/s2/favicons?domain={host}&sz={size}";
            using var wc = new System.Net.WebClient();
            var bytes = wc.DownloadData(faviconApi);
            using var ms = new System.IO.MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = ms;
            bmp.DecodePixelWidth = size;
            bmp.DecodePixelHeight = size;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            try
            {
                var uri = new Uri(url);
                var ico = new Uri(uri.GetLeftPart(UriPartial.Authority) + "/favicon.ico");
                using var wc = new System.Net.WebClient();
                var bytes = wc.DownloadData(ico);
                using var ms = new System.IO.MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.DecodePixelWidth = size;
                bmp.DecodePixelHeight = size;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }
    }

    private static string? TryGetChromeUrlViaDevTools()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
            var resp = http.GetAsync("http://127.0.0.1:9222/json").Result;
            if (!resp.IsSuccessStatusCode) return null;
            var json = resp.Content.ReadAsStringAsync().Result;
            using var doc = JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("type", out var t) && t.GetString() == "page")
                {
                    if (el.TryGetProperty("url", out var u))
                    {
                        var s = u.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    public static BitmapSource? CaptureWindowToBitmapSource(IntPtr hWnd)
    {
        try
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return null;
            if (!TryGetWindowBounds(hWnd, out var rect)) return null;
            int width = Math.Max(1, rect.Right - rect.Left);
            int height = Math.Max(1, rect.Bottom - rect.Top);

            using var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                bool printed = false;
                try { printed = PrintWindow(hWnd, hdc, 2); } catch { printed = false; }
                g.ReleaseHdc(hdc);
                if (!printed)
                {
                    try { g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height)); } catch { }
                }
            }
            var hBitmap = bmp.GetHbitmap();
            try
            {
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
        catch { return null; }
    }

    public static BitmapSource? CaptureWindowGroupToBitmapSource(IntPtr target)
    {
        try
        {
            if (!IsWindowValid(target)) return null;
            GetWindowThreadProcessId(target, out var pid);
            var group = new List<IntPtr>();
            // Collect ALL visible top-level windows that belong to the same process (covers tool windows not owned by the main root)
            EnumWindows((h, l) =>
            {
                if (!IsWindowVisible(h)) return true;
                GetWindowThreadProcessId(h, out var wpid);
                if (wpid == pid) group.Add(h);
                return true;
            }, IntPtr.Zero);
            if (group.Count == 0) group.Add(target);

            RECT? union = null;
            foreach (var h in group)
            {
                if (TryGetWindowBounds(h, out var rc))
                {
                    union = union == null ? rc : Union(union.Value, rc);
                }
            }
            if (union == null) return null;
            var u = union.Value;
            int width = Math.Max(1, u.Right - u.Left);
            int height = Math.Max(1, u.Bottom - u.Top);

            using var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                foreach (var h in group)
                {
                    if (!TryGetWindowBounds(h, out var rc)) continue;
                    int ox = rc.Left - u.Left;
                    int oy = rc.Top - u.Top;
                    using (var sub = new System.Drawing.Bitmap(Math.Max(1, rc.Right - rc.Left), Math.Max(1, rc.Bottom - rc.Top), System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        using (var sg = System.Drawing.Graphics.FromImage(sub))
                        {
                            IntPtr hdc = sg.GetHdc(); bool printed = false; try { printed = PrintWindow(h, hdc, 2); } catch { printed = false; } sg.ReleaseHdc(hdc);
                            if (!printed)
                            {
                                try { sg.CopyFromScreen(rc.Left, rc.Top, 0, 0, new System.Drawing.Size(sub.Width, sub.Height)); } catch { }
                            }
                        }
                        g.DrawImageUnscaled(sub, ox, oy);
                    }
                }
            }
            var hb = bmp.GetHbitmap();
            try
            {
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hb, IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally { DeleteObject(hb); }
        }
        catch { return null; }
    }

    private static RECT Union(RECT a, RECT b)
    {
        return new RECT
        {
            Left = Math.Min(a.Left, b.Left),
            Top = Math.Min(a.Top, b.Top),
            Right = Math.Max(a.Right, b.Right),
            Bottom = Math.Max(a.Bottom, b.Bottom)
        };
    }

    private static bool TryGetWindowBounds(IntPtr hWnd, out RECT rect)
    {
        try
        {
            const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
            if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf<RECT>()) == 0)
            {
                return true;
            }
        }
        catch { }
        return GetWindowRect(hWnd, out rect);
    }

    public static bool TryGetWindowBoundsManaged(IntPtr hWnd, out System.Windows.Rect bounds)
    {
        if (TryGetWindowBounds(hWnd, out var r))
        {
            bounds = new System.Windows.Rect(r.Left, r.Top, Math.Max(0, r.Right - r.Left), Math.Max(0, r.Bottom - r.Top));
            return true;
        }
        bounds = System.Windows.Rect.Empty;
        return false;
    }

    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left; public int Top; public int Right; public int Bottom;
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


