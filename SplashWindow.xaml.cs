using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace FluentScrobbler
{
    public sealed partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            this.InitializeComponent();
            ConfigureWindow();
            StartInitialization();
        }

        private void ConfigureWindow()
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }

            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }

            DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                int width = 500;
                int height = 320;
                int x = displayArea.WorkArea.X + (displayArea.WorkArea.Width - width) / 2;
                int y = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - height) / 2;
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
            }
        }

        private async void StartInitialization()
        {
            StatusText.Text = "Loading application resources...";
            await Task.Delay(500);

            StatusText.Text = "Initializing Scrobbler services...";
            await Task.Delay(600);

            StatusText.Text = "Starting Fluent Scrobbler...";
            await Task.Delay(400);

            var mainWindow = new MainWindow();
            mainWindow.Activate();

            this.Close();
        }
    }
}
