using UnityEngine;
using Vuforia;
using Marubatsu.Presentation;

namespace Marubatsu
{
    /// <summary>Creates the board as an Instant Image Target from the project texture.</summary>
    public sealed class PhaseOneBootstrap : MonoBehaviour
    {
        public Texture2D BoardTexture;
        [SerializeField] VictorySequenceController.Settings victorySettings = new VictorySequenceController.Settings();
        ImageTargetBehaviour target;
        BoardTracker tracker;
        BoardImageExtractor extractor;
        BoardMatchView matchView;
        TicTacToeGameManager game;
        MatchHud hud;
        ArcadeAudio matchAudio;
        GameObject[] roster;
        GameObject selectedCircle, selectedCross;
        VictoryFighter circleFighter, crossFighter;

        void Start()
        {
            var characters = Resources.Load<MatchCharacterRoster>(MatchCharacterRoster.ResourcePath);
            if (characters == null || !characters.IsComplete)
            {
                Debug.LogError("[Marubatsu] Character roster is missing a prefab or portrait.", this);
                return;
            }

            game = GetComponent<TicTacToeGameManager>();
            if (game == null) game = gameObject.AddComponent<TicTacToeGameManager>();
            if (!TryGetComponent<AudioListener>(out _)) gameObject.AddComponent<AudioListener>();
            matchAudio = gameObject.AddComponent<ArcadeAudio>();
            matchAudio.Initialize(game.Session);
            hud = gameObject.AddComponent<MatchHud>();
            roster = characters.Prefabs;
            hud.Initialize(game.Session, characters.Portraits, matchAudio, BeginMatch, EndMatch,
                () => matchView != null && matchView.CanReplay);
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
            tracker = target.gameObject.AddComponent<BoardTracker>();
            extractor = target.gameObject.AddComponent<BoardImageExtractor>();
            hud.AttachBoard(tracker, extractor, GetComponent<Camera>(), target.GetSize());
            AttachMatch();
            Debug.Log("[Phase1] Instant target ready. Print the board at 270 x 180 mm.", this);
        }

        void BeginMatch(int circle, int cross)
        {
            selectedCircle = roster[circle];
            selectedCross = roster[cross];
            circleFighter = (VictoryFighter)circle;
            crossFighter = (VictoryFighter)cross;
            AttachMatch();
            matchAudio.BeginBattle();
        }

        void AttachMatch()
        {
            if (target == null || matchView != null || selectedCircle == null || selectedCross == null) return;

            var matchObject = new GameObject("Board match");
            matchObject.transform.SetParent(target.transform, false);
            matchView = matchObject.AddComponent<BoardMatchView>();
            matchView.Initialize(game.Session, tracker, target.GetSize(),
                selectedCircle, selectedCross, circleFighter, crossFighter, victorySettings);
            extractor.AttachGame(game.Session);
        }

        void EndMatch()
        {
            matchAudio.LeaveBattle();
            selectedCircle = selectedCross = null;
            if (extractor != null) extractor.AttachGame(null);
            if (matchView != null)
            {
                matchView.gameObject.SetActive(false);
                Destroy(matchView.gameObject);
                matchView = null;
            }
            game.Session.ResetGame();
        }

        void OnDestroy()
        {
            VuforiaApplication.Instance.OnVuforiaStarted -= OnStarted;
            if (target != null) Destroy(target.gameObject);
        }
    }
}
