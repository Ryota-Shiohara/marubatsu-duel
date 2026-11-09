using UnityEngine;
using Vuforia;

namespace Marubatsu
{
    /// <summary>Phase 2 diagnostic: nine independent observations with one detailed image preview.</summary>
    [RequireComponent(typeof(BoardTracker), typeof(ImageTargetBehaviour))]
    public sealed class BoardImageExtractor : MonoBehaviour
    {
        [Range(0, 8)] public int CellIndex = 4;
        // Allow a few millimeters of tracking error before the printed grid enters the crop.
        [Range(0.03f, 0.25f)] public float CellInset = 0.10f;
        public int MinimumCellPixels = 16;
        const int Size = 96;
        readonly double[] corners = new double[8];
        readonly Vector3[] worldCorners = new Vector3[4];
        readonly Color32[] pixels = new Color32[Size*Size];
        readonly BoardCellRecognition recognition = new BoardCellRecognition(Size);
        readonly CellMark[] stableCells = new CellMark[BoardCellRecognition.Count];
        GameSession session;
        PaperMoveInput paperInput;
        CameraFrameProvider provider;
        BoardTracker tracker;
        ImageTargetBehaviour target;
        Camera arCamera;
        Texture2D grayTexture, binaryTexture;
        CellClassification classification;
        CellMark lastLogged = CellMark.Unknown;
        string status = "Waiting for camera";
        string diagnostic;
        bool hasFrame;
        bool selectedValid;
        double lastFrame;
        int sampledCell = -1;
        float sampledInset = -1;

        public PaperMoveStatus PaperStatus { get; private set; }
        public bool DiagnosticsVisible { get; private set; }
        public bool HasFreshFrame => hasFrame && Time.realtimeSinceStartupAsDouble - lastFrame < 0.5;

        public void AttachGame(GameSession game)
        {
            if (session != null) session.OnSnapshotChanged -= OnGameChanged;
            session = game;
            paperInput = session != null ? new PaperMoveInput(session) : null;
            if (session != null) session.OnSnapshotChanged += OnGameChanged;
            Invalidate("Waiting for game board");
        }

        void OnGameChanged(BoardSnapshot snapshot)
        {
            if (!snapshot.LastMove.HasValue) Invalidate("Show empty paper");
        }

        void Start()
        {
            tracker = GetComponent<BoardTracker>();
            target = GetComponent<ImageTargetBehaviour>();
            var cameraObject = VuforiaBehaviour.Instance.gameObject;
            arCamera = cameraObject.GetComponent<Camera>();
            provider = cameraObject.GetComponent<CameraFrameProvider>();
            if (provider == null) provider = cameraObject.AddComponent<CameraFrameProvider>();
            provider.FrameReady += OnFrame;
            tracker.TrackingChanged += OnTracking;
            grayTexture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            binaryTexture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            OnTracking(tracker.IsTracked);
        }
        void OnTracking(bool tracked) => Invalidate(tracked ? "Waiting for next frame" : "Show the whole board to the camera");
        void Invalidate(string reason)
        {
            hasFrame = selectedValid = false;
            status = reason;
            PaperStatus = PaperMoveStatus.WaitingForStableCells;
            lastLogged = CellMark.Unknown;
            recognition.Reset();
        }

        void OnFrame(GrayCameraFrame frame)
        {
            if (!tracker.IsTracked) { Invalidate("Board not tracked"); return; }
            if (!frame.LinearCamera) { Invalidate("Nonlinear camera: calibration required"); return; }
            if (frame.FocalLength.x <= 0 || frame.FocalLength.y <= 0) { Invalidate("Invalid camera calibration"); return; }
            if (CellInset != sampledInset)
            {
                Invalidate("Crop margin changed");
                sampledInset = CellInset;
            }

            if (CellIndex != sampledCell)
            {
                sampledCell = CellIndex;
                lastLogged = CellMark.Unknown;
            }
            selectedValid = false;
            var size = target.GetSize();
            float halfCell = BoardGeometry.CellSize(size) * (0.5f - CellInset);
            var cellToWorld = transform.localToWorldMatrix;

            for (int index = 0; index < BoardCellRecognition.Count; index++)
            {
                bool selected = index == sampledCell;
                var center = BoardGeometry.CellCenter(index, size);
                string failure = ProjectCell(frame, cellToWorld, center, halfCell, selected);
                if (failure != null)
                {
                    recognition.InvalidateCell(index);
                    if (selected) status = failure;
                    continue;
                }
                if (!recognition.TryObserve(index, frame.Pixels, frame.Width, frame.Height,
                    corners, frame.Sequence, frame.Time, out var observation))
                {
                    if (selected) status = "Cell outside image or invalid perspective";
                    continue;
                }
                if (!selected) continue;

                classification = observation;
                diagnostic = recognition.DiagnosticReason;
                status = $"Cell {index} / ink {recognition.InkRatio:P0}";
                if (DiagnosticsVisible)
                {
                    Upload(grayTexture, recognition.Crop);
                    Upload(binaryTexture, recognition.Binary);
                }
                selectedValid = true;
                CellMark stable = recognition.StableAt(index);
                if (stable != lastLogged && stable != CellMark.Unknown)
                    Debug.Log($"[CellRecognition] cell={index}, stable={stable}. Diagnostic observation.", this);
                lastLogged = stable;
            }
            hasFrame = true;
            lastFrame = frame.Time;
            if (paperInput != null)
            {
                for (int index = 0; index < stableCells.Length; index++)
                    stableCells[index] = recognition.StableAt(index);
                PaperStatus = paperInput.Observe(stableCells);
            }
        }

        string ProjectCell(GrayCameraFrame frame, Matrix4x4 cellToWorld, Vector3 center,
            float halfCell, bool selected)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                float x = corner == 0 || corner == 3 ? -halfCell : halfCell;
                float z = corner < 2 ? halfCell : -halfCell;
                Vector3 world = cellToWorld.MultiplyPoint3x4(center + new Vector3(x, 0, z));
                var point = frame.WorldToCamera.MultiplyPoint3x4(world);
                if (point.z <= arCamera.nearClipPlane) return "Cell behind camera";
                if (selected) worldCorners[corner] = world;
                corners[2 * corner] = frame.FocalLength.x * point.x / point.z + frame.PrincipalPoint.x;
                corners[2 * corner + 1] = frame.PrincipalPoint.y - frame.FocalLength.y * point.y / point.z;
            }
            double minSquared = (double)MinimumCellPixels * MinimumCellPixels;
            for (int corner = 0; corner < 4; corner++)
            {
                int next = (corner + 1) % 4;
                double dx = corners[2 * corner] - corners[2 * next];
                double dy = corners[2 * corner + 1] - corners[2 * next + 1];
                if (dx * dx + dy * dy < minSquared) return "Move camera closer to the board";
            }
            return null;
        }

        void Upload(Texture2D texture, byte[] values)
        {
            // Texture2D rows start at the bottom; grayscale buffers start at the top.
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    byte v = values[y*Size+x];
                    pixels[(Size-1-y)*Size+x] = new Color32(v, v, v, 255);
                }
            texture.SetPixels32(pixels); texture.Apply(false);
        }
        void Update()
        {
            if (hasFrame && !HasFreshFrame) Invalidate("Camera frame stalled");
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F1))
            {
                DiagnosticsVisible = !DiagnosticsVisible;
                selectedValid = false;
            }
#endif
        }

        void OnGUI()
        {
            if (!DiagnosticsVisible) return;
            DrawBoardStatus();
            GUI.Box(new Rect(10, 10, 282, 232), "Phase 2 / cell detail");
            GUI.Label(new Rect(20, 34, 260, 22), status);
            if (HasFreshFrame && selectedValid && CellIndex == sampledCell)
            {
                GUI.Label(new Rect(20, 56, 130, 20), "Rectified cell");
                GUI.Label(new Rect(160, 56, 120, 20), "Binary image");
                GUI.DrawTexture(new Rect(20, 78, 96, 96), grayTexture);
                GUI.DrawTexture(new Rect(160, 78, 96, 96), binaryTexture);
                GUI.Label(new Rect(20, 178, 260, 24), $"{classification.Mark} / stable: {recognition.StableAt(sampledCell)}");
                GUI.Label(new Rect(20, 202, 260, 24), diagnostic);
                for (int i = 0; i < 4; i++) DrawEdge(worldCorners[i], worldCorners[(i+1)%4]);
            }
            else GUI.Label(new Rect(20, 78, 260, 90), "Tracking / calibration pending.\nClassification is paused.");
        }
        void DrawBoardStatus()
        {
            float left = Screen.width >= 450 ? 300 : 10;
            float top = Screen.width >= 450 ? 10 : 250;
            GUI.Box(new Rect(left, top, 146, 132), "Stable cells");
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    int index = row * 3 + column;
                    var mark = recognition.StableAt(index);
                    string symbol = mark == CellMark.Circle ? "O" :
                        mark == CellMark.Cross ? "X" : mark == CellMark.Empty ? "-" : "?";
                    GUI.Label(new Rect(left + 12 + column * 42, top + 26 + row * 24, 42, 24),
                        $"{index}: {symbol}");
                }
            }
            if (paperInput != null && HasFreshFrame)
                GUI.Label(new Rect(left + 8, top + 102, 134, 26), PaperStatus.ToString());
        }

        void DrawEdge(Vector3 a, Vector3 b)
        {
            var start = arCamera.WorldToScreenPoint(a); var end = arCamera.WorldToScreenPoint(b);
            var p = new Vector2(start.x, Screen.height-start.y); var q = new Vector2(end.x, Screen.height-end.y);
            var matrix = GUI.matrix; var color = GUI.color;
            GUI.color = Color.yellow;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(q.y-p.y, q.x-p.x)*Mathf.Rad2Deg, p);
            GUI.DrawTexture(new Rect(p.x, p.y, Vector2.Distance(p, q), 2), Texture2D.whiteTexture);
            GUI.matrix = matrix; GUI.color = color;
        }
        void OnDestroy()
        {
            if (provider != null) provider.FrameReady -= OnFrame;
            if (tracker != null) tracker.TrackingChanged -= OnTracking;
            if (session != null) session.OnSnapshotChanged -= OnGameChanged;
            if (grayTexture != null) Destroy(grayTexture);
            if (binaryTexture != null) Destroy(binaryTexture);
        }
    }
}
