using UnityEngine;

namespace Marubatsu
{
    /// <summary>One-time logging; CameraFrameProvider exclusively owns format registration.</summary>
    public sealed class CameraFrameProbe : MonoBehaviour
    {
        CameraFrameProvider provider;
        bool reported;

        void Start()
        {
            provider = GetComponent<CameraFrameProvider>();
            if (provider == null) provider = gameObject.AddComponent<CameraFrameProvider>();
            provider.FrameReady += OnFrame;
        }

        void OnFrame(GrayCameraFrame frame)
        {
            if (reported) return;
            reported = true;
            Debug.Log($"[CameraFrameProbe] CPU copy OK: {frame.Width}x{frame.Height}, " +
                $"gray bytes={frame.Pixels.Length}, linear camera={frame.LinearCamera}. " +
                "Pixel/pose correspondence requires visual calibration.", this);
        }

        void OnDestroy()
        {
            if (provider != null) provider.FrameReady -= OnFrame;
        }
    }
}
