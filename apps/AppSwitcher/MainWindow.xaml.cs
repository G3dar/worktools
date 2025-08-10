using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinForms = System.Windows.Forms;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Media = System.Windows.Media;
using System.Windows.Threading;

namespace AppSwitcher;

public partial class MainWindow : Window
{
    private ToolbarConfig _config = new();
    private WinForms.NotifyIcon? _tray;
    private HwndSource? _hwndSource;
    private int _registeredHotkeyCount = 0;
    private IntPtr _lastExternalHwnd = IntPtr.Zero;
    private string? _lastExternalExe;
    private DispatcherTimer? _foregroundTimer;

    public MainWindow()
    {
        InitializeComponent();
        this.AllowsTransparency = true;
        this.Topmost = true;
        this.PreviewMouseDown += OnPreviewMouseDownCapture;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _config = await ConfigService.LoadAsync();
        Left = _config.Left;
        Top = _config.Top;
        Topmost = _config.AlwaysOnTop;
        ShellHelpers.ApplyTopmost(this, _config.AlwaysOnTop);
        if (_config.Rows <= 0) _config.Rows = 1;
        if (_config.Columns <= 0) _config.Columns = 4;
        BuildSlotsUI();
        ApplyIcons();
        SizeToContent = SizeToContent.Manual;
        InitTrayIcon();
        InitHotkeys();
        StartForegroundWatcher();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateIconSizesFromWindow();
    }

    private void OnPreviewMouseDownCapture(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var (h, p, exe) = ShellHelpers.GetActiveWindowProcess();
            var my = new WindowInteropHelper(this).Handle;
            if (h != IntPtr.Zero && h != my && !string.IsNullOrEmpty(exe))
            {
                _lastExternalHwnd = h;
                _lastExternalExe = exe;
            }
        }
        catch { }
    }

    private void ApplyIcons()
    {
        int total = _dynamicSlots.Count;
        for (int i = 0; i < total; i++)
        {
            var (img, title, btn) = _dynamicSlots[i];
            var slot = _config.Slots[i];
            img.Width = img.Height = _config.IconSize;
            title.Text = GetShortLabel(slot);
            if (!string.IsNullOrEmpty(slot.TargetPath))
            {
                BitmapSource? icon = null;
                if (!string.IsNullOrWhiteSpace(slot.CustomIconPath) && File.Exists(slot.CustomIconPath))
                {
                    try { icon = new BitmapImage(new Uri(slot.CustomIconPath)); } catch { }
                }
                icon ??= ShellHelpers.ExtractIconImageSource(slot.TargetPath, (int)_config.IconSize);
                img.Source = icon ?? CreatePlaceholderIcon();
            }
            else
            {
                img.Source = CreatePlusPlaceholder();
            }

            try
            {
                var hex = string.IsNullOrWhiteSpace(slot.BackgroundHex) ? null : slot.BackgroundHex;
                var bgBrush = (Media.SolidColorBrush)(new Media.BrushConverter().ConvertFromString(hex ?? "#1E1E1E") ?? Media.Brushes.Transparent);
                btn.Background = bgBrush;
                title.Foreground = GetContrastingTextBrush(bgBrush.Color);
            }
            catch { title.Foreground = Media.Brushes.Gainsboro; }
        }
    }

    private static Media.Brush GetContrastingTextBrush(Media.Color bg)
    {
        double brightness = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;
        return brightness > 0.6 ? Media.Brushes.Black : Media.Brushes.Gainsboro;
    }

    private static string GetShortLabel(AppSlotConfig slot)
    {
        // Prefer custom label; else first 10 chars of the window title.
        if (!string.IsNullOrWhiteSpace(slot.CustomLabel))
        {
            var c = slot.CustomLabel!.Trim();
            return c.Length <= 10 ? c : c.Substring(0, 10);
        }
        var t = slot.WindowTitle;
        if (string.IsNullOrWhiteSpace(t)) return string.Empty;
        t = t.Trim();
        return t.Length <= 10 ? t : t.Substring(0, 10);
    }

    private static ImageSource CreatePlaceholderIcon()
    {
        var bmp = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Pbgra32, null);
        return bmp;
    }

    private static DrawingImage CreatePlusPlaceholder()
    {
        var group = new Media.DrawingGroup();
        group.Children.Add(new Media.GeometryDrawing(new Media.SolidColorBrush(Media.Color.FromRgb(64, 64, 64)), null, new Media.RectangleGeometry(new System.Windows.Rect(0, 0, 96, 96))));
        var pen = new Media.Pen(Media.Brushes.LightGray, 6);
        group.Children.Add(new Media.GeometryDrawing(null, pen, new Media.LineGeometry(new System.Windows.Point(16, 48), new System.Windows.Point(80, 48))));
        group.Children.Add(new Media.GeometryDrawing(null, pen, new Media.LineGeometry(new System.Windows.Point(48, 16), new System.Windows.Point(48, 80))));
        return new DrawingImage(group);
    }

    private readonly System.Collections.Generic.List<(System.Windows.Controls.Image img, TextBlock title, System.Windows.Controls.Button btn)> _dynamicSlots = new();

    private void BuildSlotsUI()
    {
        _dynamicSlots.Clear();
        SlotsGrid.Children.Clear();
        SlotsGrid.Columns = _config.Columns <= 0 ? 4 : _config.Columns;
        int total = Math.Min(Math.Max(1, _config.Rows) * SlotsGrid.Columns, 8);
        EnsureSlotsCount(total);
        for (int i = 0; i < total; i++)
        {
            var img = new System.Windows.Controls.Image { Width = _config.IconSize, Height = _config.IconSize };
            var title = new TextBlock { Text = GetShortLabel(_config.Slots[i]), Foreground = Media.Brushes.Gainsboro, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
            var stack = new StackPanel();
            stack.Children.Add(title);
            stack.Children.Add(img);
            var btn = new System.Windows.Controls.Button { Style = (Style)FindResource("IconButtonStyle"), AllowDrop = true, Height = _config.IconSize + 22, Content = stack, Margin = new Thickness(4) };
            btn.Drop += Slot_Drop;
            btn.DragOver += Slot_DragOver;
            btn.Click += Slot_Click;
            btn.ContextMenu = BuildSlotContextMenu(i);
            SlotsGrid.Children.Add(btn);
            _dynamicSlots.Add((img, title, btn));
        }
        UpdateIconSizesFromWindow();
        // Rebind hotkeys if already initialized
        if (_hwndSource != null)
        {
            RegisterHotkeysForCount(Math.Min(_dynamicSlots.Count, 8));
        }
    }

    private void EnsureSlotsCount(int total)
    {
        while (_config.Slots.Count < total) _config.Slots.Add(new AppSlotConfig());
        if (_config.Slots.Count > total) _config.Slots = _config.Slots.Take(total).ToList();
    }

    private void UpdateIconSizesFromWindow()
    {
        if (_dynamicSlots.Count == 0) return;
        var rows = Math.Max(1, _config.Rows);
        var cols = Math.Max(1, _config.Columns);
        double cellWidth = Math.Max(40, (SlotsGrid.ActualWidth / cols) - 12);
        double cellHeight = Math.Max(40, (SlotsGrid.ActualHeight / rows) - 12);
        double titleReserve = 22;
        double newSize = Math.Max(32, Math.Min(cellWidth, cellHeight - titleReserve));
        _config.IconSize = newSize;
        foreach (var s in _dynamicSlots)
        {
            s.img.Width = s.img.Height = newSize;
            if (s.btn.Content is StackPanel)
            {
                s.btn.Height = newSize + titleReserve + 16;
            }
        }
    }

    private ContextMenu BuildSlotContextMenu(int slotIndex)
    {
        var ctx = new ContextMenu();
        var rename = new MenuItem { Header = "Rename" };
        rename.Click += async (_, _) => await RenameSlotAsync(slotIndex);
        ctx.Items.Add(rename);

        var changeIcon = new MenuItem { Header = "Change icon..." };
        changeIcon.Click += (_, _) => ChangeSlotIcon(slotIndex);
        ctx.Items.Add(changeIcon);

        var changeBg = new MenuItem { Header = "Change background color..." };
        changeBg.Click += (_, _) => ChangeSlotBackground(slotIndex);
        ctx.Items.Add(changeBg);

        ctx.Items.Add(new Separator());
        var clear = new MenuItem { Header = "Clear slot" };
        clear.Click += async (_, _) =>
        {
            var s = _config.Slots[slotIndex];
            s.TargetPath = null;
            s.Arguments = null;
            s.WorkingDirectory = null;
            s.AppUserModelId = null;
            s.ProcessId = null;
            s.WindowTitle = null;
            s.CustomLabel = null;
            s.CustomIconPath = null;
            s.BackgroundHex = null;
            ApplyIcons();
            await SaveAsync();
        };
        ctx.Items.Add(clear);

        return ctx;
    }

    private void ChangeSlotIcon(int slotIndex)
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose icon image",
                Filter = "Images|*.png;*.jpg;*.jpeg;*.ico;*.bmp|All files|*.*"
            };
            if (dlg.ShowDialog(this) == true)
            {
                _config.Slots[slotIndex].CustomIconPath = dlg.FileName;
                ApplyIcons();
                _ = SaveAsync();
            }
        }
        catch { }
    }

    private void ChangeSlotBackground(int slotIndex)
    {
        try
        {
            var dlg = new WinForms.ColorDialog { FullOpen = true };
            // Seed current color if valid
            try
            {
                var hex = _config.Slots[slotIndex].BackgroundHex;
                if (!string.IsNullOrWhiteSpace(hex))
                {
                    var brush = (Media.SolidColorBrush)(new Media.BrushConverter().ConvertFromString(hex) ?? Media.Brushes.Transparent);
                    var c = ((Media.SolidColorBrush)brush).Color;
                    dlg.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
                }
            }
            catch { }

            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            {
                var c = dlg.Color;
                _config.Slots[slotIndex].BackgroundHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                ApplyIcons();
                _ = SaveAsync();
            }
        }
        catch { }
    }

    private async Task SaveAsync()
    {
        _config.Left = Left;
        _config.Top = Top;
        _ = ConfigService.SaveAsync(_config);
        await Task.CompletedTask;
    }

    private int SlotIndexFromSender(object sender)
    {
        for (int i = 0; i < _dynamicSlots.Count; i++)
        {
            if (ReferenceEquals(sender, _dynamicSlots[i].btn)) return i;
        }
        return -1;
    }

    private void Slot_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files.Any(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                e.Handled = true;
            }
        }
    }

    private async void Slot_Drop(object sender, System.Windows.DragEventArgs e)
    {
        try
        {
            if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            var path = files.FirstOrDefault(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(path)) return;

            var idx = SlotIndexFromSender(sender);
            if (idx < 0) return;

            var (target, args, work) = ShellHelpers.ResolveShortcutIfNeeded(path);
            if (string.IsNullOrEmpty(target)) return;

            var slot = _config.Slots[idx];
            slot.TargetPath = target;
            slot.Arguments = args;
            slot.WorkingDirectory = work;
            ApplyIcons();
            await SaveAsync();
        }
        catch { }
    }

    private async void Slot_Click(object sender, RoutedEventArgs e)
    {
        var idx = SlotIndexFromSender(sender);
        if (idx < 0) return;
        var slot = _config.Slots[idx];
        if (string.IsNullOrWhiteSpace(slot.TargetPath))
        {
            // Try assign from last captured external or current foreground
            var assigned = await TryAssignFromLastExternalAsync(idx);
            if (!assigned)
            {
                assigned = await AssignFromCurrentForegroundAsync(idx);
            }
            if (assigned)
            {
                ApplyIcons();
                await SaveAsync();
            }
            return;
        }

        if (slot.ProcessId is int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                if (p != null)
                {
                    var targetHwnd = ShellHelpers.FindWindowForProcessByTitle(pid, slot.WindowTitle);
                    if (targetHwnd != IntPtr.Zero && ShellHelpers.BringToFrontWindowHandle(targetHwnd)) return;
                    if (p.MainWindowHandle != IntPtr.Zero && ShellHelpers.BringToFrontWindowHandle(p.MainWindowHandle)) return;
                }
            }
            catch { }
        }

        var proc = ShellHelpers.FindRunningProcessForTarget(slot.TargetPath!);
        if (proc != null)
        {
            ShellHelpers.BringToFront(proc);
            return;
        }

        proc = ShellHelpers.LaunchProcess(slot.TargetPath!, slot.Arguments, slot.WorkingDirectory);
        if (proc != null)
        {
            await Task.Delay(1000);
            ShellHelpers.BringToFront(proc);
        }
    }

    // Always-on-top now controlled via menu

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        var ctx = new ContextMenu();
        var aot = new MenuItem { Header = this.Topmost ? "Always on top ✓" : "Always on top" };
        aot.Click += async (_, _) =>
        {
            Topmost = !Topmost;
            ShellHelpers.ApplyTopmost(this, Topmost);
            _config.AlwaysOnTop = Topmost;
            await SaveAsync();
        };
        ctx.Items.Add(aot);
        ctx.Items.Add(new Separator());
        var assignMenu = new MenuItem { Header = "Assign from running (pick window)..." };
        assignMenu.Click += AssignFromRunning_Click;
        ctx.Items.Add(assignMenu);
        ctx.Items.Add(new Separator());
        for (int i = 0; i < _dynamicSlots.Count; i++)
        {
            var iLocal = i;
            var mi = new MenuItem { Header = $"Assign this slot from running... (Slot {i + 1})" };
            mi.Click += async (_, _) => await AssignFromRunningToSlot(iLocal);
            ctx.Items.Add(mi);
        }

        var renameHeader = new MenuItem { Header = "Rename slot label..." };
        for (int i = 0; i < _dynamicSlots.Count; i++)
        {
            var iLocal = i;
            var rmi = new MenuItem { Header = $"Rename Slot {i + 1}" };
            rmi.Click += async (_, _) => await RenameSlotAsync(iLocal);
            renameHeader.Items.Add(rmi);
        }
        ctx.Items.Add(renameHeader);

        var layout = new MenuItem { Header = "Layout" };
        var layout4 = new MenuItem { Header = "4 slots (1 row)" };
        layout4.Click += async (_, _) => { _config.Rows = 1; _config.Columns = 4; BuildSlotsUI(); ApplyIcons(); await SaveAsync(); };
        var layout8 = new MenuItem { Header = "8 slots (2 rows)" };
        layout8.Click += async (_, _) => { _config.Rows = 2; _config.Columns = 4; BuildSlotsUI(); ApplyIcons(); await SaveAsync(); };
        layout.Items.Add(layout4);
        layout.Items.Add(layout8);
        ctx.Items.Add(layout);

        var clearAll = new MenuItem { Header = "Clear all" };
        clearAll.Click += async (_, _) =>
        {
            _config.Slots = new();
            BuildSlotsUI();
            ApplyIcons();
            await SaveAsync();
        };
        ctx.Items.Add(clearAll);

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => Close();
        ctx.Items.Add(exit);

        ctx.IsOpen = true;
    }

    private async Task RenameSlotAsync(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _config.Slots.Count) return;
        var slot = _config.Slots[slotIndex];
        var w = new Window
        {
            Title = $"Rename Slot {slotIndex + 1}",
            Width = 300,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize
        };
        var tb = new System.Windows.Controls.TextBox { Margin = new Thickness(8), Text = slot.CustomLabel ?? string.Empty };
        var ok = new System.Windows.Controls.Button { Content = "Save", Margin = new Thickness(8), HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Width = 72 };
        ok.Click += (_, _) => w.DialogResult = true;
        var panel = new DockPanel();
        DockPanel.SetDock(ok, Dock.Bottom);
        panel.Children.Add(ok);
        panel.Children.Add(tb);
        w.Content = panel;
        if (w.ShowDialog() == true)
        {
            slot.CustomLabel = string.IsNullOrWhiteSpace(tb.Text) ? null : tb.Text.Trim();
            ApplyIcons();
            await SaveAsync();
        }
    }

    private async void AssignFromRunning_Click(object sender, RoutedEventArgs e)
    {
        await AssignFromRunningToSlot(_config.Slots.FindIndex(s => string.IsNullOrEmpty(s.TargetPath)) is int idx && idx >= 0 ? idx : 0);
    }

    private async Task AssignFromRunningToSlot(int slotIndex)
    {
        var processes = Process.GetProcesses().Where(p => p.MainWindowHandle != IntPtr.Zero).OrderBy(p => p.ProcessName).ToList();

        var win = new Window
        {
            Title = "Pick running window",
            Width = 360,
            Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };
        var list = new System.Windows.Controls.ListBox();
        foreach (var p in processes)
        {
            var title = p.MainWindowTitle;
            if (string.IsNullOrWhiteSpace(title)) title = "(Untitled)";
            list.Items.Add(new ListBoxItem { Content = $"{title} — {p.ProcessName} (PID {p.Id})", Tag = p });
        }
        var ok = new System.Windows.Controls.Button { Content = $"Assign to Slot {slotIndex + 1}", Margin = new Thickness(6) };
        ok.Click += (_, _) => win.DialogResult = true;
        var panel = new DockPanel();
        DockPanel.SetDock(ok, Dock.Bottom);
        panel.Children.Add(ok);
        panel.Children.Add(list);
        win.Content = panel;
        if (win.ShowDialog() == true)
        {
            if (list.SelectedItem is ListBoxItem { Tag: Process p })
            {
                var exe = string.Empty;
                try { exe = p.MainModule?.FileName ?? string.Empty; }
                catch { }
                if (!string.IsNullOrEmpty(exe))
                {
                    var slot = _config.Slots[slotIndex];
                    slot.TargetPath = exe;
                    slot.Arguments = null;
                    slot.WorkingDirectory = Path.GetDirectoryName(exe);
                    slot.ProcessId = p.Id;
                    slot.WindowTitle = p.MainWindowTitle;
                    ApplyIcons();
                    await SaveAsync();
                }
            }
        }
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        await SaveAsync();
        TeardownHotkeys();
        try { _foregroundTimer?.Stop(); _foregroundTimer = null; } catch { }
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
    }

    private void StartForegroundWatcher()
    {
        try
        {
            _foregroundTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _foregroundTimer.Tick += (_, _) =>
            {
                try
                {
                    var (h, p, exe) = ShellHelpers.GetActiveWindowProcess();
                    if (h == IntPtr.Zero || string.IsNullOrEmpty(exe)) return;
                    var my = new WindowInteropHelper(this).Handle;
                    if (h == my) return;
                    _lastExternalHwnd = h;
                    _lastExternalExe = exe;
                }
                catch { }
            };
            _foregroundTimer.Start();
        }
        catch { }
    }

    private void InitTrayIcon()
    {
        try
        {
            _tray = new WinForms.NotifyIcon
            {
                Visible = true,
                Text = "AppSwitcher",
                Icon = System.Drawing.SystemIcons.Application
            };
            var ctx = new WinForms.ContextMenuStrip();
            for (int i = 0; i < Math.Min(_config.Rows * _config.Columns, 8); i++)
            {
                var iLocal = i;
                var item = new WinForms.ToolStripMenuItem($"Add active window to Slot {i + 1}");
                item.Click += async (_, _) => await AssignActiveWindowToSlot(iLocal);
                ctx.Items.Add(item);
            }
            ctx.Items.Add(new WinForms.ToolStripSeparator());
            var exit = new WinForms.ToolStripMenuItem("Exit");
            exit.Click += (_, _) => System.Windows.Application.Current.Shutdown();
            ctx.Items.Add(exit);
            _tray.ContextMenuStrip = ctx;
            _tray.DoubleClick += (_, _) => { try { Activate(); } catch { } };
        }
        catch { }
    }

    private async Task AssignActiveWindowToSlot(int slotIndex)
    {
        var (hWnd, process, exe) = ShellHelpers.GetActiveWindowProcess();
        if (hWnd == IntPtr.Zero || process == null || string.IsNullOrEmpty(exe)) return;
        var slot = _config.Slots[slotIndex];
        slot.TargetPath = exe;
        slot.Arguments = null;
        slot.WorkingDirectory = Path.GetDirectoryName(exe);
        var activeTitle = ShellHelpers.GetWindowTitle(hWnd);
        slot.ProcessId = process.Id;
        slot.WindowTitle = string.IsNullOrWhiteSpace(activeTitle) ? process.MainWindowTitle : activeTitle;
        ApplyIcons();
        await SaveAsync();
    }

    private async Task<bool> TryAssignFromLastExternalAsync(int slotIndex)
    {
        try
        {
            var hWnd = _lastExternalHwnd;
            var exe = _lastExternalExe;
            if (hWnd == IntPtr.Zero || string.IsNullOrEmpty(exe)) return false;
            var slot = _config.Slots[slotIndex];
            slot.TargetPath = exe;
            slot.Arguments = null;
            slot.WorkingDirectory = Path.GetDirectoryName(exe);
            slot.ProcessId = null;
            slot.WindowTitle = ShellHelpers.GetWindowTitle(hWnd);
            await SaveAsync();
            return true;
        }
        catch { return false; }
    }

    private async Task<bool> AssignFromCurrentForegroundAsync(int slotIndex)
    {
        try
        {
            var (hWnd, process, exe) = ShellHelpers.GetActiveWindowProcess();
            if (hWnd == IntPtr.Zero || process == null || string.IsNullOrEmpty(exe)) return false;
            var my = new WindowInteropHelper(this).Handle;
            if (hWnd == my) return false;
            var slot = _config.Slots[slotIndex];
            slot.TargetPath = exe;
            slot.Arguments = null;
            slot.WorkingDirectory = Path.GetDirectoryName(exe);
            slot.ProcessId = process.Id;
            slot.WindowTitle = ShellHelpers.GetWindowTitle(hWnd);
            await SaveAsync();
            return true;
        }
        catch { return false; }
    }

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private void InitHotkeys()
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            if (_hwndSource == null) return;
            _hwndSource.AddHook(WndProcHook);
            RegisterHotkeysForCount(Math.Min(_dynamicSlots.Count, 8));
        }
        catch { }
    }

    private void TeardownHotkeys()
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            var handle = helper.Handle;
            for (int i = 0; i < _registeredHotkeyCount; i++) UnregisterHotKey(handle, 9001 + i);
            _hwndSource?.RemoveHook(WndProcHook);
            _hwndSource = null;
            _registeredHotkeyCount = 0;
        }
        catch { }
    }

    private void RegisterHotkeysForCount(int count)
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            var handle = helper.Handle;
            // Unregister previous
            for (int i = 0; i < _registeredHotkeyCount; i++)
            {
                UnregisterHotKey(handle, 9001 + i);
            }
            // Register Ctrl+Alt+1..count
            for (int i = 0; i < count; i++)
            {
                RegisterHotKey(handle, 9001 + i, MOD_CONTROL | MOD_ALT, (uint)(0x31 + i));
            }
            _registeredHotkeyCount = count;
        }
        catch { }
    }

    private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (id >= 9001 && id < 9001 + _dynamicSlots.Count)
            {
                var slotIndex = id - 9001;
                _ = AssignActiveWindowToSlot(slotIndex);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }
}


