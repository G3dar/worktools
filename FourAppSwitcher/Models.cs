using System.Collections.Generic;

namespace FourAppSwitcher;

public class AppSlotConfig
{
    public string? FriendlyName { get; set; }
    public string? TargetPath { get; set; }
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? AppUserModelId { get; set; }
    public int? ProcessId { get; set; }
    public string? WindowTitle { get; set; }
}

public class ToolbarConfig
{
    public List<AppSlotConfig> Slots { get; set; } = new() { new(), new(), new(), new() };
    public bool AlwaysOnTop { get; set; } = true;
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double IconSize { get; set; } = 96;
    public int Columns { get; set; } = 4;
    public int Rows { get; set; } = 1; // up to 2 rows (8 slots)
}


