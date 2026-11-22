using System;
using UnityEngine;

namespace HrdnLab.FpsWave.Audio
{
    /// <summary>Tutti i suoni del gioco: ogni voce ha uno slot in <see cref="SoundLibrary"/>.</summary>
    public enum SoundId : byte
    {
        None = 0,
        RifleShotClose, RifleShotFar,
        PistolShotClose, PistolShotFar,
        ImpactSurface, ImpactMetal, ImpactFlesh,
        HitMarker, HitMarkerPrecision, KillConfirm,
        ArmorPlateHit, ArmorPlateBreak,
        Footstep, Land,
        AbilityOvercharge, AbilityArmor, AbilityScan, AbilityDenied, AbilityReady,
        SuitCallout, SuitAlarm,
        Ping,
        ReviveTick, ReviveDone, Downed,
        EnemyHitPlayer,
        BruteWindup, BruteChargeImpact,
        Count,
    }

    /// <summary>Un suono: più varianti, volume, variazione di tono, 2D/3D e distanza.</summary>
    [Serializable]
    public sealed class SoundCue
    {
        public SoundId Id;
        public AudioClip[] Clips = Array.Empty<AudioClip>();
        [Range(0f, 1.5f)] public float Volume = 1f;
        [Tooltip("Variazione casuale del tono (min, max): evita l'effetto mitragliatrice sullo stesso campione.")]
        public Vector2 Pitch = new(0.95f, 1.05f);
        [Tooltip("Distanza oltre cui il suono 3D non si sente più (m).")]
        [Min(1f)] public float MaxDistance = 60f;
        [Tooltip("Intervallo minimo tra due riproduzioni (s): niente suoni accatastati nello stesso frame.")]
        [Min(0f)] public float MinInterval = 0f;

        public AudioClip Pick()
        {
            if (Clips == null || Clips.Length == 0)
                return null;
            return Clips[Clips.Length == 1 ? 0 : UnityEngine.Random.Range(0, Clips.Length)];
        }
    }

    /// <summary>
    /// Libreria dei suoni (Resources/SoundLibrary.asset, generata da FpsWave/Setup/9. Build Audio).
    /// I file originali stanno in Assets/ThirdParty/Audio (fuori dal repo, `tools/setup-audio-assets.ps1`),
    /// gli spari rifiniti (tagliati e ricampionati) in Assets/ThirdParty/_Own/Audio.
    /// </summary>
    [CreateAssetMenu(menuName = "FpsWave/Sound Library", fileName = "SoundLibrary")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [SerializeField] private SoundCue[] _cues = Array.Empty<SoundCue>();

        private SoundCue[] _byId;

        public SoundCue Get(SoundId id)
        {
            if (_byId == null || _byId.Length != (int)SoundId.Count)
            {
                _byId = new SoundCue[(int)SoundId.Count];
                foreach (SoundCue cue in _cues)
                    if (cue != null && cue.Id > SoundId.None && cue.Id < SoundId.Count)
                        _byId[(int)cue.Id] = cue;
            }
            int index = (int)id;
            return index > 0 && index < _byId.Length ? _byId[index] : null;
        }

#if UNITY_EDITOR
        public void EditorSetCues(SoundCue[] cues)
        {
            _cues = cues;
            _byId = null;
        }
#endif
    }
}
