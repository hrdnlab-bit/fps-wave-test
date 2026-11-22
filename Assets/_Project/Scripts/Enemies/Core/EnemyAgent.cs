using System;
using System.Collections.Generic;
using FishNet.Object;
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

        /// <summary>Client: il nemico ha colpito (evento cosmetico per l'affondo).</summary>
        public event Action ClientAttacked;

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

            _health.ServerDied += HandleServerDied;
            _health.ClientHealthChanged += HandleClientHealthChanged;
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.ServerDied -= HandleServerDied;
                _health.ClientHealthChanged -= HandleClientHealthChanged;
            }
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
            _agent.speed = _definition.MoveSpeed;
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

        /// <summary>Un passo dell'AI (solo server). Chiamato dal director quando <see cref="NextThink"/> è scaduto.</summary>
        internal void ServerThink(float now, List<EnemyTarget> targets)
        {
            float dt = now - _lastThink;
            _lastThink = now;
            if (_state == EnemyState.Dead)
                return;

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
            target.Health.ApplyDamage(new DamageInfo(_definition.AttackDamage, false, false, -1, to));
            RpcAttacked();
        }

        [ObserversRpc]
        private void RpcAttacked() => ClientAttacked?.Invoke();

        private void HandleServerDied(Health health, DamageInfo info)
        {
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
#endif
    }
}
