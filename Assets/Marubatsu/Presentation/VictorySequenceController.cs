using UnityEngine;

namespace Marubatsu.Presentation
{
    public sealed class VictorySequenceController : MonoBehaviour
    {
        const float WinnerScaleMultiplier = 2f;

        [System.Serializable]
        public sealed class Settings
        {
            [Min(.1f)] public float chargeSeconds = .5f;
            [Min(.3f)] public float takeoffSeconds = .9f;
            [Min(.4f)] public float performanceSeconds = 1.2f;
            [Min(.2f)] public float revealSeconds = 1.4f;
            [Range(.65f, 2f)] public float liftInCells = 1.12f;
            [Range(0f, .35f)] public float staggerFraction = .11f;
            [Range(.6f, 1.7f)] public float spinSpeed = 1f;
            [Range(.22f, .48f)] public float ringRadius = .35f;
            [Range(0, 30)] public int launchSparks = 18;
            [Range(0, 100)] public int celebrationParticles = 60;
        }

        Settings settings;

        sealed class Actor
        {
            public Transform Pivot;
            public VictoryCharacterAnimator Rig;
            public Vector3 Start;
            public Quaternion Facing, StageFacing, StartRotation;
            public Vector3 StartScale;
            public bool Sparked, LiftSparked;
            public float PunchTime = -1f;
        }

        readonly Actor[] winners = new Actor[3];
        readonly Actor[] losers = new Actor[GameSession.CellCount];
        readonly Vector3[] centers = new Vector3[3];
        readonly int[] winningIndices = new int[3];
        VictoryEffectController effects;
        VictoryWorldLabel label;
        BoardPieceView pieces;
        Camera viewCamera;
        Vector2 boardSize;
        float unit, elapsed;
        int revision = -1;
        bool active, draw, celebrated;
        VictoryFighter fighter;
        Color tint;

        float FlightStart => settings.chargeSeconds;
        float SpinStart => FlightStart + settings.takeoffSeconds;
        float RevealStart => SpinStart + settings.performanceSeconds;
        float EndTime => draw ? 1.2f : RevealStart + settings.revealSeconds;

        public bool CanReplay => active && elapsed >= EndTime;

        internal void Build(BoardPieceView boardPieces, Vector2 size, Settings configuration)
        {
            settings = configuration ?? new Settings();
            pieces = boardPieces;
            boardSize = size;
            unit = BoardGeometry.CellSize(size);
            var lights = new GameObject("Victory lights");
            lights.transform.SetParent(transform, false);
            effects = lights.AddComponent<VictoryEffectController>();
            effects.Build(unit);
            label = gameObject.AddComponent<VictoryWorldLabel>();
            label.Build(unit);
        }

        internal void Begin(BoardSnapshot snapshot, VictoryFighter circleFighter, VictoryFighter crossFighter)
        {
            if (snapshot.Outcome == OutcomeType.Playing || snapshot.Revision <= revision) return;
            Stop();
            revision = snapshot.Revision;
            draw = snapshot.Outcome == OutcomeType.Draw;
            bool circle = snapshot.Outcome == OutcomeType.CircleWon;
            fighter = circle ? circleFighter : crossFighter;
            tint = draw ? new Color32(255, 216, 133, 255) : circle ?
                new Color32(102, 221, 255, 255) : new Color32(255, 139, 71, 255);
            elapsed = 0;
            active = true;
            viewCamera = Camera.main;
            if (!draw)
            {
                MarkType winnerMark = circle ? MarkType.Circle : MarkType.Cross;
                for (int i = 0; i < winners.Length; i++)
                {
                    int cell = snapshot.WinningCells[i];
                    winningIndices[i] = cell;
                    centers[i] = BoardGeometry.CellCenter(cell, boardSize);
                    winners[i] = TakeControl(cell, fighter);
                }

                for (int cell = 0; cell < snapshot.Cells.Length; cell++)
                {
                    MarkType mark = snapshot.Cells[cell];
                    if (mark != MarkType.Empty && mark != winnerMark)
                        losers[cell] = TakeControl(cell, circle ? crossFighter : circleFighter);
                }
            }
            label.SetResult(draw, circle, fighter);
            effects.Begin(centers, BoardGeometry.CellCenter(4, boardSize), tint, fighter, settings.ringRadius, draw);
        }

        Actor TakeControl(int cell, VictoryFighter character)
        {
            if (!pieces.TryControlPose(cell, out Transform pivot, out Animator animator)) return null;
            Vector3 forward = viewCamera != null
                ? transform.InverseTransformPoint(viewCamera.transform.position) - pivot.parent.localPosition
                : Vector3.forward;
            forward.y = 0;
            Quaternion facing = forward.sqrMagnitude > .000001f ?
                Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
            return new Actor
            {
                Pivot = pivot,
                Rig = new VictoryCharacterAnimator(animator, character),
                Start = pivot.localPosition,
                StartScale = pivot.localScale,
                StartRotation = pivot.localRotation,
                StageFacing = facing,
                Facing = facing * Quaternion.Euler(0, character == VictoryFighter.Yagapon ? -90 : 0, 0)
            };
        }

        void LateUpdate() => Advance(Time.deltaTime);

        internal void Advance(float delta)
        {
            if (!active || !isActiveAndEnabled) return;
            elapsed += delta;
            if (draw)
            {
                float progress = Mathf.Clamp01(elapsed / EndTime);
                effects.DrawTie(progress);
                if (!celebrated)
                {
                    effects.Celebrate(settings.celebrationParticles / 5, 0f);
                    celebrated = true;
                }
                label.Show("DRAW", Vector3.up * unit * .95f, tint,
                    Mathf.SmoothStep(0, 1, progress * 4), .84f);
                return;
            }

            float charge = Mathf.Clamp01(elapsed / settings.chargeSeconds);
            float lift = Mathf.Clamp01((elapsed - FlightStart) / settings.takeoffSeconds);
            float aerial = Mathf.Clamp01((elapsed - SpinStart) / settings.performanceSeconds);
            float reveal = Mathf.Clamp01((elapsed - RevealStart) / settings.revealSeconds);
            effects.Charge(charge, Mathf.Max(lift, reveal));

            for (int i = 0; i < winners.Length; i++)
                PoseWinner(i, charge, lift, aerial, reveal);
            for (int cell = 0; cell < losers.Length; cell++)
                PoseLoser(losers[cell], charge, elapsed - SpinStart - cell * .035f);

            if (elapsed >= RevealStart)
            {
                if (!celebrated)
                {
                    effects.Celebrate(settings.celebrationParticles, settings.liftInCells);
                    celebrated = true;
                }
                float rise = EaseOut(Mathf.Clamp01(reveal * 6));
                label.Show("WINNER!", centers[1] + Vector3.up * (unit * (settings.liftInCells + 3.37f)), tint,
                    rise, Mathf.Lerp(.66f, 1f, rise));
            }
            else
            {
                float flash = Mathf.Clamp01(1 - Mathf.Abs(charge - .46f) * 2.3f);
                label.Show("FINISH!", centers[1] + Vector3.up * (unit * 1.46f), tint, flash, .75f + charge * .25f);
            }
        }

        void PoseWinner(int index, float charge, float lift, float aerial, float reveal)
        {
            Actor actor = winners[index];
            if (actor == null) return;
            float delay = index * settings.staggerFraction;
            float launch = Mathf.InverseLerp(delay, 1, lift);
            float routine = Mathf.InverseLerp(delay * .65f, 1, aerial);
            float spin = Mathf.InverseLerp(0, .79f, routine);
            float pose = Mathf.InverseLerp(.79f, 1, routine);
            float spinEase = Mathf.SmoothStep(0, 1, Mathf.Pow(spin, 1 / settings.spinSpeed));
            float hoverAge = Mathf.Max(0, elapsed - RevealStart);
            float hover = Mathf.Sin(hoverAge * 5f + index) * Mathf.Clamp01(reveal * 4) * .09f;
            float accentHeight = index == 1 ? .36f : .02f + .07f * index;
            float high = unit * (settings.liftInCells + accentHeight);
            float settle = Mathf.SmoothStep(0, 1, Mathf.Clamp01(charge * 2));
            Vector3 position = Vector3.Lerp(actor.Start, Vector3.up * (unit * .03f), settle);
            float rise = EaseOut(launch);
            position.y += high * rise + unit * (hover + .15f * Mathf.Sin(spin * Mathf.PI));
            position += (centers[index] - centers[1]) * (.65f * rise);

            Vector3 localAxis = fighter == VictoryFighter.Mitanuki ? actor.StageFacing * Vector3.right :
                fighter == VictoryFighter.Yagapon ? new Vector3(.16f, 1, .25f) : Vector3.up;
            float turn = fighter == VictoryFighter.Yaguchi ? 720 : 360;
            Quaternion stunt = Quaternion.AngleAxis(turn * spinEase, localAxis);
            Quaternion facing = Quaternion.Slerp(actor.StartRotation, actor.Facing, settle);
            // Rotate around the body rather than the feet during an aerial stunt.
            Vector3 bodyCenter = Vector3.up * (unit * .33f * WinnerScaleMultiplier);
            actor.Pivot.localPosition = position + bodyCenter - stunt * bodyCenter;
            actor.Pivot.localRotation = stunt * facing;
            float targetScale = (index == 1 ? .91f : .81f) * WinnerScaleMultiplier;
            actor.Pivot.localScale = Vector3.Lerp(actor.StartScale, Vector3.one * targetScale, settle);

            if (!actor.Sparked && charge >= .18f + .27f * index)
            {
                effects.SparkAt(index, settings.launchSparks);
                actor.Sparked = true;
            }

            Vector3 airborne = centers[index] + position;
            if (!actor.LiftSparked && launch >= .22f)
            {
                effects.LiftSparks(airborne, settings.launchSparks / 2);
                actor.LiftSparked = true;
            }

            float anticipation = Mathf.InverseLerp(.48f + delay, 1f, charge);
            if (elapsed < SpinStart)
                actor.Rig.Sample(VictoryTake.Takeoff, .26f * anticipation + .74f * launch);
            else if (routine < .79f) actor.Rig.Sample(VictoryTake.Spin, spin);
            else if (elapsed < RevealStart) actor.Rig.Sample(VictoryTake.Pose, pose);
            else actor.Rig.Sample(VictoryTake.Loop, Mathf.Repeat(hoverAge / 1.4f, 1));

            if (fighter == VictoryFighter.Yaguchi && actor.PunchTime < 0f && spin >= .83f)
            {
                effects.Punch(airborne, settings.launchSparks);
                actor.PunchTime = elapsed;
            }
            effects.DrawCell(index, charge, launch, spin, reveal, airborne, actor.StageFacing,
                elapsed >= RevealStart, actor.PunchTime < 0f ? -1f : elapsed - actor.PunchTime);
        }

        static void PoseLoser(Actor actor, float charge, float defeatAge)
        {
            if (actor == null) return;
            float settle = Mathf.SmoothStep(0, 1, charge);
            actor.Pivot.localPosition = Vector3.Lerp(actor.Start, Vector3.zero, settle);
            actor.Pivot.localRotation = Quaternion.Slerp(actor.StartRotation, actor.Facing, settle);
            actor.Pivot.localScale = Vector3.Lerp(actor.StartScale, Vector3.one * .82f, settle);
            actor.Rig.Sample(VictoryTake.Defeat, Mathf.Clamp01(defeatAge / 1.2f));
        }

        static float EaseOut(float t) => 1 - (1 - t) * (1 - t) * (1 - t);

        internal void Pause(bool paused) => effects.Pause(paused);

        internal void Stop()
        {
            if (!active) return;
            active = celebrated = false;
            for (int i = 0; i < winners.Length; i++)
            {
                if (winners[i] == null) continue;
                winners[i].Rig.Dispose();
                pieces.ReleasePose(winningIndices[i]);
                winners[i] = null;
            }
            for (int cell = 0; cell < losers.Length; cell++)
            {
                if (losers[cell] == null) continue;
                losers[cell].Rig.Dispose();
                pieces.ReleasePose(cell);
                losers[cell] = null;
            }
            effects.Stop();
            label.Hide();
        }

        void OnDestroy()
        {
            foreach (Actor actor in winners) actor?.Rig.Dispose();
            foreach (Actor actor in losers) actor?.Rig.Dispose();
        }
    }
}
