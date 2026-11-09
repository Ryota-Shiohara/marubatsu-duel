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
        AudioClip menuMusic, battleMusic, victoryMusic;
        AudioClip select, versus, circle, cross, victory, draw;
        bool inBattle;
        float duckUntil;

        public void Initialize(GameSession game)
        {
            session = game;
            music = CreateSource();
            effects = CreateSource();
            music.loop = true;
            effects.volume = .68f;

            menuMusic = Load("Blueberry_Bounce");
            battleMusic = Load("Crown_of_the_Arena");
            victoryMusic = Load("Podium_Finish");
            select = Load("Select");
            versus = Load("Versus");
            circle = Load("Circle");
            cross = Load("Cross");
            victory = Load("Victory");
            draw = Load("Draw");
            session.OnSnapshotChanged += OnSnapshotChanged;
            SwitchMusic(menuMusic);
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
        public void PlayVersus()
        {
            effects.Stop();
            Play(versus, duckMusic: true);
        }

        public void BeginBattle()
        {
            inBattle = true;
            SwitchMusic(battleMusic);
        }

        public void LeaveBattle()
        {
            inBattle = false;
            SwitchMusic(menuMusic);
        }

        void OnSnapshotChanged(BoardSnapshot snapshot)
        {
            if (!inBattle) return;
            if (!snapshot.LastMove.HasValue)
            {
                SwitchMusic(battleMusic);
                return;
            }

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
                    SwitchMusic(victoryMusic);
                    Play(victory, duckMusic: true);
                    break;
            }
        }

        void SwitchMusic(AudioClip clip)
        {
            if (clip == null || music.clip == clip) return;
            music.Stop();
            music.clip = clip;
            music.volume = Time.unscaledTime < duckUntil ? DuckVolume : MusicVolume;
            music.Play();
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
