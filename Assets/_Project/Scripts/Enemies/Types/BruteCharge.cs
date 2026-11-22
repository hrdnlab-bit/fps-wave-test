using System;
using FishNet.Object;
using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Core;
using HrdnLab.FpsWave.Player;
using UnityEngine;
using UnityEngine.AI;

namespace HrdnLab.FpsWave.Enemies
{
    /// <summary>
    /// Carica del Brute (solo server, il resto dell'AI resta quella di <see cref="EnemyAgent"/>):
    /// a media distanza e con il bersaglio in vista si ferma, si prepara (telegrafata a tutti con l'affondo all'indietro),
    /// poi carica in linea retta sul NavMesh. Chi viene preso subisce un colpo pesante e uno spintone;
    /// contro un muro la carica si interrompe. Dopo la carica resta fermo un attimo (finestra per i punti deboli).
    /// </summary>
    [RequireComponent(typeof(EnemyAgent), typeof(NavMeshAgent))]
    public sealed class BruteCharge : NetworkBehaviour, IEnemyAbility
    {
        private enum Phase : byte
        {
            None,
            Windup,
            Charging,
            Recover,
        }

        [SerializeField, Min(0f)] private float _minRange = 6f;
        [SerializeField, Min(1f)] private float _maxRange = 16f;
        [SerializeField, Min(0f)] private float _cooldown = 7f;
        [SerializeField, Min(0.1f)] private float _windup = 0.75f;
        [SerializeField, Min(1f)] private float _speed = 12f;
        [SerializeField, Min(0.1f)] private float _maxDuration = 1.3f;
        [SerializeField, Min(0f)] private float _recover = 1.1f;
        [SerializeField, Min(0f)] private float _damage = 30f;
        [SerializeField, Min(0f)] private float _knockback = 9f;
        [SerializeField, Min(0.3f)] private float _hitRadius = 1.4f;

        private EnemyAgent _agent;
        private NavMeshAgent _nav;
        private Phase _phase;
        private float _phaseEnd;
        private float _nextCharge;
        private Vector3 _direction;
        private EnemyTarget _target;

        /// <summary>Client: il Brute si prepara a caricare (per l'affondo all'indietro di <see cref="EnemyVisual"/>).</summary>
        public event Action ClientWindup;

        public float ServerSpeedOverride => _phase == Phase.Charging ? _speed : 0f;

        private void Awake()
        {
            _agent = GetComponent<EnemyAgent>();
            _nav = GetComponent<NavMeshAgent>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _phase = Phase.None;
            _nextCharge = Time.time + _cooldown * 0.5f;
            _target = null;
        }

        public bool ServerOverrides(float now, EnemyTarget target)
        {
            if (_phase != Phase.None)
                return true;
            if (target == null || !target.IsAlive || now < _nextCharge || !_nav.enabled || !_nav.isOnNavMesh)
                return false;
            Vector3 delta = target.Position - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < _minRange || distance > _maxRange)
                return false;
            Vector3 from = transform.position + Vector3.up * 1.4f;
            if (Physics.Linecast(from, target.Position + Vector3.up * 1.2f, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
                return false;

            _target = target;
            _direction = delta / distance;
            _phase = Phase.Windup;
            _phaseEnd = now + _windup;
            _nav.isStopped = true;
            _nav.velocity = Vector3.zero;
            transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
            RpcWindup();
            return true;
        }

        private void Update()
        {
            if (!IsServerInitialized || _phase == Phase.None)
                return;
            float now = Time.time;
            if (_agent.IsDead || !_nav.enabled)
            {
                _phase = Phase.None;
                return;
            }

            switch (_phase)
            {
                case Phase.Windup:
                    // Fino a poco prima dello scatto il Brute segue il bersaglio, poi la direzione è decisa: si schiva.
                    if (_target != null && _phaseEnd - now > 0.2f)
                    {
                        Vector3 delta = _target.Position - transform.position;
                        delta.y = 0f;
                        if (delta.sqrMagnitude > 0.01f)
                        {
                            _direction = delta.normalized;
                            transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
                        }
                    }
                    if (now >= _phaseEnd)
                    {
                        _phase = Phase.Charging;
                        _phaseEnd = now + _maxDuration;
                    }
                    break;

                case Phase.Charging:
                    Vector3 move = _direction * (_speed * Time.deltaTime);
                    Vector3 position = transform.position;
                    if (NavMesh.Raycast(position, position + move * 3f, out _, NavMesh.AllAreas))
                    {
                        EndCharge(now); // muro o bordo: si ferma contro l'ostacolo
                        break;
                    }
                    _nav.Move(move);
                    if (TryHitPlayer(position))
                    {
                        EndCharge(now);
                        break;
                    }
                    if (now >= _phaseEnd)
                        EndCharge(now);
                    break;

                case Phase.Recover:
                    if (now >= _phaseEnd)
                    {
                        _phase = Phase.None;
                        _nav.isStopped = false;
                        _nextCharge = now + _cooldown;
                    }
                    break;
            }
        }

        private bool TryHitPlayer(Vector3 position)
        {
            var targets = EnemyTarget.Active;
            for (int i = 0; i < targets.Count; i++)
            {
                EnemyTarget t = targets[i];
                if (t == null || !t.IsAlive)
                    continue;
                Vector3 delta = t.Position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude > _hitRadius * _hitRadius || Vector3.Dot(delta, _direction) < -0.2f)
                    continue;
                t.Health.ApplyDamage(new DamageInfo(_damage, false, false, -1, t.Position + Vector3.up));
                if (t.TryGetComponent(out PlayerMotor motor))
                    motor.TargetKnockback(motor.Owner, _direction * _knockback + Vector3.up * 3.5f);
                return true;
            }
            return false;
        }

        private void EndCharge(float now)
        {
            _phase = Phase.Recover;
            _phaseEnd = now + _recover;
            _nav.velocity = Vector3.zero;

        }

        [ObserversRpc]
        private void RpcWindup() => ClientWindup?.Invoke();
    }
}
