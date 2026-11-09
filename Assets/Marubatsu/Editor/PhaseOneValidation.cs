using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vuforia;

namespace Marubatsu.Editor
{
    public static class PhaseOneValidation
    {
        [MenuItem("Marubatsu/Validate Phase 1 Assets")]
        public static void Validate()
        {
            // Physical acceptance examples: 270 x 180 mm target, 108 mm square play area.
            var size = new Vector2(BoardGeometry.TargetWidthMeters, BoardGeometry.TargetHeightMeters);
            Require(Vector3.Distance(BoardGeometry.CellCenter(0, size), new Vector3(-0.036f, 0, 0.036f)) < 0.000001f,
                "Top-left cell should be 36 mm left and above target center.");
            Require(BoardGeometry.CellCenter(4, size).sqrMagnitude < 0.000001f, "Middle cell must be target center.");
            Require(Vector3.Distance(BoardGeometry.CellCenter(8, size), new Vector3(0.036f, 0, -0.036f)) < 0.000001f,
                "Bottom-right cell mapping is wrong.");
            bool rejected = false;
            try { BoardGeometry.CellCenter(9, size); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected, "Invalid cell must be rejected.");

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Marubatsu/Art/BoardTarget.png");
            Require(texture != null && texture.width == 1800 && texture.height == 1200 && texture.isReadable,
                "Target must remain readable and retain original dimensions.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene("Assets/Marubatsu/Scenes/Phase1_BoardTracking.unity");
            int cameras = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0,
                        "Missing script on " + t.name);
                cameras += root.GetComponentsInChildren<Camera>(true).Length;
            }
            Require(cameras == 1, "Exactly one camera is required.");
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<PhaseOneBootstrap>();
            Require(bootstrap != null && bootstrap.BoardTexture == texture, "Bootstrap texture reference is broken.");
            Require(bootstrap.GetComponent<VuforiaBehaviour>() != null && bootstrap.GetComponent<CameraFrameProbe>() != null,
                "ARCamera components are missing.");
            Debug.Log("[Phase1Validation] PASS: physical cell positions, bounds, texture import, scene references. Camera tracking not tested.");
        }

        static void Require(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
        }
    }
}
