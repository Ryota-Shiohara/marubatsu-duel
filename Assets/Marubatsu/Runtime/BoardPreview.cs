using UnityEngine;
using Vuforia;

namespace Marubatsu
{
    /// <summary>Phase 1 calibration objects. These do not represent accepted game moves.</summary>
    [RequireComponent(typeof(BoardTracker))]
    public sealed class BoardPreview : MonoBehaviour
    {
        BoardTracker tracker;
        GameObject content;
        Material material;

        void Start()
        {
            tracker = GetComponent<BoardTracker>();
            Vector2 size = GetComponent<ImageTargetBehaviour>().GetSize();
            content = new GameObject("CalibrationObjects_NotGameState");
            content.transform.SetParent(transform, false);
            material = new Material(Shader.Find("Standard")) { color = new Color(0.1f, 0.7f, 1f) };
            float side = size.x * BoardGeometry.PlayAreaFraction / 3f * 0.35f;
            for (int i = 0; i < 9; i++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = $"Cell_{i / 3}_{i % 3}";
                piece.transform.SetParent(content.transform, false);
                piece.transform.localPosition = BoardGeometry.CellCenter(i, size) + Vector3.up * side;
                piece.transform.localScale = Vector3.one * side;
                piece.GetComponent<Renderer>().sharedMaterial = material;
                Destroy(piece.GetComponent<Collider>());
            }
            tracker.TrackingChanged += SetVisible;
            SetVisible(tracker.IsTracked);
        }

        void SetVisible(bool visible) => content.SetActive(visible);

        void OnDestroy()
        {
            if (tracker != null) tracker.TrackingChanged -= SetVisible;
            if (material != null) Destroy(material);
        }
    }
}
