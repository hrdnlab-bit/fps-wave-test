using HrdnLab.FpsWave.Stats;
using UnityEngine;

namespace HrdnLab.FpsWave.Weapons
{
    public enum WeaponSlot : byte
    {
        Primary = 0,
        Secondary = 1,
    }

    /// <summary>
    /// Dati di base di un'arma. I valori effettivi in gioco passano da <see cref="CreateStats"/>,
    /// così upgrade e mod li modificano senza toccare l'asset.
    /// </summary>
    [CreateAssetMenu(menuName = "FpsWave/Weapon Definition", fileName = "Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Header("Identità")]
        [SerializeField] private string _id = "weapon";
        [SerializeField] private string _displayName = "Weapon";
        [SerializeField] private WeaponSlot _slot = WeaponSlot.Primary;

        [Header("Fuoco")]
        [SerializeField, Min(0f)] private float _damage = 20f;
        [SerializeField, Min(1f)] private float _roundsPerMinute = 600f;
        [SerializeField] private bool _automatic = true;
        [SerializeField, Min(1)] private int _magazineSize = 30;
        [SerializeField, Min(0.1f)] private float _reloadTime = 2f;
        [SerializeField, Min(1f)] private float _range = 100f;

        [Header("Precisione (gradi)")]
        [SerializeField, Min(0f)] private float _hipSpread = 2f;
        [SerializeField, Min(0f)] private float _adsSpread = 0.3f;
        [SerializeField, Min(0f)] private float _spreadPerShot = 0.3f;
        [SerializeField, Min(0f)] private float _maxBloom = 4f;
        [SerializeField, Min(0f)] private float _bloomRecoveryPerSecond = 8f;

        [Header("Rinculo (gradi)")]
        [SerializeField, Min(0f)] private float _recoilPitch = 1f;
        [SerializeField, Min(0f)] private float _recoilYaw = 0.3f;
        [SerializeField, Min(0f)] private float _recoilRecoveryPerSecond = 10f;

        [Header("Mira (ADS)")]
        [SerializeField, Range(0.3f, 1f)] private float _adsFovMultiplier = 0.8f;
        [SerializeField, Min(0.01f)] private float _adsTime = 0.2f;
        [Tooltip("Tempo per tornare a sparare dopo la corsa (s), come nei Call of Duty: l'arma deve rialzarsi.")]
        [SerializeField, Min(0f)] private float _sprintToFireTime = 0.2f;

        [Header("Critici e weakspot")]
        [SerializeField, Range(0f, 1f)] private float _critChance = 0.05f;
        [SerializeField, Min(1f)] private float _critMultiplier = 1.5f;
        [SerializeField, Min(1f)] private float _weakspotMultiplier = 2f;

        [Header("Visivo")]
        [Tooltip("Modello in prima persona. Se vuoto si usa il segnaposto greybox.")]
        [SerializeField] private GameObject _viewModelPrefab;
        [SerializeField] private Vector3 _placeholderSize = new(0.07f, 0.12f, 0.45f);
        [Tooltip("Se attivo, il modello usa le posizioni sotto invece di quelle generiche del player (i rig allineati alla camera partono da zero).")]
        [SerializeField] private bool _overrideViewOffsets;
        [SerializeField] private Vector3 _viewHipOffset = Vector3.zero;
        [SerializeField] private Vector3 _viewAdsOffset = Vector3.zero;

        [Header("Sensazione (cosmetica: rinculo, molle, oscillazioni, energia, calore)")]
        [SerializeField] private WeaponFeel _feel = new();

        [Header("Suoni")]
        [Tooltip("Sparo sentito da chi spara (in testa).")]
        [SerializeField] private HrdnLab.FpsWave.Audio.SoundId _fireSoundClose = HrdnLab.FpsWave.Audio.SoundId.RifleShotClose;
        [Tooltip("Sparo sentito dagli altri (nel mondo, con la distanza).")]
        [SerializeField] private HrdnLab.FpsWave.Audio.SoundId _fireSoundFar = HrdnLab.FpsWave.Audio.SoundId.RifleShotFar;

        public string Id => _id;
        public string DisplayName => _displayName;
        public WeaponSlot Slot => _slot;
        public bool Automatic => _automatic;
        public float MaxBloom => _maxBloom;
        public float BloomRecoveryPerSecond => _bloomRecoveryPerSecond;
        public float RecoilRecoveryPerSecond => _recoilRecoveryPerSecond;
        public float AdsFovMultiplier => _adsFovMultiplier;
        public float AdsTime => _adsTime;
        public float SprintToFireTime => _sprintToFireTime;
        public GameObject ViewModelPrefab => _viewModelPrefab;
        public Vector3 PlaceholderSize => _placeholderSize;
        public bool OverrideViewOffsets => _overrideViewOffsets && _viewModelPrefab != null;
        public Vector3 ViewHipOffset => _viewHipOffset;
        public Vector3 ViewAdsOffset => _viewAdsOffset;

        /// <summary>Crea un blocco di statistiche nuovo, indipendente dagli altri giocatori.</summary>
        public StatBlock CreateStats()
        {
            var stats = new StatBlock();
            stats.Define(StatType.Damage, _damage, 0f);
            stats.Define(StatType.RoundsPerMinute, _roundsPerMinute, 30f, 1800f);
            stats.Define(StatType.MagazineSize, _magazineSize, 1f, 500f);
            stats.Define(StatType.ReloadTime, _reloadTime, 0.2f, 10f);
            stats.Define(StatType.Range, _range, 1f, 1000f);
            stats.Define(StatType.HipSpread, _hipSpread, 0f, 30f);
            stats.Define(StatType.AdsSpread, _adsSpread, 0f, 30f);
            stats.Define(StatType.SpreadPerShot, _spreadPerShot, 0f, 10f);
            stats.Define(StatType.RecoilPitch, _recoilPitch, 0f, 20f);
            stats.Define(StatType.RecoilYaw, _recoilYaw, 0f, 20f);
            stats.Define(StatType.CritChance, _critChance, 0f, 1f);
            stats.Define(StatType.CritMultiplier, _critMultiplier, 1f, 20f);
            stats.Define(StatType.WeakspotMultiplier, _weakspotMultiplier, 1f, 20f);
            return stats;
        }

#if UNITY_EDITOR
        /// <summary>Usato dagli strumenti Editor per creare le armi di base.</summary>
        public void EditorSetup(string id, string displayName, WeaponSlot slot, float damage, float rpm, bool automatic,
            int magazine, float reload, float range, float hipSpread, float adsSpread, float spreadPerShot, float maxBloom,
            float recoilPitch, float recoilYaw, float adsFov, float adsTime, Vector3 placeholderSize)
        {
            _id = id; _displayName = displayName; _slot = slot; _damage = damage; _roundsPerMinute = rpm;
            _automatic = automatic; _magazineSize = magazine; _reloadTime = reload; _range = range;
            _hipSpread = hipSpread; _adsSpread = adsSpread; _spreadPerShot = spreadPerShot; _maxBloom = maxBloom;
            _recoilPitch = recoilPitch; _recoilYaw = recoilYaw; _adsFovMultiplier = adsFov; _adsTime = adsTime;
            _placeholderSize = placeholderSize;
        }

        /// <summary>Usato da <c>WeaponViewModelBuilder</c> per collegare il modello realistico.</summary>
        public WeaponFeel Feel => _feel ??= new WeaponFeel();
        public HrdnLab.FpsWave.Audio.SoundId FireSoundClose => _fireSoundClose;
        public HrdnLab.FpsWave.Audio.SoundId FireSoundFar => _fireSoundFar;

        public void EditorSetFeel(WeaponFeel feel) => _feel = feel;
        public void EditorSetSounds(HrdnLab.FpsWave.Audio.SoundId close, HrdnLab.FpsWave.Audio.SoundId far)
        {
            _fireSoundClose = close;
            _fireSoundFar = far;
        }

        public void EditorSetViewModel(GameObject prefab, Vector3 hipOffset, Vector3 adsOffset)
        {
            _viewModelPrefab = prefab;
            _overrideViewOffsets = prefab != null;
            _viewHipOffset = hipOffset;
            _viewAdsOffset = adsOffset;
        }
#endif
    }
}
