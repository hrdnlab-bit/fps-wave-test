using System;
using UnityEngine;

namespace HrdnLab.FpsWave.Weapons
{
    /// <summary>
    /// Profilo di "sensazione" di un'arma (solo cosmetico/locale, nessun effetto sul bilanciamento in rete).
    /// Vale per qualunque modello: agisce su camera e radice del view model, non sulle animazioni dell'asset.
    /// La FORZA del rinculo resta nelle statistiche (RecoilPitch, RecoilYaw: modificabili dalle mod);
    /// qui c'è la FORMA: schema, crescita nella raffica, velocità di scatto e di ritorno, molle, oscillazioni, calore.
    /// Ogni arma ha il suo profilo in WeaponDefinition; i valori predefiniti sono un punto di partenza sensato.
    /// </summary>
    [Serializable]
    public sealed class WeaponFeel
    {
        [Header("Rinculo della camera (forma; la forza è in RecoilPitch/RecoilYaw)")]
        [Tooltip("Moltiplica la salita verticale data da RecoilPitch: più alto = l'arma sale di più (sensazione di potenza).")]
        [Min(0f)] public float VerticalMultiplier = 1.5f;
        [Tooltip("Moltiplica lo spostamento laterale dato da RecoilYaw.")]
        [Min(0f)] public float HorizontalMultiplier = 0.7f;
        [Tooltip("Il primo colpo di una raffica scalcia di più (x).")]
        [Min(0f)] public float FirstShotMultiplier = 1.2f;
        [Tooltip("Colpi dopo i quali il rinculo verticale raggiunge il massimo della crescita.")]
        [Min(1)] public int RampShots = 8;
        [Tooltip("Rinculo verticale a fine crescita rispetto all'inizio (x).")]
        [Min(0f)] public float RampMultiplier = 1.25f;
        [Tooltip("Quanto lo schema orizzontale fisso pesa (0 = solo casuale).")]
        [Range(0f, 1f)] public float PatternStrength = 0.75f;
        [Tooltip("Parte casuale del rinculo orizzontale.")]
        [Range(0f, 1f)] public float HorizontalRandomness = 0.3f;
        [Tooltip("Velocità con cui la camera raggiunge il rinculo (scatto).")]
        [Min(1f)] public float Snappiness = 30f;
        [Tooltip("Velocità di ritorno della parte recuperabile.")]
        [Min(0.1f)] public float ReturnSpeed = 9f;
        [Tooltip("Parte del rinculo che resta (da compensare tirando giù il mouse, come in Rainbow Six Siege). 0 = torna tutto da solo.")]
        [Range(0f, 1f)] public float PermanentFraction = 0.85f;
        [Tooltip("Pausa senza sparare dopo la quale la raffica ricomincia da capo.")]
        [Min(0.05f)] public float BurstResetTime = 0.35f;
        [Range(0f, 1.5f)] public float AdsMultiplier = 0.65f;
        [Range(0f, 1.5f)] public float CrouchMultiplier = 0.8f;

        [Header("Arma in mano: calcio a molla")]
        [Tooltip("Scala di tutto il calcio del view model (0 se l'asset anima già il suo rinculo).")]
        [Min(0f)] public float ViewKickScale = 1f;
        [Tooltip("Spostamento per colpo (m): x laterale, y alto, z indietro (negativo).")]
        public Vector3 KickPosition = new(0.001f, 0.002f, -0.022f);
        [Tooltip("Rotazione per colpo (gradi): x alza la volata (negativo), y laterale, z rollio.")]
        public Vector3 KickRotation = new(-1.2f, 0.4f, 0.8f);
        [Range(0f, 1f)] public float KickRandomness = 0.35f;
        [Min(1f)] public float PositionStiffness = 220f;
        [Min(0f)] public float PositionDamping = 28f;
        [Min(1f)] public float RotationStiffness = 200f;
        [Min(0f)] public float RotationDamping = 26f;

        [Header("Arma in mano: movimento")]
        [Tooltip("Oscillazione dell'arma che segue in ritardo il mouse (gradi per grado di rotazione).")]
        [Min(0f)] public float SwayAmount = 1.2f;
        [Min(0f)] public float SwayMax = 5f;
        [Min(0.1f)] public float SwaySmoothing = 9f;
        [Tooltip("Ondeggio camminando/correndo (x).")]
        [Min(0f)] public float BobAmount = 1f;
        [Tooltip("Inclinazione laterale spostandosi di lato (gradi a piena velocità).")]
        [Min(0f)] public float StrafeTilt = 3f;
        [Tooltip("Quanto l'arma si abbassa e ruota correndo (x).")]
        [Range(0f, 1.5f)] public float SprintPose = 1f;
        [Tooltip("Respiro da fermi (x).")]
        [Min(0f)] public float IdleBreath = 1f;

        [Header("Camera")]
        [Tooltip("Tremore aggiunto a ogni colpo (0-1, cresce con il fuoco prolungato).")]
        [Range(0f, 1f)] public float ShakePerShot = 0.03f;
        [Tooltip("Allargamento istantaneo del FOV a ogni colpo (frazione).")]
        [Range(0f, 0.1f)] public float FovPunch = 0.004f;

        [Header("Sci-fi: energia e calore (cosmetici)")]
        [Tooltip("Arma a energia: accumula calore (barra nel HUD, luce che vira al caldo, tremolio). Le armi a proiettili non ne hanno.")]
        public bool UsesHeat;
        [Tooltip("Colore della scarica: luce di volata e tracciante.")]
        public Color EnergyColor = new(0.35f, 0.85f, 1f);
        [Tooltip("Colore della luce a calore massimo.")]
        public Color HotColor = new(1f, 0.55f, 0.25f);
        [Range(0f, 1f)] public float HeatPerShot = 0.05f;
        [Tooltip("Raffreddamento al secondo dopo la pausa.")]
        [Min(0f)] public float HeatDecay = 0.55f;
        [Min(0f)] public float HeatDecayDelay = 0.25f;
        [Min(0f)] public float MuzzleLightIntensity = 5f;

        /// <summary>
        /// Profilo di base per un'arma automatica (fucili, mitragliette), alla Rainbow Six Siege: ogni colpo alza la mira e
        /// lì restano anche i proiettili (rinculo quasi tutto permanente, da compensare col mouse), deriva laterale riconoscibile,
        /// arma in mano che si muove poco così la mira resta sempre visibile.
        /// </summary>
        public static WeaponFeel AutomaticPreset() => new()
        {
            VerticalMultiplier = 1.5f, HorizontalMultiplier = 0.7f,
            FirstShotMultiplier = 1.2f, RampShots = 8, RampMultiplier = 1.25f, PatternStrength = 0.75f, HorizontalRandomness = 0.3f,
            Snappiness = 30f, ReturnSpeed = 9f, PermanentFraction = 0.85f, BurstResetTime = 0.3f,
            ViewKickScale = 0.6f, KickPosition = new Vector3(0.001f, 0.002f, -0.022f), KickRotation = new Vector3(-1.2f, 0.4f, 0.8f),
            ShakePerShot = 0.025f, FovPunch = 0.004f, HeatPerShot = 0.035f, HeatDecay = 0.45f,
            EnergyColor = new Color(0.35f, 0.85f, 1f),
        };

        /// <summary>Profilo di base per un'arma semi-automatica (pistole, fucili di precisione): salto secco della mira, arma stabile.</summary>
        public static WeaponFeel SemiAutoPreset() => new()
        {
            VerticalMultiplier = 1.6f, HorizontalMultiplier = 0.6f,
            FirstShotMultiplier = 1f, RampShots = 4, RampMultiplier = 1.1f, PatternStrength = 0.4f, HorizontalRandomness = 0.5f,
            Snappiness = 34f, ReturnSpeed = 10f, PermanentFraction = 0.8f, BurstResetTime = 0.45f,
            ViewKickScale = 0.8f, KickPosition = new Vector3(0.002f, 0.004f, -0.03f), KickRotation = new Vector3(-2.5f, 0.6f, 1.2f),
            ShakePerShot = 0.04f, FovPunch = 0.006f, HeatPerShot = 0.09f, HeatDecay = 0.7f,
            EnergyColor = new Color(0.55f, 0.75f, 1f),
        };
    }
}
