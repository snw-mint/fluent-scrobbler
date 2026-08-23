using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentScrobbler.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;

namespace FluentScrobbler.Views
{
    public sealed partial class AboutPage : Page
    {
        public AboutPage()
        {
            this.InitializeComponent();
            this.Loaded += AboutPage_Loaded;
            this.Unloaded += AboutPage_Unloaded;
        }

        private void AboutPage_Loaded(object sender, RoutedEventArgs e)
        {
            AppVersionText.Text = AppInfoService.FormattedVersion;
            OsVersionText.Text = RuntimeInformation.OSDescription;
            ArchitectureText.Text = $"{RuntimeInformation.OSArchitecture} (Process: {RuntimeInformation.ProcessArchitecture})";
            RuntimeVersionText.Text = RuntimeInformation.FrameworkDescription;

            UpdateService.Instance.UpdateStatusChanged += OnUpdateStatusChanged;
            UpdateStatusUi();
        }

        private void AboutPage_Unloaded(object sender, RoutedEventArgs e)
        {
            UpdateService.Instance.UpdateStatusChanged -= OnUpdateStatusChanged;
        }

        private void OnUpdateStatusChanged(object? sender, EventArgs e)
        {
            this.DispatcherQueue.TryEnqueue(UpdateStatusUi);
        }

        private void UpdateStatusUi()
        {
            if (UpdateService.Instance.IsUpdateAvailable)
            {
                StatusIcon.Symbol = FluentIcons.Common.Symbol.ArrowSync;
                StatusIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
                UpdateNowButton.Visibility = Visibility.Visible;
                CheckNowButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                StatusIcon.Symbol = FluentIcons.Common.Symbol.Checkmark;
                StatusIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
                UpdateNowButton.Visibility = Visibility.Collapsed;
                CheckNowButton.Visibility = Visibility.Visible;
                CheckNowButton.IsEnabled = true;
                CheckNowButton.Content = "Check now";
            }
        }

        private async void CheckNowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckNowButton.IsEnabled = false;
                CheckNowButton.Content = "Checking...";
                LogService.LogInfo("[AboutPage] User requested manual update check.");
                await UpdateService.Instance.CheckForUpdatesAsync(force: true);
                UpdateStatusUi();
            }
            catch (Exception ex)
            {
                LogService.LogError("[AboutPage] Error during manual update check", ex);
                CheckNowButton.IsEnabled = true;
                CheckNowButton.Content = "Check now";
            }
        }

        private async void UpdateNowButton_Click(object sender, RoutedEventArgs e)
        {
            string url = string.IsNullOrEmpty(UpdateService.Instance.ReleaseUrl) 
                ? UpdateService.DefaultReleasesUrl 
                : UpdateService.Instance.ReleaseUrl;
            await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
        }

        private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
        {
            LogService.OpenLogLocation();
        }

        private async void CopySystemInfoButton_Click(object sender, RoutedEventArgs e)
        {
            string info = $"- App: Fluent Scrobbler {AppInfoService.FormattedVersion}\n" +
                          $"- OS: {RuntimeInformation.OSDescription}\n" +
                          $"- Architecture: {RuntimeInformation.OSArchitecture} (Process: {RuntimeInformation.ProcessArchitecture})\n" +
                          $"- Framework: {RuntimeInformation.FrameworkDescription}\n" +
                          $"- WinUI: Windows App SDK 1.5";

            var dp = new DataPackage();
            dp.SetText(info);
            Clipboard.SetContent(dp);

            CopyButtonText.Text = "Copied to Clipboard!";
            await Task.Delay(2000);
            CopyButtonText.Text = "Copy System Info for Bug Report";
        }

        private void OpenLicensesButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame?.Navigate(typeof(LicensesPage));
        }
    }
}
