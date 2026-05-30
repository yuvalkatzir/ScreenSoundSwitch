using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Diagnostics;

namespace ScreenSoundSwitch.WinUI.Audio
{
    internal class AudioDeviceNotificationClient : IMMNotificationClient
    {
        public void OnDeviceStateChanged(string deviceId, uint newState)
        {
            Trace.TraceInformation($"Device state changed: {deviceId}, new state: {newState}");
        }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
            Trace.TraceInformation($"Device added: {pwstrDeviceId}");
        }

        public void OnDeviceRemoved(string deviceId)
        {
            Trace.TraceInformation($"Device removed: {deviceId}");
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string pwstrDefaultDeviceId)
        {
            Trace.TraceInformation($"Default device changed: {pwstrDefaultDeviceId}, flow: {flow}, role: {role}");
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
            Trace.TraceInformation($"Property value changed: {pwstrDeviceId}");
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            Trace.TraceInformation($"Device state changed: {deviceId}, new state: {newState}");
        }
    }
}
