using System.Runtime.InteropServices;
using UnityEngine;
using Vuforia;

namespace Marubatsu
{
    /// <summary>Checks CPU access without opening a second camera or retaining SDK-owned buffers.</summary>
    public sealed class CameraFrameProbe : MonoBehaviour
    {
        VuforiaBehaviour behaviour;
        bool registered;
        bool reported;
        byte[] buffer;
        PixelFormat format = PixelFormat.GRAYSCALE;

        void Start()
        {
            behaviour = GetComponent<VuforiaBehaviour>();
            VuforiaApplication.Instance.OnVuforiaStarted += OnStarted;
            VuforiaApplication.Instance.OnVuforiaStopped += OnStopped;
            behaviour.World.OnStateUpdated += OnStateUpdated;
            if (VuforiaApplication.Instance.IsRunning) OnStarted();
        }

        void OnStarted()
        {
            if (registered) return;
            format = PixelFormat.GRAYSCALE;
            registered = behaviour.CameraDevice.SetFrameFormat(format, true);
            if (!registered)
            {
                format = PixelFormat.RGB888;
                registered = behaviour.CameraDevice.SetFrameFormat(format, true);
            }
            reported = false;
            if (!registered) Debug.LogWarning("[CameraFrameProbe] No supported CPU image format.", this);
        }

        void OnStopped() { registered = false; reported = false; }

        void OnStateUpdated()
        {
            if (!registered || reported) return;
            var image = behaviour.CameraDevice.GetCameraImage(format);
            if (image == null || image.Width <= 0 || image.Height <= 0 ||
                image.PixelBufferPtr == System.IntPtr.Zero) return;
            // Copy while this Vuforia state is current. The image may have row padding.
            int length = checked(image.Stride * image.Height);
            buffer = new byte[length];
            Marshal.Copy(image.PixelBufferPtr, buffer, 0, length);
            reported = true;
            Debug.Log($"[CameraFrameProbe] CPU copy OK: {image.Width}x{image.Height}, " +
                $"format={format}, stride={image.Stride}, bytes={buffer.Length}. " +
                "Image coordinates/orientation still require calibration.", this);
        }

        void OnDestroy()
        {
            VuforiaApplication.Instance.OnVuforiaStarted -= OnStarted;
            VuforiaApplication.Instance.OnVuforiaStopped -= OnStopped;
            if (behaviour != null)
            {
                behaviour.World.OnStateUpdated -= OnStateUpdated;
                if (registered && VuforiaApplication.Instance.IsRunning)
                    behaviour.CameraDevice.SetFrameFormat(format, false);
            }
        }
    }
}
