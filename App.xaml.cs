using System;
using Microsoft.UI.Xaml;

namespace FluentScrobbler;

public partial class App : Application
{
    private Window? _window;
    
    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new SplashWindow();
        _window.Activate();
    }
}
