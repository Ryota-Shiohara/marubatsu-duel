using System;
using System.Linq;
using Marubatsu.Presentation;
using UnityEditor;
using UnityEngine;

namespace Marubatsu.Editor
{
    public static class VictoryAnimationValidation
    {
        [MenuItem("Marubatsu/Characters/Validate Victory Clips")]
        public static void Validate()
        {
            var roster = Resources.Load<MatchCharacterRoster>(MatchCharacterRoster.ResourcePath);
            Require(roster != null && roster.IsComplete, "Missing character roster");
            string[] names = { "VictoryTakeoff", "VictorySpin", "VictoryPose", "VictoryLoop", "Defeat" };
            foreach (VictoryFighter fighter in Enum.GetValues(typeof(VictoryFighter)))
            {
                var prefab = roster.Prefabs[(int)fighter];
                var instance = UnityEngine.Object.Instantiate(prefab);
                var mesh = new Mesh();
                try
                {
                    var animator = instance.GetComponentInChildren<Animator>();
                    Require(animator != null && animator.avatar != null && animator.avatar.isValid &&
                        animator.runtimeAnimatorController != null, fighter + ": missing Generic rig or controller");
                    var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                    Require(skin != null && skin.bones.All(bone => bone != null), fighter + ": invalid skin");

                    var clips = Resources.LoadAll<AnimationClip>("MarubatsuVictory/" + fighter);
                    foreach (string name in names)
                    {
                        var clip = clips.SingleOrDefault(c => c.name == name ||
                            c.name.EndsWith("|" + name, StringComparison.Ordinal));
                        Require(clip != null && !clip.legacy && clip.length > 0, fighter + ": missing " + name);
                        Require(AnimationUtility.GetCurveBindings(clip).Any(b =>
                            b.path.EndsWith("/Arm_L", StringComparison.Ordinal) &&
                            animator.transform.Find(b.path) != null), fighter + ": FBX hierarchy mismatch for " + name);
                        clip.SampleAnimation(animator.gameObject, 0);
                        skin.BakeMesh(mesh);
                        Vector3[] start = mesh.vertices;
                        clip.SampleAnimation(animator.gameObject, clip.length * .51f);
                        skin.BakeMesh(mesh);
                        Vector3[] end = mesh.vertices;
                        float movement = 0;
                        for (int i = 0; i < end.Length; i++)
                            movement = Mathf.Max(movement, (end[i] - start[i]).magnitude);
                        Require(float.IsFinite(movement) && movement > .003f && movement < 2,
                            fighter + ": invalid deformation in " + name + " (" + movement + ")");
                    }
                    Debug.Log("[VictoryAnimation] " + fighter + ": five takes applied to the existing skinned prefab");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
