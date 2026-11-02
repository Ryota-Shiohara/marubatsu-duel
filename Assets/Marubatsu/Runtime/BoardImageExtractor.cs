using UnityEngine;
using Vuforia;

namespace Marubatsu
{
    /// <summary>Phase 2 diagnostic: one rectified cell and provisional classification; never writes game state.</summary>
    [RequireComponent(typeof(BoardTracker), typeof(ImageTargetBehaviour))]
    public sealed class BoardImageExtractor : MonoBehaviour
    {
        [Range(0, 8)] public int CellIndex = 4;
        // Leave a narrow grid-line margin without clipping normally sized handwritten symbols.
        [Range(0.03f, 0.25f)] public float CellInset = 0.06f;
        public int MinimumCellPixels = 16;
        const int Size = 96;
        readonly byte[] crop = new byte[Size*Size];
        readonly double[] corners = new double[8];
        readonly Vector3[] worldCorners = new Vector3[4];
        readonly Color32[] pixels = new Color32[Size*Size];
        readonly CellImageProcessing recognizer = new CellImageProcessing(Size);
        readonly CellDetectionStabilizer stabilizer = new CellDetectionStabilizer();
        CameraFrameProvider provider;
        BoardTracker tracker;
        ImageTargetBehaviour target;
        Camera arCamera;
        Texture2D grayTexture, binaryTexture;
        CellClassification classification;
        CellMark lastLogged = CellMark.Unknown;
        string status = "Waiting for camera";
        bool valid;
        double lastFrame;
        int sampledCell = -1;
        float sampledInset = -1;

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
            valid = false; status = reason; stabilizer.Reset(); lastLogged = CellMark.Unknown;
        }
        void OnFrame(GrayCameraFrame frame)
        {
            if (CellIndex != sampledCell || CellInset != sampledInset)
            {
                Invalidate("Cell selection changed");
                sampledCell = CellIndex; sampledInset = CellInset;
            }
            if (!tracker.IsTracked) { Invalidate("Board not tracked"); return; }
            if (!frame.LinearCamera) { Invalidate("Nonlinear camera: calibration required"); return; }
            if (frame.FocalLength.x <= 0 || frame.FocalLength.y <= 0) { Invalidate("Invalid camera calibration"); return; }
            var size = target.GetSize();
            var center = BoardGeometry.CellCenter(CellIndex, size);
            float halfX = size.x*BoardGeometry.PlayAreaFraction/3f*(0.5f-CellInset);
            float halfZ = size.y*BoardGeometry.PlayAreaFraction/3f*(0.5f-CellInset);
            worldCorners[0] = transform.TransformPoint(center + new Vector3(-halfX, 0, halfZ));
            worldCorners[1] = transform.TransformPoint(center + new Vector3(halfX, 0, halfZ));
            worldCorners[2] = transform.TransformPoint(center + new Vector3(halfX, 0, -halfZ));
            worldCorners[3] = transform.TransformPoint(center + new Vector3(-halfX, 0, -halfZ));
            for (int i = 0; i < 4; i++)
            {
                // Unity local camera axes are +X right, +Y up, +Z forward.
                // Raw image coordinates are +X right, +Y down, origin top-left.
                var p = frame.WorldToCamera.MultiplyPoint3x4(worldCorners[i]);
                if (p.z <= arCamera.nearClipPlane) { Invalidate("Cell behind camera"); return; }
                corners[2*i] = frame.FocalLength.x*p.x/p.z + frame.PrincipalPoint.x;
                corners[2*i+1] = frame.PrincipalPoint.y-frame.FocalLength.y*p.y/p.z;
            }
            double minSide = double.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                int next = (i+1)%4;
                double dx = corners[2*i]-corners[2*next], dy = corners[2*i+1]-corners[2*next+1];
                minSide = System.Math.Min(minSide, System.Math.Sqrt(dx*dx+dy*dy));
            }
            if (minSide < MinimumCellPixels) { Invalidate("Move camera closer to the board"); return; }
            if (!PerspectiveCellSampler.Sample(frame.Pixels, frame.Width, frame.Height, corners, crop, Size))
            { Invalidate("Cell outside image or invalid perspective"); return; }
            classification = recognizer.Classify(crop);
            stabilizer.Observe(classification, frame.Sequence, frame.Time);
            Upload(grayTexture, crop); Upload(binaryTexture, recognizer.Binary);
            valid = true; lastFrame = frame.Time;
            status = $"Cell {CellIndex} / ink {recognizer.InkRatio:P0}";
            if (stabilizer.Stable != CellMark.Unknown && stabilizer.Stable != lastLogged)
            {
                lastLogged = stabilizer.Stable;
                Debug.Log($"[CellRecognition] cell={CellIndex}, stable={lastLogged}, " +
                    $"ink={recognizer.InkRatio:P1}. Diagnostic only; no game move accepted.", this);
            }
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
        void OnGUI()
        {
            bool fresh = valid && Time.realtimeSinceStartupAsDouble-lastFrame < 0.5;
            if (!fresh && valid) Invalidate("Camera frame stalled");
            GUI.Box(new Rect(10, 10, 282, 232), "Phase 2 / one-cell recognition");
            GUI.Label(new Rect(20, 34, 260, 22), status);
            if (fresh)
            {
                GUI.Label(new Rect(20, 56, 130, 20), "Rectified cell");
                GUI.Label(new Rect(160, 56, 120, 20), "Binary image");
                GUI.DrawTexture(new Rect(20, 78, 96, 96), grayTexture);
                GUI.DrawTexture(new Rect(160, 78, 96, 96), binaryTexture);
                GUI.Label(new Rect(20, 178, 260, 24), $"{classification.Mark} / stable: {stabilizer.Stable}");
                GUI.Label(new Rect(20, 202, 260, 24), recognizer.DiagnosticReason);
                for (int i = 0; i < 4; i++) DrawEdge(worldCorners[i], worldCorners[(i+1)%4]);
            }
            else GUI.Label(new Rect(20, 78, 260, 90), "Tracking / calibration pending.\nClassification is paused.");
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
            if (grayTexture != null) Destroy(grayTexture);
            if (binaryTexture != null) Destroy(binaryTexture);
        }
    }
}
