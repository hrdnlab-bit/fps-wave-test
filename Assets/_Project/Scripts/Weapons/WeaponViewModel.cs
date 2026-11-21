using UnityEngine;

namespace HrdnLab.FpsWave.Weapons
{
    /// <summary>
    /// Modello in prima persona di un'arma (braccia + arma animate). Solo cosmetico e solo sul proprietario:
    /// <see cref="PlayerWeapons"/> lo comanda in base allo stato predetto dell'arma, nessun dato passa in rete.
    /// Lo stato dell'Animator è costruito da <c>WeaponViewModelBuilder</c> (Idle di base, gli altri tornano a Idle a fine clip).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponViewModel : MonoBehaviour
    {
        public const string IdleState = "Idle";
        public const string DrawState = "Draw";
        public const string FireState = "Fire";
        public const string ReloadState = "Reload";
        public const string ReloadEmptyState = "ReloadEmpty";
        public const string ReloadSpeedParameter = "ReloadSpeed";

        private static readonly int IdleHash = Animator.StringToHash(IdleState);
        private static readonly int DrawHash = Animator.StringToHash(DrawState);
        private static readonly int FireHash = Animator.StringToHash(FireState);
        private static readonly int ReloadHash = Animator.StringToHash(ReloadState);
        private static readonly int ReloadEmptyHash = Animator.StringToHash(ReloadEmptyState);
        private static readonly int ReloadSpeedHash = Animator.StringToHash(ReloadSpeedParameter);

        [SerializeField] private Animator _animator;
        [Tooltip("Punto di uscita di tracer e flash; segue le animazioni dell'arma.")]
        [SerializeField] private Transform _muzzle;
        [Tooltip("Durata delle clip di ricarica, per adattarle al tempo di ricarica dell'arma (che cambia con mod e upgrade).")]
        [SerializeField, Min(0.01f)] private float _reloadClipLength = 1f;
        [SerializeField, Min(0.01f)] private float _reloadEmptyClipLength = 1f;
        [SerializeField, Min(0f)] private float _crossFade = 0.06f;

        [Header("Allineamento alla camera")]
        [Tooltip("Radice del modello importato: viene spostata a ogni frame per tenere l'osso camera sulla camera del player.")]
        [SerializeField] private Transform _rig;
        [Tooltip("Osso camera animato del rig (es. FPS_Camera). Vuoto = nessuna compensazione.")]
        [SerializeField] private Transform _cameraBone;
        [Tooltip("Rotazione dell'osso camera rispetto a questo oggetto nella posa di riferimento.")]
        [SerializeField] private Quaternion _cameraBoneRestRotation = Quaternion.identity;

        public Transform Muzzle => _muzzle;

        /// <summary>Animazione di estrazione all'equipaggiamento.</summary>
        public void PlayDraw() => Play(DrawHash, 0f);

        /// <summary>Riparte da capo a ogni colpo: alle cadenze alte si vede solo la parte iniziale del rinculo, come previsto.</summary>
        public void PlayFire() => Play(FireHash, 0f);

        /// <summary>Ricarica, con velocità adattata alla durata effettiva <paramref name="duration"/> in secondi.</summary>
        public void PlayReload(bool empty, float duration)
        {
            if (_animator == null || duration <= 0f)
                return;
            float clip = empty ? _reloadEmptyClipLength : _reloadClipLength;
            _animator.SetFloat(ReloadSpeedHash, clip / duration);
            Play(empty ? ReloadEmptyHash : ReloadHash, _crossFade);
        }

        /// <summary>Interrompe una ricarica (es. cambio arma annullato) tornando a Idle.</summary>
        public void ReturnToIdle() => Play(IdleHash, _crossFade);

        // Le clip animano anche l'osso camera (la "testa" segue la ricarica): la camera del player resta ferma,
        // quindi si muove il rig in modo che l'osso camera coincida con questo oggetto. Dopo l'Animator, zero allocazioni.
        private void LateUpdate()
        {
            if (_rig == null || _cameraBone == null)
                return;
            Quaternion wanted = transform.rotation * _cameraBoneRestRotation;
            Quaternion correction = wanted * Quaternion.Inverse(_cameraBone.rotation);
            _rig.rotation = correction * _rig.rotation;
            _rig.position += transform.position - _cameraBone.position;
        }

        private void Play(int stateHash, float fade)
        {
            if (_animator == null || !_animator.isActiveAndEnabled)
                return;
            if (fade > 0f)
                _animator.CrossFadeInFixedTime(stateHash, fade, 0, 0f);
            else
                _animator.Play(stateHash, 0, 0f);
        }

#if UNITY_EDITOR
        public void EditorSetup(Animator animator, Transform muzzle, float reloadClipLength, float reloadEmptyClipLength,
            Transform rig, Transform cameraBone, Quaternion cameraBoneRestRotation)
        {
            _rig = rig;
            _cameraBone = cameraBone;
            _cameraBoneRestRotation = cameraBoneRestRotation;
            _animator = animator;
            _muzzle = muzzle;
            _reloadClipLength = Mathf.Max(0.01f, reloadClipLength);
            _reloadEmptyClipLength = Mathf.Max(0.01f, reloadEmptyClipLength);
        }
#endif
    }
}
