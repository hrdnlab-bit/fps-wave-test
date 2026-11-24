using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using HrdnLab.FpsWave.Weapons;
using UnityEngine;

namespace HrdnLab.FpsWave.Player.Character
{
    /// <summary>
    /// Corpo del player (modello AIRO) visibile a tutti, anche al proprietario in prima persona (gambe e busto).
    /// Rete (docs/NETCODE.md): nessuna animazione viene replicata. Le gambe derivano dalla velocità della posizione
    /// già sincronizzata dal NetworkTransform; il proprietario invia solo pitch (1 byte) e flag (1 byte) a 10 Hz
    /// quando cambiano; l'arma equipaggiata arriva da <see cref="PlayerWeapons"/>.
    /// Esegue dopo <see cref="PlayerLook"/> e <see cref="PlayerMotor"/> per usare la rotazione del frame.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class PlayerCharacter : NetworkBehaviour, IThirdPersonMuzzle
    {
        [SerializeField] private CharacterRig _rig;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private PlayerLook _look;
        [SerializeField] private PlayerWeapons _weapons;
        [Tooltip("Pivot dell'arma in terza persona (figlio della radice del player).")]
        [SerializeField] private Transform _weaponAnchor;
        [Tooltip("Spostamento del corpo per il proprietario: la camera resta davanti al collo, non dentro il busto.")]
        [SerializeField] private Vector3 _firstPersonBodyOffset = new(0f, 0f, -0.1f);
        [SerializeField, Min(0.02f)] private float _sendInterval = 0.1f;

        private readonly SyncVar<sbyte> _netPitch = new(0,
            new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.ExcludeOwner, 0.1f, Channel.Unreliable));
        private readonly SyncVar<byte> _netFlags = new(CharacterPoseMath.FlagGrounded,
            new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.ExcludeOwner, 0.1f, Channel.Reliable));

        private static readonly int MoveXId = Animator.StringToHash("MoveX");
        private static readonly int MoveZId = Animator.StringToHash("MoveZ");
        private static readonly int CrouchId = Animator.StringToHash("Crouch");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int ReloadId = Animator.StringToHash("Reload");
        private static readonly int ReloadSpeedId = Animator.StringToHash("ReloadSpeed");
        private const float SwitchSeconds = 0.5f;

        private GameObject[] _worldWeapons = System.Array.Empty<GameObject>();
        private WorldWeaponModel[] _worldModels = System.Array.Empty<WorldWeaponModel>();
        private int _shownWeapon = -2;
        private float _pitch;
        private float _crouch;
        private float _kick;
        private Vector3 _lastPosition;
        private Vector3 _remoteVelocity;
        private float _nextSendTime;
        private sbyte _sentPitch = sbyte.MinValue;
        private byte _sentFlags = byte.MaxValue;
        private bool _isLocal;
        private int _frameCounter;
        private float _accumulatedDt;
        private Camera _mainCamera;
        private Vector2 _animMove;
        private float _reloadElapsed = float.MaxValue;
        private float _reloadDuration = 1f;
        private float _switchElapsed = float.MaxValue;
        private int _lastEquipped = -1;

        public Transform WorldMuzzle =>
            _shownWeapon >= 0 && _shownWeapon < _worldModels.Length && _worldModels[_shownWeapon] != null
                ? _worldModels[_shownWeapon].Muzzle : null;

        private void Awake()
        {
            BuildWorldWeapons();
            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            if (_weapons != null)
            {
                _weapons.ObserverShotFired += HandleRemoteShot;
                _weapons.ReloadStarted += HandleReloadStarted;
            }
        }

        private void OnDisable()
        {
            if (_weapons != null)
            {
                _weapons.ObserverShotFired -= HandleRemoteShot;
                _weapons.ReloadStarted -= HandleReloadStarted;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _isLocal = IsOwner;
            if (_rig != null)
            {
                _rig.SetFirstPerson(_isLocal);
                if (_isLocal)
                    _rig.transform.localPosition += _firstPersonBodyOffset;
                // Il proprietario aggiorna sempre il corpo (è attorno alla camera); i remoti lo saltano fuori camera.
                foreach (SkinnedMeshRenderer r in _rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    r.updateWhenOffscreen = _isLocal;
            }
            if (_weapons != null)
                _weapons.ThirdPersonMuzzleProvider = this;
        }

        private void BuildWorldWeapons()
        {
            if (_weapons == null || _weaponAnchor == null)
                return;
            int count = _weapons.LoadoutCount;
            _worldWeapons = new GameObject[count];
            _worldModels = new WorldWeaponModel[count];
            for (int i = 0; i < count; i++)
            {
                WeaponDefinition def = _weapons.GetDefinition(i);
                if (def == null || def.WorldModelPrefab == null)
                    continue;
                GameObject go = Instantiate(def.WorldModelPrefab, _weaponAnchor, false);
                go.SetActive(false);
                _worldWeapons[i] = go;
                go.TryGetComponent(out _worldModels[i]);
            }
        }

        private void HandleRemoteShot() => _kick = 1f;

        /// <summary>Ricarica (proprietario in locale, gli altri dall'RPC): clip sul busto, mano sinistra libera, arma inclinata.</summary>
        private void HandleReloadStarted(float duration)
        {
            _reloadDuration = Mathf.Max(0.2f, duration);
            _reloadElapsed = 0f;
            if (_rig != null && _rig.Animated)
            {
                _rig.Animator.SetFloat(ReloadSpeedId, _rig.ReloadClipLength / _reloadDuration);
                _rig.Animator.SetTrigger(ReloadId);
            }
        }

        /// <summary>0 -> 1 -> 0 lungo la ricarica: sale nel primo 15%, scende nell'ultimo 20%.</summary>
        private float ReloadWeight()
        {
            if (_reloadElapsed >= _reloadDuration)
                return 0f;
            float t = _reloadElapsed / _reloadDuration;
            return Mathf.SmoothStep(0f, 1f, t / 0.15f) * (1f - Mathf.SmoothStep(0f, 1f, (t - 0.8f) / 0.2f));
        }

        private void Update()
        {
            if (!IsOwner || !IsClientInitialized || _motor == null)
                return;
            if (Time.unscaledTime < _nextSendTime)
                return;
            _nextSendTime = Time.unscaledTime + _sendInterval;
            sbyte pitch = CharacterPoseMath.PackPitch(_look != null ? _look.ViewPitch : 0f);
            byte flags = CharacterPoseMath.PackFlags(_motor.IsCrouching, _motor.IsGrounded);
            if (pitch == _sentPitch && flags == _sentFlags)
                return;
            _sentPitch = pitch;
            _sentFlags = flags;
            ServerSetState(pitch, flags);
        }

        [ServerRpc]
        private void ServerSetState(sbyte pitch, byte flags, Channel channel = Channel.Unreliable)
        {
            _netPitch.Value = (sbyte)Mathf.Clamp(pitch, -127, 127);
            _netFlags.Value = (byte)(flags & (CharacterPoseMath.FlagCrouch | CharacterPoseMath.FlagGrounded));
        }

        private void LateUpdate()
        {
            if (_rig == null || !IsClientInitialized)
                return;
            float dt = Time.deltaTime;
            Vector3 position = transform.position;

            // Player remoti lontani o fuori camera: posa aggiornata a frequenza ridotta (TECH_BUDGETS).
            _accumulatedDt += dt;
            if (!_isLocal && !ShouldUpdateRemote(position))
                return;
            float stepDt = _accumulatedDt;
            _accumulatedDt = 0f;

            Vector3 worldVelocity;
            bool grounded, crouching;
            float targetPitch;
            if (_isLocal)
            {
                worldVelocity = _motor != null ? _motor.Velocity : Vector3.zero;
                grounded = _motor == null || _motor.IsGrounded;
                crouching = _motor != null && _motor.IsCrouching;
                targetPitch = _look != null ? _look.ViewPitch : 0f;
            }
            else
            {
                Vector3 measured = stepDt > 1e-4f ? (position - _lastPosition) / stepDt : Vector3.zero;
                _remoteVelocity = CharacterPoseMath.Damp(_remoteVelocity, measured, 12f, stepDt);
                worldVelocity = _remoteVelocity;
                grounded = CharacterPoseMath.HasFlag(_netFlags.Value, CharacterPoseMath.FlagGrounded);
                crouching = CharacterPoseMath.HasFlag(_netFlags.Value, CharacterPoseMath.FlagCrouch);
                targetPitch = CharacterPoseMath.UnpackPitch(_netPitch.Value);
            }
            _lastPosition = position;

            _pitch = _isLocal ? targetPitch : CharacterPoseMath.Damp(_pitch, targetPitch, 14f, stepDt);
            _crouch = Mathf.MoveTowards(_crouch, crouching ? 1f : 0f, stepDt * 5f);
            _kick = Mathf.MoveTowards(_kick, 0f, stepDt * 9f);
            if (_reloadElapsed < float.MaxValue) _reloadElapsed += stepDt;
            if (_switchElapsed < float.MaxValue) _switchElapsed += stepDt;

            // L'arma in terza persona è nascosta al proprietario, ma posizionata lo stesso: l'IK delle braccia
            // la segue e l'ombra del corpo mostra il pilota che impugna l'arma.
            int equipped = _weapons != null ? _weapons.EquippedIndex : -1;
            if (equipped != _lastEquipped)
            {
                // Cambio arma visto da tutti (indice già sincronizzato): l'arma scende, cambia, risale.
                if (_lastEquipped >= 0)
                    _switchElapsed = 0f;
                _lastEquipped = equipped;
                _reloadElapsed = float.MaxValue;
            }
            ShowWeapon(_isLocal ? -1 : equipped);
            WorldWeaponModel model = equipped >= 0 && equipped < _worldModels.Length ? _worldModels[equipped] : null;
            if (model != null)
                PlaceWeapon(model);

            Vector3 local = transform.InverseTransformDirection(worldVelocity);
            if (_rig.Animated)
            {
                Animator animator = _rig.Animator;
                _animMove = CharacterPoseMath.Damp(_animMove, new Vector2(local.x, local.z), 10f, stepDt);
                animator.SetFloat(MoveXId, _animMove.x);
                animator.SetFloat(MoveZId, _animMove.y);
                animator.SetFloat(CrouchId, _crouch);
                animator.SetBool(GroundedId, grounded);
                // Livello del busto acceso solo in ricarica (a riposo comandano locomozione e IK sull'arma).
                if (animator.layerCount > 1)
                    animator.SetLayerWeight(1, ReloadWeight());
            }
            float reload = ReloadWeight();
            var input = new CharacterPoseInput
            {
                LocalVelocity = new Vector3(local.x, 0f, local.z),
                Grounded = grounded,
                Crouch = _crouch,
                Pitch = _pitch,
                RightHandTarget = model != null ? model.RightHand : null,
                LeftHandTarget = model != null ? model.LeftHand : null,
                // In ricarica la sinistra lascia l'arma (con le clip segue il caricatore; senza, pende vicino all'arma).
                LeftHandRelease = _rig.Animated ? reload : reload * 0.6f,
                Dt = stepDt,
            };
            _rig.Solve(input);
        }

        private bool ShouldUpdateRemote(Vector3 position)
        {
            _frameCounter++;
            CharacterAnimationSettings s = _rig.Settings;
            if (s == null)
                return true;
            if (_mainCamera == null || !_mainCamera.isActiveAndEnabled)
                _mainCamera = Camera.main;
            bool far = _mainCamera != null &&
                       (position - _mainCamera.transform.position).sqrMagnitude > s.LowRateDistance * s.LowRateDistance;
            bool slow = far || !_rig.IsVisible;
            return !slow || _frameCounter % s.LowRateFrameInterval == 0;
        }

        private void ShowWeapon(int index)
        {
            if (index == _shownWeapon)
                return;
            for (int i = 0; i < _worldWeapons.Length; i++)
                if (_worldWeapons[i] != null) _worldWeapons[i].SetActive(i == index);
            _shownWeapon = index >= 0 && index < _worldWeapons.Length && _worldWeapons[index] != null ? index : -1;
        }

        /// <summary>Arma sul pivot di mira: ruota col pitch, arretra col rinculo; le mani la seguono con l'IK.</summary>
        private void PlaceWeapon(WorldWeaponModel model)
        {
            CharacterAnimationSettings s = _rig.Settings;
            if (s == null || _weaponAnchor == null)
                return;
            Vector3 pivot = s.AimPivot + Vector3.down * (s.CrouchHipDrop * 1.05f * _crouch);
            Quaternion aim = Quaternion.Euler(_pitch - s.RecoilKickPitch * _kick * _kick, 0f, 0f);
            Vector3 grip = model.Grip == CharacterGrip.Pistol ? s.PistolGrip : s.RifleGrip;
            grip.z -= s.RecoilKickBack * _kick * _kick;
            // Ricarica: arma inclinata verso l'esterno e un po' giù; cambio arma: scende e risale.
            float reload = ReloadWeight();
            float sw = _switchElapsed < SwitchSeconds ? Mathf.Sin(Mathf.Clamp01(_switchElapsed / SwitchSeconds) * Mathf.PI) : 0f;
            Quaternion pose = Quaternion.Euler(12f * reload + 50f * sw, 0f, -28f * reload);
            Vector3 offset = new Vector3(0f, -0.06f * reload - 0.22f * sw, -0.04f * reload);
            _weaponAnchor.localPosition = pivot + aim * (grip + offset);
            _weaponAnchor.localRotation = aim * pose;
        }

#if UNITY_EDITOR
        public void EditorSetup(CharacterRig rig, PlayerMotor motor, PlayerLook look, PlayerWeapons weapons, Transform weaponAnchor)
        {
            _rig = rig;
            _motor = motor;
            _look = look;
            _weapons = weapons;
            _weaponAnchor = weaponAnchor;
        }
#endif
    }
}
