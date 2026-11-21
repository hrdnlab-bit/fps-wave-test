using UnityEngine;

namespace HrdnLab.FpsWave.Core
{
    /// <summary>
    /// Layer fisici del gioco. Gli indici sono creati dallo strumento Editor
    /// "FpsWave/Setup/3. Build M1 Content" e devono restare allineati con il TagManager.
    /// </summary>
    public static class GameLayers
    {
        public const int Environment = 6;
        public const int Player = 7;
        public const int Hittable = 8;

        public const string EnvironmentName = "Environment";
        public const string PlayerName = "Player";
        public const string HittableName = "Hittable";

        /// <summary>Cosa può colpire un proiettile hitscan (niente fuoco amico: i player sono esclusi).</summary>
        public static readonly int ShotMask = (1 << Environment) | (1 << Hittable) | (1 << 0);

        /// <summary>Geometria che blocca la linea di vista nella validazione server.</summary>
        public static readonly int LineOfSightMask = (1 << Environment) | (1 << 0);
    }
}
