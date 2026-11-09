using UnityEngine;
using UnityEngine.Rendering;

namespace Marubatsu.Presentation
{
    sealed class VictoryEffectController : MonoBehaviour
    {
        const int RingPoints = 48;
        const int RibbonPoints = 26;
        static readonly Color Ice = new Color32(30, 157, 255, 255);
        static readonly Color Ember = new Color32(255, 70, 15, 255);
        static readonly Color Gold = new Color32(255, 196, 70, 255);

        sealed class CellLight
        {
            public LineRenderer Ring, Beam, Ribbon, Sigil, SecondStroke, PunchWave;
            public Transform Shadow;
            public Vector3 Center;
        }

        readonly CellLight[] cells = new CellLight[3];
        LineRenderer energy, core, pulse, halo;
        ParticleSystem sparks, flakes, stars;
        Material glow, edge, soft, confetti, starLight, shadow;
        Mesh shadowMesh;
        Vector3 middle;
        float unit, ringRadius;
        Color sideColor, motionColor;
        VictoryFighter fighter;
        bool draw;

        public void Build(float cellSize)
        {
            unit = cellSize;
            var shader = Resources.Load<Shader>("MarubatsuVictory/VictoryLight");
            glow = new Material(shader);
            edge = new Material(shader);
            edge.SetFloat("_Destination", (float)BlendMode.OneMinusSrcAlpha);
            soft = new Material(shader);
            soft.SetFloat("_Profile", 1);
            confetti = new Material(shader);
            confetti.SetFloat("_Profile", 2);
            starLight = new Material(shader);
            starLight.SetFloat("_Profile", 3);
            shadow = new Material(shader);
            shadow.SetFloat("_Profile", 1);
            shadow.SetColor("_Tint", new Color(.04f, .02f, .09f, .52f));
            shadow.SetFloat("_Destination", (float)BlendMode.OneMinusSrcAlpha);
            shadow.renderQueue = 2980;
            shadowMesh = CreateShadowMesh();

            energy = Line("Energy path", 2);
            core = Line("Energy core", 2);
            core.sharedMaterial = edge;
            pulse = Line("Shock wave", 2);
            halo = Ring("Victory halo");
            for (int i = 0; i < cells.Length; i++)
            {
                var beam = Line("Launch column", 2);
                beam.widthCurve = AnimationCurve.Linear(0, 1, 1, .16f);
                cells[i] = new CellLight
                {
                    Ring = Ring("Launch ring"),
                    Beam = beam,
                    Ribbon = Line("Flight ribbon", RibbonPoints),
                    Sigil = Line("Character emblem", RingPoints),
                    SecondStroke = Line("Cross stroke", 2),
                    PunchWave = Ring("Punch shock wave"),
                    Shadow = Shadow()
                };
            }

            sparks = Particles("Energy sparks", soft, 150, false);
            flakes = Particles("Confetti", confetti, 100, true);
            stars = Particles("Stars", starLight, 28, true);
            Stop();
        }

        public void Begin(Vector3[] centers, Vector3 boardCenter, Color tint, VictoryFighter character,
            float radiusInCells, bool isDraw)
        {
            Stop();
            gameObject.SetActive(true);
            draw = isDraw;
            sideColor = tint;
            fighter = character;
            motionColor = character == VictoryFighter.Yaguchi ? Ember :
                character == VictoryFighter.Yagapon ? Ice : Gold;
            middle = isDraw ? boardCenter : centers[1];
            ringRadius = unit * radiusInCells;
            if (draw) return;

            for (int i = 0; i < cells.Length; i++)
            {
                var light = cells[i];
                light.Center = centers[i] + Vector3.up * (unit * .025f);
                PlaceRing(light.Ring, light.Center, ringRadius);
                light.Shadow.localPosition = centers[i] + Vector3.up * (unit * .01f);
                light.Shadow.gameObject.SetActive(true);
                light.Beam.SetPosition(0, light.Center);
            }
            Vector3 lineLift = Vector3.up * (unit * .06f);
            energy.SetPosition(0, centers[0] + lineLift);
            core.SetPosition(0, centers[0] + lineLift);
        }

        public void Charge(float progress, float afterCharge)
        {
            if (draw) return;
            float path = Mathf.Clamp01(progress * 2.8f);
            Vector3 from = energy.GetPosition(0);
            Vector3 end = cells[2].Center + Vector3.up * (unit * .035f);
            Vector3 tip = Vector3.Lerp(from, end, path);
            energy.SetPosition(1, tip);
            core.SetPosition(1, tip);
            float flash = (1 - progress * .65f) * (1 - afterCharge);
            Style(energy, sideColor, .12f * flash, .75f * flash);
            Style(core, Color.Lerp(sideColor, Color.white, .18f), .062f, .65f * (1 - afterCharge) + .26f);
            float wave = Mathf.Clamp01(progress * 1.45f);
            pulse.SetPosition(0, Vector3.Lerp(from, end, Mathf.Max(0, wave - .16f)));
            pulse.SetPosition(1, Vector3.Lerp(from, end, wave));
            Style(pulse, Color.white, .072f, Mathf.Sin(wave * Mathf.PI));
        }

        public void DrawCell(int index, float charge, float lift, float spin, float fade,
            Vector3 airborne, Quaternion rotation, bool arrived, float punchAge)
        {
            var light = cells[index];
            float ringGlow = (1 - fade) * (.75f + .25f * Mathf.Sin(charge * Mathf.PI));
            Style(light.Ring, sideColor, .105f - .05f * fade, Mathf.Max(.45f, ringGlow));
            light.Shadow.localScale = new Vector3(unit * (.75f - .27f * lift), 1, unit * (.63f - .23f * lift));
            float chargeHeight = Mathf.Sin(charge * Mathf.PI) * .5f;
            light.Beam.SetPosition(1, airborne + Vector3.up * (unit * (.17f + chargeHeight)));
            Style(light.Beam, Color.Lerp(sideColor, motionColor, .6f), .52f,
                (1 - fade) * Mathf.Max(Mathf.Sin(charge * Mathf.PI) * .42f, lift * .42f));

            float trailStrength = (1 - fade) * Mathf.Max(Mathf.Sin(lift * Mathf.PI) * .7f, Mathf.Sin(spin * Mathf.PI));
            Style(light.Ribbon, motionColor, .19f, trailStrength);
            if (light.Ribbon.enabled)
            {
                float radius = index == 1 ? .62f : .48f;
                for (int i = 0; i < RibbonPoints; i++)
                {
                    float t = i / (RibbonPoints - 1f);
                    float angle = (lift * 1.4f + spin * 2f - t * .67f) * Mathf.PI * 2f;
                    Vector3 orbit = rotation * new Vector3(Mathf.Cos(angle) * radius, .33f, Mathf.Sin(angle) * radius);
                    light.Ribbon.SetPosition(i, airborne + (orbit - Vector3.up * (t * (.3f + .62f * (1 - lift)))) * unit);
                }
            }

            if (fighter == VictoryFighter.Yaguchi && punchAge >= 0f)
            {
                float wave = Mathf.Clamp01(punchAge / .32f);
                Style(light.PunchWave, Ember, .14f, (1 - wave) * .93f);
                if (light.PunchWave.enabled)
                    PlaceRing(light.PunchWave, airborne + Vector3.up * (unit * .42f), unit * (.17f + wave * .52f));
            }

            if (index != 1) return;
            float emblem = Mathf.InverseLerp(.1f, .86f, spin);
            float strength = (1 - fade) * Mathf.Clamp01(emblem * 4f);
            if (fighter == VictoryFighter.Yagapon)
            {
                Style(light.Sigil, Ice, .085f, strength);
                if (light.Sigil.enabled)
                    for (int i = 0; i < RingPoints; i++)
                    {
                        float angle = i / (RingPoints - 1f) * emblem * 2f * Mathf.PI;
                        light.Sigil.SetPosition(i, airborne + rotation * new Vector3(Mathf.Cos(angle) * .51f,
                            .45f + Mathf.Sin(angle) * .51f, -.19f) * unit);
                    }
            }
            else if (fighter == VictoryFighter.Yaguchi)
            {
                if (light.Sigil.positionCount != 2) light.Sigil.positionCount = 2;
                Vector3 center = airborne + rotation * (new Vector3(0, .42f, -.22f) * unit);
                Stroke(light.Sigil, center, rotation * new Vector3(.46f, .48f, 0) * unit,
                    Mathf.Clamp01(emblem * 2), strength);
                Stroke(light.SecondStroke, center, rotation * new Vector3(-.46f, .48f, 0) * unit,
                    Mathf.Clamp01(emblem * 2 - 1), strength);
            }
            if (arrived)
            {
                Vector3 center = light.Center + Vector3.up * (unit * .04f);
                Style(halo, Color.Lerp(sideColor, Gold, .5f), .12f, (1 - fade) * .88f);
                if (halo.enabled) PlaceRing(halo, center, unit * (.45f + 1.4f * fade));
            }
        }

        void Stroke(LineRenderer line, Vector3 center, Vector3 diagonal, float progress, float strength)
        {
            line.SetPosition(0, center - diagonal);
            line.SetPosition(1, center + diagonal * (2 * progress - 1));
            Style(line, Ember, .125f, strength * Mathf.Clamp01(progress * 8));
        }

        public void DrawTie(float progress)
        {
            PlaceRing(halo, middle + Vector3.up * (.04f * unit), unit * (.45f + progress));
            Style(halo, Gold, .068f, Mathf.Sin(progress * Mathf.PI) * .42f);
        }

        public void SparkAt(int index, int count)
        {
            Burst(sparks, cells[index].Center, count, .95f, sideColor);
        }

        public void LiftSparks(Vector3 at, int count)
        {
            Burst(sparks, at, count, .57f, motionColor);
        }

        public void Punch(Vector3 at, int count)
        {
            Burst(sparks, at + Vector3.up * (.4f * unit), count, 1.45f, Ember);
        }

        public void Celebrate(int count, float liftInCells)
        {
            if (draw)
            {
                Burst(flakes, middle + Vector3.up * (unit * .08f), count, .52f, Gold);
                return;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                int cellCount = (count + i) / cells.Length;
                int starCount = cellCount / 4;
                Vector3 center = cells[i].Center + Vector3.up * (unit * (liftInCells + .66f));
                Burst(flakes, center, cellCount - starCount, 1.55f, sideColor);
                Burst(stars, center, starCount, 1.95f, Gold);
            }
        }

        void Burst(ParticleSystem particles, Vector3 center, int count, float speed, Color accent)
        {
            if (count == 0) return;
            particles.Play();
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.39996f;
                float velocity = speed * unit * (.6f + (i % 9) * .08f);
                var parameters = new ParticleSystem.EmitParams
                {
                    position = center,
                    velocity = new Vector3(Mathf.Cos(angle), .22f + (i % 7) * .13f, Mathf.Sin(angle)) * velocity,
                    startColor = i % 3 == 0 ? Gold : i % 3 == 1 ? accent : Color.white,
                    rotation = angle * Mathf.Rad2Deg
                };
                particles.Emit(parameters, 1);
            }
        }

        LineRenderer Line(string label, int points)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.useWorldSpace = false;
            line.sharedMaterial = glow;
            line.positionCount = points;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        LineRenderer Ring(string label)
        {
            LineRenderer ring = Line(label, RingPoints);
            ring.alignment = LineAlignment.TransformZ;
            ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
            ring.loop = true;
            return ring;
        }

        static void PlaceRing(LineRenderer ring, Vector3 center, float radius)
        {
            ring.transform.localPosition = center;
            for (int i = 0; i < RingPoints; i++)
            {
                float angle = i * (2f * Mathf.PI / RingPoints);
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
            }
        }

        void Style(LineRenderer line, Color color, float width, float opacity)
        {
            line.enabled = opacity > .001f;
            if (!line.enabled) return;
            color.a = opacity;
            line.startColor = color;
            color.a *= line.positionCount == RibbonPoints ? .03f : 1f;
            line.endColor = color;
            line.widthMultiplier = width * unit;
        }

        ParticleSystem Particles(string label, Material material, int maxParticles, bool paper)
        {
            var particles = new GameObject(label).AddComponent<ParticleSystem>();
            particles.transform.SetParent(transform, false);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = maxParticles;
            main.startLifetime = paper ? new ParticleSystem.MinMaxCurve(.75f, 1.25f) :
                new ParticleSystem.MinMaxCurve(.16f, .47f);
            main.startSize = new ParticleSystem.MinMaxCurve(unit * .026f, unit * (paper ? .075f : .10f));
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.88f, .45f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            if (paper)
            {
                var velocity = particles.velocityOverLifetime;
                velocity.enabled = true;
                velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                velocity.y = new ParticleSystem.MinMaxCurve(-unit * .46f, -unit * .15f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                var rotation = particles.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-7, 7);
            }
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        static Mesh CreateShadowMesh()
        {
            var mesh = new Mesh { name = "Paper contact shadow" };
            mesh.vertices = new[] { new Vector3(-1, 0, -1), new Vector3(-1, 0, 1),
                new Vector3(1, 0, 1), new Vector3(1, 0, -1) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            return mesh;
        }

        Transform Shadow()
        {
            var obj = new GameObject("Paper contact shadow");
            obj.transform.SetParent(transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = shadowMesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = shadow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return obj.transform;
        }

        public void Pause(bool paused)
        {
            SetParticlePlayback(sparks, paused);
            SetParticlePlayback(flakes, paused);
            SetParticlePlayback(stars, paused);
        }

        static void SetParticlePlayback(ParticleSystem particles, bool paused)
        {
            if (paused && particles.isPlaying) particles.Pause();
            else if (!paused && particles.isPaused) particles.Play();
        }

        public void Stop()
        {
            if (sparks == null) return;
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            flakes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            energy.enabled = core.enabled = pulse.enabled = halo.enabled = false;
            foreach (var light in cells)
            {
                light.Ring.enabled = light.Beam.enabled = light.Ribbon.enabled = light.Sigil.enabled =
                    light.SecondStroke.enabled = light.PunchWave.enabled = false;
                light.Shadow.gameObject.SetActive(false);
            }
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            Destroy(glow);
            Destroy(edge);
            Destroy(soft);
            Destroy(confetti);
            Destroy(starLight);
            Destroy(shadow);
            Destroy(shadowMesh);
        }
    }
}
