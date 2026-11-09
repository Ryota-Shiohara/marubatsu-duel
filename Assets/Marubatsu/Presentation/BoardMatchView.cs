using UnityEngine;

namespace Marubatsu.Presentation
{
    public sealed class BoardMatchView : MonoBehaviour
    {
        const float TurnPreviewScale = 0.012f;
        const float TurnPreviewLift = 0.003f;
        const float TurnPreviewDegreesPerSecond = 60f;

        GameSession session;
        BoardTracker tracker;
        BoardPieceView pieces;
        GameObject circlePrefab;
        GameObject crossPrefab;
        Transform circlePreview;
        Transform crossPreview;
        Transform activePreview;
        LineRenderer winningLine;
        Material lineMaterial;
        Vector2 boardSize;
        int displayedRevision = -1;

        public void Initialize(GameSession game, BoardTracker boardTracker, Vector2 size,
            GameObject circle, GameObject cross)
        {
            session = game;
            tracker = boardTracker;
            boardSize = size;
            circlePrefab = circle;
            crossPrefab = cross;

            var content = new GameObject("Match presentation");
            content.transform.SetParent(transform, false);
            pieces = content.AddComponent<BoardPieceView>();
            pieces.SetBoardSize(size);
            circlePreview = CreateTurnPreview(circle, content.transform, "Circle turn preview",
                BoardGeometry.TurnStandCenter(onLeft: true, targetSize: size));
            crossPreview = CreateTurnPreview(cross, content.transform, "Cross turn preview",
                BoardGeometry.TurnStandCenter(onLeft: false, targetSize: size));

            var lineObject = new GameObject("Winning line");
            lineObject.transform.SetParent(content.transform, false);
            winningLine = lineObject.AddComponent<LineRenderer>();
            winningLine.enabled = false;
            winningLine.useWorldSpace = false;
            winningLine.positionCount = 2;
            winningLine.widthMultiplier = Mathf.Min(size.x, size.y) * 0.018f;
            winningLine.numCapVertices = 4;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                lineMaterial = new Material(shader) { color = Color.cyan };
                winningLine.sharedMaterial = lineMaterial;
            }

            tracker.TrackingChanged += SetVisible;
            session.OnSnapshotChanged += OnSnapshotChanged;
            SetVisible(tracker.IsTracked);
            Apply(session.GetSnapshot(), animate: false);
        }

        void OnSnapshotChanged(BoardSnapshot snapshot) => Apply(snapshot, tracker.IsTracked);

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

        void Apply(BoardSnapshot snapshot, bool animate)
        {
            if (snapshot.Revision <= displayedRevision) return;
            displayedRevision = snapshot.Revision;

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
                pieces.SetCell(index, prefab);
            }

            winningLine.enabled = snapshot.WinningCells.Length == 3;
            if (winningLine.enabled)
            {
                Vector3 lift = Vector3.up * (Mathf.Min(boardSize.x, boardSize.y) * 0.025f);
                winningLine.SetPosition(0, BoardGeometry.CellCenter(snapshot.WinningCells[0], boardSize) + lift);
                winningLine.SetPosition(1, BoardGeometry.CellCenter(snapshot.WinningCells[2], boardSize) + lift);
                if (animate)
                    foreach (int cell in snapshot.WinningCells)
                        pieces.PlayAnimation(cell, "Victory");
            }
            else if (animate && snapshot.LastMove.HasValue && snapshot.LastMove.Value.CellIndex == 4)
                pieces.PlayAnimation(4, "Wave");
        }

        void SetVisible(bool visible)
        {
            pieces.gameObject.SetActive(visible);
        }

        void OnDestroy()
        {
            if (tracker != null) tracker.TrackingChanged -= SetVisible;
            if (session != null) session.OnSnapshotChanged -= OnSnapshotChanged;
            if (lineMaterial != null) Destroy(lineMaterial);
        }
    }
}
