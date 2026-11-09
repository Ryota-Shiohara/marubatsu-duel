using System.IO;
using UnityEditor;
using UnityEngine;

namespace Marubatsu.Editor
{
    public static class CharacterAnimationPreview
    {
        [MenuItem("Marubatsu/Characters/Render Animation Previews")]
        public static void Render()
        {
            foreach (string name in new[] { "Mitanuki", "Yagapon", "Yaguchi" }) RenderCharacter(name);
        }

        public static void RenderCharacter(string name)
        {
            {
                var preview = new PreviewRenderUtility();
                var baked = new Mesh();
                try
                {
                    string folder = "Assets/Marubatsu/Art/" + name;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + name + "_Animated.prefab");
                    var instance = Object.Instantiate(prefab);
                    preview.AddSingleGO(instance);
                    var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                    skin.updateWhenOffscreen = true;
                    // Bake each sample for deterministic rendering in one editor tick.
                    var snapshot = new GameObject("PoseSnapshot");
                    snapshot.transform.SetParent(skin.transform, false);
                    snapshot.AddComponent<MeshFilter>().sharedMesh = baked;
                    snapshot.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false;
                    var camera = preview.camera;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(.12f, .15f, .2f);
                    camera.orthographic = true;
                    camera.nearClipPlane = .01f;
                    camera.farClipPlane = 30;
                    camera.orthographicSize = name != "Yagapon" ? 1.5f : 2.1f;
                    camera.transform.position = name != "Yagapon" ? new Vector3(3, 2.7f, 8) : new Vector3(6, 3, 8);
                    camera.transform.LookAt(new Vector3(0, 1.05f, 0));
                    preview.ambientColor = new Color(.5f, .5f, .5f);
                    preview.lights[0].intensity = 1.2f;
                    preview.lights[0].transform.rotation = Quaternion.Euler(35, 145, 0);
                    preview.lights[1].intensity = .6f;
                    preview.lights[1].transform.rotation = Quaternion.Euler(25, -35, 0);
                    foreach (string kind in new[] { "Idle", "Wave", "Victory", "Blink" })
                    {
                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/Animations/" + kind + ".anim");
                        clip.SampleAnimation(instance, kind == "Blink" ? .42f : kind == "Idle" ? 0 : .9f);
                        skin.BakeMesh(baked);
                        preview.BeginStaticPreview(new Rect(0, 0, 900, 850));
                        preview.Render();
                        var image = preview.EndStaticPreview();
                        File.WriteAllBytes("ArtSource/" + name + "/" + name + "_unity_" + kind.ToLowerInvariant() + ".png", image.EncodeToPNG());
                        Object.DestroyImmediate(image);
                    }
                }
                finally { preview.Cleanup(); Object.DestroyImmediate(baked); }
            }
            Debug.Log("[CharacterAnimation] Unity render previews saved for " + name);
        }
    }
}
