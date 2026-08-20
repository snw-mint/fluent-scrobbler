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
                StatusIconImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/Status/update.png"));
                StatusText.Text = $"Update Available ({UpdateService.Instance.LatestVersion})";
                UpdateNowButton.Visibility = Visibility.Visible;
            }
            else
            {
                StatusIconImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/Status/updated.png"));
                StatusText.Text = "Updated";
                UpdateNowButton.Visibility = Visibility.Collapsed;
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
    }
}
