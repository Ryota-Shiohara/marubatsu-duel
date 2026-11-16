using UnityEngine;

namespace Marubatsu.Presentation
{
    /// <summary>Attach to a transform aligned with the center and axes of the board.</summary>
    public sealed class BoardPieceView : MonoBehaviour
    {
        [SerializeField] Vector2 boardSize = new Vector2(BoardGeometry.TargetWidthMeters, BoardGeometry.TargetHeightMeters);
        [SerializeField, Min(0.0001f)] float modelScale = 0.021f;
        [SerializeField, Min(0f)] float liftHeight = 0.006f;
        [SerializeField, Min(0f)] float liftDuration = 0.5f;

        const float CharacterDisplayMultiplier = 2.5f;
        const float EntryScalePeak = 1.25f;
        const float PatrolWidth = 0.19f;
        const float PatrolDepth = 0.13f;
        const int CenterCell = 4;
        const float EntryHopHeightInCells = .31f;
        const float CenterHopMultiplier = 6f;
        const float CenterStartScale = 3f;
        const float CenterEntryDurationMultiplier = 3f;
        const float CenterFacingBlendSeconds = .3f;
        const float ShockwaveDuration = .18f;
        const float ShockwaveEchoDelay = .05f;
        const float ShockwaveRadiusInCells = 6f;
        const float ShockwaveStartRadiusInCells = .28f;
        const float LandingSparkDuration = .54f;
        const float FlameLifetime = .78f;
        const float FlameEmissionDuration = 2f;
        const float FlameFadeDuration = .5f;
        const float FlameEmissionInterval = .1f;
        const int IgnitionParticlesPerCell = 30;
        const int SustainedParticlesPerCell = 6;

        sealed class Piece
        {
            public GameObject Prefab;
            public Transform Anchor, Pivot, Portal;
            public LineRenderer PortalGlow;
            public Transform BurstRoot;
            public LineRenderer Refraction;
            public LineRenderer[] Shockwaves, Sparks;
            public ParticleSystem Flames;
            public float BurstAge;
            public Color Tint;
            public Animator Animator;
            public CenterLandingPose LandingPose;
            public Quaternion CenterFacing, ModelFacingOffset;
            public Transform LeftLeg, RightLeg;
            public Quaternion LeftRest, RightRest;
            public Vector3 LeftAxis, RightAxis;
            public bool Floats, PoseControlled;
            public float Age;
        }

        readonly Piece[] pieces = new Piece[9];
        Material refractionMaterial, flameMaterial;
        Camera viewCamera;

        float CenterEntryDuration => liftDuration * CenterEntryDurationMultiplier;

        public void SetBoardSize(Vector2 size)
        {
            boardSize = size;
        }

        internal void SetViewCamera(Camera camera) => viewCamera = camera;

        /// <summary>Place a prefab in row-major order. A null prefab clears the cell.</summary>
        public void SetCell(int index, GameObject prefab, Color tint, Material ringMaterial, VictoryFighter fighter)
        {
            Vector3 center = BoardGeometry.CellCenter(index, boardSize);
            if (pieces[index] != null && pieces[index].Prefab == prefab)
                return;

            RemovePiece(index);
            if (prefab == null) return;

            var anchor = new GameObject($"Cell {index}").transform;
            anchor.SetParent(transform, false);
            anchor.localPosition = center;
            float cellSize = BoardGeometry.CellSize(boardSize);
            LineRenderer portalGlow = null;
            Transform portal = ringMaterial != null
                ? CreateCellPortal(anchor, cellSize, tint, ringMaterial, out portalGlow) : null;

            var pivot = new GameObject("Character motion").transform;
            pivot.SetParent(anchor, false);
            Transform model = Instantiate(prefab, pivot, false).transform;
            model.localPosition = Vector3.zero;
            model.localScale *= modelScale;
            FitModel(model, pivot);
            model.localScale *= CharacterDisplayMultiplier;

            var piece = new Piece
            {
                Prefab = prefab,
                Anchor = anchor,
                Pivot = pivot,
                Portal = portal,
                PortalGlow = portalGlow,
                Tint = tint,
                Animator = model.GetComponentInChildren<Animator>()
            };
            Transform[] bones = model.GetComponentsInChildren<Transform>();
            foreach (Transform bone in bones)
            {
                switch (bone.name)
                {
                    case "Arrow":
                        piece.Floats = true;
                        break;
                    case "Leg_L":
                        piece.LeftLeg = bone;
                        piece.LeftRest = bone.localRotation;
                        piece.LeftAxis = bone.InverseTransformDirection(model.right);
                        break;
                    case "Leg_R":
                        piece.RightLeg = bone;
                        piece.RightRest = bone.localRotation;
                        piece.RightAxis = bone.InverseTransformDirection(model.right);
                        break;
                }
            }
            pieces[index] = piece;
            if (index == CenterCell)
            {
                piece.ModelFacingOffset = MatchCharacterRoster.FrontFacingOffset(fighter);
                piece.CenterFacing = piece.ModelFacingOffset;
                piece.LandingPose = new CenterLandingPose(model, piece.Animator, fighter, bones);
                if (ringMaterial != null) CreateCenterBurst(piece, cellSize, ringMaterial);
            }
            PosePiece(piece, index, cellSize);
        }

        static Transform CreateCellPortal(Transform anchor, float size, Color tint, Material material,
            out LineRenderer glow)
        {
            var portal = new GameObject("Cell energy portal").transform;
            portal.SetParent(anchor, false);
            portal.localPosition = Vector3.up * (size * .055f);
            glow = DrawCellRing(portal, size * .38f, size * .23f, tint, material);
            Color rim = Color.Lerp(tint, new Color(.02f, .07f, .19f), .39f);
            DrawCellRing(portal, size * .39f, size * .09f, rim, material);
            DrawCellRing(portal, size * .47f, size * .07f, tint, material, turns: .82f);
            return portal;
        }

        static LineRenderer DrawCellRing(Transform parent, float radius, float width, Color tint,
            Material material, float turns = 1)
        {
            const int segments = 48;
            var ringObject = new GameObject("Energy ring");
            ringObject.transform.SetParent(parent, false);
            ringObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.alignment = LineAlignment.TransformZ;
            ring.loop = turns >= 1;
            ring.sharedMaterial = material;
            ring.startColor = ring.endColor = tint;
            ring.widthMultiplier = width;
            ring.positionCount = ring.loop ? segments : segments + 1;
            for (int index = 0; index < ring.positionCount; index++)
            {
                float angle = index * (turns * 2f * Mathf.PI / segments);
                ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
            return ring;
        }

        void CreateCenterBurst(Piece piece, float size, Material material)
        {
            piece.BurstRoot = new GameObject("Center landing burst").transform;
            piece.BurstRoot.gameObject.SetActive(false);
            piece.BurstRoot.SetParent(piece.Anchor, false);
            piece.BurstRoot.localPosition = Vector3.up * (size * .085f);
            piece.Shockwaves = new[]
            {
                DrawCellRing(piece.BurstRoot, size, size * .17f, piece.Tint, material),
                DrawCellRing(piece.BurstRoot, size, size * .09f, Color.white, material)
            };
            if (refractionMaterial == null)
            {
                var shader = Resources.Load<Shader>("MarubatsuVictory/LandingRefraction");
                if (shader != null && shader.isSupported) refractionMaterial = new Material(shader);
            }
            if (refractionMaterial != null)
                piece.Refraction = DrawCellRing(piece.BurstRoot, size, size * .75f, Color.white, refractionMaterial);

            piece.Flames = CreateLandingFlames(piece.BurstRoot, material);
            piece.Sparks = new LineRenderer[16];
            for (int i = 0; i < piece.Sparks.Length; i++)
            {
                var spark = new GameObject("Entry spark").AddComponent<LineRenderer>();
                spark.transform.SetParent(piece.BurstRoot, false);
                spark.useWorldSpace = false;
                spark.sharedMaterial = material;
                spark.startWidth = size * .015f;
                spark.endWidth = size * .065f;
                spark.positionCount = 2;
                piece.Sparks[i] = spark;
            }
            PoseCenterBurst(piece, size);
        }

        ParticleSystem CreateLandingFlames(Transform parent, Material ringMaterial)
        {
            if (flameMaterial == null)
            {
                var shader = Resources.Load<Shader>("MarubatsuVictory/LandingFlame");
                flameMaterial = shader != null && shader.isSupported
                    ? new Material(shader)
                    : new Material(ringMaterial);
                if (flameMaterial.HasProperty("_Profile")) flameMaterial.SetFloat("_Profile", 1f);
            }

            var flames = new GameObject("Surrounding cell fire").AddComponent<ParticleSystem>();
            flames.transform.SetParent(parent, false);
            flames.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = flames.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            int livePulses = Mathf.CeilToInt(FlameLifetime / FlameEmissionInterval);
            main.maxParticles = (pieces.Length - 1) *
                (IgnitionParticlesPerCell + livePulses * SustainedParticlesPerCell);
            var emission = flames.emission;
            emission.enabled = false;
            var shape = flames.shape;
            shape.enabled = false;

            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[]
            {
                new GradientAlphaKey(.75f, 0f), new GradientAlphaKey(1f, .15f),
                new GradientAlphaKey(.65f, .5f),
                new GradientAlphaKey(0f, 1f)
            });
            var color = flames.colorOverLifetime;
            color.enabled = true;
            color.color = fade;
            var sizeOverLifetime = flames.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, .72f), new Keyframe(.27f, 1f), new Keyframe(1f, .32f)));

            var renderer = flames.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = flameMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.15f;
            renderer.velocityScale = .12f;
            return flames;
        }

        void AdvanceFireColumns(Piece piece, float previousAge, float size)
        {
            Vector3 center = piece.Anchor.localPosition;
            for (int index = 0; index < pieces.Length; index++)
            {
                if (index == CenterCell) continue;
                Vector3 offset = BoardGeometry.CellCenter(index, boardSize) - center;
                float arrival = ShockwaveDuration * Mathf.InverseLerp(ShockwaveStartRadiusInCells,
                    ShockwaveRadiusInCells, offset.magnitude / size);
                float sinceIgnition = piece.BurstAge - arrival;
                if (sinceIgnition < 0f || sinceIgnition >= FlameEmissionDuration) continue;

                if (previousAge < arrival)
                {
                    EmitFireColumn(piece.Flames, offset, size, index, IgnitionParticlesPerCell);
                    continue;
                }

                int pulse = Mathf.FloorToInt(sinceIgnition / FlameEmissionInterval);
                int previousPulse = Mathf.FloorToInt((previousAge - arrival) / FlameEmissionInterval);
                if (pulse <= previousPulse) continue;

                float extinguish = Mathf.InverseLerp(FlameEmissionDuration - FlameFadeDuration,
                    FlameEmissionDuration, sinceIgnition);
                int count = Mathf.CeilToInt(Mathf.Lerp(SustainedParticlesPerCell, 1f, extinguish));
                EmitFireColumn(piece.Flames, offset, size, index + pulse * SustainedParticlesPerCell, count);
            }
        }

        static void EmitFireColumn(ParticleSystem flames, Vector3 offset, float size, int phase, int count)
        {
            if (!flames.isPlaying) flames.Play(false);
            for (int i = 0; i < count; i++)
            {
                int sample = i + phase;
                float angle = sample * 2.39996f;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float spread = (sample % 7) / 6f;
                float heat = (sample % 5) / 4f;
                bool smoke = i % 6 == 0;
                bool ember = i % 6 == 5;
                float radius = smoke ? .24f : .15f;
                float sideways = smoke ? .83f : ember ? 1.75f : .65f;
                float speed = smoke ? 2.0f : ember ? 4.4f : 2.2f + heat * 2.5f;
                Vector3 lift = Vector3.up * (smoke ? .47f : .08f);
                flames.Emit(new ParticleSystem.EmitParams
                {
                    position = offset + (lift + outward * (spread * radius)) * size,
                    velocity = (outward * (spread * sideways) + Vector3.up * speed) * size,
                    startSize = size * (smoke ? .58f : ember ? .085f : Mathf.Lerp(.61f, .34f, heat)),
                    startLifetime = smoke ? FlameLifetime : Mathf.Lerp(.28f, .5f, spread),
                    startColor = smoke ? new Color(.25f, .22f, .21f, .27f) :
                        Color.Lerp(new Color(1f, .27f, .045f), new Color(1f, .8f, .32f), heat)
                }, 1);
            }
        }

        static void PoseCenterBurst(Piece piece, float size)
        {
            for (int i = 0; i < piece.Shockwaves.Length; i++)
            {
                var ring = piece.Shockwaves[i];
                float delay = i * ShockwaveEchoDelay;
                float progress = Mathf.Clamp01((piece.BurstAge - delay) / ShockwaveDuration);
                ring.enabled = piece.BurstAge >= delay && progress < 1f;
                ring.transform.localScale = Vector3.one *
                    Mathf.Lerp(ShockwaveStartRadiusInCells, ShockwaveRadiusInCells, progress);
                Color tint = i == 0 ? piece.Tint : Color.Lerp(Color.white, piece.Tint, .3f);
                tint.a = .95f * (1f - Mathf.SmoothStep(0f, 1f, progress));
                ring.startColor = ring.endColor = tint;
            }

            if (piece.Refraction != null)
            {
                var front = piece.Shockwaves[0];
                piece.Refraction.enabled = front.enabled;
                piece.Refraction.transform.localScale = front.transform.localScale;
                Color strength = Color.white;
                strength.a = front.startColor.a;
                piece.Refraction.startColor = piece.Refraction.endColor = strength;
            }

            float travel = Mathf.Clamp01(piece.BurstAge / LandingSparkDuration);
            float fade = 1f - travel;
            for (int i = 0; i < piece.Sparks.Length; i++)
            {
                var spark = piece.Sparks[i];
                spark.enabled = fade > 0f;
                if (!spark.enabled) continue;
                float angle = i * (2f * Mathf.PI / piece.Sparks.Length);
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float distance = Mathf.Lerp(.22f, i % 2 == 0 ? 1.4f : 1.1f, 1f - fade * fade);
                float height = Mathf.Sin(travel * Mathf.PI) * (i % 2 == 0 ? .65f : .38f);
                Vector3 tip = (direction * distance + Vector3.up * height) * size;
                spark.SetPosition(0, tip - (direction * .24f + Vector3.up * .12f) * (size * fade));
                spark.SetPosition(1, tip);
                Color tint = piece.Tint;
                tint.a = 0f;
                spark.startColor = tint;
                tint = Color.Lerp(piece.Tint, Color.white, .72f);
                tint.a = fade;
                spark.endColor = tint;
            }
        }

        void FitModel(Transform model, Transform pivot)
        {
            float radius = 0f;
            float tiltAllowance = Mathf.Sin(14f * Mathf.Deg2Rad);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var bounds = renderer.localBounds;
                var toPivot = pivot.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 local = toPivot.MultiplyPoint3x4(point);
                    float cornerRadius = new Vector2(local.x, local.z).magnitude + Mathf.Abs(local.y) * tiltAllowance;
                    radius = Mathf.Max(radius, cornerRadius);
                }
            }

            float maximumRadius = BoardGeometry.CellSize(boardSize) * 0.73f / EntryScalePeak;
            if (radius > maximumRadius) model.localScale *= maximumRadius / radius;
        }

        public void Clear()
        {
            for (int index = 0; index < pieces.Length; index++)
                RemovePiece(index);
        }

        internal bool TryControlPose(int index, out Transform pivot, out Animator animator)
        {
            Piece piece = pieces[index];
            pivot = piece?.Pivot;
            animator = piece?.Animator;
            if (piece == null) return false;
            if (piece.BurstRoot != null && !piece.BurstRoot.gameObject.activeSelf)
                RemoveCenterBurst(piece);
            ReleaseLandingPose(piece);
            piece.PoseControlled = true;
            return true;
        }

        internal void ReleasePose(int index)
        {
            if (pieces[index] != null) pieces[index].PoseControlled = false;
        }

        void LateUpdate()
        {
            float cellSize = BoardGeometry.CellSize(boardSize);
            for (int index = 0; index < pieces.Length; index++)
            {
                Piece piece = pieces[index];
                if (piece == null) continue;
                if (piece.BurstRoot != null) AdvanceCenterBurst(piece, cellSize);
                if (piece.PoseControlled) continue;
                piece.Age += Time.deltaTime;
                if (piece.Portal != null)
                {
                    float arrival = Mathf.Clamp01(piece.Age * 2);
                    piece.Portal.localScale = Vector3.one * Mathf.SmoothStep(.68f, 1f, arrival);
                    piece.Portal.localRotation = Quaternion.Euler(0, piece.Age * 82, 0);
                    Color tint = piece.Tint;
                    tint.a = .38f + (1 - arrival) * .42f + Mathf.Sin(piece.Age * 3) * .09f;
                    piece.PortalGlow.startColor = piece.PortalGlow.endColor = tint;
                }
                PosePiece(piece, index, cellSize);
            }
        }

        void AdvanceCenterBurst(Piece piece, float cellSize)
        {
            if (!piece.BurstRoot.gameObject.activeSelf)
            {
                if (piece.Age + Time.deltaTime < CenterEntryDuration) return;
                piece.BurstRoot.gameObject.SetActive(true);
                return;
            }

            float previousAge = piece.BurstAge;
            piece.BurstAge += Time.deltaTime;
            if (piece.BurstAge >= ShockwaveDuration + FlameEmissionDuration + FlameLifetime)
            {
                RemoveCenterBurst(piece);
                return;
            }

            if (previousAge < ShockwaveDuration + FlameEmissionDuration)
                AdvanceFireColumns(piece, previousAge, cellSize);
            if (previousAge <= LandingSparkDuration) PoseCenterBurst(piece, cellSize);
        }

        static void RemoveCenterBurst(Piece piece)
        {
            piece.BurstRoot.gameObject.SetActive(false);
            Destroy(piece.BurstRoot.gameObject);
            piece.BurstRoot = null;
            piece.Refraction = null;
            piece.Shockwaves = piece.Sparks = null;
            piece.Flames = null;
        }

        void PosePiece(Piece piece, int index, float cellSize)
        {
            bool isCenter = index == CenterCell;
            float entryDuration = isCenter ? CenterEntryDuration : liftDuration;
            float entry = entryDuration > 0f ? Mathf.Clamp01(piece.Age / entryDuration) : 1f;
            float rise = Mathf.SmoothStep(0f, 1f, entry);
            float entryHop = entry < 1f ? Mathf.Sin(Mathf.PI * entry) : 0f;
            float timeSinceLanding = piece.Age - entryDuration;
            if (timeSinceLanding >= CenterLandingPose.RecoverySeconds) ReleaseLandingPose(piece);
            float dip = piece.LandingPose?.Sample(entry, timeSinceLanding) ?? 0f;
            float motion = isCenter ? Mathf.SmoothStep(0f, 1f,
                timeSinceLanding / CenterLandingPose.RecoverySeconds) : rise;
            float phase = piece.Age * (piece.Floats ? 2.2f : 2.7f) + index * 0.9f;
            float x = Mathf.Sin(phase);
            float z = Mathf.Cos(phase);
            float stride = Mathf.Sin(phase * 4f);
            float bob = piece.Floats ? (1f + stride) * 0.5f : Mathf.Abs(stride);
            var offset = new Vector3(x * cellSize * PatrolWidth, bob * cellSize * 0.13f, z * cellSize * PatrolDepth);
            float restingLift = piece.Floats ? liftHeight : liftHeight * 0.35f;
            float lift = Mathf.Max(0f, restingLift * rise - dip * cellSize);
            float hopHeight = EntryHopHeightInCells * (isCenter ? CenterHopMultiplier : 1f);
            piece.Pivot.localPosition = offset * motion +
                Vector3.up * (lift + entryHop * cellSize * hopHeight);
            float scale = isCenter
                ? Mathf.Lerp(CenterStartScale, 1f, rise)
                : Mathf.Min(EntryScalePeak, Mathf.Lerp(0.62f, 1f, rise) + entryHop * 0.46f);
            piece.Pivot.localScale = Vector3.one * scale;

            float yaw = piece.Floats ? x * 46f : Mathf.Atan2(z * PatrolWidth, -x * PatrolDepth) * Mathf.Rad2Deg;
            var facing = Quaternion.Euler(0f, yaw, stride * (piece.Floats ? 14f : 9f));
            Quaternion patrolFacing = Quaternion.Slerp(Quaternion.identity, facing, motion);
            if (isCenter)
            {
                float blendAge = timeSinceLanding - CenterLandingPose.RecoverySeconds;
                if (blendAge <= 0f) UpdateCenterFacing(piece);
                float blend = Mathf.SmoothStep(0f, 1f, blendAge / CenterFacingBlendSeconds);
                patrolFacing = Quaternion.Slerp(piece.CenterFacing, patrolFacing, blend);
            }
            piece.Pivot.localRotation = patrolFacing;
            if (piece.Floats || piece.LandingPose != null) return;

            float swing = stride * 35f * motion;
            if (piece.LeftLeg != null)
                piece.LeftLeg.localRotation = piece.LeftRest * Quaternion.AngleAxis(swing, piece.LeftAxis);
            if (piece.RightLeg != null)
                piece.RightLeg.localRotation = piece.RightRest * Quaternion.AngleAxis(-swing, piece.RightAxis);
        }

        void UpdateCenterFacing(Piece piece)
        {
            if (viewCamera == null) return;
            Vector3 towardCamera = piece.Anchor.InverseTransformPoint(viewCamera.transform.position);
            towardCamera.y = 0f;
            if (towardCamera.sqrMagnitude < .000001f)
            {
                // With the camera directly overhead, face the bottom of its image.
                towardCamera = piece.Anchor.InverseTransformDirection(-viewCamera.transform.up);
                towardCamera.y = 0f;
            }
            if (towardCamera.sqrMagnitude >= .000001f)
                piece.CenterFacing = Quaternion.LookRotation(towardCamera, Vector3.up) * piece.ModelFacingOffset;
        }

        static void ReleaseLandingPose(Piece piece)
        {
            piece.LandingPose?.Release();
            piece.LandingPose = null;
        }

        void RemovePiece(int index)
        {
            if (pieces[index] != null)
            {
                GameObject anchor = pieces[index].Anchor.gameObject;
                anchor.SetActive(false);
                Destroy(anchor);
            }
            pieces[index] = null;
        }

        void OnDestroy()
        {
            if (refractionMaterial != null) Destroy(refractionMaterial);
            if (flameMaterial != null) Destroy(flameMaterial);
        }
    }

    sealed class CenterLandingPose
    {
        internal const float RecoverySeconds = .52f;
        const float HoldSeconds = .10f;

        struct Joint
        {
            readonly Transform bone;
            readonly Quaternion rest;
            readonly Vector3 axis;

            public Joint(Transform transform, Vector3 rotationAxis)
            {
                bone = transform;
                rest = bone.localRotation;
                axis = bone.InverseTransformDirection(rotationAxis);
            }

            public void Rotate(float degrees)
            {
                if (bone != null) bone.localRotation = rest * Quaternion.AngleAxis(degrees, axis);
            }
        }

        readonly struct Pose
        {
            public readonly Vector2 Arms, Legs;
            public readonly float Lean, Nod, Accent, Dip;

            public Pose(float leftArm, float rightArm, float leftLeg, float rightLeg,
                float lean, float nod, float accent, float dip)
            {
                Arms = new Vector2(leftArm, rightArm);
                Legs = new Vector2(leftLeg, rightLeg);
                Lean = lean;
                Nod = nod;
                Accent = accent;
                Dip = dip;
            }
        }

        readonly Animator animator;
        readonly bool animatorWasEnabled;
        readonly Pose flight, impact;
        Joint leftArm, rightArm, leftLeg, rightLeg, body, head, accent;

        internal CenterLandingPose(Transform model, Animator animator, VictoryFighter fighter, Transform[] bones)
        {
            this.animator = animator;
            animatorWasEnabled = animator != null && animator.enabled;
            if (animatorWasEnabled) animator.enabled = false;

            (flight, impact) = fighter switch
            {
                VictoryFighter.Yagapon => (new Pose(-42f, 27f, 20f, 12f, -12f, 10f, -18f, 0f),
                    new Pose(-64f, 39f, -22f, -15f, 17f, -9f, 24f, .08f)),
                VictoryFighter.Mitanuki => (new Pose(27f, -31f, 24f, 18f, -9f, 7f, -25f, 0f),
                    new Pose(44f, -52f, -31f, -28f, 25f, -14f, 36f, .16f)),
                _ => (new Pose(-26f, 18f, 31f, -13f, -11f, 6f, 0f, 0f),
                    new Pose(-64f, 12f, -34f, 18f, 20f, -10f, 0f, .12f))
            };

            bool yagapon = fighter == VictoryFighter.Yagapon;
            Vector3 spread = yagapon ? model.right : model.forward;
            Vector3 pitch = yagapon ? model.forward : model.right;
            foreach (Transform bone in bones)
            {
                switch (bone.name)
                {
                    case "Arm_L":
                        // Yaguchi braces with the left arm forward and the right arm out for balance.
                        Vector3 armAxis = spread;
                        if (fighter == VictoryFighter.Yaguchi)
                        {
                            float side = Vector3.Dot(bone.position - model.position, model.right);
                            armAxis = Mathf.Sign(side) * model.up;
                        }
                        leftArm = new Joint(bone, armAxis);
                        break;
                    case "Arm_R": rightArm = new Joint(bone, spread); break;
                    case "Leg_L": leftLeg = new Joint(bone, pitch); break;
                    case "Leg_R": rightLeg = new Joint(bone, pitch); break;
                    case "Body": body = new Joint(bone, pitch); break;
                    case "Head": head = new Joint(bone, pitch); break;
                    case "Arrow" when yagapon:
                    case "Tail_02" when fighter == VictoryFighter.Mitanuki:
                        accent = new Joint(bone, pitch);
                        break;
                }
            }
        }

        internal float Sample(float entry, float timeSinceLanding)
        {
            if (entry < 1f)
            {
                if (entry < .72f)
                    return Apply(default, flight, Ease(entry / .38f));
                return Apply(flight, impact, Ease(Mathf.InverseLerp(.72f, 1f, entry)));
            }

            return Apply(impact, default, Ease(Mathf.InverseLerp(HoldSeconds, RecoverySeconds, timeSinceLanding)));
        }

        float Apply(Pose from, Pose to, float progress)
        {
            Vector2 arms = Vector2.Lerp(from.Arms, to.Arms, progress);
            Vector2 legs = Vector2.Lerp(from.Legs, to.Legs, progress);
            leftArm.Rotate(arms.x);
            rightArm.Rotate(arms.y);
            leftLeg.Rotate(legs.x);
            rightLeg.Rotate(legs.y);
            body.Rotate(Mathf.Lerp(from.Lean, to.Lean, progress));
            head.Rotate(Mathf.Lerp(from.Nod, to.Nod, progress));
            accent.Rotate(Mathf.Lerp(from.Accent, to.Accent, progress));
            return Mathf.Lerp(from.Dip, to.Dip, progress);
        }

        static float Ease(float progress) => Mathf.SmoothStep(0f, 1f, progress);

        internal void Release()
        {
            Apply(default, default, 0f);
            if (animatorWasEnabled && animator != null) animator.enabled = true;
        }
    }
}
