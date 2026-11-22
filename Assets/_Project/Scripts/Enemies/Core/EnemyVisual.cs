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
        private static readonly int DeathVariantId = Animator.StringToHash("DeathVariant");
        private static readonly int HitIndexId = Animator.StringToHash("HitIndex");
        private static readonly int CrawlId = Animator.StringToHash("Crawl");
        private static readonly int LegLostId = Animator.StringToHash("LegLost");
        private static readonly int CrawlLocoId = Animator.StringToHash("Crawl");
        private static readonly int CrawlDeathId = Animator.StringToHash("CrawlDeath");
        /// <summary>Velocità normalizzata (velocità / velocità della clip dell'andatura) a cui il blend arriva alla corsa piena.</summary>
        private const float GaitBlendPoint = 0.4f;
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
        [Tooltip("Velocità naturale (m/s) della clip di ogni andatura: la clip segue la velocità vera (piedi che non scivolano).")]
        [SerializeField] private float[] _gaitClipSpeeds = { 4f };
        [SerializeField, Min(0.05f)] private float _crawlClipSpeed = 0.36f;
        [SerializeField, Min(1)] private int _attackVariants = 1;
        [SerializeField, Min(1)] private int _hitVariants = 1;
        [Tooltip("Varianti per tipo di caduta (stati Death_<tipo>_<variante>), indice = EnemyHitRegions.Death*.")]
        [SerializeField] private int[] _deathVariants = System.Array.Empty<int>();
        [SerializeField] private EnemyGore _gore;
        private int _variant;
        private bool _crawling;
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
                _agent.ClientLimbsChanged += HandleLimbsChanged;
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
                _agent.ClientLimbsChanged -= HandleLimbsChanged;
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
            ushort lost = _agent != null ? _agent.LostLimbs : (ushort)0;
            _crawling = EnemyHitRegions.IsCrippled(lost);
            if (_gore != null)
            {
                _gore.ResetGore();
                if (lost != 0) // arrivato a partita in corso: arti già persi, senza effetti
                    _gore.ApplyMask(lost, transform.position, -transform.forward, false);
            }
            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.Rebind();
                // Andatura decisa dal server (la velocità di movimento dipende da questa).
                _variant = _agent != null ? Mathf.Min(_agent.Gait, _gaitClipSpeeds.Length - 1) : 0;
                _animator.SetFloat(VariantId, _variant);
                _animator.SetBool(CrawlId, _crawling);
                // Sciame non sincronizzato: fase e ritmo un po' diversi per ognuno.
                _animator.speed = Random.Range(0.92f, 1.08f);
                if (_health.IsDead)
                {
                    _killReceived = true;
                    if (_crawling)
                    {
                        _animator.SetBool(DeadId, true);
                        _animator.Play(CrawlDeathId, 0, 1f);
                    }
                    else
                    {
                        int type = _agent != null ? _agent.ClientDeathType : 0;
                        int v = DeathVariant(type, _agent != null ? _agent.ClientDeathVariant : 0);
                        _animator.SetInteger(DeathTypeId, type);
                        _animator.SetInteger(DeathVariantId, v);
                        _animator.SetBool(DeadId, true);
                        _animator.Play(Animator.StringToHash($"Death_{type}_{v}"), 0, 1f);
                    }
                }
                else
                {
                    _animator.Play(_crawling ? CrawlLocoId : LocomotionId, 0, Random.value);
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
            if (_animator != null && !_crawling && next < previous && next > 0f && Time.time >= _nextHitReact)
            {
                _animator.SetInteger(HitIndexId, Random.Range(0, _hitVariants));
                _animator.SetTrigger(HitId);
                _nextHitReact = Time.time + Random.Range(1.1f, 1.8f);
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

        /// <summary>Variante della caduta: il server manda un byte a caso, qui si riduce alle clip disponibili per quel tipo.</summary>
        private int DeathVariant(int type, int roll)
        {
            int count = type >= 0 && type < _deathVariants.Length ? _deathVariants[type] : 1;
            return count > 0 ? roll % count : 0;
        }

        private void HandleLimbsChanged(ushort mask, Vector3 localPoint, Vector3 localDir, bool effects)
        {
            if (_gore != null)
                _gore.ApplyMask(mask, transform.TransformPoint(localPoint), transform.TransformDirection(localDir), effects);
            bool crawl = EnemyHitRegions.IsCrippled(mask);
            if (crawl && !_crawling && _animator != null)
            {
                _crawling = true;
                _animator.SetBool(CrawlId, true);
                if (!_health.IsDead)
                    _animator.SetTrigger(LegLostId); // crolla a terra, poi striscia
            }
        }

        private void HandleKilled(Vector3 localPoint, Vector3 localDir, byte deathType, byte variant)
        {
            _killReceived = true;
            if (_gore != null)
                _gore.Impact(transform.TransformPoint(localPoint), transform.TransformDirection(localDir));
            if (_animator == null)
                return;
            _animator.enabled = true;
            if (_crawling)
            {
                // Senza gambe il corpo non può restare in piedi: crolla a faccia in giù. Da terra (striscio) si parte quasi
                // dalla fine della caduta, da in piedi (gambe staccate dal colpo letale) a metà, così cade subito.
                bool wasCrawling = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == CrawlLocoId
                                   || _animator.GetNextAnimatorStateInfo(0).shortNameHash == CrawlLocoId;
                _animator.SetBool(DeadId, true);
                // Offset normalizzato nella clip (la clip parte in piedi): 0.78 = già a terra, 0.42 = sta crollando.
                _animator.CrossFade(CrawlDeathId, 0.15f, 0, wasCrawling ? 0.78f : 0.42f);
                return;
            }
            _animator.SetInteger(DeathTypeId, deathType);
            _animator.SetInteger(DeathVariantId, DeathVariant(deathType, variant));
            _animator.SetBool(DeadId, true);
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
                if (_crawling)
                {
                    // Striscio: la clip segue la velocità (ferma = si trascina piano sul posto).
                    _animator.SetFloat(RunRateId, _speed < 0.1f ? 0.35f : Mathf.Clamp(_speed / _crawlClipSpeed, 0.5f, 3f));
                }
                else
                {
                    // Blend Idle -> andatura sulla velocità normalizzata, poi la clip segue la velocità vera.
                    float clipSpeed = _gaitClipSpeeds.Length > 0 ? _gaitClipSpeeds[Mathf.Clamp(_variant, 0, _gaitClipSpeeds.Length - 1)] : 4f;
                    float norm = _speed / Mathf.Max(0.1f, clipSpeed);
                    _animator.SetFloat(SpeedId, norm);
                    _animator.SetFloat(RunRateId, norm < GaitBlendPoint ? 1f : Mathf.Clamp(norm, 0.7f, 1.7f));
                }
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

        public void EditorSetAnimator(Animator animator, float[] gaitClipSpeeds,
            float crawlClipSpeed, int attackVariants, int hitVariants, int[] deathVariants, EnemyGore gore)
        {
            _animator = animator;
            _gaitClipSpeeds = gaitClipSpeeds;
            _crawlClipSpeed = crawlClipSpeed;
            _attackVariants = attackVariants;
            _hitVariants = hitVariants;
            _deathVariants = deathVariants;
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
