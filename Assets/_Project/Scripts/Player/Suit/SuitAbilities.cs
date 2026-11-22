using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Enemies;
using HrdnLab.FpsWave.UI;
using UnityEngine;

namespace HrdnLab.FpsWave.Player
{
    public enum SuitAbility : byte
    {
        Overcharge = 0,
        ArmorMode = 1,
        PulseScan = 2,
    }

    /// <summary>
    /// Tuta Mk-VII: energia autorevole sul server (SyncVar) che si ricarica dopo una breve pausa, e tre abilità brevi:
    /// - Overcharge (V): scatto, sprint più veloce per qualche secondo;
    /// - Armor mode (F): danni subiti ridotti, movimento un po' più lento;
    /// - Pulse scan (Q): rivela a tutta la squadra i nemici nel raggio, anche dietro i muri.
    /// Il client chiede, il server valida (vivo, energia, ricarica) e avvisa tutti con un RPC (scansione, messaggi).
    /// Gestisce anche lo scudo temporaneo dato dalla rianimazione (<see cref="ServerGrantShield"/>).
    /// I valori si ritoccano qui; più avanti le mod potranno modificarli tramite StatBlock.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class SuitAbilities : NetworkBehaviour
    {
        private const byte FlagOvercharge = 1;
        private const byte FlagArmor = 2;
        private const byte FlagShield = 4;

        [Header("Energia")]
        [SerializeField, Min(1f)] private float _maxEnergy = 100f;
        [SerializeField, Min(0f)] private float _regenPerSecond = 14f;
        [SerializeField, Min(0f)] private float _regenDelay = 1.5f;

        [Header("Overcharge (V)")]
        [SerializeField, Min(0f)] private float _overchargeCost = 30f;
        [SerializeField, Min(0.1f)] private float _overchargeDuration = 3.5f;
        [SerializeField, Min(1f)] private float _overchargeSpeed = 1.35f;

        [Header("Armor mode (F)")]
        [SerializeField, Min(0f)] private float _armorCost = 40f;
        [SerializeField, Min(0.1f)] private float _armorDuration = 4f;
        [SerializeField, Range(0f, 1f)] private float _armorDamageScale = 0.4f;
        [SerializeField, Range(0.1f, 1f)] private float _armorMoveScale = 0.85f;

        [Header("Pulse scan (Q)")]
        [SerializeField, Min(0f)] private float _scanCost = 25f;
        [SerializeField, Min(0f)] private float _scanCooldown = 6f;
        [SerializeField, Min(1f)] private float _scanRadius = 40f;
        [SerializeField, Min(0.1f)] private float _scanDuration = 5f;

        [Header("Scudo della rianimazione")]
        [SerializeField, Range(0f, 1f)] private float _shieldDamageScale = 0.5f;

        private readonly SyncVar<float> _energy = new(100f);
        private readonly SyncVar<byte> _flags = new();

        private Health _health;
        private PlayerMotor _motor;
        private PlayerInputReader _input;
        private PlayerLife _life;

        // Server
        private float _serverEnergy;
        private float _lastSpend = -10f;
        private float _overchargeEnd;
        private float _armorEnd;
        private float _shieldEnd;
        private float _scanReadyAt;

        // Client proprietario
        private float _localScanReadyAt;

        /// <summary>Tutti i client: impulso di scansione (origine, raggio, durata). Il HUD mostra i nemici rivelati.</summary>
        public static event Action<Vector3, float, float> ClientScanPulse;

        public float Energy => _energy.Value;
        public float MaxEnergy => _maxEnergy;
        public float Energy01 => _maxEnergy > 0f ? Mathf.Clamp01(_energy.Value / _maxEnergy) : 0f;
        public bool OverchargeActive => (_flags.Value & FlagOvercharge) != 0;
        public bool ArmorActive => (_flags.Value & FlagArmor) != 0;
        public bool Shielded => (_flags.Value & FlagShield) != 0;
        public float ScanCooldownRemaining => Mathf.Max(0f, _localScanReadyAt - Time.time);

        public float Cost(SuitAbility ability) => ability switch
        {
            SuitAbility.Overcharge => _overchargeCost,
            SuitAbility.ArmorMode => _armorCost,
            _ => _scanCost,
        };

        public bool IsActive(SuitAbility ability) => ability switch
        {
            SuitAbility.Overcharge => OverchargeActive,
            SuitAbility.ArmorMode => ArmorActive,
            _ => false,
        };

        private void Awake()
        {
            _health = GetComponent<Health>();
            _motor = GetComponent<PlayerMotor>();
            _input = GetComponent<PlayerInputReader>();
            _life = GetComponent<PlayerLife>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _serverEnergy = _maxEnergy;
            _energy.Value = _maxEnergy;
            _flags.Value = 0;
            _overchargeEnd = _armorEnd = _shieldEnd = _scanReadyAt = 0f;
        }

        private void Update()
        {
            if (IsOwner)
                OwnerUpdate();
            if (IsServerInitialized)
                ServerUpdate(Time.deltaTime);
        }

        // ---------------------------------------------------------------- Proprietario

        private void OwnerUpdate()
        {
            if (_motor != null)
            {
                float speed = 1f;
                if (OverchargeActive) speed *= _overchargeSpeed;
                if (ArmorActive) speed *= _armorMoveScale;
                _motor.AbilitySpeedMultiplier = speed;
            }
            if (_input == null || (_life != null && !_life.IsUp))
                return;
            if (_input.AbilityOverchargePressed) TryActivate(SuitAbility.Overcharge);
            else if (_input.AbilityArmorPressed) TryActivate(SuitAbility.ArmorMode);
            else if (_input.AbilityScanPressed) TryActivate(SuitAbility.PulseScan);
        }

        private void TryActivate(SuitAbility ability)
        {
            if (ability == SuitAbility.PulseScan && ScanCooldownRemaining > 0f)
            {
                Deny("SCAN RECHARGING");
                return;
            }
            if (IsActive(ability))
                return;
            if (_energy.Value + 0.5f < Cost(ability))
            {
                Deny("INSUFFICIENT SUIT ENERGY");
                return;
            }
            RpcRequest((byte)ability);
        }

        private static void Deny(string message)
        {
            SuitCallouts.Raise(message, CalloutKind.Warning);
        }

        // ---------------------------------------------------------------- Server

        [ServerRpc]
        private void RpcRequest(byte abilityByte)
        {
            if (abilityByte > (byte)SuitAbility.PulseScan || (_life != null && !_life.IsUp))
                return;
            var ability = (SuitAbility)abilityByte;
            float now = Time.time;
            float cost = Cost(ability);
            if (_serverEnergy + 0.01f < cost)
                return;
            switch (ability)
            {
                case SuitAbility.Overcharge:
                    if (now < _overchargeEnd) return;
                    _overchargeEnd = now + _overchargeDuration;
                    break;
                case SuitAbility.ArmorMode:
                    if (now < _armorEnd) return;
                    _armorEnd = now + _armorDuration;
                    break;
                case SuitAbility.PulseScan:
                    if (now < _scanReadyAt) return;
                    _scanReadyAt = now + _scanCooldown;
                    break;
            }
            _serverEnergy -= cost;
            _lastSpend = now;
            _energy.Value = _serverEnergy;
            ServerRefreshFlags(now);
            RpcActivated(abilityByte, transform.position);
        }

        /// <summary>Scudo temporaneo (rianimazione): danni ridotti per <paramref name="seconds"/> secondi.</summary>
        [Server]
        public void ServerGrantShield(float seconds)
        {
            _shieldEnd = Mathf.Max(_shieldEnd, Time.time + seconds);
            ServerRefreshFlags(Time.time);
        }

        private void ServerUpdate(float dt)
        {
            float now = Time.time;
            ServerRefreshFlags(now);
            if (_serverEnergy < _maxEnergy && now - _lastSpend >= _regenDelay)
            {
                _serverEnergy = Mathf.Min(_maxEnergy, _serverEnergy + _regenPerSecond * dt);
                // Si replica a passi di 1 punto (o al pieno): poca banda per un valore che cambia ogni frame.
                if (_serverEnergy >= _maxEnergy || Mathf.Abs(_serverEnergy - _energy.Value) >= 1f)
                    _energy.Value = _serverEnergy;
            }
        }

        private void ServerRefreshFlags(float now)
        {
            byte flags = 0;
            if (now < _overchargeEnd) flags |= FlagOvercharge;
            if (now < _armorEnd) flags |= FlagArmor;
            if (now < _shieldEnd) flags |= FlagShield;
            if (_flags.Value != flags)
                _flags.Value = flags;
            float scale = 1f;
            if ((flags & FlagArmor) != 0) scale *= _armorDamageScale;
            if ((flags & FlagShield) != 0) scale *= _shieldDamageScale;
            _health.ServerDamageScale = scale;
        }

        // ---------------------------------------------------------------- Tutti i client

        [ObserversRpc]
        private void RpcActivated(byte abilityByte, Vector3 position)
        {
            var ability = (SuitAbility)abilityByte;
            if (ability != SuitAbility.PulseScan)
            {
                if (IsOwner)
                    SuitCallouts.Raise(ability == SuitAbility.Overcharge ? "OVERCHARGE ENGAGED" : "ARMOR MODE ENGAGED", CalloutKind.Positive);
                return;
            }
            ClientScanPulse?.Invoke(position, _scanRadius, _scanDuration);
            if (!IsOwner)
                return;
            _localScanReadyAt = Time.time + _scanCooldown;
            int found = 0;
            float radiusSqr = _scanRadius * _scanRadius;
            foreach (EnemyAgent enemy in EnemyAgent.ClientActive)
                if (enemy != null && !enemy.ClientIsDead && (enemy.transform.position - position).sqrMagnitude <= radiusSqr)
                    found++;
            SuitCallouts.Raise(found == 0 ? "SCAN COMPLETE // NO HOSTILES" : $"SCAN COMPLETE // {found} HOSTILES",
                CalloutKind.Info);
        }
    }
}
