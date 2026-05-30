using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace ScreenSoundSwitch.WinUI.Data
{
    public class ScreenToAudioDevice
    {
        private static ScreenToAudioDevice? _instance;
        private readonly Dictionary<string, MMDevice> _map = new();

        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScreenSoundSwitch",
            "bindings.json");

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

        private void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                var data = _map.ToDictionary(kv => kv.Key, kv => kv.Value.ID);
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to save screen-device bindings: {ex.Message}");
            }
        }

        public void LoadConfig(Func<string, MMDevice?> deviceResolver)
        {
            if (!File.Exists(ConfigPath)) return;
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (data == null) return;

                foreach (var kv in data)
                {
                    var device = deviceResolver(kv.Value);
                    if (device != null)
                        _map[kv.Key] = device;
                }

                Trace.TraceInformation($"Loaded {_map.Count} screen-device binding(s) from config.");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to load screen-device bindings: {ex.Message}");
            }
        }
    }
}
