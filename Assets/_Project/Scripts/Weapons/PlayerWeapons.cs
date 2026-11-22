using System;
using FishNet.Connection;
using FishNet.Object;
using HrdnLab.FpsWave.Audio;
using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Core;
using HrdnLab.FpsWave.Player;
using HrdnLab.FpsWave.Stats;
using UnityEngine;

namespace HrdnLab.FpsWave.Weapons
{
    public enum HitMarkerType : byte
    {
        Hit = 0,
        Weakspot = 1,
        Critical = 2,
        Kill = 3,
    }

    /// <summary>
    /// Armi del player. Modello PvE MVP (docs/NETCODE.md): il proprietario spara in locale (raycast e feedback
    /// immediati) e invia il colpo al server, che ne valida cadenza, munizioni, origine, gittata, bersaglio
    /// e linea di vista, poi applica il danno. Client e server usano istanze separate di <see cref="WeaponState"/>,
    /// così sull'Host lo stato predetto e quello autorevole non si sovrappongono.
    /// </summary>
    public sealed class PlayerWeapons : NetworkBehaviour
    {
        [Header("Armi")]
        [Tooltip("Indice 0 = primaria, 1 = secondaria. Una voce vuota = slot vuoto.")]
        [SerializeField] private WeaponDefinition[] _loadout = Array.Empty<WeaponDefinition>();
        [SerializeField] private int _startingWeapon = 1;

        [Header("Riferimenti")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerLook _look;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private Transform _viewModelRoot;
        [SerializeField] private Light _muzzleLight;
        [Tooltip("Sensazione delle armi (rinculo, molle, oscillazioni, calore). Se manca resta il rinculo semplice di M1.")]
        [SerializeField] private WeaponFeelDriver _feel;

        [Header("Effetti")]
        [SerializeField] private Material _tracerMaterial;
        [SerializeField] private Material _impactMaterial;
        [SerializeField] private Material _placeholderMaterial;

        [Header("Mira (ADS)")]
        [SerializeField] private Vector3 _hipPosition = new(0.22f, -0.2f, 0.42f);
        [SerializeField] private Vector3 _adsPosition = new(0f, -0.12f, 0.32f);
        [SerializeField, Range(0.1f, 1f)] private float _adsMoveMultiplier = 0.6f;
        [SerializeField, Range(0.1f, 1f)] private float _adsRecoilMultiplier = 0.7f;

        private WeaponState[] _local;
        private WeaponState[] _server;
        private FireRateLimiter[] _limiters;
        private int _localEquipped = -1;
        private int _localTracerCounter;
        private float _lastSprintTime = -10f;
        private bool _queuedSemiShot;
        private float _queuedSemiShotTime;
        private int _serverEquipped;
        private float _aim;
        private float _muzzleLightUntil;
        private float _viewKick;
        private GameObject _viewModel;
        private WeaponViewModel _viewModelAnim;
        private Transform _muzzle;
        private ShotFxPool _fx;
        private float _nextRejectLogTime;

        /// <summary>Solo player locale: un colpo ha toccato un bersaglio (predetto) o è stato confermato dal server (kill/critico).</summary>
        public event Action<HitMarkerType> LocalHitMarker;

        /// <summary>
        /// Solo per chi ha sparato: colpo confermato dal server (punto colpito, danno arrotondato, tipo di colpo).
        /// Usato dai numeri di danno (<c>UI.DamageNumbers</c>).
        /// </summary>
        public event Action<Vector3, ushort, HitFlags> LocalHitConfirmed;

        public WeaponState Current => _local != null && _localEquipped >= 0 ? _local[_localEquipped] : null;
        public float AimAmount => _aim;
        public Camera ViewCamera => _look != null ? _look.Camera : null;
        /// <summary>Calore cosmetico dell'arma impugnata (0-1) e stato di raffreddamento, per il HUD.</summary>
        public float Heat => _feel != null ? _feel.Heat : 0f;
        public bool IsVenting => _feel != null && _feel.IsVenting;
        /// <summary>L'arma impugnata è a energia (ha il calore): il HUD mostra la barra solo in quel caso.</summary>
        public bool UsesHeat => Current != null && Current.Definition.Feel.UsesHeat;
        /// <summary>Armi della dotazione e indice di quella impugnata (per la striscia delle armi nel HUD).</summary>
        public System.Collections.Generic.IReadOnlyList<WeaponDefinition> Loadout => _loadout;
        public int EquippedIndex => _localEquipped;
        /// <summary>A terra (PlayerLife): si usa solo la secondaria.</summary>
        public bool ForceSecondary { get; set; }
        /// <summary>Morto in attesa di tornare in vita: niente spari.</summary>
        public bool WeaponsDisabled { get; set; }
        /// <summary>Mira con curva morbida (ease in/out) per posa dell'arma e FOV: entra e esce come nei Call of Duty.</summary>
        public float AimEased => _aim * _aim * (3f - 2f * _aim);

        /// <summary>Dispersione attuale in gradi (per il mirino dell'HUD).</summary>
        public float CurrentSpread
        {
            get
            {
                WeaponState s = Current;
                if (s == null) return 0f;
                float hip = s.Stats.Get(StatType.HipSpread);
                float baseSpread = Mathf.Lerp(hip, s.Stats.Get(StatType.AdsSpread), _aim);
                // Come nei Call of Duty: da fianco la dispersione cresce sparando (bloom), muovendosi e in aria, cala
                // accovacciati; mirando l'arma è precisa (niente bloom) e si gestisce solo il rinculo.
                float hipWeight = 1f - _aim;
                float movement = 0f;
                if (_motor != null)
                {
                    Vector3 v = _motor.Velocity;
                    float moving = Mathf.Clamp01(new Vector2(v.x, v.z).magnitude / 4.5f);
                    movement = hip * (0.5f * moving + (_motor.IsGrounded ? 0f : 0.9f));
                    if (_motor.IsCrouching)
                        baseSpread *= Mathf.Lerp(1f, 0.85f, hipWeight);
                }
                return baseSpread + (s.Bloom + movement) * hipWeight;
            }
        }

        private void Awake()
        {
            int count = _loadout.Length;
            _local = new WeaponState[count];
            _server = new WeaponState[count];
            _limiters = new FireRateLimiter[count];
            for (int i = 0; i < count; i++)
            {
                if (_loadout[i] == null) continue;
                _local[i] = new WeaponState(_loadout[i], _loadout[i].CreateStats());
                _server[i] = new WeaponState(_loadout[i], _loadout[i].CreateStats());
                _limiters[i] = new FireRateLimiter(_loadout[i].CreateStats().Get(StatType.RoundsPerMinute) / 60f);
            }
            _serverEquipped = ClampToValidSlot(_startingWeapon);
            if (_feel == null)
                _feel = GetComponent<WeaponFeelDriver>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
                return;
            _fx = ShotFxPool.Get(_tracerMaterial, _impactMaterial);
            EquipLocal(ClampToValidSlot(_startingWeapon), notifyServer: false);
        }

        private int ClampToValidSlot(int index)
        {
            if (index >= 0 && index < _loadout.Length && _loadout[index] != null)
                return index;
            for (int i = 0; i < _loadout.Length; i++)
                if (_loadout[i] != null) return i;
            return -1;
        }

        private void Update()
        {
            double now = Time.timeAsDouble;
            float dt = Time.deltaTime;

            if (IsServerInitialized)
                TickStates(_server, now, dt);

            if (!IsOwner || _localEquipped < 0)
                return;

            TickStates(_local, now, dt);
            HandleOwnerInput(now, dt);
            UpdateViewModel(dt);
        }

        private static void TickStates(WeaponState[] states, double now, float dt)
        {
            for (int i = 0; i < states.Length; i++)
                states[i]?.Tick(now, dt, states[i].Definition.BloomRecoveryPerSecond);
        }

        // ---------------------------------------------------------------- Client (owner)

        private void HandleOwnerInput(double now, float dt)
        {
            if (_input == null)
                return;

            if (ForceSecondary)
            {
                int secondary = SecondaryIndex();
                if (secondary >= 0 && secondary != _localEquipped)
                    EquipLocal(secondary, true);
            }
            else if (_input.Weapon1Pressed) EquipLocal(0, true);
            else if (_input.Weapon2Pressed) EquipLocal(1, true);
            else if (_input.SwitchWeaponPressed) EquipLocal(NextSlot(), true);

            WeaponState state = Current;
            if (state == null)
                return;
            if (WeaponsDisabled)
            {
                _aim = 0f;
                return;
            }

            if (_input.ReloadPressed)
                RequestReload(state, now);

            // Sprint e arma, come nei Call of Duty: sparare o mirare interrompe la corsa, e dopo la corsa
            // serve un attimo (sprint-to-fire) perché l'arma torni in posizione prima del primo colpo.
            if (_motor != null)
            {
                _motor.SprintBlocked = _input.FireHeld || _input.AimHeld;
                if (_motor.IsSprinting)
                    _lastSprintTime = Time.time;
            }
            bool sprintRecovered = Time.time - _lastSprintTime >= state.Definition.SprintToFireTime;

            bool wantsAim = _input.AimHeld && !state.IsReloading && !(_motor != null && _motor.IsSprinting);
            _aim = Mathf.MoveTowards(_aim, wantsAim ? 1f : 0f, dt / state.Definition.AdsTime);
            float aimEased = AimEased;
            if (_look != null) _look.FovMultiplier = Mathf.Lerp(1f, state.Definition.AdsFovMultiplier, aimEased);
            if (_motor != null) _motor.SpeedMultiplier = Mathf.Lerp(1f, _adsMoveMultiplier, _aim);

            bool trigger;
            if (state.Definition.Automatic)
            {
                trigger = _input.FireHeld;
            }
            else
            {
                // Il colpo premuto durante il rialzo dell'arma non va perso: parte appena l'arma è pronta.
                if (_input.FirePressed)
                {
                    _queuedSemiShot = true;
                    _queuedSemiShotTime = Time.time;
                }
                if (_queuedSemiShot && Time.time - _queuedSemiShotTime > 0.35f)
                    _queuedSemiShot = false; // premuto troppo prima: non spara in ritardo
                trigger = _queuedSemiShot;
            }
            if (!trigger || !sprintRecovered)
                return;
            _queuedSemiShot = false;

            if (state.Ammo <= 0 && !state.IsReloading)
            {
                RequestReload(state, now);
                return;
            }

            // Più colpi per frame se la cadenza lo richiede (frame lenti o armi molto veloci).
            int guard = 0;
            while (state.CanFire(now) && guard++ < 4)
            {
                FireLocal(state, now);
                if (!state.Definition.Automatic)
                    break;
            }
        }

        private int SecondaryIndex()
        {
            for (int i = 0; i < _loadout.Length; i++)
                if (_loadout[i] != null && _loadout[i].Slot == WeaponSlot.Secondary)
                    return i;
            return -1;
        }

        private int NextSlot()
        {
            for (int step = 1; step <= _loadout.Length; step++)
            {
                int i = (_localEquipped + step) % _loadout.Length;
                if (_loadout[i] != null) return i;
            }
            return _localEquipped;
        }

        private void EquipLocal(int index, bool notifyServer)
        {
            if (index < 0 || index >= _loadout.Length || _loadout[index] == null || index == _localEquipped)
                return;
            Current?.CancelReload();
            _localEquipped = index;
            _aim = 0f;
            BuildViewModel(_loadout[index]);
            _feel?.OnEquip(_loadout[index]);
            if (notifyServer)
                RpcEquip((byte)index);
        }

        private void RequestReload(WeaponState state, double now)
        {
            bool empty = state.Ammo <= 0;
            if (!state.StartReload(now))
                return;
            _viewModelAnim?.PlayReload(empty, state.Stats.Get(StatType.ReloadTime));
            RpcReload((byte)_localEquipped);
        }

        private void FireLocal(WeaponState state, double now)
        {
            Camera cam = ViewCamera;
            if (cam == null)
                return;

            Transform camTransform = cam.transform;
            Vector3 origin = camTransform.position;
            float spread = CurrentSpread;
            Vector3 direction = ShotValidation.SpreadDirection(camTransform.forward, camTransform.up, spread,
                UnityEngine.Random.value, UnityEngine.Random.value);
            float range = state.Stats.Get(StatType.Range);

            Vector3 end = origin + direction * range;
            HitZone zone = null;
            bool hitSomething = Physics.Raycast(origin, direction, out RaycastHit hit, range, GameLayers.ShotMask, QueryTriggerInteraction.Ignore);
            if (hitSomething)
            {
                end = hit.point;
                hit.collider.TryGetComponent(out zone);
            }

            state.RegisterShot(now, state.Definition.MaxBloom);

            // Feedback immediato (predizione locale).
            // Tracciante del tiratore come nei giochi AAA: solo un colpo su tre delle automatiche (i "traccianti" del caricatore),
            // mai per le semiautomatiche; la mira si legge da mirino e impatti, non da una riga davanti alla faccia.
            Vector3 muzzle = _muzzle != null ? _muzzle.position : origin;
            if (state.Definition.Automatic && _localTracerCounter++ % 3 == 0)
                _fx?.Tracer(muzzle, end, state.Definition.Feel.MuzzleColor);
            GameAudio.Play2D(state.Definition.FireSoundClose);
            if (hitSomething)
            {
                // Sui personaggi niente segno permanente: resterebbe sospeso in aria quando il nemico si muove o sparisce.
                if (zone != null)
                {
                    bool plate = zone.TryGetComponent(out HrdnLab.FpsWave.Enemies.ArmorPlate armorPlate) && !armorPlate.IsBroken;
                    if (plate)
                        _fx?.SparkImpact(hit.point, hit.normal);
                    else
                        _fx?.FleshImpact(hit.point, hit.normal);
                    if (plate)
                        GameAudio.PlayAt(SoundId.ArmorPlateHit, hit.point);
                    else if (!zone.Owner.IsDead)
                        GameAudio.Play2D(SoundId.HitMarker); // tick immediato (predetto), i suoni speciali arrivano con la conferma
                }
                else
                {
                    _fx?.Impact(hit.point, hit.normal);
                    GameAudio.PlayAt(SoundId.ImpactSurface, hit.point, 0.8f);
                }
            }
            if (_feel != null)
            {
                _feel.OnShot(state, _aim); // rinculo, calcio a molla, tremore, FOV, calore
            }
            else
            {
                float recoilScale = Mathf.Lerp(1f, _adsRecoilMultiplier, _aim);
                _look?.AddRecoil(state.Stats.Get(StatType.RecoilPitch) * recoilScale,
                    UnityEngine.Random.Range(-1f, 1f) * state.Stats.Get(StatType.RecoilYaw) * recoilScale,
                    state.Definition.RecoilRecoveryPerSecond);
                _viewKick = 1f;
            }
            FlashMuzzle();
            _viewModelAnim?.PlayFire();

            NetworkObject target = null;
            sbyte zoneIndex = -1;
            if (zone != null && zone.Owner != null && !zone.Owner.IsDead)
            {
                target = zone.Owner.NetworkObject;
                zoneIndex = (sbyte)zone.Index;
                LocalHitMarker?.Invoke(zone.IsWeakspot ? HitMarkerType.Weakspot : HitMarkerType.Hit);
            }

            RpcShoot((byte)_localEquipped, origin, direction, end, target, zoneIndex);
        }

        private void FlashMuzzle()
        {
            if (_muzzleLight == null) return;
            if (_muzzle != null)
                _muzzleLight.transform.position = _muzzle.position;
            _muzzleLight.enabled = true;
            _muzzleLightUntil = Time.time + 0.055f;
            _feel?.ConfigureMuzzleLight(_muzzleLight);
        }

        private void BuildViewModel(WeaponDefinition definition)
        {
            if (_viewModelRoot == null)
                return;
            if (_viewModel != null)
                Destroy(_viewModel);

            _viewModelAnim = null;
            if (definition.ViewModelPrefab != null)
            {
                _viewModel = Instantiate(definition.ViewModelPrefab, _viewModelRoot, false);
                _viewModel.TryGetComponent(out _viewModelAnim);
            }
            else
            {
                // Segnaposto greybox finché non arrivano i modelli realistici (M1.6).
                _viewModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(_viewModel.GetComponent<Collider>());
                _viewModel.name = $"Placeholder_{definition.Id}";
                _viewModel.transform.SetParent(_viewModelRoot, false);
                _viewModel.transform.localScale = definition.PlaceholderSize;
                _viewModel.transform.localPosition = new Vector3(0f, 0f, definition.PlaceholderSize.z * 0.5f);
                var r = _viewModel.GetComponent<MeshRenderer>();
                if (_placeholderMaterial != null) r.sharedMaterial = _placeholderMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            SetLayerRecursively(_viewModel, GameLayers.Player);

            if (_viewModelAnim != null && _viewModelAnim.Muzzle != null)
            {
                _muzzle = _viewModelAnim.Muzzle;
                _viewModelAnim.PlayDraw();
                if (_muzzleLight != null)
                    _muzzleLight.enabled = false;
                return;
            }

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(_viewModel.transform.parent, false);
            muzzle.localPosition = new Vector3(0f, 0f, definition.PlaceholderSize.z + 0.02f);
            muzzle.SetParent(_viewModel.transform, true);
            _muzzle = muzzle;
            // La luce resta figlia del root dell'arma (non del modello, che viene distrutto al cambio arma).
            if (_muzzleLight != null)
                _muzzleLight.enabled = false;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private void UpdateViewModel(float dt)
        {
            if (_muzzleLight != null && _muzzleLight.enabled)
            {
                if (Time.time >= _muzzleLightUntil)
                    _muzzleLight.enabled = false;
                else
                    _feel?.UpdateMuzzleLight(_muzzleLight);
            }
            if (_viewModelRoot == null)
                return;
            WeaponDefinition def = _loadout[_localEquipped];
            Vector3 hip = def.OverrideViewOffsets ? def.ViewHipOffset : _hipPosition;
            Vector3 ads = def.OverrideViewOffsets ? def.ViewAdsOffset : _adsPosition;
            Vector3 basePosition = Vector3.Lerp(hip, ads, AimEased);
            if (_feel != null)
            {
                _feel.ApplyViewModel(_viewModelRoot, basePosition, _aim, dt);
                return;
            }
            _viewKick = Mathf.MoveTowards(_viewKick, 0f, dt * 12f);
            _viewModelRoot.localPosition = basePosition + new Vector3(0f, 0f, -0.04f * _viewKick);
        }

        // ---------------------------------------------------------------- Server

        [ServerRpc]
        private void RpcEquip(byte index)
        {
            if (index >= _server.Length || _server[index] == null)
                return;
            _server[_serverEquipped]?.CancelReload();
            _serverEquipped = index;
        }

        [ServerRpc]
        private void RpcReload(byte index)
        {
            if (index >= _server.Length || _server[index] == null)
                return;
            _server[index].StartReload(Time.timeAsDouble);
        }

        [ServerRpc]
        private void RpcShoot(byte weaponIndex, Vector3 origin, Vector3 direction, Vector3 end, NetworkObject target, sbyte zoneIndex)
        {
            ShotRejectReason reason = ServerProcessShot(weaponIndex, origin, direction, end, target, zoneIndex, out bool fired);

            if (fired)
                RpcShotFx(origin, end, weaponIndex);

            if (reason == ShotRejectReason.None)
                return;

            if ((reason == ShotRejectReason.NoAmmo || reason == ShotRejectReason.Reloading || reason == ShotRejectReason.NotEquipped)
                && weaponIndex < _server.Length && _server[weaponIndex] != null)
            {
                WeaponState s = _server[weaponIndex];
                RpcSyncWeapon(Owner, weaponIndex, (ushort)s.Ammo, s.IsReloading);
            }

            if (Time.time >= _nextRejectLogTime)
            {
                _nextRejectLogTime = Time.time + 2f;
                Debug.Log($"[FpsWave][Server] Colpo del client {OwnerId} rifiutato: {reason}");
            }
        }

        /// <summary>Validazione e applicazione del colpo. <paramref name="fired"/> = il colpo è stato sparato (munizione consumata).</summary>
        private ShotRejectReason ServerProcessShot(byte weaponIndex, Vector3 origin, Vector3 direction, Vector3 end,
            NetworkObject target, sbyte zoneIndex, out bool fired)
        {
            fired = false;
            if (weaponIndex >= _server.Length || _server[weaponIndex] == null)
                return ShotRejectReason.InvalidWeapon;
            if (weaponIndex != _serverEquipped)
                return ShotRejectReason.NotEquipped;

            WeaponState state = _server[weaponIndex];
            if (state.IsReloading)
                return ShotRejectReason.Reloading;
            if (state.Ammo <= 0)
                return ShotRejectReason.NoAmmo;

            FireRateLimiter limiter = _limiters[weaponIndex];
            limiter.SetRate(state.Stats.Get(StatType.RoundsPerMinute) / 60f);
            if (!limiter.TryConsume(Time.timeAsDouble))
                return ShotRejectReason.FireRate;

            state.TryConsumeRound();
            fired = true;

            if (_motor != null && !ShotValidation.IsOriginPlausible(origin, _motor.ServerEyePosition))
                return ShotRejectReason.OriginTooFar;
            if (!ShotValidation.IsWithinRange(origin, end, state.Stats.Get(StatType.Range)))
                return ShotRejectReason.OutOfRange;

            Vector3 toEnd = end - origin;
            float distance = toEnd.magnitude;
            if (distance > 0.01f && Vector3.Angle(direction, toEnd) > 3f)
                return ShotRejectReason.TargetMismatch;

            if (target == null)
                return ShotRejectReason.None; // colpo a vuoto o su ambiente: solo effetti

            if (!target.TryGetComponent(out Health health) || health.IsDead)
                return ShotRejectReason.TargetMismatch;
            HitZone zone = health.GetZone(zoneIndex);
            if (zone == null)
                return ShotRejectReason.TargetMismatch;

            // Bersagli in movimento (nemici): tolleranza allargata di velocità * ritardo stimato del client.
            float targetSpeed = target.TryGetComponent(out IMovingTarget moving) ? moving.ServerSpeed : 0f;
            Vector3 closest = zone.Collider.ClosestPoint(end);
            if (!ShotValidation.IsTargetPlausible(end, closest, targetSpeed, ShotValidation.MovingTargetLagSeconds))
                return ShotRejectReason.TargetMismatch;

            if (distance > 0.2f && Physics.Linecast(origin, end - toEnd / distance * 0.1f, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
                return ShotRejectReason.LineOfSightBlocked;

            StatBlock stats = state.Stats;
            bool crit = DamageCalculator.RollCritical(stats.Get(StatType.CritChance), UnityEngine.Random.value);
            float damage = DamageCalculator.Compute(stats.Get(StatType.Damage), zone.DamageMultiplier, zone.IsWeakspot,
                stats.Get(StatType.WeakspotMultiplier), crit, stats.Get(StatType.CritMultiplier));
            // Corazza a piastre: una piastra intera assorbe il colpo e al corpo arriva solo una parte.
            float shown = damage;
            bool armorHit = false, armorBreak = false;
            if (target.TryGetComponent(out HrdnLab.FpsWave.Enemies.EnemyArmor armor)
                && armor.ServerAbsorb(zone.Index, damage, out float bodyDamage, out armorBreak))
            {
                armorHit = true;
                damage = bodyDamage;
            }
            bool killed = damage > 0f && health.ApplyDamage(new DamageInfo(damage, crit, zone.IsWeakspot && !armorHit, OwnerId, end));

            // Conferma per ogni colpo andato a segno (≈20 byte): numeri di danno e, per kill/critico, l'hit marker confermato.
            RpcConfirmHit(Owner, end, HitConfirm.PackDamage(shown),
                (byte)HitConfirm.MakeFlags(crit, zone.IsWeakspot && !armorHit, killed, armorHit, armorBreak));
            return ShotRejectReason.None;
        }

        // ---------------------------------------------------------------- RPC verso i client

        [TargetRpc]
        private void RpcSyncWeapon(NetworkConnection connection, byte weaponIndex, ushort ammo, bool reloading)
        {
            if (_local == null || weaponIndex >= _local.Length || _local[weaponIndex] == null)
                return;
            _local[weaponIndex].ForceState(ammo, reloading, Time.timeAsDouble);
        }

        [TargetRpc]
        private void RpcConfirmHit(NetworkConnection connection, Vector3 point, ushort damage, byte flagBits)
        {
            var flags = (HitFlags)flagBits;
            if (HitConfirm.Has(flags, HitFlags.Kill))
                LocalHitMarker?.Invoke(HitMarkerType.Kill);
            else if (HitConfirm.Has(flags, HitFlags.Critical))
                LocalHitMarker?.Invoke(HitMarkerType.Critical);

            if (HitConfirm.Has(flags, HitFlags.Kill))
                GameAudio.Play2D(SoundId.KillConfirm);
            else if (HitConfirm.Has(flags, HitFlags.ArmorBreak))
            {
                GameAudio.Play2D(SoundId.ArmorPlateBreak);
                HrdnLab.FpsWave.UI.SuitCallouts.Show("ARMOR BREACHED // WEAK POINT EXPOSED", HrdnLab.FpsWave.UI.CalloutKind.Positive);
            }
            else if (!HitConfirm.Has(flags, HitFlags.ArmorHit)
                     && (HitConfirm.Has(flags, HitFlags.Weakspot) || HitConfirm.Has(flags, HitFlags.Critical)))
                GameAudio.Play2D(SoundId.HitMarkerPrecision);
            LocalHitConfirmed?.Invoke(point, damage, flags);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void RpcShotFx(Vector3 origin, Vector3 end, byte weaponIndex)
        {
            if (_fx == null)
                _fx = ShotFxPool.Get(_tracerMaterial, _impactMaterial);
            // Per i player remoti il tracer parte poco sotto e davanti agli occhi, all'altezza dell'arma.
            Vector3 start = origin + (end - origin).normalized * 0.6f + Vector3.down * 0.15f;
            Color energy = weaponIndex < _loadout.Length && _loadout[weaponIndex] != null
                ? _loadout[weaponIndex].Feel.MuzzleColor : Color.white;
            _fx.Tracer(start, end, energy); // anche gli altri vedono il colore dell'arma
            if (weaponIndex < _loadout.Length && _loadout[weaponIndex] != null)
                GameAudio.PlayAt(_loadout[weaponIndex].FireSoundFar, origin);
            GameAudio.PlayAt(SoundId.ImpactSurface, end, 0.6f);
        }

#if UNITY_EDITOR
        public void EditorSetup(WeaponDefinition[] loadout, int startingWeapon, PlayerInputReader input, PlayerLook look,
            PlayerMotor motor, Transform viewModelRoot, Light muzzleLight, Material tracer, Material impact, Material placeholder)
        {
            _loadout = loadout;
            _startingWeapon = startingWeapon;
            _input = input;
            _look = look;
            _motor = motor;
            _viewModelRoot = viewModelRoot;
            _muzzleLight = muzzleLight;
            _tracerMaterial = tracer;
            _impactMaterial = impact;
            _placeholderMaterial = placeholder;
        }
#endif
    }
}
