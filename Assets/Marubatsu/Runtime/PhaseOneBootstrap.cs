using UnityEngine;
using Vuforia;

namespace Marubatsu
{
    /// <summary>Instant target for initial experiments; replace with a rated database target later.</summary>
    public sealed class PhaseOneBootstrap : MonoBehaviour
    {
        public Texture2D BoardTexture;
        ImageTargetBehaviour target;

        void Start()
        {
            VuforiaApplication.Instance.OnVuforiaStarted += OnStarted;
            if (VuforiaApplication.Instance.IsRunning) OnStarted();
        }

        void OnStarted()
        {
            if (target != null) return;
            if (BoardTexture == null)
            {
                Debug.LogError("[Phase1] Board texture is missing.", this);
                return;
            }
            target = VuforiaBehaviour.Instance.ObserverFactory.CreateImageTarget(
                BoardTexture, BoardGeometry.TargetWidthMeters, "MarubatsuBoard_v1");
            if (target == null)
            {
                Debug.LogError("[Phase1] Image Target creation failed. Check license and texture.", this);
                return;
            }
            target.gameObject.AddComponent<BoardTracker>();
            target.gameObject.AddComponent<BoardPreview>();
            Debug.Log("[Phase1] Instant target ready. Print the board at 180 mm width. " +
                "This target has not been rated in Target Manager.", this);
        }

        void OnDestroy()
        {
            VuforiaApplication.Instance.OnVuforiaStarted -= OnStarted;
            if (target != null) Destroy(target.gameObject);
        }
    }
}
