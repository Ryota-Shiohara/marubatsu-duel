using UnityEngine;

namespace Marubatsu.Presentation
{
    /// <summary>Attach to a transform aligned with the center and axes of the board.</summary>
    public sealed class BoardPieceView : MonoBehaviour
    {
        [SerializeField] Vector2 boardSize = new Vector2(BoardGeometry.TargetWidthMeters, BoardGeometry.TargetHeightMeters);
        [SerializeField, Min(0.0001f)] float modelScale = 0.021f;
        [SerializeField, Min(0f)] float liftHeight = 0.006f;
        [SerializeField, Min(0f)] float liftDuration = 0.5f;

        const float CharacterDisplayMultiplier = 2.5f;
        const float EntryScalePeak = 1.25f;
        const float PatrolWidth = 0.19f;
        const float PatrolDepth = 0.13f;
        const float ReactionSeconds = 2.2f;

        sealed class Piece
        {
            public GameObject Prefab;
            public Transform Anchor, Pivot;
            public Animator Animator;
            public Transform LeftLeg, RightLeg;
            public Quaternion LeftRest, RightRest;
            public Vector3 LeftAxis, RightAxis;
            public bool Floats, PoseControlled;
            public float Age, ReactionTime, MotionWeight = 1f;
        }

        readonly Piece[] pieces = new Piece[9];

        public void SetBoardSize(Vector2 size)
        {
            boardSize = size;
        }

        /// <summary>Place a prefab in row-major order. A null prefab clears the cell.</summary>
        public void SetCell(int index, GameObject prefab, Color tint, Material ringMaterial)
        {
            Vector3 center = BoardGeometry.CellCenter(index, boardSize);
            if (pieces[index] != null && pieces[index].Prefab == prefab)
                return;

            RemovePiece(index);
            if (prefab == null) return;

            var anchor = new GameObject($"Cell {index}").transform;
            anchor.SetParent(transform, false);
            anchor.localPosition = center;
            float cellSize = BoardGeometry.CellSize(boardSize);
            if (ringMaterial != null) DrawCellRing(anchor, cellSize, tint, ringMaterial);

            var pivot = new GameObject("Character motion").transform;
            pivot.SetParent(anchor, false);
            Transform model = Instantiate(prefab, pivot, false).transform;
            model.localPosition = Vector3.zero;
            model.localScale *= modelScale;
            FitModel(model, pivot);
            model.localScale *= CharacterDisplayMultiplier;

            var piece = new Piece
            {
                Prefab = prefab,
                Anchor = anchor,
                Pivot = pivot,
                Animator = model.GetComponentInChildren<Animator>()
            };
            foreach (Transform bone in model.GetComponentsInChildren<Transform>())
            {
                switch (bone.name)
                {
                    case "Arrow":
                        piece.Floats = true;
                        break;
                    case "Leg_L":
                        piece.LeftLeg = bone;
                        piece.LeftRest = bone.localRotation;
                        piece.LeftAxis = bone.InverseTransformDirection(model.right);
                        break;
                    case "Leg_R":
                        piece.RightLeg = bone;
                        piece.RightRest = bone.localRotation;
                        piece.RightAxis = bone.InverseTransformDirection(model.right);
                        break;
                }
            }
            pieces[index] = piece;
            PosePiece(piece, index, cellSize);
        }

        static void DrawCellRing(Transform anchor, float cellSize, Color tint, Material material)
        {
            const int segments = 32;
            var ringObject = new GameObject("Cell marker");
            ringObject.transform.SetParent(anchor, false);
            ringObject.transform.localPosition = Vector3.up * (cellSize * 0.04f);
            ringObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.alignment = LineAlignment.TransformZ;
            ring.loop = true;
            ring.sharedMaterial = material;
            ring.startColor = ring.endColor = tint;
            ring.widthMultiplier = cellSize * 0.035f;
            ring.positionCount = segments;
            for (int index = 0; index < segments; index++)
            {
                float angle = index * (2f * Mathf.PI / segments);
                ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * cellSize * 0.33f,
                    Mathf.Sin(angle) * cellSize * 0.33f, 0f));
            }
        }

        void FitModel(Transform model, Transform pivot)
        {
            float radius = 0f;
            float tiltAllowance = Mathf.Sin(14f * Mathf.Deg2Rad);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var bounds = renderer.localBounds;
                var toPivot = pivot.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 local = toPivot.MultiplyPoint3x4(point);
                    float cornerRadius = new Vector2(local.x, local.z).magnitude + Mathf.Abs(local.y) * tiltAllowance;
                    radius = Mathf.Max(radius, cornerRadius);
                }
            }

            float maximumRadius = BoardGeometry.CellSize(boardSize) * 0.73f / EntryScalePeak;
            if (radius > maximumRadius) model.localScale *= maximumRadius / radius;
        }

        public void Clear()
        {
            for (int index = 0; index < pieces.Length; index++)
                RemovePiece(index);
        }

        public void PlayAnimation(int index, string trigger)
        {
            var piece = pieces[index];
            if (piece?.Animator == null) return;
            piece.ReactionTime = ReactionSeconds;
            piece.Animator.SetTrigger(trigger);
        }

        internal bool TryControlPose(int index, out Transform pivot, out Animator animator)
        {
            Piece piece = pieces[index];
            pivot = piece?.Pivot;
            animator = piece?.Animator;
            if (piece == null) return false;
            piece.PoseControlled = true;
            return true;
        }

        internal void ReleasePose(int index)
        {
            if (pieces[index] != null) pieces[index].PoseControlled = false;
        }

        void LateUpdate()
        {
            float cellSize = BoardGeometry.CellSize(boardSize);
            for (int index = 0; index < pieces.Length; index++)
            {
                Piece piece = pieces[index];
                if (piece == null || piece.PoseControlled) continue;
                piece.Age += Time.deltaTime;
                piece.ReactionTime = Mathf.Max(0f, piece.ReactionTime - Time.deltaTime);
                piece.MotionWeight = Mathf.MoveTowards(piece.MotionWeight,
                    piece.ReactionTime > 0f ? 0f : 1f, Time.deltaTime * 5f);
                PosePiece(piece, index, cellSize);
            }
        }

        void PosePiece(Piece piece, int index, float cellSize)
        {
            float entry = liftDuration > 0f ? Mathf.Clamp01(piece.Age / liftDuration) : 1f;
            float rise = Mathf.SmoothStep(0f, 1f, entry);
            float entryHop = Mathf.Sin(Mathf.PI * entry);
            float motion = piece.MotionWeight * rise;
            float phase = piece.Age * (piece.Floats ? 2.2f : 2.7f) + index * 0.9f;
            float x = Mathf.Sin(phase);
            float z = Mathf.Cos(phase);
            float stride = Mathf.Sin(phase * 4f);
            float bob = piece.Floats ? (1f + stride) * 0.5f : Mathf.Abs(stride);
            var offset = new Vector3(x * cellSize * PatrolWidth, bob * cellSize * 0.13f, z * cellSize * PatrolDepth);
            float restingLift = piece.Floats ? liftHeight : liftHeight * 0.35f;
            piece.Pivot.localPosition = offset * motion +
                Vector3.up * (restingLift * rise + entryHop * cellSize * 0.31f);
            piece.Pivot.localScale = Vector3.one *
                Mathf.Min(EntryScalePeak, Mathf.Lerp(0.62f, 1f, rise) + entryHop * 0.46f);

            float yaw = piece.Floats ? x * 46f : Mathf.Atan2(z * PatrolWidth, -x * PatrolDepth) * Mathf.Rad2Deg;
            var facing = Quaternion.Euler(0f, yaw, stride * (piece.Floats ? 14f : 9f));
            piece.Pivot.localRotation = Quaternion.Slerp(Quaternion.identity, facing, motion);
            if (piece.Floats) return;

            float swing = stride * 35f * motion;
            if (piece.LeftLeg != null)
                piece.LeftLeg.localRotation = piece.LeftRest * Quaternion.AngleAxis(swing, piece.LeftAxis);
            if (piece.RightLeg != null)
                piece.RightLeg.localRotation = piece.RightRest * Quaternion.AngleAxis(-swing, piece.RightAxis);
        }

        void RemovePiece(int index)
        {
            if (pieces[index] != null)
            {
                GameObject anchor = pieces[index].Anchor.gameObject;
                anchor.SetActive(false);
                Destroy(anchor);
            }
            pieces[index] = null;
        }
    }
}
