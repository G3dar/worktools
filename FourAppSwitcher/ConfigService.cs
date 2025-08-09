using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace FourAppSwitcher;

public static class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string GetConfigDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "FourAppSwitcher");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetConfigPath() => Path.Combine(GetConfigDirectory(), "config.json");

    public static async Task<ToolbarConfig> LoadAsync()
    {
        try
        {
            var path = GetConfigPath();
            if (!File.Exists(path)) return new ToolbarConfig();
            await using var fs = File.OpenRead(path);
            var cfg = await JsonSerializer.DeserializeAsync<ToolbarConfig>(fs, JsonOptions);
            return cfg ?? new ToolbarConfig();
        }
        catch
        {
            return new ToolbarConfig();
        }
    }

    public static async Task SaveAsync(ToolbarConfig config)
    {
        try
        {
            var path = GetConfigPath();
            await using var fs = File.Create(path);
            await JsonSerializer.SerializeAsync(fs, config, JsonOptions);
        }
        catch
        {
            // ignore
        }
    }
}


