using Microsoft.UI.Xaml;

namespace FluentScrobbler
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.Title = "Fluent Scrobbler";
        }
    }
}