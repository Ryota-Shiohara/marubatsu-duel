using UnityEngine;

namespace Marubatsu.Presentation
{
    public sealed class ArcadeAudio : MonoBehaviour
    {
        const string ResourceFolder = "MarubatsuAudio/";
        const float MusicVolume = .36f;
        const float DuckVolume = .11f;

        GameSession session;
        AudioSource music, effects;
        AudioClip select, versus, circle, cross, victory, draw;
        float duckUntil;

        public void Initialize(GameSession game)
        {
            session = game;
            music = CreateSource();
            effects = CreateSource();
            music.clip = Load("Blueberry_Bounce");
            music.loop = true;
            music.volume = MusicVolume;
            effects.volume = .68f;

            select = Load("Select");
            versus = Load("Versus");
            circle = Load("Circle");
            cross = Load("Cross");
            victory = Load("Victory");
            draw = Load("Draw");
            session.OnSnapshotChanged += OnSnapshotChanged;
            if (music.clip != null) music.Play();
        }

        AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        AudioClip Load(string name)
        {
            AudioClip clip = Resources.Load<AudioClip>(ResourceFolder + name);
            if (clip == null) Debug.LogWarning($"[ArcadeAudio] Audio clip is missing: {name}", this);
            return clip;
        }

        public void PlaySelect() => Play(select);
        public void PlayVersus() => Play(versus, duckMusic: true);

        void OnSnapshotChanged(BoardSnapshot snapshot)
        {
            if (!snapshot.LastMove.HasValue) return;
            switch (snapshot.Outcome)
            {
                case OutcomeType.Playing:
                    Play(snapshot.LastMove.Value.Mark == MarkType.Circle ? circle : cross);
                    break;
                case OutcomeType.Draw:
                    Play(draw, duckMusic: true);
                    break;
                case OutcomeType.CircleWon:
                case OutcomeType.CrossWon:
                    Play(victory, duckMusic: true);
                    break;
            }
        }

        void Play(AudioClip clip, bool duckMusic = false)
        {
            if (clip == null) return;
            if (duckMusic)
                duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + clip.length);
            effects.PlayOneShot(clip);
        }

        void Update()
        {
            if (music == null) return;
            bool ducking = Time.unscaledTime < duckUntil;
            float target = ducking ? DuckVolume : MusicVolume;
            music.volume = Mathf.MoveTowards(music.volume, target, Time.unscaledDeltaTime * (ducking ? 4f : .65f));
        }

        void OnDestroy()
        {
            if (session != null) session.OnSnapshotChanged -= OnSnapshotChanged;
        }
    }
}
