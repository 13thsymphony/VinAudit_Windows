using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;

namespace VinAudit.uwp
{
    /// <summary>
    /// Gets info about the camera devices on the system. Currently does not respond
    /// to changes in device configuration, but does handle errors.
    /// </summary>
    class CameraEnumerator
    {
        private static List<string> s_videoDeviceIds = new List<string>();
        private static int s_deviceIndex;

        /// <summary>
        /// Updates the list of available camera devices.
        /// </summary>
        /// <returns>The number of available devices.</returns>
        public async static Task<int> QueryCamerasAsync()
        {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            foreach (var device in devices)
            {
                if (device.IsEnabled)
                {
                    s_videoDeviceIds.Add(device.Id);
                }
            }

            s_deviceIndex = 0;
            return s_videoDeviceIds.Count;
        }

        /// <summary>
        /// Use the string with MediaCaptureInitializationSettings.VideoDeviceId.
        /// </summary>
        /// <returns>If any error occurs (e.g. the device is not available anymore), returns null.</returns>
        public async static Task<string> TryGetVideoDeviceIdAsync(int index)
        {
            if (index >= s_videoDeviceIds.Count)
            {
                return null;
            }

            string result = s_videoDeviceIds[index];
            try
            {
                var info = await DeviceInformation.CreateFromIdAsync(result);
                if (!info.IsEnabled)
                {
                    result = null;
                }
            }
            catch (Exception)
            {
                result = null;
            }

            return result;
        }

        /// <summary>
        /// Cycles through the available cameras.
        /// Use the string with MediaCaptureInitializationSettings.VideoDeviceId.
        /// </summary>
        /// <returns>If any error occurs (e.g. the device is not available anymore), returns null.</returns>
        public async static Task<string> TryGetNextVideoDeviceIdAsync()
        {
            if (s_videoDeviceIds.Count == 0)
            {
                return null;
            }

            Debug.Assert(s_deviceIndex < s_videoDeviceIds.Count);

            string result = await TryGetVideoDeviceIdAsync(s_deviceIndex++);
            if (s_deviceIndex == s_videoDeviceIds.Count)
            {
                s_deviceIndex = 0;
            }

            return result;
        }
    }
}
