using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using ScreenSoundSwitch.WinUI.Audio;
using System;
using System.Collections.Generic;
using System.Diagnostics;

#nullable enable

namespace ScreenSoundSwitch
{
    public class AudioDeviceManager : IDisposable
    {
        private static AudioDeviceManager? _instance;
        private readonly MMDeviceEnumerator _enumerator;
        private readonly AudioDeviceNotificationClient _notificationClient;

        private AudioDeviceManager()
        {
            _enumerator = new MMDeviceEnumerator();
            _notificationClient = new AudioDeviceNotificationClient();
            _enumerator.RegisterEndpointNotificationCallback(_notificationClient);
        }

        public static AudioDeviceManager Instance => _instance ??= new AudioDeviceManager();

        public MMDeviceCollection Devices => _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        public MMDevice GetDefaultAudioEndpoint() =>
            _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);

        public MMDevice? GetDeviceById(string deviceId)
        {
            foreach (MMDevice device in Devices)
                if (device.ID == deviceId)
                    return device;
            return null;
        }

        public MMDevice? GetDeviceByFriendlyName(string friendlyName)
        {
            foreach (MMDevice device in Devices)
            {
                if (device.FriendlyName == friendlyName)
                {
                    Trace.TraceInformation($"Found device: {device.FriendlyName}");
                    return device;
                }
            }
            Trace.TraceWarning($"Device not found by name: {friendlyName}");
            return null;
        }

        public bool IsUsingAudioDeviceByProcessId(int processId, Dictionary<string, MMDevice?> deviceInfoDict)
        {
            foreach (var deviceInfo in deviceInfoDict)
            {
                if (deviceInfo.Value?.AudioSessionManager.Sessions == null)
                    continue;

                var sessions = deviceInfo.Value.AudioSessionManager.Sessions;
                for (int i = sessions.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        var session = sessions[i];
                        if (session.State != AudioSessionState.AudioSessionStateActive || session.GetProcessID == 0)
                            continue;
                        if (session.GetProcessID == processId)
                            return true;
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceWarning($"Skipping session during IsUsingAudioDevice check: {ex.Message}");
                    }
                }
            }
            return false;
        }

        public string? GetUsingAudioDeviceNameById(int processId, Dictionary<string, MMDevice?> deviceInfoDict)
        {
            foreach (var deviceInfo in deviceInfoDict)
            {
                if (deviceInfo.Value?.AudioSessionManager.Sessions == null)
                    continue;

                var sessions = deviceInfo.Value.AudioSessionManager.Sessions;
                for (int i = sessions.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        var session = sessions[i];
                        if (session.State != AudioSessionState.AudioSessionStateActive || session.GetProcessID == 0)
                            continue;
                        if (session.GetProcessID == processId)
                            return deviceInfo.Key;
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceWarning($"Skipping session during GetUsingAudioDeviceNameById: {ex.Message}");
                    }
                }
            }
            return null;
        }

        public static int GetAudioDeviceProcessId(MMDevice device)
        {
            var sessions = device.AudioSessionManager.Sessions;
            if (sessions == null) return -1;

            for (int i = 0; i < sessions.Count; i++)
            {
                try
                {
                    var session = sessions[i];
                    if (session.State == AudioSessionState.AudioSessionStateActive)
                    {
                        using var process = Process.GetProcessById((int)session.GetProcessID);
                        return process.Id;
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Skipping session during GetAudioDeviceProcessId: {ex.Message}");
                }
            }
            return -1;
        }

        public List<AudioSessionControl> GetSessions()
        {
            var result = new List<AudioSessionControl>();
            foreach (var device in Devices)
            {
                try
                {
                    device.AudioSessionManager.RefreshSessions();
                    var sessions = device.AudioSessionManager.Sessions;
                    if (sessions == null) continue;

                    var count = sessions.Count;
                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            var session = sessions[i];
                            if (!session.IsSystemSoundsSession)
                                result.Add(session);
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceWarning($"Skipping audio session at index {i}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Failed to enumerate sessions for device: {ex.Message}");
                }
            }
            return result;
        }

        public void SetProcessVolume(MMDeviceCollection devices, uint processId, float volume)
        {
            foreach (var device in devices)
            {
                var sessions = device.AudioSessionManager.Sessions;
                if (sessions == null) continue;

                for (int i = 0; i < sessions.Count; i++)
                {
                    try
                    {
                        if (sessions[i].GetProcessID == processId)
                            sessions[i].SimpleAudioVolume.Volume = volume;
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceWarning($"Failed to set process volume: {ex.Message}");
                    }
                }
            }
        }

        public void Dispose()
        {
            _enumerator.UnregisterEndpointNotificationCallback(_notificationClient);
            _enumerator.Dispose();
        }
    }
}
