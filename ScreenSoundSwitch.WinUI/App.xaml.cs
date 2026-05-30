using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using ScreenSoundSwitch.WinUI.Data;
using ScreenSoundSwitch.WinUI.ViewModels;
using Serilog;
using System;
using System.IO;

namespace ScreenSoundSwitch.WinUI
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }
        public new static App Current => (App)Application.Current;

        public App()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ScreenSoundSwitch", "logs", "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7)
                .CreateLogger();

            Services = ConfigureServices();
            this.InitializeComponent();
            DebugLogStore.Initialize();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton<AudioDeviceManager>(provider => AudioDeviceManager.Instance);
            services.AddSingleton<ScreenToAudioDevice>(provider => ScreenToAudioDevice.Instance);
            services.AddTransient<ScreenViewModel>();
            return services.BuildServiceProvider();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                ScreenToAudioDevice.Instance.LoadConfig(deviceId => AudioDeviceManager.Instance.GetDeviceById(deviceId));

                m_window = new MainWindow();
                m_window.Activate();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to launch application");
            }
        }

        public static Window m_window;
    }
}
