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
        [Min(0f)] public float HorizontalMultiplier = 1f;
        [Tooltip("Primo colpo di una raffica (x): sotto 1 i colpi singoli (tap) saltano meno della raffica.")]
        [Min(0f)] public float FirstShotMultiplier = 0.75f;
        [Tooltip("Colpi dopo i quali il rinculo verticale raggiunge il massimo della crescita.")]
        [Min(1)] public int RampShots = 6;
        [Tooltip("Rinculo verticale a fine crescita rispetto all'inizio (x).")]
        [Min(0f)] public float RampMultiplier = 1.3f;
        [Tooltip("Quanto lo schema orizzontale fisso pesa (0 = solo casuale).")]
        [Range(0f, 1f)] public float PatternStrength = 0.5f;
        [Tooltip("Parte casuale del rinculo orizzontale.")]
        [Range(0f, 1f)] public float HorizontalRandomness = 0.6f;
        [Tooltip("Variazione casuale della salita per colpo (0.15 = ±15%).")]
        [Range(0f, 0.5f)] public float VerticalRandomness = 0.15f;
        [Tooltip("Velocità con cui la camera raggiunge il rinculo (scatto).")]
        [Min(1f)] public float Snappiness = 24f;
        [Tooltip("Velocità di ritorno della parte recuperabile.")]
        [Min(0.1f)] public float ReturnSpeed = 9f;
        [Tooltip("Parte del rinculo che resta (da compensare tirando giù il mouse, come in Rainbow Six Siege). 0 = torna tutto da solo.")]
        [Range(0f, 1f)] public float PermanentFraction = 0.85f;
        [Tooltip("Pausa senza sparare dopo la quale la raffica ricomincia da capo.")]
        [Min(0.05f)] public float BurstResetTime = 0.35f;
        [Tooltip("Rinculo della camera sparando da fianco (x). Basso: da fianco la mira quasi non sale, ma la precisione peggiora col bloom.")]
        [Range(0f, 1.5f)] public float HipRecoilMultiplier = 0.15f;
        [Tooltip("Parte del calcio da fianco che resta nella mira: un filo di salita, giusto per sentire l'arma.")]
        [Range(0f, 1f)] public float HipPermanentFraction = 0.35f;
        [Tooltip("Rinculo della camera mirando (x): qui si gestisce la salita col mouse, come in Siege.")]
        [Range(0f, 1.5f)] public float AdsMultiplier = 1f;
        [Range(0f, 1.5f)] public float CrouchMultiplier = 0.8f;

        [Header("Arma in mano: calcio a molla")]
        [Tooltip("Scala di tutto il calcio del view model (0 se l'asset anima già il suo rinculo).")]
        [Min(0f)] public float ViewKickScale = 0.25f;
        [Tooltip("Spostamento per colpo (m): x laterale, y alto, z indietro (negativo).")]
        public Vector3 KickPosition = new(0.0006f, 0.0012f, -0.012f);
        [Tooltip("Rotazione per colpo (gradi): x alza la volata (negativo), y laterale, z rollio.")]
        public Vector3 KickRotation = new(-0.6f, 0.25f, 0.4f);
        [Range(0f, 1f)] public float KickRandomness = 0.35f;
        [Min(1f)] public float PositionStiffness = 220f;
        [Min(0f)] public float PositionDamping = 28f;
        [Min(1f)] public float RotationStiffness = 200f;
        [Min(0f)] public float RotationDamping = 26f;

        [Header("Arma in mano: movimento")]
        [Tooltip("Oscillazione dell'arma che segue in ritardo il mouse (gradi per grado di rotazione).")]
        [Min(0f)] public float SwayAmount = 1f;
        [Min(0f)] public float SwayMax = 5f;
        [Min(0.1f)] public float SwaySmoothing = 6.5f;
        [Tooltip("Ondeggio camminando/correndo (x).")]
        [Min(0f)] public float BobAmount = 1f;
        [Tooltip("Inclinazione laterale spostandosi di lato (gradi a piena velocità).")]
        [Min(0f)] public float StrafeTilt = 2.5f;
        [Tooltip("Quanto l'arma si abbassa e ruota correndo (x).")]
        [Range(0f, 1.5f)] public float SprintPose = 1f;
        [Tooltip("Respiro da fermi (x).")]
        [Min(0f)] public float IdleBreath = 1f;

        [Header("Camera")]
        [Tooltip("Tremore aggiunto a ogni colpo (0-1, cresce con il fuoco prolungato).")]
        [Range(0f, 1f)] public float ShakePerShot = 0.005f;
        [Tooltip("Allargamento istantaneo del FOV a ogni colpo (frazione).")]
        [Range(0f, 0.1f)] public float FovPunch = 0.001f;

        [Header("Sci-fi: energia e calore (cosmetici)")]
        [Tooltip("Arma a energia: accumula calore (barra nel HUD, luce che vira al caldo, tremolio). Le armi a proiettili non ne hanno.")]
        public bool UsesHeat;
        [Tooltip("Colore di tracciante e lampo per le armi a proiettili (le armi a energia usano EnergyColor).")]
        public Color BallisticColor = new(1f, 0.8f, 0.55f);

        /// <summary>Colore di tracciante e luce di volata: energia per le armi a energia, caldo e realistico per quelle a proiettili.</summary>
        public Color MuzzleColor => UsesHeat ? EnergyColor : BallisticColor;
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
        /// Profilo di base per un'arma automatica (fucili, mitragliette), alla Call of Duty moderno: primo colpo morbido, salita
        /// che cresce nella raffica con uno schema riconoscibile per arma, i proiettili vanno dove punta la mira, e lasciando
        /// il grilletto la mira torna in parte da sola (recoil reset); arma in mano che si muove poco.
        /// </summary>
        public static WeaponFeel AutomaticPreset() => new()
        {
            VerticalMultiplier = 1.4f, HorizontalMultiplier = 0.9f, VerticalRandomness = 0.1f,
            FirstShotMultiplier = 0.8f, RampShots = 8, RampMultiplier = 1.35f, PatternStrength = 0.6f, HorizontalRandomness = 0.4f,
            HipRecoilMultiplier = 0.15f, HipPermanentFraction = 0.35f, AdsMultiplier = 1f,
            Snappiness = 18f, ReturnSpeed = 5f, PermanentFraction = 0.55f, BurstResetTime = 0.3f,
            ViewKickScale = 0.25f, KickPosition = new Vector3(0.0006f, 0.0012f, -0.012f), KickRotation = new Vector3(-0.6f, 0.25f, 0.4f),
            ShakePerShot = 0.005f, FovPunch = 0.001f, HeatPerShot = 0.035f, HeatDecay = 0.45f,
            EnergyColor = new Color(0.35f, 0.85f, 1f),
        };

        /// <summary>Profilo di base per un'arma semi-automatica (pistole, fucili di precisione): salto secco della mira, arma stabile.</summary>
        public static WeaponFeel SemiAutoPreset() => new()
        {
            VerticalMultiplier = 1.1f, HorizontalMultiplier = 0.8f, VerticalRandomness = 0.15f,
            FirstShotMultiplier = 1f, RampShots = 4, RampMultiplier = 1.15f, PatternStrength = 0.3f, HorizontalRandomness = 0.6f,
            HipRecoilMultiplier = 0.2f, HipPermanentFraction = 0.3f, AdsMultiplier = 1f,
            Snappiness = 22f, ReturnSpeed = 7f, PermanentFraction = 0.4f, BurstResetTime = 0.45f,
            ViewKickScale = 0.35f, KickPosition = new Vector3(0.001f, 0.002f, -0.018f), KickRotation = new Vector3(-1.4f, 0.4f, 0.7f),
            ShakePerShot = 0.008f, FovPunch = 0.0015f, HeatPerShot = 0.09f, HeatDecay = 0.7f,
            EnergyColor = new Color(0.55f, 0.75f, 1f),
        };
    }
}
