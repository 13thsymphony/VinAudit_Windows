using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234237

namespace VinAudit.uwp
{
    /// <summary>
    /// A basic page that provides characteristics common to most applications.
    /// </summary>
    public sealed partial class WebViewPage : Page
    {
        // ************** Member variables *********************

        public WebViewPage()
        {
            this.InitializeComponent();

            // We don't bother with any page state.
        }

        /// <summary>
        /// Loads the page and receives the requested VIN from the main page.
        /// </summary>
        /// <param name="e">e.Parameter contains a string that is the VIN.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            string vin = (string)e.Parameter;

            if (vin == null)
            {
                vin = "";
            }

            Uri vinURL = vinURL = new Uri("http://www.vinaudit.com/go.php?r=simontao&mobile=1&vin=" + vin);

            if (IsVinKnownDemonstrationValue(vin))
            {
                // We are in demo mode, bypass to the sample VIN report.
                // This allows us to avoid needing a credit card for demo scenarios.
                vinURL = new Uri("http://www.vinaudit.com/report?id=sample");
            }

            VinAuditWebView.Navigate(vinURL);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
        }

        /// <summary>
        /// Checks if we are requesting a known demo VIN.
        /// </summary>
        /// <param name="vin"></param>
        /// <returns>True if this is a known demo VIN. False otherwise.</returns>
        private bool IsVinKnownDemonstrationValue(string vin)
        {
            return vin == "1VXBR12EXCP901213";
        }

        private async void VinAuditWebView_NavigationCompleted(object sender, WebViewNavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                var dialog = new MessageDialog(
                    "We cannot reach VinAudit.com. Please ensure you have access to the Internet.",
                    "Connection error"
                    );

                await dialog.ShowAsync();
            }

            // Inject JS to partially de-brand the website. This is necessary because
            // we are already showing the VinAudit logo on the app header.
            string script = @"document.getElementById('default-logo').style.display='none';";
            string[] args = { script };
            try
            {
                // This could fail if the webpage was changed?
                await VinAuditWebView.InvokeScriptAsync("eval", args);
            }
            catch (Exception)
            {
                // Ignore.
            }
        }
    }
}
