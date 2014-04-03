using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
        NotInitialized,
        NotCapturing, // The camera is ready but it is not capturing yet.
        Capturing,    // The camera is actively capturing/previewing and can take a photo.
        InAsyncTask   // We are awaiting one or more async tasks. No other operations should be done.
    }

    /// <summary>
    /// Event handler for when BarcodeReaderManager has finished decoding a barcode.
    /// </summary>
    /// <param name="requestId">The unique ID of the task that was completed.</param>
    /// <param name="barcode">The decoded barcode. If no barcode could be detected, it is null.</param>
    delegate void BarcodeReadyDelegate(int requestId, string barcode);

    /// <summary>
    /// Manages a camera capture device (MediaCapture) and barcode reader (ZXing.NET).
    /// Can perform multiple simultaneous "barcode operations" using the thread pool.
    /// BarcodeReaderManager works on the assumption that camera photo capture and ZXing
    /// barcode reading are single-threaded and CPU bound, and uses a single thread from the pool per operation.
    /// All public methods must be called from the UI thread.
    /// </summary>
    class BarcodeCaptureManager
    {
        private BarcodeReadyDelegate m_completedHandler;
        private CoreDispatcher m_dispatcher;
        private CameraCaptureState m_captureState = CameraCaptureState.NotInitialized;
        private MediaCapture m_captureManager;
        private CaptureElement m_captureElement;

        // The unique ID of each decode task is just incremented by 1 each time.
        private int m_nextTaskId;
        private string m_deviceId;

        // Must be accessed in a multithread-safe manner.
        private int m_tasksInFlight;

        /// <summary>
        /// You cannot directly construct BarcodeCaptureManager. Instead, call (static) CreateAsync.
        /// </summary>
        private BarcodeCaptureManager(
            BarcodeReadyDelegate del,
            CoreDispatcher dispatcher,
            CaptureElement element,
            string deviceId
            )
        {
            m_dispatcher = dispatcher;
            m_completedHandler = del;
            m_captureElement = element;
            m_deviceId = deviceId;

            // 0 is sentinel value for "no task created".
            m_nextTaskId = 1;
        }

        /// <summary>
        /// Asynchronously creates and initializes the BarcodeCaptureManager. When this returns,
        /// the MediaCapture is already previewing to the CaptureElement control.
        /// </summary>
        /// <param name="del">Callback.</param>
        /// <param name="dispatcher">CoreDispatcher associated with the window.</param>
        /// <param name="element">The CaptureElement control that will display the preview.</param>
        /// <param name="deviceId">MediaCaptureInitializationSettings.VideoDeviceId.</param>
        /// <returns></returns>
        public async static Task<BarcodeCaptureManager> CreateAsync(
            BarcodeReadyDelegate del,
            CoreDispatcher dispatcher,
            CaptureElement element,
            string deviceId
            )
        {
            var bcm = new BarcodeCaptureManager(del, dispatcher, element, deviceId);
            await bcm.initializeAsync();
            await bcm.startCaptureAsync();
            return bcm;
        }

        /// <summary>
        /// TODO: How do we guarantee the async stop task has completed before destruction?
        /// </summary>
        ~BarcodeCaptureManager()
        {
            if (m_captureState == CameraCaptureState.Capturing)
            {
                TryStopCaptureAsync();
            }
        }

        private async Task initializeAsync()
        {
            if (m_captureState != CameraCaptureState.NotInitialized)
            {
                throw new InvalidOperationException();
            }

            m_captureState = CameraCaptureState.InAsyncTask;

            var settings = new MediaCaptureInitializationSettings();
            settings.VideoDeviceId = m_deviceId;
            settings.PhotoCaptureSource = PhotoCaptureSource.Auto;
            settings.StreamingCaptureMode = StreamingCaptureMode.Video;

            m_captureManager = new MediaCapture();
            await m_captureManager.InitializeAsync(settings);
            m_captureElement.Source = m_captureManager;

            m_captureState = CameraCaptureState.NotCapturing;
        }

        /// <summary>
        /// Requests a barcode from the current camera preview.
        /// </summary>
        /// <returns>A unique ID for the decode task. 0 means no task was created.</returns>
        public int RequestBarcodeNow()
        {
            // State could be InAsyncTask because we are stopping, which waits for all in flight tasks
            // to complete before actually stopping.
            Debug.Assert(m_captureState == CameraCaptureState.Capturing ||
                         m_captureState == CameraCaptureState.InAsyncTask);

            // Immediately indicate that we have requested a capture task, even before it starts.
            // No need for Interlocked.Increment because we are only touching this variable on the UI thread.
            m_tasksInFlight++;
            int taskId = m_nextTaskId++;

            var workItem = ThreadPool.RunAsync(
                async (source) =>
                {
                    // We are relying on checks elsewhere in the class to ensure we are in valid state.
                    Debug.Assert(m_captureState == CameraCaptureState.Capturing);
                    string barcode = null;
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
                            var reader = new BarcodeReader();

                            reader.Options.TryHarder = true;
                            reader.AutoRotate = false;
                            reader.Options.PossibleFormats = new List<BarcodeFormat>()
                            {
                                BarcodeFormat.CODE_39 // Used by VINs
                            };

                            var result = reader.Decode(
                                pixels,
                                (int)decoder.PixelWidth,
                                (int)decoder.PixelHeight,
#if WINDOWS_PHONE_APP // The version of ZXing.NET appears to be slightly different when we compile from source.
                                RGBLuminanceSource.BitmapFormat.BGRA32
#else
                                BitmapFormat.BGRA32
#endif
                                );



                            if (result != null)
                            {
                                barcode = result.Text;
                            }
                        }
                        catch (Exception)
                        {
                            // Errors = just return null barcode.
                        }
                    }

                    // We don't decrement m_tasksInFlight until we've done the callback.
                    signalBarcodeReady(taskId, barcode);
                });

            return taskId;
        }

        private async Task startCaptureAsync()
        {
            if (m_captureState != CameraCaptureState.NotCapturing)
            {
                throw new InvalidOperationException();
            }

            m_captureState = CameraCaptureState.InAsyncTask;
            await m_captureManager.StartPreviewAsync();
            m_captureState = CameraCaptureState.Capturing;
        }

        /// <summary>
        /// Stops the MediaCapture preview. Does nothing if it is not currently Capturing.
        /// This is called by the destructor.
        /// </summary>
        public async Task TryStopCaptureAsync()
        {
            if (m_captureState == CameraCaptureState.Capturing)
            {
                m_captureState = CameraCaptureState.InAsyncTask;

                // TODO: this whole async construct/destroy on client side state is a mess...
                // Just do a spinlock to wait for all capture tasks to finish
                while (m_tasksInFlight > 0)
                {
                    await Task.Delay(50);
                }

                await m_captureManager.StopPreviewAsync();
                m_captureState = CameraCaptureState.NotCapturing;
            }
        }

        /// <summary>
        /// Ensures the completed handler is called from the Dispatcher thread.
        /// Returns immediately (does not await).
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="barcode">If no barcode was found, is null.</param>
        private void signalBarcodeReady(int requestId, string barcode)
        {
// Async method is not awaited by design.
#pragma warning disable 4014
            m_dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    m_tasksInFlight--;
                    Debug.Assert(m_tasksInFlight >= 0);
                    m_completedHandler(requestId, barcode);
                });
#pragma warning restore 4014
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
            try
            {
                await m_captureManager.CapturePhotoToStreamAsync(props, stream);
            }
            catch (Exception)
            {
                // TODO: Swalling errors here....
                stream = null;
            }
            return stream;
        }
    }
}
