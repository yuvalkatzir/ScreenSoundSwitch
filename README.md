# ScreenSoundSwitch

A Windows desktop app that automatically routes audio playback devices based on which monitor an application window is on. Bind each display to an audio device and the app handles switching when windows move between screens.

## Requirements

- Windows 10/11
- .NET 10 SDK

## Building and Running

All dependencies are on NuGet and restored automatically.

```powershell
dotnet restore
dotnet run --project ScreenSoundSwitch.App/ScreenSoundSwitch.App.csproj
```

## How It Works

1. Open the **Displays** tab and bind each monitor to a playback device.
2. Open an audio application (music player, browser, etc.).
3. Move its window to a different monitor - the app switches the process audio endpoint to the bound device automatically.

Volume control and per-session adjustment are available on the **Volume** tab. A system tray icon and launch-on-login option can be configured in **Settings**.

## Known Limitations

- Some third-party players do not respond immediately to process-level audio endpoint changes. The routing is applied correctly but the player may need to rebuild its audio stream (e.g. pause/resume, track change) before it takes effect. This is a behavior difference in the target app's audio engine, not something ScreenSoundSwitch can force.
- Audio sessions created after the Volume page loads are not yet auto-discovered.
