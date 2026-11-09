using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Marubatsu.Editor
{
    /// <summary>Creates editable examples without changing any game scene.</summary>
    public static class CharacterAnimationSetup
    {
        [MenuItem("Marubatsu/Characters/Prepare Animation Examples")]
        public static void Build()
        {
            BuildAll(false);
        }

        // Explicit regeneration entry point for the asset-authoring pipeline.
        public static void RebuildGeneratedClips() { BuildAll(true); }
        public static void RebuildAndPreview()
        {
            BuildAll(true);
            CharacterAnimationPreview.Render();
        }
        public static void PrepareYaguchi()
        {
            BuildCharacter("Yaguchi", true);
            AssetDatabase.SaveAssets();
            CharacterAnimationPreview.RenderCharacter("Yaguchi");
        }

        static void BuildAll(bool replaceClips)
        {
            foreach (string name in new[] { "Mitanuki", "Yagapon", "Yaguchi" }) BuildCharacter(name, replaceClips);
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterAnimation] PASS: Generic rigs, skin weights, blink shapes, sampled animations and prefabs. AR performance not tested.");
        }

        static void BuildCharacter(string name, bool replaceClips)
        {
            string folder = "Assets/Marubatsu/Art/" + name;
            string path = folder + "/" + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            Require(importer != null, "Missing FBX: " + path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.importBlendShapes = true;
            importer.importAnimation = true;
            importer.isReadable = true;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                Require(renderer != null, name + ": no skinned mesh");
                Require(renderer.sharedMesh.triangles.Length / 3 <= 4600, name + ": triangle budget");
                Require(renderer.bones.Length >= 7 && renderer.bones.All(b => b != null), name + ": invalid bones");
                Require(renderer.sharedMaterials.All(m => m != null), name + ": missing materials");
                if (name == "Yaguchi")
                    Require(renderer.sharedMaterials.Any(m => m.mainTexture != null), "Yaguchi: missing warm gradient texture");
                var weights = renderer.sharedMesh.boneWeights;
                Require(weights.Length == renderer.sharedMesh.vertexCount, name + ": missing weights");
                foreach (var w in weights)
                    Require(Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1) < .001f, name + ": weight sum");
                foreach (string key in new[] { "Blink", "Blink_L", "Blink_R" })
                    Require(Shape(renderer, key) >= 0, name + ": missing " + key);
                var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
                Require(animator.avatar != null && animator.avatar.isValid && !animator.avatar.isHuman, name + ": invalid Generic avatar");
                animator.applyRootMotion = false;
                string animations = folder + "/Animations";
                if (!AssetDatabase.IsValidFolder(animations)) AssetDatabase.CreateFolder(folder, "Animations");
                var clips = new[] { "Idle", "Wave", "Victory", "Blink" }
                    .Select(kind => MakeClip(name, kind, instance, renderer, animations, replaceClips)).ToArray();
                string controllerPath = animations + "/" + name + ".controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                    controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
                if (replaceClips || controller.layers[0].stateMachine.states.Length == 0)
                {
                    var machine = controller.layers[0].stateMachine;
                    foreach (var state in machine.states) machine.RemoveState(state.state);
                    foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
                    controller.parameters = Array.Empty<AnimatorControllerParameter>();
                    var states = clips.Select(c => { var s = machine.AddState(c.name); s.motion = c; return s; }).ToArray();
                    machine.defaultState = states[0];
                    for (int i = 1; i < states.Length; i++)
                    {
                        string trigger = new[] { "", "Wave", "Victory", "Blink" }[i];
                        controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                        var enter = machine.AddAnyStateTransition(states[i]);
                        enter.hasExitTime = false;
                        enter.duration = .12f;
                        enter.canTransitionToSelf = false;
                        enter.AddCondition(AnimatorConditionMode.If, 0, trigger);
                        var leave = states[i].AddTransition(states[0]);
                        leave.hasExitTime = true;
                        leave.exitTime = 1;
                        leave.duration = .12f;
                    }
                    EditorUtility.SetDirty(machine);
                }
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                animator.runtimeAnimatorController = controller;
                ValidateSamples(instance, renderer, clips, name);
                // Save only a new example prefab; never overwrite hand-edited examples.
                string prefab = folder + "/" + name + "_Animated.prefab";
                if (!File.Exists(prefab)) PrefabUtility.SaveAsPrefabAsset(instance, prefab);
                Debug.Log($"[CharacterAnimation] {name}: {renderer.sharedMesh.triangles.Length / 3} triangles, {renderer.bones.Length} skin bones, {renderer.sharedMesh.blendShapeCount} shapes; animation samples passed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        static int Shape(SkinnedMeshRenderer renderer, string key)
        {
            var mesh = renderer.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; i++)
                if (mesh.GetBlendShapeName(i) == key || mesh.GetBlendShapeName(i).EndsWith("." + key, StringComparison.Ordinal)) return i;
            return -1;
        }

        static AnimationClip MakeClip(string name, string kind, GameObject instance, SkinnedMeshRenderer renderer, string folder, bool replace)
        {
            string path = folder + "/" + kind + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null && !replace) return existing;
            var clip = new AnimationClip { name = kind, frameRate = 30 };
            float[] times = { 0, .3f, .6f, .9f, 1.2f, 1.6f, 2f };
            bool mita = name != "Yagapon";
            var transforms = instance.GetComponentsInChildren<Transform>();
            foreach (string boneName in new[] { "Head", "Arm_L", "Arm_R", "Tail_02" })
            {
                var bone = transforms.FirstOrDefault(t => t.name == boneName);
                if (bone == null) continue;
                string bonePath = AnimationUtility.CalculateTransformPath(bone, instance.transform);
                Vector3 worldAxis = instance.transform.TransformDirection(mita ? Vector3.forward : Vector3.right);
                Vector3 axis = bone.InverseTransformDirection(worldAxis).normalized;
                var rotations = times.Select(t =>
                {
                    float angle = 0;
                    float envelope = Mathf.Sin(Mathf.PI * t / 2);
                    if (kind == "Idle" && boneName == "Head") angle = 2 * Mathf.Sin(Mathf.PI * t);
                    if (kind == "Wave" && boneName == "Arm_L") angle = (name == "Mitanuki" ? 1 : -1) * (40 + 10 * Mathf.Sin(t * Mathf.PI * 4)) * envelope;
                    if (kind == "Wave" && boneName == "Head") angle = -5 * envelope;
                    if (kind == "Victory" && boneName.StartsWith("Arm_", StringComparison.Ordinal))
                        angle = (name == "Yaguchi" ? (boneName == "Arm_L" ? -40 : 40)
                            : mita ? (boneName == "Arm_L" ? 55 : -55) : (boneName == "Arm_L" ? -35 : 15)) * envelope;
                    if (kind == "Victory" && boneName == "Tail_02") angle = 8 * envelope;
                    return bone.localRotation * Quaternion.AngleAxis(angle, axis);
                }).ToArray();
                for (int channel = 0; channel < 4; channel++)
                {
                    int c = channel;
                    clip.SetCurve(bonePath, typeof(Transform), "localRotation." + "xyzw"[c],
                        new AnimationCurve(times.Select((t, i) => new Keyframe(t, rotations[i][c])).ToArray()));
                }
            }
            var root = transforms.First(t => t.name == "Root");
            string rootPath = AnimationUtility.CalculateTransformPath(root, instance.transform);
            Vector3 localUp = root.parent.InverseTransformVector(instance.transform.TransformDirection(Vector3.up));
            // Root bone moves the whole character, including Yagapon's arrow.
            for (int channel = 0; channel < 3; channel++)
            {
                int c = channel;
                var keys = times.Select(t =>
                {
                    float height = kind == "Idle" ? .018f * Mathf.Sin(Mathf.PI * t)
                        : kind == "Victory" ? .10f * Mathf.Sin(Mathf.PI * t / 2) : 0;
                    return new Keyframe(t, root.localPosition[c] + localUp[c] * height);
                }).ToArray();
                clip.SetCurve(rootPath, typeof(Transform), "localPosition." + "xyz"[c],
                    new AnimationCurve(keys));
            }
            foreach (string key in new[] { "Blink", "Blink_L", "Blink_R" })
            {
                string shape = renderer.sharedMesh.GetBlendShapeName(Shape(renderer, key));
                var curve = kind == "Blink" && key == "Blink"
                    ? new AnimationCurve(new Keyframe(0, 0), new Keyframe(.3f, 0), new Keyframe(.42f, 100), new Keyframe(.54f, 0), new Keyframe(2, 0))
                    : AnimationCurve.Constant(0, 2, 0);
                clip.SetCurve(AnimationUtility.CalculateTransformPath(renderer.transform, instance.transform), typeof(SkinnedMeshRenderer), "blendShape." + shape, curve);
            }
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = kind == "Idle";
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                UnityEngine.Object.DestroyImmediate(clip);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static void ValidateSamples(GameObject instance, SkinnedMeshRenderer renderer, AnimationClip[] clips, string name)
        {
            var baked = new Mesh();
            try
            {
                clips[0].SampleAnimation(instance, 0);
                renderer.BakeMesh(baked);
                var rest = baked.vertices.Select(v => renderer.transform.TransformPoint(v)).ToArray();
                Debug.Log($"[CharacterAnimation] {name} mesh bounds={baked.bounds.size}, rendererScale={renderer.transform.lossyScale}, worldBounds={renderer.bounds.size}");
                foreach (var clip in clips.Skip(1))
                {
                    clip.SampleAnimation(instance, clip.name == "Blink" ? .42f : .9f);
                    renderer.BakeMesh(baked);
                    var posed = baked.vertices;
                    Require(posed.Length == rest.Length, name + ": changed vertex count");
                    float maxDelta = 0;
                    for (int i = 0; i < posed.Length; i++)
                    {
                        Require(float.IsFinite(posed[i].x) && float.IsFinite(posed[i].y) && float.IsFinite(posed[i].z), name + ": invalid pose");
                        maxDelta = Mathf.Max(maxDelta, Vector3.Distance(renderer.transform.TransformPoint(posed[i]), rest[i]));
                    }
                    Require(maxDelta > .001f, name + ": no deformation for " + clip.name);
                    Debug.Log($"[CharacterAnimation] {name} {clip.name} delta={maxDelta}, firstRest={rest[0]}, firstPose={renderer.transform.TransformPoint(posed[0])}");
                    Require(maxDelta < 2f, name + ": excessive deformation for " + clip.name);
                }
                clips[0].SampleAnimation(instance, 0);
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
        }

        static void Require(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
        }
    }
}
