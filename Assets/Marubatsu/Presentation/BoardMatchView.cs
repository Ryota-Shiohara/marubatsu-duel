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
        ParticleSystem criticalBurst;
        Material lineMaterial;
        Vector2 boardSize;
        float lineWidth;
        float criticalTime;
        Color victoryColor;
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
            lineWidth = Mathf.Min(size.x, size.y) * 0.018f;
            winningLine.widthMultiplier = lineWidth;
            winningLine.numCapVertices = 4;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                lineMaterial = new Material(shader);
                winningLine.sharedMaterial = lineMaterial;
                criticalBurst = CreateCriticalBurst(content.transform);
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

            if (criticalTime <= 0f || !winningLine.enabled) return;
            criticalTime = Mathf.Max(0f, criticalTime - Time.deltaTime);
            UpdateVictoryLine(criticalTime / 0.65f);
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
                victoryColor = snapshot.Outcome == OutcomeType.CircleWon
                    ? new Color32(125, 211, 252, 255) : new Color32(253, 141, 99, 255);
                criticalTime = animate ? 0.65f : 0f;
                UpdateVictoryLine(animate ? 1f : 0f);
                if (animate)
                {
                    foreach (int cell in snapshot.WinningCells)
                        pieces.PlayAnimation(cell, "Victory");
                    PlayCriticalBurst(snapshot.WinningCells, lift);
                }
            }
            else if (animate && snapshot.LastMove.HasValue && snapshot.LastMove.Value.CellIndex == 4)
                pieces.PlayAnimation(4, "Wave");
        }

        ParticleSystem CreateCriticalBurst(Transform parent)
        {
            var burstObject = new GameObject("Critical victory burst");
            burstObject.transform.SetParent(parent, false);
            var burst = burstObject.AddComponent<ParticleSystem>();
            burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = burst.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(lineWidth * 0.6f, lineWidth * 1.4f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 48;
            var emission = burst.emission;
            emission.enabled = false;
            var shape = burst.shape;
            shape.enabled = false;
            burst.GetComponent<ParticleSystemRenderer>().sharedMaterial = lineMaterial;
            return burst;
        }

        void PlayCriticalBurst(int[] winningCells, Vector3 lift)
        {
            if (criticalBurst == null) return;
            criticalBurst.Play();
            float speed = BoardGeometry.CellSize(boardSize) * 1.8f;
            foreach (int cell in winningCells)
            {
                Vector3 center = BoardGeometry.CellCenter(cell, boardSize) + lift;
                for (int ray = 0; ray < 14; ray++)
                {
                    float angle = ray * (2f * Mathf.PI / 14f);
                    var particle = new ParticleSystem.EmitParams
                    {
                        position = center,
                        velocity = new Vector3(Mathf.Cos(angle), 0.24f, Mathf.Sin(angle)) * speed,
                        startColor = ray % 3 == 0 ? victoryColor : new Color32(255, 220, 128, 255)
                    };
                    criticalBurst.Emit(particle, 1);
                }
            }
        }

        void UpdateVictoryLine(float flash)
        {
            Color color = Color.Lerp(victoryColor, new Color32(255, 238, 174, 255), flash);
            winningLine.startColor = winningLine.endColor = color;
            winningLine.widthMultiplier = lineWidth * (1f + flash * 2f);
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
