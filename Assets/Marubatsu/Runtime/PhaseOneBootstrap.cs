using UnityEngine;
using Vuforia;
using Marubatsu.Presentation;

namespace Marubatsu
{
    /// <summary>Instant target for initial experiments; replace with a rated database target later.</summary>
    public sealed class PhaseOneBootstrap : MonoBehaviour
    {
        public Texture2D BoardTexture;
        public GameObject CirclePrefab;
        public GameObject CrossPrefab;
        ImageTargetBehaviour target;
        TicTacToeGameManager game;
        MatchHud hud;

        void Start()
        {
            game = GetComponent<TicTacToeGameManager>();
            if (game == null) game = gameObject.AddComponent<TicTacToeGameManager>();
            hud = gameObject.AddComponent<MatchHud>();
            hud.Initialize(game.Session);
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
            var tracker = target.gameObject.AddComponent<BoardTracker>();
            var extractor = target.gameObject.AddComponent<BoardImageExtractor>();
            extractor.AttachGame(game.Session);
            hud.AttachBoard(tracker, extractor);
            if (CirclePrefab != null && CrossPrefab != null)
                target.gameObject.AddComponent<BoardMatchView>().Initialize(
                    game.Session, tracker, target.GetSize(), CirclePrefab, CrossPrefab);
            else
                Debug.LogWarning("[Marubatsu] Assign both character prefabs on PhaseOneBootstrap.", this);
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
