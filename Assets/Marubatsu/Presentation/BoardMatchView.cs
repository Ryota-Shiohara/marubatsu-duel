using UnityEngine;

namespace Marubatsu.Presentation
{
    public sealed class BoardMatchView : MonoBehaviour
    {
        const float TurnPreviewScale = 0.012f;
        const float TurnPreviewLift = 0.003f;
        const float TurnPreviewDegreesPerSecond = 60f;
        static readonly Color CircleColor = new Color32(125, 211, 252, 255);
        static readonly Color CrossColor = new Color32(253, 141, 99, 255);

        GameSession session;
        BoardTracker tracker;
        BoardPieceView pieces;
        VictorySequenceController victory;
        GameObject circlePrefab, crossPrefab;
        VictoryFighter circleFighter, crossFighter;
        Transform circlePreview, crossPreview, activePreview;
        Material ringMaterial;
        int displayedRevision = -1;

        public bool CanReplay => victory != null && victory.CanReplay;

        public void Initialize(GameSession game, BoardTracker boardTracker, Vector2 size,
            GameObject circle, GameObject cross, VictoryFighter circleCharacter, VictoryFighter crossCharacter,
            VictorySequenceController.Settings victorySettings = null)
        {
            session = game;
            tracker = boardTracker;
            circlePrefab = circle;
            crossPrefab = cross;
            circleFighter = circleCharacter;
            crossFighter = crossCharacter;

            var content = new GameObject("Match presentation");
            content.transform.SetParent(transform, false);
            pieces = content.AddComponent<BoardPieceView>();
            pieces.SetBoardSize(size);
            circlePreview = CreateTurnPreview(circle, content.transform, "Circle turn preview",
                BoardGeometry.TurnStandCenter(onLeft: true, targetSize: size));
            crossPreview = CreateTurnPreview(cross, content.transform, "Cross turn preview",
                BoardGeometry.TurnStandCenter(onLeft: false, targetSize: size));

            var shader = Shader.Find("Sprites/Default");
            if (shader != null) ringMaterial = new Material(shader);
            victory = content.AddComponent<VictorySequenceController>();
            victory.Build(pieces, size, victorySettings);
            if (tracker != null) tracker.TrackingChanged += SetVisible;
            session.OnSnapshotChanged += Apply;
            Apply(session.GetSnapshot());
            SetVisible(tracker == null || tracker.IsTracked);
        }

        static Transform CreateTurnPreview(GameObject prefab, Transform parent, string name, Vector3 center)
        {
            if (prefab == null) return null;

            var pivot = new GameObject(name);
            pivot.SetActive(false);
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = center + Vector3.up * TurnPreviewLift;

            var model = Instantiate(prefab, pivot.transform, false).transform;
            model.localPosition = Vector3.zero;
            model.localScale *= TurnPreviewScale;
            return pivot.transform;
        }

        void Update()
        {
            if (activePreview != null && activePreview.gameObject.activeInHierarchy)
                activePreview.Rotate(Vector3.up, TurnPreviewDegreesPerSecond * Time.deltaTime, Space.Self);
        }

        void Apply(BoardSnapshot snapshot)
        {
            if (snapshot.Revision <= displayedRevision) return;
            displayedRevision = snapshot.Revision;
            if (snapshot.Outcome == OutcomeType.Playing) victory.Stop();

            Transform nextPreview = snapshot.NextTurn switch
            {
                MarkType.Circle => circlePreview,
                MarkType.Cross => crossPreview,
                _ => null
            };
            if (activePreview != nextPreview)
            {
                if (activePreview != null) activePreview.gameObject.SetActive(false);
                activePreview = nextPreview;
                if (activePreview != null) activePreview.gameObject.SetActive(true);
            }

            for (int index = 0; index < snapshot.Cells.Length; index++)
            {
                MarkType mark = snapshot.Cells[index];
                GameObject prefab = mark == MarkType.Circle ? circlePrefab :
                    mark == MarkType.Cross ? crossPrefab : null;
                pieces.SetCell(index, prefab, mark == MarkType.Circle ? CircleColor : CrossColor, ringMaterial);
            }

            if (snapshot.Outcome != OutcomeType.Playing)
                victory.Begin(snapshot, circleFighter, crossFighter);
            else if ((tracker == null || tracker.IsTracked) && snapshot.LastMove.HasValue &&
                snapshot.LastMove.Value.CellIndex == 4)
                pieces.PlayAnimation(4, "Wave");
        }

        public void SetVisible(bool visible)
        {
            if (!visible) victory.Pause(true);
            pieces.gameObject.SetActive(visible);
            if (visible) victory.Pause(false);
        }

        void OnDestroy()
        {
            if (tracker != null) tracker.TrackingChanged -= SetVisible;
            if (session != null) session.OnSnapshotChanged -= Apply;
            if (ringMaterial != null) Destroy(ringMaterial);
        }
    }
}
