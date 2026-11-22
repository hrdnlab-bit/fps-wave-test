using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Core;
using UnityEngine;
using UnityEngine.AI;

namespace HrdnLab.FpsWave.Enemies
{
    public enum EnemyState : byte
    {
        Idle,
        Chase,
        Attack,
        Dead,
    }

    /// <summary>
    /// Nemico corpo a corpo generico: le statistiche del tipo (Runner, Brute...) vengono da <see cref="EnemyDefinition"/>.
    /// L'AI esiste solo sul server: NavMeshAgent attivo solo lì,
    /// posizione e yaw replicati da un NetworkTransform server-authoritative, i client interpolano.
    /// Non ha un Update proprio: <see cref="EnemyDirector"/> chiama <see cref="ServerThink"/> a frequenza ridotta
    /// (più spesso da vicino, meno da lontano), così il costo resta piatto anche con 40 nemici.
    /// Riciclato dal pool di rete: tutto lo stato viene azzerato in <see cref="OnStartServer"/>.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(NavMeshAgent))]
    public sealed class EnemyAgent : NetworkBehaviour, IMovingTarget
    {
        private const float RetargetInterval = 1.5f;
        /// <summary>Si cambia bersaglio solo se un altro player è molto più vicino (0.6x la distanza): niente esitazioni.</summary>
        private const float SwitchRatioSqr = 0.36f;
        /// <summary>Si ricalcola il percorso prima del previsto se il bersaglio si è spostato di oltre 1.5 m.</summary>
        private const float RepathMoveSqr = 1.5f * 1.5f;
        private const float AttackExitMargin = 0.4f;
        private const float MaxAttackHeightDelta = 2f;
        /// <summary>Senza posto di attacco libero il nemico si ferma a questa distanza in più dalla portata e aspetta.</summary>
        private const float WaitRingMargin = 1.3f;
        private const float CloseDistance = 4f;

        [Tooltip("Tipo di nemico: salute, movimento, attacco e spawn (Assets/_Project/Data/Enemies).")]
        [SerializeField] private EnemyDefinition _definition;

        [Header("Ritmo dell'AI")]
        [SerializeField, Min(0.05f)] private float _thinkIntervalNear = 0.2f;
        [Tooltip("Da molto vicino (entro 4 m dal bersaglio) l'AI pensa più spesso, per fermarsi e colpire al momento giusto.")]
        [SerializeField, Min(0.05f)] private float _thinkIntervalClose = 0.1f;
        [SerializeField, Min(0.05f)] private float _thinkIntervalFar = 0.5f;
        [SerializeField, Min(1f)] private float _nearDistance = 25f;
        [SerializeField, Min(1f)] private float _stuckRecycleSeconds = 8f;
        [SerializeField, Min(0f)] private float _corpseSeconds = 0.6f;

        [Header("Smembramento (solo modelli con scheletro)")]
        [Tooltip("Arti staccabili a colpi, morte diversa per parte colpita. Spento per i nemici greybox/corazzati (Brute).")]
        [SerializeField] private bool _dismemberable;
        [Tooltip("Velocità strisciando dopo aver perso una gamba (m/s).")]
        [SerializeField, Min(0.1f)] private float _crawlSpeed = 0.8f;
        [Tooltip("Tempo a terra dopo la perdita della gamba prima di strisciare (caduta).")]
        [SerializeField, Min(0f)] private float _legLostStun = 1.1f;

        [Header("Andature (modelli con più corse)")]
        [Tooltip("Velocità (m/s) di ogni andatura: il server ne sceglie una per nemico. Vuoto = MoveSpeed della scheda.")]
        [SerializeField] private float[] _gaitSpeeds = System.Array.Empty<float>();
        [Tooltip("Probabilità relativa di ogni andatura (stessa lunghezza di Gait Speeds).")]
        [SerializeField] private float[] _gaitWeights = System.Array.Empty<float>();

        private static readonly List<EnemyAgent> s_clientActive = new(48);

        private NavMeshAgent _agent;
        private Health _health;
        private IEnemyAbility _ability;
        private Collider[] _hitboxes = System.Array.Empty<Collider>();
        private EnemyTarget _target;
        private EnemyTarget _slotTarget;
        private EnemyState _state;
        private Vector3 _lastDestination;
        private bool _hasDestination;
        private float _nextThink;
        private float _lastThink;
        private float _nextRetarget;
        private float _nextRepath;
        private float _nextAttack;
        private float _stuckTime;
        private float _despawnAt = -1f;

        /// <summary>Arti persi (bit = indice arto di <see cref="EnemyHitRegions"/>, bit 9 testa esplosa). Server-authoritative.</summary>
        private readonly SyncVar<ushort> _lostLimbs = new();
        /// <summary>Andatura scelta dal server (indice della corsa nel controller): replicata una volta allo spawn.</summary>
        private readonly SyncVar<byte> _gait = new();
        private readonly float[] _limbDamage = new float[EnemyHitRegions.LimbCount];
        private float _crawlFrom = -1f;
        private Transform _hitboxRoot;
        private Vector3 _hitboxRootPos;
        private Quaternion _hitboxRootRot;
        private Vector2 _bodyCenter;

        /// <summary>Client: il nemico ha colpito (evento cosmetico per l'affondo).</summary>
        public event Action ClientAttacked;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Solo prove (F10 nell'EnemyDirector): i nemici attaccano senza fare danno.</summary>
        public static bool DebugNoDamage;
#endif

        /// <summary>
        /// Morte vista dai client: punto del colpo letale e direzione del tiro nello spazio locale del nemico,
        /// tipo di caduta (EnemyHitRegions.Death*) e variante a caso. Solo cosmetico (animazione, smembramento).
        /// La maschera degli arti arriva insieme (vedi <see cref="ClientLimbsChanged"/>).
        /// </summary>
        public event Action<Vector3, Vector3, byte, byte> ClientKilled;

        /// <summary>
        /// Client: arti persi cambiati (maschera completa, punto e direzione locali del colpo, con effetti).
        /// Arriva con gli RPC di taglio e di morte: chi lo ascolta applica solo i bit nuovi.
        /// Chi entra dopo legge <see cref="LostLimbs"/> in OnStartClient e li applica in silenzio.
        /// </summary>
        public event Action<ushort, Vector3, Vector3, bool> ClientLimbsChanged;

        /// <summary>Tipo e variante dell'ultima morte ricevuta (per i client che arrivano dopo l'RPC).</summary>
        public byte ClientDeathType { get; private set; }
        public byte ClientDeathVariant { get; private set; }
        public ushort LostLimbs => _lostLimbs.Value;
        public byte Gait => _gait.Value;
        public bool IsCrippled => EnemyHitRegions.IsCrippled(_lostLimbs.Value);
        public bool Dismemberable => _dismemberable;

        /// <summary>Client: nemici in gioco (per la scansione della tuta e i ping). Iterare con l'indice, niente allocazioni.</summary>
        public static List<EnemyAgent> ClientActive => s_clientActive;
        /// <summary>Morto secondo la salute replicata (valido anche sui client, dove l'AI non gira).</summary>
        public bool ClientIsDead => _health == null || _health.IsDead;
        public EnemyDefinition Definition => _definition;
        public EnemyState State => _state;
        public EnemyTarget Target => _target;
        public bool IsDead => _state == EnemyState.Dead;
        public bool WantsRecycle { get; private set; }
        public float NextThink => _nextThink;
        public float DespawnAt => _despawnAt;
        public float ServerSpeed => Mathf.Max(_agent != null && _agent.enabled ? _agent.velocity.magnitude : 0f,
            _ability != null ? _ability.ServerSpeedOverride : 0f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_clientActive.Clear();

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _ability = GetComponent<IEnemyAbility>();
            _agent.enabled = false; // solo il server lo attiva
            if (_definition != null)
                _health.SetMaxHealth(_definition.MaxHealth); // su tutti i peer, prima dello spawn
            else
                Debug.LogError($"[FpsWave] {name}: EnemyDefinition non assegnata.", this);

            _hitboxes = new Collider[_health.HitZoneCount];
            for (int i = 0; i < _hitboxes.Length; i++)
                _hitboxes[i] = _health.GetZone(i) != null ? _health.GetZone(i).Collider : null;

            if (_hitboxes.Length > 0 && _hitboxes[0] != null)
            {
                _hitboxRoot = _hitboxes[0].transform.parent;
                Vector3 c = _hitboxes[0].transform.localPosition;
                _bodyCenter = new Vector2(c.x, c.z);
            }
            if (_hitboxRoot != null && _hitboxRoot != transform)
            {
                _hitboxRootPos = _hitboxRoot.localPosition;
                _hitboxRootRot = _hitboxRoot.localRotation;
            }
            else
            {
                _hitboxRoot = null;
            }

            _health.ServerDied += HandleServerDied;
            _health.ServerDamaged += HandleServerDamaged;
            _health.ClientHealthChanged += HandleClientHealthChanged;
            _lostLimbs.OnChange += HandleLostLimbsChanged;
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.ServerDied -= HandleServerDied;
                _health.ServerDamaged -= HandleServerDamaged;
                _health.ClientHealthChanged -= HandleClientHealthChanged;
            }
            _lostLimbs.OnChange -= HandleLostLimbsChanged;
            s_clientActive.Remove(this);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            s_clientActive.Remove(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!s_clientActive.Contains(this))
                s_clientActive.Add(this);
            SetHitboxesEnabled(!_health.IsDead);
            ApplyCrippledHitboxes(IsCrippled);
        }

        /// <summary>
        /// Da morto il nemico non è più colpibile: i colpi passano oltre e i segni d'impatto finiscono sull'ambiente,
        /// non a mezz'aria dove c'era la hitbox mentre il modello cade.
        /// </summary>
        private void SetHitboxesEnabled(bool enabled)
        {
            for (int i = 0; i < _hitboxes.Length; i++)
            {
                if (_hitboxes[i] == null)
                    continue;
                // Le piastre di corazza rotte restano spente (le gestisce EnemyArmor).
                bool brokenPlate = _hitboxes[i].TryGetComponent(out ArmorPlate plate) && plate.IsBroken;
                _hitboxes[i].enabled = enabled && !brokenPlate;
            }
        }

        private void HandleClientHealthChanged(float previous, float next)
        {
            if (next <= 0f)
                SetHitboxesEnabled(false);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            SetHitboxesEnabled(true);
            _lostLimbs.Value = 0;
            _gait.Value = PickGait();
            System.Array.Clear(_limbDamage, 0, _limbDamage.Length);
            _crawlFrom = -1f;
            ApplyCrippledHitboxes(false);
            ApplyAgentSettings();
            _agent.enabled = true;
            _agent.Warp(transform.position);
            _agent.isStopped = false;

            float now = Time.time;
            _state = EnemyState.Idle;
            _target = null;
            _slotTarget = null;
            _hasDestination = false;
            WantsRecycle = false;
            _stuckTime = 0f;
            _despawnAt = -1f;
            _lastThink = now;
            _nextThink = now + UnityEngine.Random.value * _thinkIntervalNear; // scaglionati: non pensano tutti nello stesso frame
            _nextRetarget = now + RetargetInterval;
            _nextRepath = 0f;
            _nextAttack = 0f;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ReleaseSlot();
            if (_agent.enabled)
            {
                if (_agent.isOnNavMesh)
                    _agent.ResetPath();
                _agent.enabled = false;
            }
            _target = null;
        }

        /// <summary>Server, subito dopo lo spawn: bersaglio iniziale scelto dal director (il player meno pressato).</summary>
        internal void ServerAssignTarget(EnemyTarget target)
        {
            _target = target;
            _state = target != null ? EnemyState.Chase : EnemyState.Idle;
        }

        private void ApplyAgentSettings()
        {
            _agent.speed = _gait.Value < _gaitSpeeds.Length ? _gaitSpeeds[_gait.Value] : _definition.MoveSpeed;
            _agent.acceleration = _definition.Acceleration;
            _agent.angularSpeed = _definition.AngularSpeed;
            _agent.radius = _definition.Radius;
            _agent.height = _definition.Height;
            _agent.stoppingDistance = _definition.StopDistance;
            // Frenata attiva: senza, tra due "pensieri" il nemico supera la distanza di arresto ed entra nel player
            // (la camera finisce dentro la sua hitbox e i raycast partiti da dentro un collider non lo colpiscono).
            _agent.autoBraking = true;
            _agent.autoRepath = true;
            _agent.updateRotation = true;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            // Priorità diverse: in un gruppo si superano invece di bloccarsi a vicenda.
            _agent.avoidancePriority = UnityEngine.Random.Range(30, 70);
        }

        private byte PickGait()
        {
            if (_gaitSpeeds.Length == 0)
                return 0;
            float total = 0f;
            for (int i = 0; i < _gaitSpeeds.Length; i++)
                total += i < _gaitWeights.Length ? Mathf.Max(0f, _gaitWeights[i]) : 1f;
            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < _gaitSpeeds.Length; i++)
            {
                roll -= i < _gaitWeights.Length ? Mathf.Max(0f, _gaitWeights[i]) : 1f;
                if (roll <= 0f)
                    return (byte)i;
            }
            return (byte)(_gaitSpeeds.Length - 1);
        }

        /// <summary>Un passo dell'AI (solo server). Chiamato dal director quando <see cref="NextThink"/> è scaduto.</summary>
        internal void ServerThink(float now, List<EnemyTarget> targets)
        {
            float dt = now - _lastThink;
            _lastThink = now;
            if (_state == EnemyState.Dead)
                return;

            // Appena persa una gamba: a terra un attimo (caduta), poi striscia piano.
            if (_crawlFrom >= 0f)
            {
                if (now < _crawlFrom)
                {
                    _nextThink = now + _thinkIntervalNear;
                    return;
                }
                _crawlFrom = -1f;
                _agent.speed = _crawlSpeed;
                _agent.isStopped = false;
                _hasDestination = false;
            }

            if (!_agent.isOnNavMesh)
            {
                AccumulateStuck(dt);
                _nextThink = now + _thinkIntervalFar;
                return;
            }

            if (_target == null || !_target.IsAlive || !_target.IsServerInitialized || now >= _nextRetarget)
            {
                EnemyTarget next = SelectTarget(targets, _target);
                if (next != _slotTarget)
                    ReleaseSlot();
                _target = next;
                _nextRetarget = now + RetargetInterval;
            }

            if (_target == null)
            {
                if (_state != EnemyState.Idle)
                {
                    _agent.ResetPath();
                    _hasDestination = false;
                    _state = EnemyState.Idle;
                }
                _stuckTime = 0f;
                _nextThink = now + _thinkIntervalFar;
                return;
            }

            // Abilità del tipo (es. carica del Brute): se agisce, l'AI base aspetta.
            if (_ability != null && _ability.ServerOverrides(now, _target))
            {
                ReleaseSlot();
                _hasDestination = false;
                _stuckTime = 0f;
                _nextThink = now + _thinkIntervalClose;
                return;
            }

            Vector3 position = transform.position;
            Vector3 targetPosition = _target.Position;
            Vector3 delta = targetPosition - position;
            float heightDelta = Mathf.Abs(delta.y);
            delta.y = 0f;
            float distance = delta.magnitude;

            float reach = _state == EnemyState.Attack ? _definition.AttackRange + AttackExitMargin : _definition.AttackRange;
            bool closeEnough = distance <= reach && heightDelta < MaxAttackHeightDelta;
            if (closeEnough && _slotTarget != _target && !TryTakeSlot(_target))
            {
                // Tutti i posti di attacco occupati: si aspetta appena fuori portata, rivolti al player.
                closeEnough = false;
                if (distance <= _definition.AttackRange + WaitRingMargin)
                {
                    HoldPosition(delta, distance);
                    _nextThink = now + _thinkIntervalNear;
                    return;
                }
            }
            if (closeEnough)
            {
                if (_state != EnemyState.Attack)
                {
                    _state = EnemyState.Attack;
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                    _nextAttack = Mathf.Max(_nextAttack, now + _definition.AttackWindup);
                }
                if (distance > 0.01f)
                    transform.rotation = Quaternion.LookRotation(delta / distance, Vector3.up);
                if (now >= _nextAttack)
                {
                    _nextAttack = now + _definition.AttackInterval;
                    TryHit(position, _target);
                }
                _stuckTime = 0f;
            }
            else
            {
                if (distance > _definition.AttackRange + AttackExitMargin)
                    ReleaseSlot();
                if (_state != EnemyState.Chase)
                {
                    _state = EnemyState.Chase;
                    _agent.isStopped = false;
                }
                float repathInterval = distance > _nearDistance ? 1f : 0.35f;
                if (!_hasDestination || now >= _nextRepath || (targetPosition - _lastDestination).sqrMagnitude > RepathMoveSqr)
                {
                    _agent.SetDestination(targetPosition);
                    _lastDestination = targetPosition;
                    _hasDestination = true;
                    _nextRepath = now + repathInterval;
                }

                if (!_agent.pathPending && _agent.velocity.sqrMagnitude < 0.25f)
                    AccumulateStuck(dt);
                else
                    _stuckTime = 0f;
            }

            _nextThink = now + (distance > _nearDistance ? _thinkIntervalFar
                : distance < CloseDistance ? _thinkIntervalClose : _thinkIntervalNear);
        }

        private bool TryTakeSlot(EnemyTarget target)
        {
            ReleaseSlot();
            if (target == null || !target.TryAcquireAttackSlot())
                return false;
            _slotTarget = target;
            return true;
        }

        private void ReleaseSlot()
        {
            if (_slotTarget != null)
                _slotTarget.ReleaseAttackSlot();
            _slotTarget = null;
        }

        private void HoldPosition(Vector3 delta, float distance)
        {
            if (_state != EnemyState.Idle)
            {
                _state = EnemyState.Idle;
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
                _hasDestination = false;
            }
            if (distance > 0.01f)
                transform.rotation = Quaternion.LookRotation(delta / distance, Vector3.up);
            _stuckTime = 0f;
        }

        private void AccumulateStuck(float dt)
        {
            _stuckTime += dt;
            if (_stuckTime > _stuckRecycleSeconds)
                WantsRecycle = true;
        }

        private EnemyTarget SelectTarget(List<EnemyTarget> targets, EnemyTarget current)
        {
            Vector3 position = transform.position;
            EnemyTarget best = null;
            float bestSqr = float.MaxValue;
            float currentSqr = float.MaxValue;
            bool currentValid = current != null && current.IsAlive && current.IsServerInitialized;
            for (int i = 0; i < targets.Count; i++)
            {
                EnemyTarget t = targets[i];
                if (t == null || !t.IsAlive)
                    continue;
                float sqr = (t.Position - position).sqrMagnitude;
                if (t == current)
                    currentSqr = sqr;
                if (sqr < bestSqr)
                {
                    best = t;
                    bestSqr = sqr;
                }
            }
            if (currentValid && best != current && bestSqr >= currentSqr * SwitchRatioSqr)
                return current;
            return best;
        }

        private void TryHit(Vector3 position, EnemyTarget target)
        {
            Vector3 from = position + Vector3.up * 1.2f;
            Vector3 to = target.Position + Vector3.up * 1.2f;
            if (Physics.Linecast(from, to, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
                return; // un muro in mezzo: niente colpi attraverso la geometria
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!DebugNoDamage)
#endif
            target.Health.ApplyDamage(new DamageInfo(_definition.AttackDamage, false, false, -1, to));
            RpcAttacked();
        }

        [ObserversRpc]
        private void RpcAttacked() => ClientAttacked?.Invoke();

        [ObserversRpc]
        private void RpcKilled(Vector3 localPoint, Vector3 localDir, byte deathType, byte variant, ushort mask)
        {
            ClientDeathType = deathType;
            ClientDeathVariant = variant;
            // Prima gli arti (con effetti), poi la caduta: la SyncVar potrebbe arrivare dopo l'RPC.
            ClientLimbsChanged?.Invoke(mask, localPoint, localDir, true);
            ClientKilled?.Invoke(localPoint, localDir, deathType, variant);
        }

        /// <summary>Direzione del tiro (dal giocatore che ha sparato, se lo troviamo; altrimenti dal davanti), in spazio mondo.</summary>
        private Vector3 ShotDirection(in DamageInfo info)
        {
            foreach (HrdnLab.FpsWave.Player.PlayerLife life in HrdnLab.FpsWave.Player.PlayerLife.All)
            {
                if (life != null && life.OwnerId == info.AttackerClientId)
                {
                    Vector3 d = info.Point - life.transform.position;
                    d.y *= 0.3f;
                    if (d.sqrMagnitude > 0.01f)
                        return d.normalized;
                    break;
                }
            }
            return -transform.forward;
        }

        /// <summary>Parte colpita, nello spazio "in piedi" delle hitbox (vale anche quando striscia, con le hitbox ruotate).</summary>
        private HitRegion ClassifyHit(in DamageInfo info)
        {
            Transform space = _hitboxRoot != null ? _hitboxRoot : transform;
            Vector3 p = space.InverseTransformPoint(info.Point);
            p.x -= _bodyCenter.x;
            p.z -= _bodyCenter.y;
            HitRegion region = EnemyHitRegions.Classify(p, info.IsWeakspot, _definition != null ? _definition.Height : 1.8f);
            // Un arto già perso non si ricolpisce: il colpo va al busto.
            if (region != HitRegion.Head && EnemyHitRegions.IsLost(_lostLimbs.Value, EnemyHitRegions.LimbIndex(region)))
                region = HitRegion.Body;
            return region;
        }

        /// <summary>Server, a ogni colpo (prima della morte): accumula il danno sull'arto e lo stacca oltre la soglia.</summary>
        private void HandleServerDamaged(Health health, DamageInfo info)
        {
            if (!_dismemberable || _state == EnemyState.Dead)
                return;
            HitRegion region = ClassifyHit(info);
            int limb = EnemyHitRegions.LimbIndex(region);
            if (limb <= 0) // busto o testa: nessun arto da vivo
                return;
            _limbDamage[limb] += info.Amount * health.ServerDamageScale;
            if (health.IsDead)
                return; // il colpo letale lo decide HandleServerDied
            float threshold = EnemyHitRegions.SeverThreshold(region) * health.Max;
            if (_limbDamage[limb] >= threshold)
                ServerSever(limb, info);
        }

        private void ServerSever(int limb, in DamageInfo info)
        {
            ushort before = _lostLimbs.Value;
            ushort after = (ushort)(before | (1 << limb));
            if (after == before)
                return;
            _lostLimbs.Value = after;
            RpcSevered(after, transform.InverseTransformPoint(info.Point), transform.InverseTransformDirection(ShotDirection(info)));
            if (!EnemyHitRegions.IsCrippled(before) && EnemyHitRegions.IsCrippled(after))
            {
                // Senza una gamba: cade e striscia, hitbox basse (stessa posa sui client, dalla maschera).
                ApplyCrippledHitboxes(true);
                _crawlFrom = Time.time + _legLostStun;
                if (_agent.enabled && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                }
                ReleaseSlot();
                if (_state == EnemyState.Attack)
                    _state = EnemyState.Chase;
            }
        }

        [ObserversRpc]
        private void RpcSevered(ushort mask, Vector3 localPoint, Vector3 localDir) =>
            ClientLimbsChanged?.Invoke(mask, localPoint, localDir, true);

        private void HandleLostLimbsChanged(ushort previous, ushort next, bool asServer)
        {
            if (asServer && IsClientInitialized)
                return; // l'host lo riceve anche come client
            // Solo le hitbox: gli arti si staccano con gli RPC (con effetti), chi arriva dopo li applica in OnStartClient.
            ApplyCrippledHitboxes(EnemyHitRegions.IsCrippled(next));
        }

        /// <summary>
        /// Strisciando le hitbox si sdraiano in avanti (busto lungo +Z a terra, testa davanti), uguali su server e client.
        /// </summary>
        private void ApplyCrippledHitboxes(bool crippled)
        {
            if (_hitboxRoot == null)
                return;
            if (crippled)
            {
                _hitboxRoot.localRotation = _hitboxRootRot * Quaternion.Euler(78f, 0f, 0f);
                _hitboxRoot.localPosition = _hitboxRootPos + new Vector3(0f, 0.28f, -0.15f);
            }
            else
            {
                _hitboxRoot.localRotation = _hitboxRootRot;
                _hitboxRoot.localPosition = _hitboxRootPos;
            }
        }

        private void HandleServerDied(Health health, DamageInfo info)
        {
            Vector3 shotDir = ShotDirection(info);
            Vector3 localPoint = transform.InverseTransformPoint(info.Point);
            Vector3 localDir = transform.InverseTransformDirection(shotDir);
            byte deathType;
            ushort mask = _lostLimbs.Value;
            if (_dismemberable)
            {
                HitRegion region = ClassifyHit(info);
                int limb = EnemyHitRegions.LimbIndex(region);
                float roll = UnityEngine.Random.value;
                if (region == HitRegion.Head)
                {
                    // Colpo alla testa letale: di solito esplode, a volte vola via intera, a volte resta.
                    if (roll < 0.65f)
                        mask |= (1 << 0) | (1 << EnemyHitRegions.HeadExplodedBit);
                    else if (roll < 0.85f)
                        mask |= 1 << 0;
                }
                else if (limb > 0)
                {
                    // Arto: si stacca se già malridotto, altrimenti a volte. Busto: mai.
                    float threshold = EnemyHitRegions.SeverThreshold(region) * health.Max;
                    if (_limbDamage[limb] >= threshold * 0.5f || UnityEngine.Random.value < 0.55f)
                        mask |= (ushort)(1 << limb);
                }
                deathType = EnemyHitRegions.ChooseDeathType(region, localDir, UnityEngine.Random.value);
                if (mask != _lostLimbs.Value)
                    _lostLimbs.Value = mask;
            }
            else
            {
                deathType = info.IsWeakspot ? EnemyHitRegions.DeathHeadFront
                    : localDir.z > 0f ? EnemyHitRegions.DeathForward : EnemyHitRegions.DeathBack;
            }
            RpcKilled(localPoint, localDir, deathType, (byte)UnityEngine.Random.Range(0, 256), mask);
            _crawlFrom = -1f;

            ReleaseSlot();
            SetHitboxesEnabled(false);
            _state = EnemyState.Dead;
            _target = null;
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
            _despawnAt = Time.time + _corpseSeconds;
        }

#if UNITY_EDITOR
        public void EditorSetup(EnemyDefinition definition) => _definition = definition;

        /// <summary>Tempo a terra prima del riciclo (più lungo con l'animazione di morte del modello vero).</summary>
        public void EditorSetCorpseSeconds(float seconds) => _corpseSeconds = seconds;

        public void EditorSetDismemberable(bool value) => _dismemberable = value;

        public void EditorSetGaits(float[] speeds, float[] weights)
        {
            _gaitSpeeds = speeds ?? System.Array.Empty<float>();
            _gaitWeights = weights ?? System.Array.Empty<float>();
        }
#endif
    }
}
