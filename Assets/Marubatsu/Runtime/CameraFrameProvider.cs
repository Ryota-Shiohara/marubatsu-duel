using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Vuforia;

namespace Marubatsu
{
    /// <summary>Pixels are borrowed until the callback returns; copy before retaining them.</summary>
    public readonly struct GrayCameraFrame
    {
        public readonly byte[] Pixels;
        public readonly int Width, Height;
        public readonly long Sequence;
        public readonly double Time;
        public readonly Vector2 FocalLength, PrincipalPoint;
        public readonly Matrix4x4 WorldToCamera;
        public readonly bool LinearCamera;
        public GrayCameraFrame(byte[] pixels, int width, int height, long sequence, double time,
            Vector2 focalLength, Vector2 principalPoint, Matrix4x4 worldToCamera, bool linearCamera)
        {
            Pixels = pixels; Width = width; Height = height; Sequence = sequence; Time = time;
            FocalLength = focalLength; PrincipalPoint = principalPoint;
            WorldToCamera = worldToCamera; LinearCamera = linearCamera;
        }
    }

    [RequireComponent(typeof(VuforiaBehaviour), typeof(Camera))]
    public sealed class CameraFrameProvider : MonoBehaviour
    {
        public event Action<GrayCameraFrame> FrameReady;
        [Range(1, 20)] public int FramesPerSecond = 8;
        VuforiaBehaviour behaviour;
        bool registered;
        PixelFormat format;
        byte[] raw, gray;
        double nextTime;
        long sequence;
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
            nextTime = 0;
            if (!registered) Debug.LogWarning("[CameraFrameProvider] No supported CPU image format.", this);
        }
        void OnStopped() { registered = false; nextTime = 0; }
        void OnStateUpdated()
        {
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            if (!registered || FrameReady == null || now < nextTime) return;
            var image = behaviour.CameraDevice.GetCameraImage(format);
            if (image == null || image.Width <= 0 || image.Height <= 0 || image.PixelBufferPtr == IntPtr.Zero) return;
            var intrinsics = behaviour.CameraDevice.GetCameraIntrinsics();
            if (intrinsics == null || intrinsics.Size.x <= 0 || intrinsics.Size.y <= 0) return;
            int channels = format == PixelFormat.GRAYSCALE ? 1 : 3;
            if (image.Stride < image.Width * channels) return;
            int length = checked(image.Stride * image.Height);
            if (raw == null || raw.Length != length) raw = new byte[length];
            if (gray == null || gray.Length != image.Width * image.Height) gray = new byte[image.Width * image.Height];
            Marshal.Copy(image.PixelBufferPtr, raw, 0, length);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    int source = y * image.Stride + x * channels;
                    gray[y * image.Width + x] = channels == 1 ? raw[source] :
                        (byte)((77 * raw[source] + 150 * raw[source + 1] + 29 * raw[source + 2]) >> 8);
                }
            var scale = new Vector2(image.Width / intrinsics.Size.x, image.Height / intrinsics.Size.y);
            var frame = new GrayCameraFrame(gray, image.Width, image.Height, ++sequence, now,
                Vector2.Scale(intrinsics.FocalLength, scale), Vector2.Scale(intrinsics.PrincipalPoint, scale),
                transform.worldToLocalMatrix, intrinsics.DistortionMode == DistortionMode.MODE_LINEAR);
            nextTime = now + 1.0 / Mathf.Max(1, FramesPerSecond);
            FrameReady.Invoke(frame);
        }
        void OnDestroy()
        {
            VuforiaApplication.Instance.OnVuforiaStarted -= OnStarted;
            VuforiaApplication.Instance.OnVuforiaStopped -= OnStopped;
            if (behaviour == null) return;
            behaviour.World.OnStateUpdated -= OnStateUpdated;
            if (registered && VuforiaApplication.Instance.IsRunning)
                behaviour.CameraDevice.SetFrameFormat(format, false);
        }
    }
}
