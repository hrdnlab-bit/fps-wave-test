using UnityEngine;

namespace HrdnLab.FpsWave.Audio
{
    /// <summary>
    /// Riproduzione dei suoni, solo cosmetica e locale: pool fisso di AudioSource (nessuna allocazione in gioco).
    /// <see cref="Play2D"/> per i suoni "in testa" (il proprio sparo, hit marker, voce della tuta),
    /// <see cref="PlayAt"/> per quelli nel mondo (spari degli altri, impatti, nemici).
    /// Senza libreria (asset non generati) resta muto senza errori. Volume generale: AudioListener (GameSettings).
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private const int Pool2D = 12;
        private const int Pool3D = 28;

        private static GameAudio s_instance;
        private static bool s_searched;
        private static SoundLibrary s_library;

        private AudioSource[] _sources2D;
        private AudioSource[] _sources3D;
        private int _next2D;
        private int _next3D;
        private float[] _lastPlayed;

        public static SoundLibrary Library
        {
            get
            {
                if (!s_searched)
                {
                    s_searched = true;
                    s_library = Resources.Load<SoundLibrary>("SoundLibrary");
                }
                return s_library;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_instance = null;
            s_searched = false;
            s_library = null;
        }

        private static GameAudio Instance
        {
            get
            {
                if (s_instance != null)
                    return s_instance;
                var go = new GameObject("[GameAudio]");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<GameAudio>();
                s_instance.Build();
                return s_instance;
            }
        }

        private void Build()
        {
            _lastPlayed = new float[(int)SoundId.Count];
            _sources2D = new AudioSource[Pool2D];
            for (int i = 0; i < Pool2D; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _sources2D[i] = s;
            }
            _sources3D = new AudioSource[Pool3D];
            for (int i = 0; i < Pool3D; i++)
            {
                var child = new GameObject("Src3D");
                child.transform.SetParent(transform, false);
                var s = child.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = 2f;
                s.dopplerLevel = 0f;
                _sources3D[i] = s;
            }
        }

        /// <summary>Suono "in testa" (non posizionale). <paramref name="volumeScale"/> moltiplica il volume della voce.</summary>
        public static void Play2D(SoundId id, float volumeScale = 1f, float pitchScale = 1f)
        {
            SoundCue cue = Resolve(id);
            if (cue == null)
                return;
            GameAudio audio = Instance;
            if (!audio.Allow(id, cue))
                return;
            AudioClip clip = cue.Pick();
            if (clip == null)
                return;
            AudioSource source = audio._sources2D[audio._next2D];
            audio._next2D = (audio._next2D + 1) % Pool2D;
            source.pitch = Random.Range(cue.Pitch.x, cue.Pitch.y) * pitchScale;
            source.PlayOneShot(clip, cue.Volume * volumeScale);
        }

        /// <summary>Suono nel mondo, attenuato con la distanza.</summary>
        public static void PlayAt(SoundId id, Vector3 position, float volumeScale = 1f, float pitchScale = 1f)
        {
            SoundCue cue = Resolve(id);
            if (cue == null)
                return;
            Camera cam = Camera.main;
            if (cam != null && (cam.transform.position - position).sqrMagnitude > cue.MaxDistance * cue.MaxDistance)
                return; // troppo lontano: niente sorgente sprecata
            GameAudio audio = Instance;
            if (!audio.Allow(id, cue))
                return;
            AudioClip clip = cue.Pick();
            if (clip == null)
                return;
            AudioSource source = audio._sources3D[audio._next3D];
            audio._next3D = (audio._next3D + 1) % Pool3D;
            source.transform.position = position;
            source.maxDistance = cue.MaxDistance;
            source.pitch = Random.Range(cue.Pitch.x, cue.Pitch.y) * pitchScale;
            source.clip = clip;
            source.volume = cue.Volume * volumeScale;
            source.Play();
        }

        private static SoundCue Resolve(SoundId id)
        {
            if (id == SoundId.None)
                return null;
            SoundLibrary library = Library;
            return library != null ? library.Get(id) : null;
        }

        private bool Allow(SoundId id, SoundCue cue)
        {
            if (cue.MinInterval <= 0f)
                return true;
            float now = Time.unscaledTime;
            if (now - _lastPlayed[(int)id] < cue.MinInterval)
                return false;
            _lastPlayed[(int)id] = now;
            return true;
        }
    }
}
