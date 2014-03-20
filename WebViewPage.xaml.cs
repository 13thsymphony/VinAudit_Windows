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

namespace VinAudit
{
    /// <summary>
    /// A basic page that provides characteristics common to most applications.
    /// </summary>
    public sealed partial class WebViewPage : VinAudit.Common.LayoutAwarePage
    {
        public WebViewPage()
        {
            this.InitializeComponent();

            VinAuditWebView.NavigationFailed += OnNavigationFailed;
        }

        private async void OnNavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
        {
            var dialog = new MessageDialog("We cannot reach VinAudit.com. Please ensure you have access to the Internet.", "Connection error");
            await dialog.ShowAsync();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // I can't figure out how to get the Frame.Navigated event to work, so use a
            // shared var in App.Xaml to pass the VIN...
            App theApp = (App)App.Current;
            OnNavigated(theApp.m_vin, null);
        }

        /// <summary>
        /// Real page initialization should happen here, as we have the information from our caller.
        /// </summary>
        /// <param name="sender">String containing VIN, or null.</param>
        /// <param name="e"></param>
        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if ((string)sender == null)
            {
                sender = "";
            }

            Uri vinURL = vinURL = new Uri("http://www.vinaudit.com/go.php?r=simontao&mobile=1&vin=" + (string)sender);

            if (IsVinKnownDemonstrationValue((string)sender))
            {
                // We are in demo mode, bypass to the sample VIN report.
                // This allows us to avoid needing a credit card for demo scenarios.
                vinURL = new Uri("http://www.vinaudit.com/report?id=sample");
            }

            VinAuditWebView.Navigate(vinURL);
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

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="navigationParameter">The parameter value passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested.
        /// </param>
        /// <param name="pageState">A dictionary of state preserved by this page during an earlier
        /// session.  This will be null the first time a page is visited.</param>
        protected override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache.  Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="pageState">An empty dictionary to be populated with serializable state.</param>
        protected override void SaveState(Dictionary<String, Object> pageState)
        {
        }

        private void VinAuditWebView_LoadCompleted(object sender, NavigationEventArgs e)
        {
            // Inject JS to partially de-brand the website. This is necessary because
            // we are already showing the VinAudit logo on the app header.
            string script = @"document.getElementById('default-logo').style.display='none';";
            string[] args = { script };
            try
            {
                // This could fail if the webpage was changed?
                VinAuditWebView.InvokeScript("eval", args);
            }
            catch (Exception err)
            {
                // Ignore
            }
        }
    }
}
