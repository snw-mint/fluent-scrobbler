using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentScrobbler.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace FluentScrobbler.Views
{
    public sealed partial class AboutPage : Page
    {
        public AboutPage()
        {
            this.InitializeComponent();
            this.Loaded += AboutPage_Loaded;
        }

        private void AboutPage_Loaded(object sender, RoutedEventArgs e)
        {
            OsVersionText.Text = RuntimeInformation.OSDescription;
            ArchitectureText.Text = $"{RuntimeInformation.OSArchitecture} (Process: {RuntimeInformation.ProcessArchitecture})";
            RuntimeVersionText.Text = RuntimeInformation.FrameworkDescription;
        }

        private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
        {
            LogService.OpenLogLocation();
        }

        private async void CopySystemInfoButton_Click(object sender, RoutedEventArgs e)
        {
            string info = $"- App: Fluent Scrobbler v0.1\n" +
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
