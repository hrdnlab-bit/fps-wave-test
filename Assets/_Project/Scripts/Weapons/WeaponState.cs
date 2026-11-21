using HrdnLab.FpsWave.Stats;

namespace HrdnLab.FpsWave.Weapons
{
    /// <summary>
    /// Stato runtime di un'arma (munizioni, ricarica, cadenza, bloom). Logica pura: la stessa classe
    /// gira sul client proprietario (predizione locale) e sul server (stato autorevole), in istanze separate.
    /// </summary>
    public sealed class WeaponState
    {
        private double _nextShotTime;
        private double _reloadEndTime;
        private double _lastShotTime = double.MinValue;

        /// <summary>Pausa senza sparare prima che il bloom inizi a calare: sparando di continuo la precisione peggiora fino al massimo.</summary>
        public const float BloomRecoveryDelay = 0.2f;

        public WeaponDefinition Definition { get; }
        public StatBlock Stats { get; }
        public int Ammo { get; private set; }
        public bool IsReloading { get; private set; }
        public float Bloom { get; private set; }

        public WeaponState(WeaponDefinition definition, StatBlock stats)
        {
            Definition = definition;
            Stats = stats;
            Ammo = MagazineSize;
        }

        public int MagazineSize => (int)Stats.Get(StatType.MagazineSize, 1f);
        /// <summary>Intervallo tra due colpi in secondi (double: si somma a tempi double senza errori di arrotondamento).</summary>
        public double ShotInterval => 60.0 / Stats.Get(StatType.RoundsPerMinute, 600f);
        public float ReloadTime => Stats.Get(StatType.ReloadTime, 1f);

        /// <summary>Avanza la ricarica e il recupero del bloom.</summary>
        public void Tick(double now, float deltaTime, float bloomRecoveryPerSecond)
        {
            if (IsReloading && now >= _reloadEndTime)
            {
                IsReloading = false;
                Ammo = MagazineSize;
            }
            if (Bloom > 0f && now - _lastShotTime >= BloomRecoveryDelay)
            {
                Bloom -= bloomRecoveryPerSecond * deltaTime;
                if (Bloom < 0f) Bloom = 0f;
            }
        }

        public bool CanFire(double now) => !IsReloading && Ammo > 0 && now >= _nextShotTime;

        /// <summary>Registra un colpo sparato: consuma un colpo, programma il prossimo e aggiunge bloom.</summary>
        public void RegisterShot(double now, float maxBloom)
        {
            Ammo--;
            _lastShotTime = now;
            // Se il colpo arriva in ritardo (frame lento), non si "recupera" cadenza oltre un intervallo.
            double baseTime = now - _nextShotTime > ShotInterval ? now : _nextShotTime;
            _nextShotTime = baseTime + ShotInterval;
            Bloom += Stats.Get(StatType.SpreadPerShot);
            if (Bloom > maxBloom) Bloom = maxBloom;
        }

        /// <summary>Consuma un colpo senza logica di cadenza (usato dal server, che ha il proprio limitatore).</summary>
        public bool TryConsumeRound()
        {
            if (IsReloading || Ammo <= 0)
                return false;
            Ammo--;
            return true;
        }

        public bool StartReload(double now)
        {
            if (IsReloading || Ammo >= MagazineSize)
                return false;
            IsReloading = true;
            _reloadEndTime = now + ReloadTime;
            return true;
        }

        public void CancelReload() => IsReloading = false;

        /// <summary>Allinea lo stato al valore autorevole ricevuto dal server.</summary>
        public void ForceState(int ammo, bool reloading, double now)
        {
            Ammo = ammo;
            if (reloading && !IsReloading)
                _reloadEndTime = now + ReloadTime;
            IsReloading = reloading;
        }
    }
}
