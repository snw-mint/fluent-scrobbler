using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using FluentScrobbler.Services;

namespace FluentScrobbler
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
            if (args.Arguments.TryGetValue("action", out var action))
            {
                if (action == "open_source_settings")
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
                else if (action == "open_home")
                {
                    MainWindow.Current?.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (MainWindow.Current != null)
                        {
                            if (MainWindow.Current.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                            {
                                if (presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Minimized)
                                {
                                    presenter.Restore();
                                }
                            }
                            MainWindow.Current.AppWindow.Show();
                            MainWindow.Current.Activate();
                            MainWindow.Current.NavigateToHome();
                        }
                    });
                }
                else if (action == "open_update_url")
                {
                    string defUrl = AppInfoService.IsPackaged ? UpdateService.StoreProductUrl : UpdateService.DefaultReleasesUrl;
                    string targetUrl = args.Arguments.TryGetValue("url", out var url) && !string.IsNullOrEmpty(url)
                        ? url
                        : defUrl;

                    _ = Windows.System.Launcher.LaunchUriAsync(new Uri(targetUrl));
                }
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

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var mainInstance = AppInstance.FindOrRegisterForKey("FluentScrobbler-MainInstance");
            if (!mainInstance.IsCurrent)
            {
                var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                await mainInstance.RedirectActivationToAsync(activatedArgs);
                Process.GetCurrentProcess().Kill();
                return;
            }

            AppInstance.GetCurrent().Activated += OnAppInstanceActivated;
            _window = new MainWindow();

            var currentArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            bool isStartupTask = currentArgs != null && currentArgs.Kind == ExtendedActivationKind.StartupTask;
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            bool hasMinimizedFlag = Array.Exists(commandLineArgs, arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase));

            bool shouldMinimize = hasMinimizedFlag || (isStartupTask && StartupService.IsStartMinimizedToTrayEnabled());

            if (!shouldMinimize)
            {
                _window.Activate();
            }
        }

        private void OnAppInstanceActivated(object? sender, AppActivationArguments e)
        {
            MainWindow.Current?.DispatcherQueue.TryEnqueue(() =>
            {
                if (MainWindow.Current == null) return;
                MainWindow.Current.AppWindow.Show();
                MainWindow.Current.Activate();
            });
        }
    }
}
