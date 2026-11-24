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
        public const string HolsterState = "Holster";
        public const string HolsterSpeedParameter = "HolsterSpeed";

        private static readonly int IdleHash = Animator.StringToHash(IdleState);
        private static readonly int DrawHash = Animator.StringToHash(DrawState);
        private static readonly int FireHash = Animator.StringToHash(FireState);
        private static readonly int ReloadHash = Animator.StringToHash(ReloadState);
        private static readonly int ReloadEmptyHash = Animator.StringToHash(ReloadEmptyState);
        private static readonly int ReloadSpeedHash = Animator.StringToHash(ReloadSpeedParameter);
        private static readonly int HolsterHash = Animator.StringToHash(HolsterState);
        private static readonly int HolsterSpeedHash = Animator.StringToHash(HolsterSpeedParameter);

        [SerializeField] private Animator _animator;
        [Tooltip("Punto di uscita di tracer e flash; segue le animazioni dell'arma.")]
        [SerializeField] private Transform _muzzle;
        [Tooltip("Durata delle clip di ricarica, per adattarle al tempo di ricarica dell'arma (che cambia con mod e upgrade).")]
        [SerializeField, Min(0.01f)] private float _reloadClipLength = 1f;
        [SerializeField, Min(0.01f)] private float _reloadEmptyClipLength = 1f;
        [Tooltip("Durata della clip Holster (0 = il controller non ce l'ha: discesa procedurale).")]
        [SerializeField, Min(0f)] private float _holsterClipLength;
        [SerializeField, Min(0f)] private float _crossFade = 0.06f;
        [Tooltip("Quanta parte della clip di sparo dell'asset si vede (s) prima di tornare a Idle: le clip dei pacchetti hanno un "
                 + "calcio molto ampio, troncarle presto lo rende sobrio come nei Call of Duty. 0 = clip intera.")]
        [SerializeField, Min(0f)] private float _fireClipHold = 0.045f;
        [SerializeField, Min(0.01f)] private float _fireReturnFade = 0.09f;

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
        [SerializeField, Min(0f)] private float _fireKickPitch = 1.2f;
        [SerializeField, Min(0f)] private float _fireKickBack = 0.008f;

        private Vector3 _restPos;
        private Quaternion _restRot = Quaternion.identity;
        private Vector3 _slideRest;
        private Vector3 _slideBack;
        private float _drawElapsed = float.MaxValue;
        private float _kick;
        private float _reloadElapsed = float.MaxValue;
        private float _reloadDuration = 1f;
        private float _fireReturnAt = -1f;
        private float _holsterElapsed = float.MaxValue;
        private float _holsterDuration = 0.25f;
        private bool _holsterProcedural;

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
            _fireReturnAt = -1f;
            Play(DrawHash, 0f);
        }

        /// <summary>Riparte da capo a ogni colpo: alle cadenze alte si vede solo la parte iniziale del rinculo, come previsto.</summary>
        public void PlayFire()
        {
            if (IsProcedural) { _kick = 1f; return; }
            Play(FireHash, 0f);
            if (_fireClipHold > 0f)
                _fireReturnAt = Time.time + _fireClipHold;
        }

        /// <summary>Ricarica, con velocità adattata alla durata effettiva <paramref name="duration"/> in secondi.</summary>
        public void PlayReload(bool empty, float duration)
        {
            if (duration <= 0f)
                return;
            if (IsProcedural) { _reloadElapsed = 0f; _reloadDuration = duration; return; }
            if (_animator == null)
                return;
            _fireReturnAt = -1f;
            float clip = empty ? _reloadEmptyClipLength : _reloadClipLength;
            _animator.SetFloat(ReloadSpeedHash, clip / duration);
            Play(empty ? ReloadEmptyHash : ReloadHash, _crossFade);
        }

        /// <summary>
        /// Cambio arma: l'arma scende in <paramref name="duration"/> secondi e resta giù (il modello viene poi sostituito).
        /// Con la clip Holster del rig si usa quella, altrimenti una discesa con rotazione via codice.
        /// </summary>
        public void PlayHolster(float duration)
        {
            _holsterDuration = Mathf.Max(0.05f, duration);
            _holsterElapsed = 0f;
            _reloadElapsed = float.MaxValue;
            _holsterProcedural = IsProcedural || _animator == null || _holsterClipLength <= 0f;
            if (_holsterProcedural)
                return;
            _fireReturnAt = -1f;
            _animator.SetFloat(HolsterSpeedHash, _holsterClipLength / _holsterDuration);
            Play(HolsterHash, 0.05f);
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
            if (_holsterProcedural && _holsterElapsed < float.MaxValue && _rig != null)
                _holsterElapsed += Time.deltaTime;
            if (_fireReturnAt > 0f && Time.time >= _fireReturnAt)
            {
                _fireReturnAt = -1f;
                Play(IdleHash, _fireReturnFade); // si vede solo l'inizio del calcio della clip
            }
            if (_rig == null || _cameraBone == null)
                return;
            Quaternion wanted = transform.rotation * _cameraBoneRestRotation;
            Quaternion correction = wanted * Quaternion.Inverse(_cameraBone.rotation);
            _rig.rotation = correction * _rig.rotation;
            _rig.position += transform.position - _cameraBone.position;
            if (_holsterProcedural && _holsterElapsed < float.MaxValue)
            {
                float k = Ease(Mathf.Clamp01(_holsterElapsed / _holsterDuration));
                _rig.position += transform.TransformVector(new Vector3(0.02f, -0.18f, -0.05f) * k);
                _rig.rotation = Quaternion.AngleAxis(40f * k, transform.right) * _rig.rotation;
            }
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
                ReloadPose(Mathf.Clamp01(_reloadElapsed / _reloadDuration), ref pos, ref euler);
            }

            if (_holsterElapsed < float.MaxValue)
            {
                _holsterElapsed += dt;
                float k = Ease(Mathf.Clamp01(_holsterElapsed / _holsterDuration));
                pos += new Vector3(0.03f, -0.16f, -0.05f) * k;
                euler += new Vector3(45f, -15f, 20f) * k;
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

        /// <summary>
        /// Ricarica della pistola senza clip, a fasi: inclina verso sinistra e in basso (0-20%), sgancio del caricatore con un
        /// colpetto (30%), caricatore nuovo inserito con una spinta verso l'alto (55%), ritorno (70-85%) e carrello
        /// tirato con uno scatto all'indietro (90%).
        /// </summary>
        private static void ReloadPose(float t, ref Vector3 pos, ref Vector3 euler)
        {
            float tilt = Mathf.SmoothStep(0f, 1f, t / 0.2f) * (1f - Mathf.SmoothStep(0f, 1f, (t - 0.7f) / 0.15f));
            pos += new Vector3(-0.035f, -0.06f, -0.02f) * tilt;
            euler += new Vector3(10f, -18f, 38f) * tilt;
            euler.x += Bump(t, 0.3f, 0.06f) * -6f;            // sgancio: la canna scatta in su
            pos.y += Bump(t, 0.55f, 0.07f) * 0.018f;            // caricatore inserito: spinta dal basso
            euler.z += Bump(t, 0.55f, 0.07f) * -8f;
            pos.z += Bump(t, 0.9f, 0.05f) * -0.025f;            // carrello: arma indietro di scatto
            euler.x += Bump(t, 0.9f, 0.05f) * 7f;
        }

        /// <summary>Impulso morbido centrato in <paramref name="at"/> largo <paramref name="width"/> (0..1).</summary>
        private static float Bump(float t, float at, float width)
        {
            float d = (t - at) / width;
            return d <= -1f || d >= 1f ? 0f : 0.5f * (1f + Mathf.Cos(d * Mathf.PI));
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
        public void EditorSetHolsterClipLength(float length) => _holsterClipLength = Mathf.Max(0f, length);

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
