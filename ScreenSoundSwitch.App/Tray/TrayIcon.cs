using Crescendo.Core.Animation;
using Crescendo.Core.Platform;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ScreenSoundSwitch.App.Tray;

public sealed class TrayIcon : IDisposable
{
    private NotifyIcon? _notify;
    private Thread? _thread;
    private bool _disposed;

    // Actions posted from the WinForms thread, drained on the SDL main thread.
    private readonly ConcurrentQueue<Action> _sdlQueue = new();

    public void Attach(IWindow window)
    {
        // Intercept close button - hide instead of exit.
        // OnCloseRequested fires on the SDL thread, so window.Hide() is safe here.
        window.OnCloseRequested += window.Hide;

        // Drain posted actions every frame on the SDL/render thread.
        AnimationScheduler.AddUpdater(_ =>
        {
            while (_sdlQueue.TryDequeue(out var action))
                action();
            return true;
        });

        _thread = new Thread(() =>
        {
            _notify = new NotifyIcon
            {
                Visible = true,
                Text = "ScreenSoundSwitch",
                Icon = LoadIcon(),
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open", null, (_, _) => _sdlQueue.Enqueue(window.Show));
            menu.Items.Add("Exit", null, (_, _) =>
            {
                _notify.Visible = false;
                _sdlQueue.Enqueue(window.Close);
                Application.ExitThread();
            });

            _notify.ContextMenuStrip = menu;
            _notify.DoubleClick += (_, _) => _sdlQueue.Enqueue(window.Show);

            Application.Run();
        });

        _thread.IsBackground = false;
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private static Icon LoadIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(path)) return new Icon(path);
        }
        catch { }
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_notify != null)
        {
            _notify.Visible = false;
            _notify.Dispose();
        }
    }
}
