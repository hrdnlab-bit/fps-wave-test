using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace HrdnLab.FpsWave.Combat
{
    /// <summary>
    /// Salute autorevole sul server, replicata ai client con una SyncVar.
    /// Le zone colpibili (<see cref="HitZone"/>) sono indicizzate per poterle riferire nei messaggi di rete.
    /// </summary>
    public sealed class Health : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float _maxHealth = 100f;
        [SerializeField] private HitZone[] _hitZones = Array.Empty<HitZone>();

        private readonly SyncVar<float> _current = new(100f);

        /// <summary>Solo server: dopo l'applicazione di un danno.</summary>
        public event Action<Health, DamageInfo> ServerDamaged;
        /// <summary>Solo server: quando la salute arriva a zero.</summary>
        public event Action<Health, DamageInfo> ServerDied;
        /// <summary>Client: quando la salute replicata cambia (precedente, nuova).</summary>
        public event Action<float, float> ClientHealthChanged;

        public float Current => _current.Value;
        public float Max => _maxHealth;
        public float Normalized => _maxHealth > 0f ? _current.Value / _maxHealth : 0f;
        public bool IsDead => _current.Value <= 0f;
        public int HitZoneCount => _hitZones.Length;

        private void Awake()
        {
            for (int i = 0; i < _hitZones.Length; i++)
            {
                if (_hitZones[i] != null)
                    _hitZones[i].Bind(this, i);
            }
            _current.OnChange += HandleChange;
        }

        private void OnDestroy() => _current.OnChange -= HandleChange;

        public override void OnStartServer()
        {
            base.OnStartServer();
            _current.Value = _maxHealth;
        }

        private void HandleChange(float previous, float next, bool asServer)
        {
            if (!asServer || !IsClientInitialized)
                ClientHealthChanged?.Invoke(previous, next);
        }

        public HitZone GetZone(int index) =>
            index >= 0 && index < _hitZones.Length ? _hitZones[index] : null;

        /// <summary>Applica il danno (solo server). Restituisce true se il colpo ha ucciso.</summary>
        [Server]
        public bool ApplyDamage(in DamageInfo info)
        {
            if (IsDead || info.Amount <= 0f)
                return false;
            float next = Mathf.Max(0f, _current.Value - info.Amount);
            _current.Value = next;
            ServerDamaged?.Invoke(this, info);
            if (next > 0f)
                return false;
            ServerDied?.Invoke(this, info);
            return true;
        }

        [Server]
        public void ResetHealth() => _current.Value = _maxHealth;

#if UNITY_EDITOR
        public void EditorSetup(float maxHealth, HitZone[] zones)
        {
            _maxHealth = maxHealth;
            _hitZones = zones;
        }
#endif
    }
}
