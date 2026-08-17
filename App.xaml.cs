using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Fluent Scrobbler.Services;

namespace Fluent Scrobbler
{
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();
            this.UnhandledException += App_UnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            try
            {
                AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
                AppNotificationManager.Default.Register();
            }
            catch { }
        }

        private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
        {
            if (args.Arguments.TryGetValue("action", out var action) && action == "open_source_settings")
            {
                MainWindow.Current?.DispatcherQueue.TryEnqueue(() =>
                {
                    if (MainWindow.Current != null)
                    {
                        MainWindow.Current.AppWindow.Show();
                        MainWindow.Current.Activate();
                        MainWindow.Current.NavigateToSourceSettings();
                    }
                });
            }
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogService.LogError($"[Render/UI Exception] {e.Message}", e.Exception);
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogService.LogError("[Unhandled Domain Exception]", ex);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogService.LogError("[Unobserved Task Exception]", e.Exception);
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();

            string[] commandLineArgs = Environment.GetCommandLineArgs();
            bool hasMinimizedFlag = Array.Exists(commandLineArgs, arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase));

            if (!hasMinimizedFlag)
            {
                _window.Activate();
            }
        }
    }
}
