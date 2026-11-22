using FishNet.Object;
using HrdnLab.FpsWave.Combat;
using UnityEngine;

namespace HrdnLab.FpsWave.Enemies
{
    /// <summary>
    /// Solo cosmetico, su ogni client (host compreso): corsa procedurale (oscillazione e inclinazione ricavate dallo
    /// spostamento interpolato), affondo quando colpisce, colore per la salute e posa a terra alla morte.
    /// Nessun dato di rete in più: usa la posizione già replicata, la SyncVar della salute e un RPC di attacco.
    /// Oltre <see cref="_animationCullDistance"/> dalla camera non anima (come l'Animator culling dei nemici veri).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class EnemyVisual : NetworkBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int DeadId = Animator.StringToHash("Dead");
        private static readonly int RunRateId = Animator.StringToHash("RunRate");
        private static readonly int VariantId = Animator.StringToHash("Variant");
        private static readonly int AttackIndexId = Animator.StringToHash("AttackIndex");
        private static readonly int HitId = Animator.StringToHash("Hit");
        private static readonly int DeathTypeId = Animator.StringToHash("DeathType");
        private static readonly int[] DeathStateIds =
            { Animator.StringToHash("DeathBack"), Animator.StringToHash("DeathForward"), Animator.StringToHash("DeathHead") };
        private static readonly int LocomotionId = Animator.StringToHash("Locomotion");

        [SerializeField] private Transform _model;
        [SerializeField] private Renderer[] _renderers = System.Array.Empty<Renderer>();
        [SerializeField] private Color _aliveColor = new(0.55f, 0.13f, 0.1f);
        [SerializeField] private Color _hurtColor = new(0.95f, 0.45f, 0.2f);
        [SerializeField] private Color _deadColor = new(0.12f, 0.1f, 0.1f);
        [SerializeField, Min(0f)] private float _runSpeedForFullCycle = 6f;
        [SerializeField, Min(1f)] private float _animationCullDistance = 45f;
        [Tooltip("Modello con scheletro: corsa, attacco e morte vengono dalle clip (parametri Speed, Attack, Dead).")]
        [SerializeField] private Animator _animator;
        [Tooltip("Velocità naturale della clip di corsa (m/s): sopra, la clip accelera in proporzione.")]
        [SerializeField, Min(0.1f)] private float _runClipSpeed = 4f;
        [SerializeField, Min(0.1f)] private float _run2ClipSpeed = 4f;
        [SerializeField, Min(1)] private int _attackVariants = 1;
        [SerializeField] private EnemyGore _gore;
        private float _variant;
        private float _nextHitReact;
        private bool _killReceived;
        private float _deadSince = -1f;

        private Health _health;
        private EnemyAgent _agent;
        private BruteCharge _charge;
        private float _windup;
        private MaterialPropertyBlock _block;
        private Vector3 _lastPosition;
        private float _speed;
        private float _phase;
        private float _lunge;
        private float _deathBlend;
        private float _hurtFlash;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _agent = GetComponent<EnemyAgent>();
            _block = new MaterialPropertyBlock();
            _health.ClientHealthChanged += HandleHealthChanged;
            if (_agent != null)
            {
                _agent.ClientAttacked += HandleAttacked;
                _agent.ClientKilled += HandleKilled;
            }
            _charge = GetComponent<BruteCharge>();
            if (_charge != null)
                _charge.ClientWindup += HandleWindup;
        }

        private void HandleWindup() => _windup = 1f;

        private void OnDestroy()
        {
            if (_health != null)
                _health.ClientHealthChanged -= HandleHealthChanged;
            if (_agent != null)
            {
                _agent.ClientAttacked -= HandleAttacked;
                _agent.ClientKilled -= HandleKilled;
            }
            if (_charge != null)
                _charge.ClientWindup -= HandleWindup;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Il pool riusa l'oggetto: riportare la posa allo stato iniziale.
            _lastPosition = transform.position;
            _speed = 0f;
            _phase = Random.value * 10f;
            _lunge = 0f;
            _hurtFlash = 0f;
            _deathBlend = _health.IsDead ? 1f : 0f;
            _killReceived = false;
            _deadSince = -1f;
            if (_gore != null)
                _gore.ResetGore();
            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.Rebind();
                _variant = Random.value < 0.5f ? 0f : 1f;
                _animator.SetFloat(VariantId, _variant);
                // Sciame non sincronizzato: fase e ritmo un po' diversi per ognuno.
                _animator.speed = Random.Range(0.92f, 1.08f);
                if (_health.IsDead)
                {
                    int type = _agent != null ? Mathf.Clamp(_agent.ClientDeathType, 0, 2) : 0;
                    _animator.SetInteger(DeathTypeId, type);
                    _animator.SetBool(DeadId, true);
                    _animator.Play(DeathStateIds[type], 0, 1f);
                    _killReceived = true;
                }
                else
                {
                    _animator.Play(LocomotionId, 0, Random.value);
                }
                _animator.Update(0f);
            }
            else
            {
                ApplyPose();
            }
            ApplyColor();
        }

        private void HandleHealthChanged(float previous, float next)
        {
            if (next < previous)
                _hurtFlash = 1f;
            // Reazione al colpo (non letale), non più di una ogni tanto per non fermare la corsa sotto il fuoco.
            if (_animator != null && next < previous && next > 0f && Time.time >= _nextHitReact)
            {
                _animator.SetTrigger(HitId);
                _nextHitReact = Time.time + 1.3f;
            }
            ApplyColor();
        }

        private void HandleAttacked()
        {
            _lunge = 1f;
            if (_animator != null && !_health.IsDead)
            {
                _animator.SetInteger(AttackIndexId, Random.Range(0, _attackVariants));
                _animator.SetTrigger(AttackId);
            }
        }

        private void HandleKilled(Vector3 localPoint, Vector3 localDir, byte deathType)
        {
            _killReceived = true;
            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.SetInteger(DeathTypeId, deathType);
                _animator.SetBool(DeadId, true);
            }
            if (_gore != null)
                _gore.Kill(transform.TransformPoint(localPoint), transform.TransformDirection(localDir), deathType == 2);
        }

        private void LateUpdate()
        {
            if (!IsClientInitialized || _model == null)
                return;

            float dt = Time.deltaTime;
            Vector3 position = transform.position;
            Vector3 moved = position - _lastPosition;
            moved.y = 0f;
            _lastPosition = position;
            if (dt > 0f)
                _speed = Mathf.Lerp(_speed, moved.magnitude / dt, 1f - Mathf.Exp(-10f * dt));

            bool dead = _health.IsDead;
            _deathBlend = Mathf.MoveTowards(_deathBlend, dead ? 1f : 0f, dt * 4f);
            _lunge = Mathf.MoveTowards(_lunge, 0f, dt * 4f);
            _windup = Mathf.MoveTowards(_windup, 0f, dt * 1.3f);
            if (_hurtFlash > 0f)
            {
                _hurtFlash = Mathf.MoveTowards(_hurtFlash, 0f, dt * 6f);
                ApplyColor();
            }

            Camera cam = Camera.main;
            bool far = !dead && cam != null && (cam.transform.position - position).sqrMagnitude > _animationCullDistance * _animationCullDistance;
            if (_animator != null)
            {
                _animator.enabled = !far;
                _animator.SetFloat(SpeedId, _speed);
                float clipSpeed = _variant > 0.5f ? _run2ClipSpeed : _runClipSpeed;
                _animator.SetFloat(RunRateId, Mathf.Max(1f, _speed / clipSpeed));
                // La caduta parte con l'RPC della morte (tipo giusto); se non arriva, dopo un attimo cade comunque.
                if (dead && _deadSince < 0f)
                    _deadSince = Time.time;
                if (!dead)
                    _deadSince = -1f;
                _animator.SetBool(DeadId, dead && (_killReceived || Time.time - _deadSince > 0.25f));
                return;
            }
            if (far)
                return;

            _phase += dt * Mathf.Lerp(0f, 11f, Mathf.Clamp01(_speed / Mathf.Max(0.1f, _runSpeedForFullCycle)));
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (_model == null)
                return;
            float run = Mathf.Clamp01(_speed / Mathf.Max(0.1f, _runSpeedForFullCycle));
            // Movimenti contenuti: le hitbox restano ferme (uguali sul server), il corpo visibile non deve allontanarsene.
            float bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.05f * run;
            float sway = Mathf.Sin(_phase) * 4f * run;
            float lean = 7f * run + 14f * _lunge - 16f * Mathf.Sin(_windup * Mathf.PI); // carica: si piega indietro e poi scatta
            Quaternion alive = Quaternion.Euler(lean, 0f, sway);
            Quaternion dead = Quaternion.Euler(-88f, 0f, 0f);
            _model.localRotation = Quaternion.Slerp(alive, dead, _deathBlend);
            _model.localPosition = new Vector3(0f, bob * (1f - _deathBlend) + 0.25f * _deathBlend, 0.08f * _lunge);
        }

        private void ApplyColor()
        {
            Color color = _health.IsDead
                ? _deadColor
                : Color.Lerp(Color.Lerp(_hurtColor, _aliveColor, _health.Normalized), Color.white, _hurtFlash * 0.6f);
            _block.SetColor(BaseColorId, color);
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].SetPropertyBlock(_block);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform model, Renderer[] renderers, Color aliveColor, float runSpeedForFullCycle)
        {
            _model = model;
            _renderers = renderers;
            _aliveColor = aliveColor;
            _runSpeedForFullCycle = runSpeedForFullCycle;
        }

        public void EditorSetAnimator(Animator animator, float runClipSpeed, float run2ClipSpeed, int attackVariants, EnemyGore gore)
        {
            _animator = animator;
            _runClipSpeed = runClipSpeed;
            _run2ClipSpeed = run2ClipSpeed;
            _attackVariants = attackVariants;
            _gore = gore;
        }

        /// <summary>Per i modelli con texture: i colori moltiplicano la texture (bianco = colori originali).</summary>
        public void EditorSetup(Transform model, Renderer[] renderers, Color aliveColor, Color hurtColor, Color deadColor, float runSpeedForFullCycle)
        {
            EditorSetup(model, renderers, aliveColor, runSpeedForFullCycle);
            _hurtColor = hurtColor;
            _deadColor = deadColor;
        }
#endif
    }
}
