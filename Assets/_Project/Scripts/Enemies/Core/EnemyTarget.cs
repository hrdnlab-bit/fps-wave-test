using System.Collections.Generic;
using FishNet.Object;
using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Player;
using UnityEngine;

namespace HrdnLab.FpsWave.Enemies
{
    /// <summary>
    /// Sul prefab del player: lo registra (solo server) tra i bersagli dei nemici e dello spawn director.
    /// Il registro è statico e senza allocazioni: AI e director iterano <see cref="Active"/> con l'indice.
    /// A terra, rianimazione e ritorno in vita sono in <see cref="HrdnLab.FpsWave.Player.PlayerLife"/>.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class EnemyTarget : NetworkBehaviour
    {
        private static readonly List<EnemyTarget> s_active = new(4);

        /// <summary>Solo server: player in gioco (vivi o morti).</summary>
        public static List<EnemyTarget> Active => s_active;

        [Tooltip("Quanti nemici possono attaccare questo player nello stesso momento: gli altri aspettano appena fuori portata.")]
        [SerializeField, Min(1)] private int _maxAttackers = 3;
        [SerializeField] private PlayerMotor _motor;

        private Health _health;
        private int _attackers;

        public Health Health => _health;
        public bool IsAlive => _health != null && !_health.IsDead;
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public int Attackers => _attackers;

        /// <summary>Server: un nemico chiede uno dei posti di attacco su questo player.</summary>
        public bool TryAcquireAttackSlot()
        {
            if (_attackers >= _maxAttackers)
                return false;
            _attackers++;
            return true;
        }

        /// <summary>Server: il nemico lascia il posto (si allontana, cambia bersaglio, muore o torna nel pool).</summary>
        public void ReleaseAttackSlot()
        {
            if (_attackers > 0)
                _attackers--;
        }

        public Vector3 EyePosition => _motor != null ? _motor.ServerEyePosition : transform.position + Vector3.up * 1.6f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_active.Clear();

        private void Awake()
        {
            _health = GetComponent<Health>();
            if (_motor == null)
                _motor = GetComponent<PlayerMotor>();
        }

        private void OnDestroy()
        {
            s_active.Remove(this);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (!s_active.Contains(this))
                s_active.Add(this);
            _attackers = 0;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            s_active.Remove(this);
        }

#if UNITY_EDITOR
        public void EditorSetup(PlayerMotor motor) => _motor = motor;
#endif
    }
}
