using NAudio.CoreAudioApi;
using Serilog;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace ScreenSoundSwitch.App.Data;

public class ScreenToAudioDevice
{
    private static ScreenToAudioDevice? _instance;
    private readonly Dictionary<string, MMDevice> _map = new();

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenSoundSwitch", "bindings.json");

    private ScreenToAudioDevice() { }

    public static ScreenToAudioDevice Instance => _instance ??= new ScreenToAudioDevice();

    public bool ContainsScreen(Screen screen) => _map.ContainsKey(screen.DeviceName);

    public bool TryGetDevice(Screen screen, out MMDevice? device) =>
        _map.TryGetValue(screen.DeviceName, out device);

    public void SetDevice(Screen screen, MMDevice device)
    {
        _map[screen.DeviceName] = device;
        SaveConfig();
    }

    public void LoadConfig(Func<string, MMDevice?> deviceResolver)
    {
        if (!File.Exists(ConfigPath)) return;
        try
        {
            var json = File.ReadAllText(ConfigPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict == null) return;

            foreach (var (screenName, deviceId) in dict)
            {
                var device = deviceResolver(deviceId);
                if (device != null)
                    _map[screenName] = device;
            }
            Log.Information("Loaded {Count} display-device binding(s) from config.", _map.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load display-device bindings.");
        }
    }

    private void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            var dict = _map.ToDictionary(kv => kv.Key, kv => kv.Value.ID);
            var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save display-device bindings.");
        }
    }
}
