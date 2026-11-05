using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentIcons.Common;
using FluentScrobbler.Services;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;
using WinRT;

namespace FluentScrobbler.Views
{
    public sealed partial class TrayFlyoutWindow : Window
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const long WS_CAPTION = 0x00C00000L;
        private const long WS_THICKFRAME = 0x00040000L;
        private const long WS_SYSMENU = 0x00080000L;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const long WS_EX_APPWINDOW = 0x00040000L;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;

        private const double FlyoutWidthDip = 196;
        private const double FlyoutHeightDip = 234;
        private const double FlyoutIdleHeightDip = 118;
        private const double ScreenMarginDip = 12;
        private const float MarqueeGapDip = 36f;

        private readonly IntPtr _hWnd;
        private readonly LastFmService _lastFmService = new();
        private readonly LowLevelMouseProc _mouseHookProc;
        private IntPtr _mouseHookId = IntPtr.Zero;

        private DesktopAcrylicController? _acrylicController;
        private SystemBackdropConfiguration? _backdropConfig;

        private DateTimeOffset _lastDeactivatedTime = DateTimeOffset.MinValue;
        private DateTimeOffset _lastToggleTime = DateTimeOffset.MinValue;
        private bool _isClosing;
        private bool _isLiking;
        private ScrobbleStatus _lastScrobbleStatus = ScrobbleStatus.Idle;

        public TrayFlyoutWindow()
        {
            this.InitializeComponent();

            _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _mouseHookProc = MouseHookCallback;

            ConfigureWindowStyle();
            ConfigureLightAcrylicBackdrop();

            ElementCompositionPreview.SetIsTranslationEnabled(FlyoutContainer, true);
            ElementCompositionPreview.SetIsTranslationEnabled(TrackMarqueePanel, true);

            this.Activated += TrayFlyoutWindow_Activated;
            this.Closed += TrayFlyoutWindow_Closed;

            SubscribeEvents();
            RefreshTrackData();
        }

        private void ConfigureWindowStyle()
        {
            this.AppWindow.IsShownInSwitchers = false;

            if (this.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
                presenter.SetBorderAndTitleBar(false, false);
            }

            long style = GetWindowLongPtr(_hWnd, GWL_STYLE).ToInt64();
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU);
            SetWindowLongPtr(_hWnd, GWL_STYLE, new IntPtr(style));

            long exStyle = GetWindowLongPtr(_hWnd, GWL_EXSTYLE).ToInt64();
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle &= ~WS_EX_APPWINDOW;
            SetWindowLongPtr(_hWnd, GWL_EXSTYLE, new IntPtr(exStyle));

            int cornerPreference = DWMWCP_ROUND;
            DwmSetWindowAttribute(_hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            this.AppWindow.Hide();
        }

        private void ConfigureLightAcrylicBackdrop()
        {
            if (!DesktopAcrylicController.IsSupported()) return;

            try
            {
                this.SystemBackdrop = null;
                _backdropConfig = new SystemBackdropConfiguration
                {
                    IsInputActive = true
                };

                _acrylicController = new DesktopAcrylicController();
                UpdateAcrylicTheme(MainWindow.Current?.CurrentTheme ?? ElementTheme.Default);
                _acrylicController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                _acrylicController.SetSystemBackdropConfiguration(_backdropConfig);
            }
            catch
            {
                this.SystemBackdrop = new DesktopAcrylicBackdrop();
            }
        }

        private void UpdateAcrylicTheme(ElementTheme theme)
        {
            if (_acrylicController == null || _backdropConfig == null) return;

            bool isDark = theme switch
            {
                ElementTheme.Dark => true,
                ElementTheme.Light => false,
                _ => Application.Current.RequestedTheme == ApplicationTheme.Dark
            };

            _backdropConfig.Theme = isDark ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light;

            if (isDark)
            {
                _acrylicController.Kind = DesktopAcrylicKind.Base;
                _acrylicController.TintColor = Windows.UI.Color.FromArgb(255, 20, 20, 20);
                _acrylicController.TintOpacity = 0.88f;
                _acrylicController.LuminosityOpacity = 0.85f;
                _acrylicController.FallbackColor = Windows.UI.Color.FromArgb(255, 20, 20, 20);
            }
            else
            {
                _acrylicController.TintColor = Windows.UI.Color.FromArgb(255, 243, 243, 243);
                _acrylicController.TintOpacity = 0.60f;
                _acrylicController.LuminosityOpacity = 0.70f;
                _acrylicController.FallbackColor = Windows.UI.Color.FromArgb(255, 243, 243, 243);
            }
        }

        public void SyncTheme(ElementTheme theme)
        {
            ElementTheme targetTheme = theme;
            if (targetTheme == ElementTheme.Default && MainWindow.Current?.Content is FrameworkElement mainRoot)
            {
                targetTheme = mainRoot.ActualTheme;
            }

            if (this.Content is FrameworkElement root)
            {
                root.RequestedTheme = targetTheme;
            }
            UpdateAcrylicTheme(targetTheme);
        }

        private void SubscribeEvents()
        {
            ScrobblerBackgroundService.Instance.NowPlayingChanged += OnNowPlayingChanged;
            ScrobblerBackgroundService.Instance.StatusChanged += OnStatusChanged;
            ScrobblerBackgroundService.Instance.CurrentTrackIgnoredChanged += OnCurrentTrackIgnoredChanged;
            ScrobblerBackgroundService.Instance.TrackScrobbled += OnTrackScrobbled;
            LastFmService.TrackLoveChanged += OnTrackLoveChanged;
            BlockedRulesService.Instance.BlockedRulesChanged += OnBlockedRulesChanged;
            OfflineCacheWorker.Instance.OfflineModeChanged += OnOfflineModeChanged;
        }

        private void UnsubscribeEvents()
        {
            ScrobblerBackgroundService.Instance.NowPlayingChanged -= OnNowPlayingChanged;
            ScrobblerBackgroundService.Instance.StatusChanged -= OnStatusChanged;
            ScrobblerBackgroundService.Instance.CurrentTrackIgnoredChanged -= OnCurrentTrackIgnoredChanged;
            ScrobblerBackgroundService.Instance.TrackScrobbled -= OnTrackScrobbled;
            LastFmService.TrackLoveChanged -= OnTrackLoveChanged;
            BlockedRulesService.Instance.BlockedRulesChanged -= OnBlockedRulesChanged;
            OfflineCacheWorker.Instance.OfflineModeChanged -= OnOfflineModeChanged;
        }

        private void OnNowPlayingChanged(object? sender, NowPlayingInfo? info)
        {
            this.DispatcherQueue?.TryEnqueue(RefreshTrackData);
        }

        private void OnStatusChanged(object? sender, ScrobbleStatusInfo statusInfo)
        {
            _lastScrobbleStatus = statusInfo.Status;
            this.DispatcherQueue?.TryEnqueue(RefreshTrackData);
        }

        private void OnCurrentTrackIgnoredChanged(object? sender, bool isIgnored)
        {
            this.DispatcherQueue?.TryEnqueue(RefreshTrackData);
        }

        private void OnTrackScrobbled(object? sender, EventArgs e)
        {
            this.DispatcherQueue?.TryEnqueue(RefreshTrackData);
        }

        private void OnTrackLoveChanged(object? sender, (string Track, string Artist, bool IsLoved) e)
        {
            this.DispatcherQueue?.TryEnqueue(() =>
            {
                var current = ScrobblerBackgroundService.Instance.CurrentTrack;
                if (current != null &&
                    string.Equals(current.Track, e.Track, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(current.Artist, e.Artist, StringComparison.OrdinalIgnoreCase))
                {
                    HeartUiHelper.SetHeartVisual(LikeTrackIcon, e.IsLoved);
                }
            });
        }

        private void OnOfflineModeChanged(object? sender, bool isOffline)
        {
            this.DispatcherQueue?.TryEnqueue(RefreshTrackData);
        }

        private void OnBlockedRulesChanged(object? sender, EventArgs e)
        {
            this.DispatcherQueue?.TryEnqueue(RefreshTrackData);
        }

        private void RefreshTrackData()
        {
            var current = ScrobblerBackgroundService.Instance.CurrentTrack;
            bool isBlocked = current != null && ScrobblerBackgroundService.Instance.IsCurrentTrackBlocked;
            bool isIgnored = ScrobblerBackgroundService.Instance.IsCurrentTrackIgnored;
            bool hasScrobbled = ScrobblerBackgroundService.Instance.HasScrobbledCurrentTrack;

            if (current != null && !string.IsNullOrWhiteSpace(current.Track))
            {
                ActionsContainer.Visibility = Visibility.Visible;
                SetTrackTitle(current.Track);
                ArtistNameText.Text = !string.IsNullOrWhiteSpace(current.Artist) ? current.Artist : "Unknown artist";

                bool? cachedLoved = LastFmService.GetLocalLoveCache(current.Track, current.Artist ?? string.Empty);
                bool isLoved = cachedLoved ?? false;
                HeartUiHelper.SetHeartVisual(LikeTrackIcon, isLoved);
                if (!cachedLoved.HasValue)
                {
                    _ = _lastFmService.CheckTrackLovedAsync(current.Track, current.Artist ?? string.Empty);
                }
                IgnoreUiHelper.SetIgnoreVisual(IgnoreTrackButton, IgnoreTrackIcon, null, null, isIgnored, hasScrobbled, isBlocked);

                bool isTrackBlocked = BlockedRulesService.Instance.IsTrackBlocked(current.Track);
                if (isTrackBlocked)
                {
                    BlockTrackText.Text = "Unblock track";
                    BlockTrackIcon.Foreground = IgnoreUiHelper.IgnoreRedBrush;
                }
                else
                {
                    BlockTrackText.Text = "Block track";
                    BlockTrackIcon.ClearValue(FluentIcons.WinUI.SymbolIcon.ForegroundProperty);
                }

                if (isBlocked || _lastScrobbleStatus == ScrobbleStatus.Blocked)
                {
                    StatusText.Text = "Blocked";
                    StatusText.Foreground = IgnoreUiHelper.IgnoreRedBrush;
                }
                else if (isIgnored)
                {
                    StatusText.Text = "Ignored";
                    StatusText.Foreground = IgnoreUiHelper.IgnoreRedBrush;
                }
                else if (OfflineCacheWorker.Instance.OfflineMode)
                {
                    StatusText.Text = "Offline";
                    ApplySecondaryStatusBrush();
                }
                else if (hasScrobbled || _lastScrobbleStatus == ScrobbleStatus.Sent)
                {
                    StatusText.Text = "Sent";
                    ApplySecondaryStatusBrush();
                }
                else if (_lastScrobbleStatus == ScrobbleStatus.Error)
                {
                    StatusText.Text = "Error";
                    StatusText.Foreground = IgnoreUiHelper.IgnoreRedBrush;
                }
                else
                {
                    StatusText.Text = "Listening";
                    ApplySecondaryStatusBrush();
                }
            }
            else
            {
                ActionsContainer.Visibility = Visibility.Collapsed;
                SetTrackTitle("No track playing");
                ArtistNameText.Text = "Fluent Scrobbler";

                if (OfflineCacheWorker.Instance.OfflineMode)
                {
                    StatusText.Text = "Offline";
                    ApplySecondaryStatusBrush();
                }
                else
                {
                    StatusText.Text = "Idle";
                    ApplySecondaryStatusBrush();
                }
            }

            if (this.AppWindow.IsVisible)
            {
                UpdatePositionAndSize();
            }
        }

        private void ApplySecondaryStatusBrush()
        {
            StatusText.ClearValue(TextBlock.ForegroundProperty);
        }

        private void SetTrackTitle(string title)
        {
            TrackTitleText.Text = title;
            TrackTitleDuplicateText.Text = title;

            if (this.AppWindow.IsVisible)
            {
                UpdateMarquee();
            }
        }

        private void TrackTitleViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0)
            {
                TrackTitleViewport.Clip = new RectangleGeometry
                {
                    Rect = new Rect(0, 0, e.NewSize.Width, Math.Max(24, e.NewSize.Height))
                };

                if (this.AppWindow.IsVisible)
                {
                    UpdateMarquee();
                }
            }
        }

        private void UpdateMarquee()
        {
            double viewportWidth = TrackTitleViewport.ActualWidth > 0
                ? TrackTitleViewport.ActualWidth
                : (FlyoutWidthDip - 28);

            TrackTitleViewport.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, viewportWidth, 24)
            };

            TrackTitleText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double textWidth = TrackTitleText.DesiredSize.Width;

            var visual = ElementCompositionPreview.GetElementVisual(TrackMarqueePanel);
            visual.StopAnimation("Translation");
            visual.Properties.InsertVector3("Translation", Vector3.Zero);

            if (textWidth > viewportWidth)
            {
                TrackTitleDuplicateText.Visibility = Visibility.Visible;

                float totalShift = (float)textWidth + MarqueeGapDip;
                var compositor = visual.Compositor;
                var linear = compositor.CreateLinearEasingFunction();

                var anim = compositor.CreateVector3KeyFrameAnimation();
                anim.InsertKeyFrame(0.0f, Vector3.Zero);
                anim.InsertKeyFrame(0.18f, Vector3.Zero);
                anim.InsertKeyFrame(0.92f, new Vector3(-totalShift, 0f, 0f), linear);
                anim.InsertKeyFrame(1.0f, new Vector3(-totalShift, 0f, 0f));

                double durationSeconds = Math.Max(5.0, totalShift / 28.0);
                anim.Duration = TimeSpan.FromSeconds(durationSeconds);
                anim.IterationBehavior = AnimationIterationBehavior.Forever;

                visual.StartAnimation("Translation", anim);
            }
            else
            {
                TrackTitleDuplicateText.Visibility = Visibility.Collapsed;
            }
        }

        private void StopMarquee()
        {
            var visual = ElementCompositionPreview.GetElementVisual(TrackMarqueePanel);
            visual.StopAnimation("Translation");
            visual.Properties.InsertVector3("Translation", Vector3.Zero);
        }

        public void ToggleFlyout()
        {
            var now = DateTimeOffset.UtcNow;
            if ((now - _lastToggleTime).TotalMilliseconds < 120)
            {
                return;
            }
            _lastToggleTime = now;

            if (this.AppWindow.IsVisible)
            {
                HideFlyout();
                return;
            }

            if ((now - _lastDeactivatedTime).TotalMilliseconds < 280)
            {
                return;
            }

            ShowFlyout();
        }

        public void ShowFlyout()
        {
            if (MainWindow.Current != null)
            {
                SyncTheme(MainWindow.Current.CurrentTheme);
            }

            RefreshTrackData();
            UpdatePositionAndSize();

            var visual = ElementCompositionPreview.GetElementVisual(FlyoutContainer);
            visual.Opacity = 0f;
            visual.Properties.InsertVector3("Translation", new Vector3(0f, 14f, 0f));

            this.AppWindow.Show();
            SetForegroundWindow(_hWnd);
            this.Activate();

            InstallMouseHook();
            PlayFadeUpAnimation();
            UpdateMarquee();
        }

        private void UpdatePositionAndSize()
        {
            uint dpi = GetDpiForWindow(_hWnd);
            double scale = dpi > 0 ? dpi / 96.0 : 1.0;

            var current = ScrobblerBackgroundService.Instance.CurrentTrack;
            bool isPlaying = current != null && !string.IsNullOrWhiteSpace(current.Track);
            double heightDip = isPlaying ? FlyoutHeightDip : FlyoutIdleHeightDip;

            int widthPx = (int)Math.Round(FlyoutWidthDip * scale);
            int heightPx = (int)Math.Round(heightDip * scale);
            int marginPx = (int)Math.Round(ScreenMarginDip * scale);
            int trayZoneThresholdPx = (int)Math.Round(240 * scale);

            GetCursorPos(out POINT cursor);
            IntPtr hMonitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            int targetX = cursor.X - (widthPx / 2);
            int targetY = cursor.Y - heightPx - marginPx;

            if (GetMonitorInfo(hMonitor, ref mi))
            {
                RECT work = mi.rcWork;
                RECT mon = mi.rcMonitor;

                bool cursorNearTrayEdge =
                    cursor.Y >= work.Bottom - trayZoneThresholdPx ||
                    cursor.Y <= work.Top + trayZoneThresholdPx ||
                    cursor.X <= work.Left + trayZoneThresholdPx ||
                    cursor.X >= work.Right - trayZoneThresholdPx;

                bool cursorOnTraySideHorizontally = cursor.X >= mon.Left + ((mon.Right - mon.Left) / 2);

                if (work.Top > mon.Top)
                {
                    targetX = (cursorNearTrayEdge && cursorOnTraySideHorizontally)
                        ? Math.Clamp(cursor.X - (widthPx / 2), work.Left + marginPx, work.Right - widthPx - marginPx)
                        : work.Right - widthPx - marginPx;
                    targetY = work.Top + marginPx;
                }
                else if (work.Left > mon.Left)
                {
                    targetX = work.Left + marginPx;
                    targetY = cursorNearTrayEdge
                        ? Math.Clamp(cursor.Y - (heightPx / 2), work.Top + marginPx, work.Bottom - heightPx - marginPx)
                        : work.Bottom - heightPx - marginPx;
                }
                else if (work.Right < mon.Right)
                {
                    targetX = work.Right - widthPx - marginPx;
                    targetY = cursorNearTrayEdge
                        ? Math.Clamp(cursor.Y - (heightPx / 2), work.Top + marginPx, work.Bottom - heightPx - marginPx)
                        : work.Bottom - heightPx - marginPx;
                }
                else
                {
                    targetX = (cursorNearTrayEdge && cursorOnTraySideHorizontally)
                        ? Math.Clamp(cursor.X - (widthPx / 2), work.Left + marginPx, work.Right - widthPx - marginPx)
                        : work.Right - widthPx - marginPx;
                    targetY = work.Bottom - heightPx - marginPx;
                }
            }

            this.AppWindow.MoveAndResize(new RectInt32(targetX, targetY, widthPx, heightPx));
        }

        private void PlayFadeUpAnimation()
        {
            var visual = ElementCompositionPreview.GetElementVisual(FlyoutContainer);
            var compositor = visual.Compositor;

            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1.0f));

            var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
            opacityAnim.InsertKeyFrame(0f, 0f);
            opacityAnim.InsertKeyFrame(1f, 1f, easing);
            opacityAnim.Duration = TimeSpan.FromMilliseconds(130);

            var slideAnim = compositor.CreateVector3KeyFrameAnimation();
            slideAnim.InsertKeyFrame(0f, new Vector3(0f, 14f, 0f));
            slideAnim.InsertKeyFrame(1f, Vector3.Zero, easing);
            slideAnim.Duration = TimeSpan.FromMilliseconds(180);

            visual.StartAnimation("Opacity", opacityAnim);
            visual.StartAnimation("Translation", slideAnim);
        }

        private void InstallMouseHook()
        {
            if (_mouseHookId != IntPtr.Zero) return;
            IntPtr hMod = GetModuleHandle(null);
            _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookProc, hMod, 0);
        }

        private void UninstallMouseHook()
        {
            if (_mouseHookId == IntPtr.Zero) return;
            UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = (int)wParam;
                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN)
                {
                    var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    if (GetWindowRect(_hWnd, out RECT rect))
                    {
                        bool inside =
                            hookStruct.pt.X >= rect.Left &&
                            hookStruct.pt.X <= rect.Right &&
                            hookStruct.pt.Y >= rect.Top &&
                            hookStruct.pt.Y <= rect.Bottom;

                        if (!inside)
                        {
                            _lastDeactivatedTime = DateTimeOffset.UtcNow;
                            this.DispatcherQueue?.TryEnqueue(HideFlyout);
                        }
                    }
                }
            }
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        public void HideFlyout()
        {
            UninstallMouseHook();
            if (!this.AppWindow.IsVisible) return;
            _lastDeactivatedTime = DateTimeOffset.UtcNow;
            StopMarquee();
            this.AppWindow.Hide();
        }

        private void TrayFlyoutWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (_backdropConfig != null)
            {
                _backdropConfig.IsInputActive = true;
            }

            if (args.WindowActivationState == WindowActivationState.Deactivated && !_isClosing)
            {
                _lastDeactivatedTime = DateTimeOffset.UtcNow;
                this.DispatcherQueue?.TryEnqueue(HideFlyout);
            }
        }

        private void TrayFlyoutWindow_Closed(object sender, WindowEventArgs args)
        {
            if (!_isClosing)
            {
                args.Handled = true;
                HideFlyout();
            }
            else
            {
                UninstallMouseHook();
                _acrylicController?.Dispose();
                _acrylicController = null;
            }
        }

        public void ForceClose()
        {
            _isClosing = true;
            UnsubscribeEvents();
            UninstallMouseHook();
            StopMarquee();
            this.Close();
        }

        private async void LikeTrackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLiking) return;

            bool willBeLoved = LikeTrackIcon.IconVariant != IconVariant.Filled;
            HeartUiHelper.SetHeartVisual(LikeTrackIcon, willBeLoved);
            HeartUiHelper.AnimateHeartPop(LikeTrackIcon);

            var current = ScrobblerBackgroundService.Instance.CurrentTrack;
            if (current == null || string.IsNullOrWhiteSpace(current.Track) || string.IsNullOrWhiteSpace(current.Artist))
            {
                return;
            }

            _isLiking = true;
            LikeTrackButton.IsEnabled = false;
            try
            {
                await _lastFmService.ToggleLoveTrackAsync(current.Track, current.Artist, willBeLoved);
            }
            finally
            {
                LikeTrackButton.IsEnabled = true;
                _isLiking = false;
            }
        }

        private void IgnoreTrackButton_Click(object sender, RoutedEventArgs e)
        {
            var current = ScrobblerBackgroundService.Instance.CurrentTrack;
            if (current != null && !string.IsNullOrWhiteSpace(current.Track))
            {
                ScrobblerBackgroundService.Instance.ToggleIgnoreCurrentTrack();
                RefreshTrackData();
            }
        }

        private void BlockTrackButton_Click(object sender, RoutedEventArgs e)
        {
            var current = ScrobblerBackgroundService.Instance.CurrentTrack;
            if (current != null && !string.IsNullOrWhiteSpace(current.Track))
            {
                if (BlockedRulesService.Instance.IsTrackBlocked(current.Track))
                {
                    BlockedRulesService.Instance.UnblockTrack(current.Track);
                }
                else
                {
                    BlockedRulesService.Instance.BlockTrack(current.Track);
                }
                RefreshTrackData();
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            HideFlyout();
            MainWindow.Current?.NavigateToSettings();
        }
    }
}
