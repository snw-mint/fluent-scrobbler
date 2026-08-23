using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FluentScrobbler.Views
{
    public sealed partial class LicensesPage : Page
    {
        public LicensesPage()
        {
            this.InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame != null && this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
            else
            {
                this.Frame?.Navigate(typeof(AboutPage));
            }
        }
    }
}
