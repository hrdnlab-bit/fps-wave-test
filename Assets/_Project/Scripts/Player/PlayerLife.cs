using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.UI;
using HrdnLab.FpsWave.Weapons;
using UnityEngine;

namespace HrdnLab.FpsWave.Player
{
    public enum LifeState : byte
    {
        Up,
        Downed,
        Dead,
    }

    /// <summary>
    /// A terra, rianimazione e morte (DESIGN_GAMEPLAY §2), autorevole sul server.
    /// - A 0 HP il player va a terra: striscia, solo pistola, 30 s prima di morire dissanguato.
    /// - Un compagno vicino tiene premuto Interagisci (E) per 3 s: torna in piedi con il 30% della salute e uno scudo
    ///   breve per entrambi (il "tether" della tuta).
    /// - Da solo (nessun compagno in piedi) si rialza da sé dopo pochi secondi: TEMPORANEO finché le wave (M3) non
    ///   gestiscono il wipe. Anche il morto, per ora, torna in vita sul posto dopo qualche secondo.
    /// I nemici ignorano chi è a terra (Health a 0). Il registro statico serve al HUD per gli indicatori dei compagni.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class PlayerLife : NetworkBehaviour
    {
        private static readonly List<PlayerLife> s_all = new(4);

        /// <summary>Tutti i player in gioco, su server e client.</summary>
        public static IReadOnlyList<PlayerLife> All => s_all;

        [SerializeField, Min(1f)] private float _bleedOutSeconds = 30f;
        [SerializeField, Min(0.5f)] private float _reviveSeconds = 3f;
        [SerializeField, Min(0.5f)] private float _reviveRange = 2.5f;
        [SerializeField, Range(0.05f, 1f)] private float _reviveHealthFraction = 0.3f;
        [SerializeField, Min(0f)] private float _reviveShieldSeconds = 3f;
        [Tooltip("Da solo: dopo quanti secondi a terra ci si rialza (temporaneo, finché non c'è il wipe delle wave).")]
        [SerializeField, Min(0.5f)] private float _soloReviveSeconds = 6f;
        [Tooltip("Dopo la morte: secondi prima di tornare in vita sul posto (temporaneo, poi si rientrerà a fine wave).")]
        [SerializeField, Min(0.5f)] private float _respawnSeconds = 10f;

        private readonly SyncVar<byte> _state = new();
        private readonly SyncVar<float> _reviveProgress = new();
        private readonly SyncVar<float> _timeLeft = new();

        private Health _health;
        private PlayerMotor _motor;
        private PlayerInputReader _input;
        private PlayerWeapons _weapons;
        private SuitAbilities _suit;

        // Server
        private float _stateEnd;
        private float _downedAt;
        private float _soloDeadline = float.MaxValue;
        private PlayerLife _reviver;
        private float _reviverSeenAt;

        // Proprietario
        private PlayerLife _reviveTarget;
        private float _nextReviveSend;
        private bool _wasHolding;

        public LifeState State => (LifeState)_state.Value;
        public bool IsUp => _state.Value == (byte)LifeState.Up;
        public bool IsDowned => _state.Value == (byte)LifeState.Downed;
        public float ReviveProgress => _reviveProgress.Value;
        /// <summary>Secondi rimasti allo stato attuale (dissanguamento o ritorno in vita), replicati 4 volte al secondo.</summary>
        public float TimeLeft => _timeLeft.Value;
        /// <summary>Proprietario: il compagno a terra che si sta rianimando (o che si può rianimare), per il HUD.</summary>
        public PlayerLife ReviveTarget => _reviveTarget;
        public bool IsHoldingRevive => _wasHolding;
        public Health Health => _health;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_all.Clear();

        private void Awake()
        {
            _health = GetComponent<Health>();
            _motor = GetComponent<PlayerMotor>();
            _input = GetComponent<PlayerInputReader>();
            _weapons = GetComponent<PlayerWeapons>();
            _suit = GetComponent<SuitAbilities>();
            _health.ServerDied += HandleServerDied;
            _state.OnChange += HandleStateChanged;
        }

        private void OnDestroy()
        {
            if (_health != null)
                _health.ServerDied -= HandleServerDied;
            _state.OnChange -= HandleStateChanged;
            s_all.Remove(this);
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!s_all.Contains(this))
                s_all.Add(this);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            s_all.Remove(this);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _state.Value = (byte)LifeState.Up;
            _reviveProgress.Value = 0f;
            _timeLeft.Value = 0f;
            _reviver = null;
        }

        // ---------------------------------------------------------------- Server

        private void HandleServerDied(Health health, DamageInfo info)
        {
            _state.Value = (byte)LifeState.Downed;
            _downedAt = Time.time;
            _soloDeadline = float.MaxValue;
            _stateEnd = ServerDownedDeadline(Time.time);
            _reviveProgress.Value = 0f;
            _reviver = null;
        }

        private bool AnyOtherUp()
        {
            for (int i = 0; i < s_all.Count; i++)
                if (s_all[i] != this && s_all[i] != null && s_all[i].IsUp && s_all[i].IsServerInitialized)
                    return true;
            return false;
        }

        [ServerRpc]
        private void RpcReviveHold(PlayerLife target)
        {
            if (target == null || target == this || !IsUp || !target.IsDowned)
                return;
            if ((target.transform.position - transform.position).sqrMagnitude > (_reviveRange + 0.75f) * (_reviveRange + 0.75f))
                return;
            target.ServerSetReviver(this);
        }

        private void ServerSetReviver(PlayerLife reviver)
        {
            if (_reviver != null && _reviver != reviver && Time.time - _reviverSeenAt < 0.35f)
                return; // già rianimato da un altro
            _reviver = reviver;
            _reviverSeenAt = Time.time;
        }

        private void ServerUpdate(float dt)
        {
            float now = Time.time;
            switch (State)
            {
                case LifeState.Downed:
                    _stateEnd = ServerDownedDeadline(now);

                    bool reviving = _reviver != null && _reviver.IsUp && now - _reviverSeenAt < 0.35f
                                    && (_reviver.transform.position - transform.position).sqrMagnitude <= (_reviveRange + 0.75f) * (_reviveRange + 0.75f);
                    float progress = reviving
                        ? _reviveProgress.Value + dt / _reviveSeconds
                        : Mathf.MoveTowards(_reviveProgress.Value, 0f, dt);
                    if (!Mathf.Approximately(progress, _reviveProgress.Value))
                        _reviveProgress.Value = Mathf.Clamp01(progress);
                    if (progress >= 1f)
                    {
                        PlayerLife helper = _reviver;
                        ServerRevive(_reviveHealthFraction);
                        if (helper != null && helper._suit != null)
                            helper._suit.ServerGrantShield(_reviveShieldSeconds);
                        RpcRevived(helper != null ? helper.OwnerId : -1);
                        return;
                    }
                    if (now >= _stateEnd)
                    {
                        if (!AnyOtherUp())
                        {
                            ServerRevive(0.5f); // da soli (temporaneo)
                            RpcRevived(-1);
                        }
                        else
                        {
                            _state.Value = (byte)LifeState.Dead;
                            _stateEnd = now + _respawnSeconds;
                            _reviveProgress.Value = 0f;
                        }
                    }
                    break;
                case LifeState.Dead:
                    if (now >= _stateEnd)
                    {
                        ServerRevive(1f); // temporaneo: poi si rientrerà a fine wave
                        RpcRevived(-1);
                    }
                    break;
            }

            float left = State == LifeState.Up ? 0f : Mathf.Max(0f, _stateEnd - now);
            if (Mathf.Abs(left - _timeLeft.Value) >= 0.25f || (left == 0f && _timeLeft.Value != 0f))
                _timeLeft.Value = left;
        }

        /// <summary>
        /// Scadenza a terra: con un compagno in piedi si ha tutto il dissanguamento (dal momento della caduta);
        /// se nessuno è in piedi (da soli o tutti a terra) parte il breve conto alla rovescia per rialzarsi da sé,
        /// che si annulla appena un compagno torna in piedi.
        /// </summary>
        private float ServerDownedDeadline(float now)
        {
            float bleedOut = _downedAt + _bleedOutSeconds;
            if (AnyOtherUp())
            {
                _soloDeadline = float.MaxValue;
                return bleedOut;
            }
            if (_soloDeadline == float.MaxValue)
                _soloDeadline = now + _soloReviveSeconds;
            return Mathf.Min(bleedOut, _soloDeadline);
        }

        private void ServerRevive(float healthFraction)
        {
            _health.ServerSetHealth(_health.Max * healthFraction);
            _state.Value = (byte)LifeState.Up;
            _reviveProgress.Value = 0f;
            _reviver = null;
            if (_suit != null)
                _suit.ServerGrantShield(_reviveShieldSeconds);
        }

        [ObserversRpc]
        private void RpcRevived(int helperClientId)
        {
            if (IsOwner)
                SuitCallouts.Raise("VITALS RESTORED", CalloutKind.Positive);
            else if (helperClientId >= 0 && LocalPlayer != null && LocalPlayer.OwnerId == helperClientId)
                SuitCallouts.Raise("OPERATOR REVIVED", CalloutKind.Positive);
        }

        // ---------------------------------------------------------------- Tutti

        private static PlayerLife LocalPlayer
        {
            get
            {
                for (int i = 0; i < s_all.Count; i++)
                    if (s_all[i] != null && s_all[i].IsOwner)
                        return s_all[i];
                return null;
            }
        }

        private void HandleStateChanged(byte previous, byte next, bool asServer)
        {
            if (asServer && IsClientInitialized)
                return; // sull'host si gestisce una volta sola, dal lato client
            if (next == (byte)LifeState.Downed && previous == (byte)LifeState.Up)
            {
                SuitCallouts.Raise(IsOwner ? "VITALS CRITICAL // YOU ARE DOWN" : "OPERATOR DOWN", CalloutKind.Warning);
            }
        }

        private void Update()
        {
            if (IsServerInitialized)
                ServerUpdate(Time.deltaTime);
            if (IsOwner)
                OwnerUpdate();
        }

        // ---------------------------------------------------------------- Proprietario

        private void OwnerUpdate()
        {
            bool up = IsUp;
            if (_motor != null)
                _motor.Downed = !up;
            if (_weapons != null)
            {
                _weapons.ForceSecondary = IsDowned;
                _weapons.WeaponsDisabled = State == LifeState.Dead;
            }

            _reviveTarget = up ? FindReviveTarget() : null;
            bool holding = _reviveTarget != null && _input != null && _input.InteractHeld;
            if (holding && Time.time >= _nextReviveSend)
            {
                _nextReviveSend = Time.time + 0.15f;
                RpcReviveHold(_reviveTarget);
            }
            _wasHolding = holding;
        }

        private PlayerLife FindReviveTarget()
        {
            PlayerLife best = null;
            float bestSqr = _reviveRange * _reviveRange;
            for (int i = 0; i < s_all.Count; i++)
            {
                PlayerLife other = s_all[i];
                if (other == null || other == this || !other.IsDowned)
                    continue;
                float sqr = (other.transform.position - transform.position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    best = other;
                    bestSqr = sqr;
                }
            }
            return best;
        }
    }
}
