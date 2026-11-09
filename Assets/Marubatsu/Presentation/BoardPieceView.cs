using UnityEngine;

namespace Marubatsu.Presentation
{
    /// <summary>Attach to a transform aligned with the center and axes of the board.</summary>
    public sealed class BoardPieceView : MonoBehaviour
    {
        [SerializeField] Vector2 boardSize = new Vector2(BoardGeometry.TargetWidthMeters, BoardGeometry.TargetHeightMeters);
        [SerializeField, Min(0.0001f)] float modelScale = 0.012f;
        [SerializeField, Min(0f)] float liftHeight = 0.006f;
        [SerializeField, Min(0f)] float liftDuration = 0.35f;

        readonly GameObject[] prefabs = new GameObject[9];
        readonly Transform[] pieces = new Transform[9];
        readonly float[] liftProgress = new float[9];

        public void SetBoardSize(Vector2 size)
        {
            boardSize = size;
        }

        /// <summary>Place a prefab in row-major order. A null prefab clears the cell.</summary>
        public void SetCell(int index, GameObject prefab)
        {
            Vector3 center = BoardGeometry.CellCenter(index, boardSize);
            if (prefab != null && prefabs[index] == prefab && pieces[index] != null)
                return;

            RemovePiece(index);
            if (prefab == null) return;

            Transform piece = Instantiate(prefab, transform, false).transform;
            piece.localPosition = center + Vector3.up * (liftDuration > 0f ? 0f : liftHeight);
            piece.localScale *= modelScale;
            pieces[index] = piece;
            prefabs[index] = prefab;
            liftProgress[index] = liftDuration > 0f ? 0f : 1f;
        }

        public void Clear()
        {
            for (int index = 0; index < pieces.Length; index++)
                RemovePiece(index);
        }

        public void PlayAnimation(int index, string trigger)
        {
            var piece = pieces[index];
            if (piece == null) return;
            var animator = piece.GetComponentInChildren<Animator>();
            if (animator != null) animator.SetTrigger(trigger);
        }

        void Update()
        {
            for (int index = 0; index < pieces.Length; index++)
            {
                Transform piece = pieces[index];
                if (piece == null || liftProgress[index] >= 1f) continue;

                float progress = liftDuration > 0f
                    ? Mathf.Clamp01(liftProgress[index] + Time.deltaTime / liftDuration)
                    : 1f;
                liftProgress[index] = progress;
                Vector3 position = piece.localPosition;
                position.y = liftHeight * Mathf.SmoothStep(0f, 1f, progress);
                piece.localPosition = position;
            }
        }

        void RemovePiece(int index)
        {
            if (pieces[index] != null)
            {
                pieces[index].gameObject.SetActive(false);
                Destroy(pieces[index].gameObject);
            }
            pieces[index] = null;
            prefabs[index] = null;
            liftProgress[index] = 0f;
        }
    }
}
