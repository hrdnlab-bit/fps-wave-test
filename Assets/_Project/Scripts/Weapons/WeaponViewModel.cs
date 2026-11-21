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

        [Header("Animazione procedurale (armi senza Animator)")]
        [Tooltip("Radice del modello mossa via codice per estrazione, rinculo e ricarica.")]
        [SerializeField] private Transform _proceduralRoot;
        [Tooltip("Carrello/otturatore che arretra a ogni colpo (opzionale).")]
        [SerializeField] private Transform _slide;
        [SerializeField, Min(0f)] private float _slideTravel = 0.025f;
        [SerializeField, Min(0.01f)] private float _drawTime = 0.35f;
        [SerializeField, Min(0f)] private float _fireKickPitch = 2.5f;
        [SerializeField, Min(0f)] private float _fireKickBack = 0.014f;

        private Vector3 _restPos;
        private Quaternion _restRot = Quaternion.identity;
        private Vector3 _slideRest;
        private Vector3 _slideBack;
        private float _drawElapsed = float.MaxValue;
        private float _kick;
        private float _reloadElapsed = float.MaxValue;
        private float _reloadDuration = 1f;

        public Transform Muzzle => _muzzle;

        private bool IsProcedural => _animator == null && _proceduralRoot != null;

        private void Awake()
        {
            if (_proceduralRoot == null)
                return;
            _restPos = _proceduralRoot.localPosition;
            _restRot = _proceduralRoot.localRotation;
            if (_slide != null && _slide.parent != null)
            {
                _slideRest = _slide.localPosition;
                // Direzione "indietro" dell'arma espressa nello spazio del genitore del carrello.
                _slideBack = _slide.parent.InverseTransformVector(-_proceduralRoot.forward).normalized;
            }
        }

        /// <summary>Animazione di estrazione all'equipaggiamento.</summary>
        public void PlayDraw()
        {
            if (IsProcedural) { _drawElapsed = 0f; _reloadElapsed = float.MaxValue; return; }
            Play(DrawHash, 0f);
        }

        /// <summary>Riparte da capo a ogni colpo: alle cadenze alte si vede solo la parte iniziale del rinculo, come previsto.</summary>
        public void PlayFire()
        {
            if (IsProcedural) { _kick = 1f; return; }
            Play(FireHash, 0f);
        }

        /// <summary>Ricarica, con velocità adattata alla durata effettiva <paramref name="duration"/> in secondi.</summary>
        public void PlayReload(bool empty, float duration)
        {
            if (duration <= 0f)
                return;
            if (IsProcedural) { _reloadElapsed = 0f; _reloadDuration = duration; return; }
            if (_animator == null)
                return;
            float clip = empty ? _reloadEmptyClipLength : _reloadClipLength;
            _animator.SetFloat(ReloadSpeedHash, clip / duration);
            Play(empty ? ReloadEmptyHash : ReloadHash, _crossFade);
        }

        /// <summary>Interrompe una ricarica (es. cambio arma annullato) tornando a Idle.</summary>
        public void ReturnToIdle()
        {
            if (IsProcedural) { _reloadElapsed = float.MaxValue; return; }
            Play(IdleHash, _crossFade);
        }

        // Le clip animano anche l'osso camera (la "testa" segue la ricarica): la camera del player resta ferma,
        // quindi si muove il rig in modo che l'osso camera coincida con questo oggetto. Dopo l'Animator, zero allocazioni.
        private void LateUpdate()
        {
            if (IsProcedural)
            {
                UpdateProcedural(Time.deltaTime);
                return;
            }
            if (_rig == null || _cameraBone == null)
                return;
            Quaternion wanted = transform.rotation * _cameraBoneRestRotation;
            Quaternion correction = wanted * Quaternion.Inverse(_cameraBone.rotation);
            _rig.rotation = correction * _rig.rotation;
            _rig.position += transform.position - _cameraBone.position;
        }

        /// <summary>Estrazione dal basso, calcio e carrello a ogni colpo, abbassamento con rotazione durante la ricarica.</summary>
        private void UpdateProcedural(float dt)
        {
            Vector3 pos = _restPos;
            Vector3 euler = Vector3.zero;

            if (_drawElapsed < _drawTime)
            {
                _drawElapsed += dt;
                float k = 1f - Ease(Mathf.Clamp01(_drawElapsed / _drawTime));
                pos += new Vector3(0f, -0.12f, -0.04f) * k;
                euler.x += 35f * k;
            }

            if (_reloadElapsed < _reloadDuration)
            {
                _reloadElapsed += dt;
                float k = Mathf.Sin(Mathf.Clamp01(_reloadElapsed / _reloadDuration) * Mathf.PI); // giù e su
                pos += new Vector3(-0.02f, -0.07f, -0.03f) * k;
                euler += new Vector3(12f, -10f, 35f) * k;
            }

            _kick = Mathf.MoveTowards(_kick, 0f, dt * 10f);
            float kick = _kick * _kick;
            pos.z -= _fireKickBack * kick;
            euler.x -= _fireKickPitch * kick;

            _proceduralRoot.localPosition = pos;
            _proceduralRoot.localRotation = _restRot * Quaternion.Euler(euler);
            if (_slide != null)
                _slide.localPosition = _slideRest + _slideBack * (_slideTravel * Mathf.Clamp01(_kick * 1.6f));
        }

        private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

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
        public void EditorSetupProcedural(Transform proceduralRoot, Transform slide, Transform muzzle, float slideTravel)
        {
            _animator = null;
            _rig = null;
            _cameraBone = null;
            _proceduralRoot = proceduralRoot;
            _slide = slide;
            _muzzle = muzzle;
            _slideTravel = slideTravel;
        }

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
