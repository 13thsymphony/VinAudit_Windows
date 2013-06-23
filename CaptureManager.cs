using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;

namespace VinAudit
{
    /// <summary>
    /// Handles MediaCapture functionality. Manages the CaptureElement control that previews the camera.
    /// </summary>
    class CaptureManager
    {
        private CameraCaptureState m_captureState = CameraCaptureState.Unknown;
        private MediaCapture m_captureManager;
        private CaptureElement m_captureElement;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element">The CaptureElement control that will display the preview.</param>
        public CaptureManager(CaptureElement element)
        {
            m_captureElement = element;
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
                m_captureState = CameraCaptureState.EnabledNotCapturing;

                m_captureManager = new MediaCapture();
                await m_captureManager.InitializeAsync();
                m_captureElement.Source = m_captureManager;
                await m_captureManager.StartPreviewAsync();

                // TODO: do a proper check
                m_captureState = CameraCaptureState.EnabledCapturing;
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
        /// 
        /// </summary>
        /// <returns>Stream with the photo. If no photo is available, this is null.</returns>
        public async Task<IRandomAccessStream> GetPhotoAsync()
        {
            InMemoryRandomAccessStream stream = null;
            try
            {
                if (m_captureState == CameraCaptureState.EnabledCapturing)
                {
                    stream = new InMemoryRandomAccessStream();
                    // PNG is faster to decode and encode than JPEG/JPEG-XR, at the cost of memory usage.
                    ImageEncodingProperties props = ImageEncodingProperties.CreatePng();
                    await m_captureManager.CapturePhotoToStreamAsync(props, stream);
                }
            }
            catch (Exception)
            {
                // Ignore any error and just pass an empty stream.
                stream = null;
            }

            return stream;
        }
    }
}
