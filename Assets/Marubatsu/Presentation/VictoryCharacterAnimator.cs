using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Marubatsu.Presentation
{
    public enum VictoryFighter { Yagapon, Mitanuki, Yaguchi }

    enum VictoryTake { Takeoff, Spin, Pose, Loop, Defeat }

    sealed class VictoryCharacterAnimator : IDisposable
    {
        static readonly string[] TakeNames = { "VictoryTakeoff", "VictorySpin", "VictoryPose", "VictoryLoop", "Defeat" };
        static readonly AnimationClip[][] Library = new AnimationClip[MatchCharacterRoster.CharacterCount][];

        readonly AnimationClip[] clips;
        readonly Animator animator;
        readonly RuntimeAnimatorController previousController;
        readonly AnimatorCullingMode previousCulling;
        PlayableGraph graph;
        AnimationPlayableOutput output;
        AnimationClipPlayable playable;
        VictoryTake? current;

        public VictoryCharacterAnimator(Animator animator, VictoryFighter fighter)
        {
            this.animator = animator;
            if (animator == null) return;
            clips = GetClips(fighter);
            if (clips[(int)VictoryTake.Takeoff] == null) return;
            previousController = animator.runtimeAnimatorController;
            previousCulling = animator.cullingMode;
            animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Board victory pose");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            output = AnimationPlayableOutput.Create(graph, "Character rig", animator);
            output.SetWeight(1f);
            graph.Play();
        }

        static AnimationClip[] GetClips(VictoryFighter fighter)
        {
            int index = (int)fighter;
            if (Library[index] != null) return Library[index];
            var clips = new AnimationClip[TakeNames.Length];
            foreach (var clip in Resources.LoadAll<AnimationClip>("MarubatsuVictory/" + fighter))
                for (int take = 0; take < clips.Length; take++)
                    if (clip.name == TakeNames[take] || clip.name.EndsWith("|" + TakeNames[take], StringComparison.Ordinal))
                        clips[take] = clip;
            return Library[index] = clips;
        }

        public void Sample(VictoryTake take, float progress)
        {
            if (!graph.IsValid()) return;
            AnimationClip clip = clips[(int)take];
            if (clip == null) return;
            if (current != take)
            {
                var next = AnimationClipPlayable.Create(graph, clip);
                next.SetApplyFootIK(false);
                output.SetSourcePlayable(next);
                if (playable.IsValid()) graph.DestroyPlayable(playable);
                playable = next;
                current = take;
            }

            // Sample the sequence clock, so tracking loss can freeze every take at its current frame.
            playable.SetTime(Mathf.Clamp01(progress) * clip.length);
            graph.Evaluate(0f);
        }

        public void Dispose()
        {
            if (!graph.IsValid()) return;
            graph.Destroy();
            if (animator == null) return;
            animator.runtimeAnimatorController = previousController;
            animator.cullingMode = previousCulling;
        }
    }
}
