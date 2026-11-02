using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vuforia;

namespace Marubatsu.Editor
{
    public static class PhaseOneSetup
    {
        const string TexturePath = "Assets/Marubatsu/Art/BoardTarget.png";
        const string ScenePath = "Assets/Marubatsu/Scenes/Phase1_BoardTracking.unity";

        [MenuItem("Marubatsu/Create Phase 1 Scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("Phase 1 scene already exists. Open it instead of overwriting.");
            ConfigureBoardTexture();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("ARCamera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.GetComponent<Camera>().nearClipPlane = 0.01f;
            // Skybox is drawn over Vuforia's video background in the Built-in pipeline.
            cameraObject.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<VuforiaBehaviour>();
            cameraObject.AddComponent<CameraFrameProbe>();
            cameraObject.AddComponent<PhaseOneBootstrap>().BoardTexture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var light = new GameObject("Directional Light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45, -30, 0);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Phase1] Scene created. Configure your local Vuforia license and Webcam Play Mode.");
        }

        public static void ConfigureBoardTexture()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null) throw new FileNotFoundException(TexturePath);
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        public static void ConfigureAndValidate()
        {
            ConfigureBoardTexture();
            PhaseOneValidation.Validate();
        }
    }
}
