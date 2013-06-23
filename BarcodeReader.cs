using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using ZXing;

namespace VinAudit
{
    enum CameraCaptureState
    {
        Unknown,             // Camera capabilities haven't been queried yet.
        NotAvailable,        // There is no valid capture device.
        Disabled,            // The user has disabled using the camera.
        EnabledNotCapturing, // The user has requested the camera but it is not capturing yet.
        EnabledCapturing     // The camera is actively capturing/previewing and can take a photo.
    }

    // TODO: This class should really be an RAII wrapper around MediaCapture. We're already doing "new MediaCapture" every time
    // we call StartCaptureAsync. RAII wrapper allows us to be safe every time the containing Page is destroyed and reloaded.
    /// <summary>
    /// Event handler for when BarcodeReaderManager has finished decoding a barcode.
    /// </summary>
    /// <param name="requestId">The unique ID of the task that was completed.</param>
    /// <param name="barcode">The decoded barcode. If no barcode could be detected, it is null.</param>
    delegate void BarcodeReadyDelegate(uint requestId, string barcode);

    /// <summary>
    /// Manages the camera and barcode reader (ZXing.NET). Can perform multiple simultaneous "barcode operations"
    /// using the thread pool. BarcodeReaderManager works on the assumption that camera photo capture and ZXing
    /// barcode reading are single-threaded and CPU bound, and uses a single thread from the pool per operation.
    /// </summary>
    class BarcodeCaptureManager
    {
        private BarcodeReadyDelegate m_completedHandler;
        private CoreDispatcher m_dispatcher;
        private CameraCaptureState m_captureState = CameraCaptureState.Unknown;
        private MediaCapture m_captureManager;
        private CaptureElement m_captureElement;

        // The unique ID of each decode task is just incremented by 1 each time.
        private uint _nextTaskId;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="del">Callback.</param>
        /// <param name="dispatcher">CoreDispatcher associated with the window.</param>
        /// <param name="element">The CaptureElement control that will display the preview.</param>
        public BarcodeCaptureManager(
            BarcodeReadyDelegate del,
            CoreDispatcher dispatcher,
            CaptureElement element
            )
        {
            m_dispatcher = dispatcher;
            m_completedHandler = del;
            m_captureElement = element;
            // 0 is sentinel value for "no task created".
            _nextTaskId = 1;
        }

        /// <summary>
        /// Requests a barcode from the current camera preview.
        /// </summary>
        /// <returns>A unique ID for the decode task. 0 means no task was created.</returns>
        public uint RequestBarcodeNow()
        {
            if (m_captureState != CameraCaptureState.EnabledCapturing)
            {
                return 0;
            }

            uint taskId = _nextTaskId++;

            var workItem = ThreadPool.RunAsync(
                async (source) =>
                {
                    string barcode = null;
                    if (m_captureState == CameraCaptureState.EnabledCapturing)
                    {
                        using (IRandomAccessStream stream = await getPhotoAsync())
                        {
                            try
                            {
                                var decoder = await BitmapDecoder.CreateAsync(stream);

                                // WIC decoders typically prefer outputting to BGRA, so this avoids a format conversion.
                                // Also avoids a needless color management step and rotation transform.
                                var pixelProvider = await decoder.GetPixelDataAsync(
                                    BitmapPixelFormat.Bgra8,
                                    BitmapAlphaMode.Ignore,
                                    new BitmapTransform(),
                                    ExifOrientationMode.IgnoreExifOrientation,
                                    ColorManagementMode.DoNotColorManage
                                    );

                                var pixels = pixelProvider.DetachPixelData();
                                var reader = new BarcodeReader
                                {
                                    TryHarder = true,
                                    PossibleFormats = new List<BarcodeFormat>()
                                    {
                                        BarcodeFormat.CODE_39 // Used by VINs
                                    }
                                };

                                var result = reader.Decode(
                                    pixels,
                                    (int)decoder.PixelWidth,
                                    (int)decoder.PixelHeight,
                                    BitmapFormat.BGRA32
                                    );

                                if (result != null)
                                {
                                    barcode = result.Text;
                                }
                            }
                            catch (Exception e)
                            {
                                // Errors = just return null barcode
                            }
                        }
                    }

                    signalBarcodeReady(taskId, barcode);
                });

            return taskId;
        }

        /// <summary>
        /// Starts the MediaCapture preview.
        /// Does nothing if it is already previewing or if a valid device is not available.
        /// </summary>
        public async Task StartCaptureAsync()
        {
            if ((m_captureState != CameraCaptureState.NotAvailable) &&
                (m_captureState != CameraCaptureState.EnabledCapturing))
            {
                // TODO: really we need an EnabledInAsyncTask state to prevent race conditions.
                m_captureState = CameraCaptureState.EnabledCapturing;

                m_captureManager = new MediaCapture();
                await m_captureManager.InitializeAsync();
                m_captureElement.Source = m_captureManager;
                await m_captureManager.StartPreviewAsync();
            }
        }

        /// <summary>
        /// Stops the MediaCapture preview.
        /// </summary>
        public async Task StopCaptureAsync()
        {
            if (m_captureState == CameraCaptureState.EnabledCapturing)
            {
                m_captureState = CameraCaptureState.Disabled;
                await m_captureManager.StopPreviewAsync();
            }
        }

        /// <summary>
        /// Ensures the completed handler is called from the Dispatcher thread.
        /// Returns immediately (does not await).
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="barcode">If no barcode was found, is null.</param>
        private void signalBarcodeReady(uint requestId, string barcode)
        {
            m_dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    m_completedHandler(requestId, barcode);
                });
        }

        /// <summary>
        /// Gets photo from MediaCapture. No error state handling.
        /// TODO: We do need to catch errors here, right?
        /// </summary>
        private async Task<IRandomAccessStream> getPhotoAsync()
        {
            InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();

            // PNG is faster to decode and encode than JPEG/JPEG-XR, at the cost of memory usage.
            ImageEncodingProperties props = ImageEncodingProperties.CreatePng();
            await m_captureManager.CapturePhotoToStreamAsync(props, stream);
            return stream;
        }
    }
}
