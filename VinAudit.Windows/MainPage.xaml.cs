using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234237

namespace VinAudit
{
    /// <summary>
    /// VIN input page. The first page that the user sees when they launch the app.
    /// </summary>
    public sealed partial class MainPage : VinAudit.Common.LayoutAwarePage
    {
        // ************** Member variables *********************
        // We only hold on to one BarcodeCaptureManager at a time for the active camera.
        // Do not destroy/construct this manually, use the helper functions.
        private BarcodeCaptureManager m_captureManager;
        private int m_activeCameraIndex;
        private int m_numCameras;
        // This is different from whether m_captureManager != null, because when we leave visibility,
        // we set m_captureManager = null, but the user's intent needs to be preserved when we come back.
        private bool m_hasUserEnabledCamera;

        //private DispatcherTimer m_timer;
        private string m_canonicalizedVin; // What is actually sent to VinAudit

        private string VIN_IS_CORRECT_LENGTH_TEXT = "Correct length: ";
        private string VIN_HAS_VALID_CHARACTERS_TEXT = "Valid characters: ";
        private string VIN_IS_CHECKSUM_VALID_TEXT = "Valid checksum: ";
        private string VIN_CONDITION_SUCCESS_TEXT = "✓";
        private string VIN_CONDITION_FAILURE_TEXT = "✗";
        private string VIN_CONDITION_UNKNOWN_TEXT = "?";
        private SolidColorBrush VIN_CONDITION_SUCCESS_COLOR = new SolidColorBrush(Windows.UI.Colors.DarkGreen);
        private SolidColorBrush VIN_CONDITION_FAILURE_COLOR = new SolidColorBrush(Windows.UI.Colors.DarkRed);
        private SolidColorBrush VIN_CONDITION_UNKNOWN_COLOR = new SolidColorBrush(Windows.UI.Colors.Gray);
        private string CAMERA_TURN_ON_TEXT = "Tap to scan VIN with camera";
        private string CAMERA_MOVE_NEXT_TEXT = "Tap to switch camera";
        private string CAMERA_TURN_OFF_TEXT = "Tap to turn off camera";
        private string CAMERA_ERROR_TEXT = "Camera not available";

        public MainPage()
        {
            this.InitializeComponent();
            //m_timer = new DispatcherTimer();
            Window.Current.VisibilityChanged += OnWindowVisibilityChanged;
            // Wait until LoadState is called to do actual page setup.
        }

        private async void OnWindowVisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            if (e.Visible && m_hasUserEnabledCamera)
            {
                await TryInitializeCaptureAsync(m_activeCameraIndex);
            }

            if (!e.Visible && m_hasUserEnabledCamera)
            {
                await TryShutdownCaptureAsync();
            }
        }

        /// <summary>
        /// Sets page to default parameters, as if it were visited for the first time.
        /// </summary>
        private void ResetPageState()
        {
            VinInputTextbox.Text = "";
            SetDefaultVinValidationText();

            // Until we determine the actual system camera capability, just show this.
            UseCameraTextBlock.Text = CAMERA_TURN_ON_TEXT;
        }

        private void SetDefaultVinValidationText()
        {
            VinIsCorrectLengthTextBlock.Foreground = VIN_CONDITION_UNKNOWN_COLOR;
            VinIsCorrectLengthTextBlock.Text = VIN_IS_CORRECT_LENGTH_TEXT + VIN_CONDITION_UNKNOWN_TEXT;
            VinHasValidCharactersTextBlock.Foreground = VIN_CONDITION_UNKNOWN_COLOR;
            VinHasValidCharactersTextBlock.Text = VIN_HAS_VALID_CHARACTERS_TEXT + VIN_CONDITION_UNKNOWN_TEXT;
            VinIsChecksumValidTextBlock.Foreground = VIN_CONDITION_UNKNOWN_COLOR;
            VinIsChecksumValidTextBlock.Text = VIN_IS_CHECKSUM_VALID_TEXT + VIN_CONDITION_UNKNOWN_TEXT;
        }

        /// <summary>
        /// BarcodeReadyDelegate function, registered as callback for m_reader.
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="barcode"></param>
        public void OnBarcodeReady(int requestId, string barcode)
        {
            if (barcode != null)
            {
                VinInputTextbox.Text = barcode;
            }

            Debug.WriteLine("OnBarcodeReady: " + barcode);

            if (m_hasUserEnabledCamera && m_captureManager != null)
            {
                m_captureManager.RequestBarcodeNow();
            }
        }

        /// <summary>
        /// Does not modify m_hasUserEnabledCamera. Right now just requests a single
        /// barcode task at a time.
        /// </summary>
        /// <param name="cameraIndex"></param>
        private async Task TryInitializeCaptureAsync(int cameraIndex)
        {
            string id = await CameraEnumerator.TryGetVideoDeviceIdAsync(m_activeCameraIndex);
            if (id != null)
            {
                m_captureManager = await BarcodeCaptureManager.CreateAsync(
                    OnBarcodeReady,
                    Dispatcher,
                    CapturePreview,
                    id
                    );

                // Ignore the task ID.
                m_captureManager.RequestBarcodeNow();
            }
            else
            {
                await DisableCameraSelectionAsync();
            }
        }

        /// <summary>
        /// Attempts to stop camera capture, and deletes m_captureManager. Does not touch m_hasUserEnabledCamera.
        /// </summary>
        /// <returns></returns>
        private async Task TryShutdownCaptureAsync()
        {
            if (m_captureManager != null)
            {
                await m_captureManager.TryStopCaptureAsync();
                m_captureManager = null;
            }
        }

        /// <summary>
        /// There was some error condition that means cameras are not valid/available.
        /// </summary>
        private async Task DisableCameraSelectionAsync()
        {
            CaptureBorder.IsTapEnabled = false;
            UseCameraTextBlock.Text = CAMERA_ERROR_TEXT;
            m_hasUserEnabledCamera = false;
            await TryShutdownCaptureAsync();
        }

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// We are assuming that LoadState is ALWAYS called when the page is loaded.
        /// </summary>
        /// <param name="navigationParameter">The parameter value passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested.
        /// </param>
        /// <param name="pageState">A dictionary of state preserved by this page during an earlier
        /// session.  This will be null the first time a page is visited.</param>
        protected async override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
            ResetPageState();

            // Determine if cameras are available.
            m_numCameras = await CameraEnumerator.QueryCamerasAsync();
            if (m_numCameras == 0)
            {
                await DisableCameraSelectionAsync();
            }

            if (pageState != null)
            {
                object value;
                if (pageState.TryGetValue("VinText", out value))
                {
                    VinInputTextbox.Text = (string)value;
                }
            }
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache.  Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="pageState">An empty dictionary to be populated with serializable state.</param>
        protected override void SaveState(Dictionary<String, Object> pageState)
        {
            pageState.Add("VinText", VinInputTextbox.Text);
        }


        private async void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (m_hasUserEnabledCamera)
            {
                await TryInitializeCaptureAsync(m_activeCameraIndex);
            }
        }

        // ************** Control event handlers ****************
        private async void SubmitVinButton_Click(object sender, RoutedEventArgs e)
        {
            if (m_captureManager != null)
            {
                await TryShutdownCaptureAsync();
            }

            // Apparently you must specifically cast the parameter to type object...
            this.Frame.Navigate(typeof(WebViewPage), (object)m_canonicalizedVin);
        }

        private void VinInputTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            VinValidationInfo info = VinDecoder.ValidateVin(VinInputTextbox.Text);

            if (VinInputTextbox.Text.Length == 0)
            {
                SetDefaultVinValidationText();
                return;
            }

            // The app makes a friendly assumption that a VIN that has invalid characters but
            // was successfully canonicalized is considered to have valid characters.
            if (info.IsCorrectLength)
            {
                VinIsCorrectLengthTextBlock.Foreground = VIN_CONDITION_SUCCESS_COLOR;
                VinIsCorrectLengthTextBlock.Text = VIN_IS_CORRECT_LENGTH_TEXT + VIN_CONDITION_SUCCESS_TEXT;
            }
            else
            {
                VinIsCorrectLengthTextBlock.Foreground = VIN_CONDITION_FAILURE_COLOR;
                VinIsCorrectLengthTextBlock.Text = VIN_IS_CORRECT_LENGTH_TEXT + VIN_CONDITION_FAILURE_TEXT;
            }

            m_canonicalizedVin = info.CanonicalizedString;
            if (info.CanonicalizedString != null)
            {
                VinHasValidCharactersTextBlock.Foreground = VIN_CONDITION_SUCCESS_COLOR;
                VinHasValidCharactersTextBlock.Text = VIN_HAS_VALID_CHARACTERS_TEXT + VIN_CONDITION_SUCCESS_TEXT;
            }
            else
            {
                VinHasValidCharactersTextBlock.Foreground = VIN_CONDITION_FAILURE_COLOR;
                VinHasValidCharactersTextBlock.Text = VIN_HAS_VALID_CHARACTERS_TEXT + VIN_CONDITION_FAILURE_TEXT;
            }

            if (info.IsChecksumValidAfterCanonicalization)
            {
                VinIsChecksumValidTextBlock.Foreground = VIN_CONDITION_SUCCESS_COLOR;
                VinIsChecksumValidTextBlock.Text = VIN_IS_CHECKSUM_VALID_TEXT + VIN_CONDITION_SUCCESS_TEXT;
            }
            else
            {
                VinIsChecksumValidTextBlock.Foreground = VIN_CONDITION_FAILURE_COLOR;
                VinIsChecksumValidTextBlock.Text = VIN_IS_CHECKSUM_VALID_TEXT + VIN_CONDITION_FAILURE_TEXT;
            }
        }

        private async void CaptureBorder_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (!m_hasUserEnabledCamera)
            {
                m_activeCameraIndex = -1;
            }

            // Set "camera off" state on every camera index change to be safe.
            m_hasUserEnabledCamera = false;
            if (m_captureManager != null)
            {
                await TryShutdownCaptureAsync();
            }

            // Transition to new camera index. If we fail this check, it means we were at the last
            // camera index already, so we just stay at the camera off state.
            if (++m_activeCameraIndex < m_numCameras)
            {
                await TryInitializeCaptureAsync(m_activeCameraIndex);
                m_hasUserEnabledCamera = true;
            }

            // Changing the UI text follows a slightly different state machine.
            if (m_activeCameraIndex < m_numCameras - 1)
            {
                UseCameraTextBlock.Text = CAMERA_MOVE_NEXT_TEXT;
            }
            else if (m_activeCameraIndex == m_numCameras - 1)
            {
                UseCameraTextBlock.Text = CAMERA_TURN_OFF_TEXT;
            }
            else
            {
                UseCameraTextBlock.Text = CAMERA_TURN_ON_TEXT;
            }
        }
    }
}
