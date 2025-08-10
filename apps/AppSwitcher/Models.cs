using System.Collections.Generic;

namespace AppSwitcher;

public class AppSlotConfig
{
    public string? FriendlyName { get; set; }
    public string? TargetPath { get; set; }
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? AppUserModelId { get; set; }
    public int? ProcessId { get; set; }
    public string? WindowTitle { get; set; }
    public long? Hwnd { get; set; } // persisted window handle (valid for current session)
    public int? BoundsLeft { get; set; }
    public int? BoundsTop { get; set; }
    public int? BoundsWidth { get; set; }
    public int? BoundsHeight { get; set; }
    public string? CustomLabel { get; set; }
    public string? CustomIconPath { get; set; }
    public string? BackgroundHex { get; set; }
}

public class ToolbarConfig
{
    public List<AppSlotConfig> Slots { get; set; } = new();
    public bool AlwaysOnTop { get; set; } = true;
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Width { get; set; } = 520;
    public double Height { get; set; } = 160;
    public double IconSize { get; set; } = 96;
    public int Columns { get; set; } = 4;
    public int Rows { get; set; } = 1;
}


