namespace HrdnLab.FpsWave.Stats
{
    /// <summary>
    /// Tutte le statistiche modificabili da upgrade, mod, buff, debuff e mutazioni.
    /// Aggiungere sempre in coda: i valori numerici possono finire in dati serializzati.
    /// </summary>
    public enum StatType : byte
    {
        // Armi
        Damage = 0,
        RoundsPerMinute = 1,
        MagazineSize = 2,
        ReloadTime = 3,
        Range = 4,
        HipSpread = 5,
        AdsSpread = 6,
        SpreadPerShot = 7,
        RecoilPitch = 8,
        RecoilYaw = 9,
        CritChance = 10,
        CritMultiplier = 11,
        WeakspotMultiplier = 12,

        // Player
        MaxHealth = 50,
        MaxShield = 51,
        ShieldRegenDelay = 52,
        MoveSpeed = 53,
        PickupRadius = 54,
    }
}
