using UnityEngine;

namespace HrdnLab.FpsWave.Weapons
{
    /// <summary>Esito della validazione server di un colpo.</summary>
    public enum ShotRejectReason : byte
    {
        None = 0,
        InvalidWeapon,
        NotEquipped,
        Reloading,
        NoAmmo,
        FireRate,
        OriginTooFar,
        OutOfRange,
        TargetMismatch,
        LineOfSightBlocked,
    }

    /// <summary>
    /// Controlli geometrici della validazione server (logica pura, coperta da test).
    /// I limiti sono volutamente tolleranti: è un co-op PvE, l'obiettivo è fermare valori
    /// palesemente falsi, non punire la latenza.
    /// </summary>
    public static class ShotValidation
    {
        /// <summary>Distanza massima tra l'origine dichiarata dal client e gli occhi del player visti dal server.</summary>
        public const float OriginTolerance = 2.5f;

        /// <summary>Tolleranza di base tra il punto d'impatto dichiarato e il collider del bersaglio sul server.</summary>
        public const float TargetBaseTolerance = 1.0f;

        /// <summary>
        /// Ritardo stimato tra ciò che vede il client e la posizione sul server per un bersaglio che si muove
        /// (interpolazione di ~2 tick + latenza tipica). Moltiplicato per la velocità del bersaglio allarga la tolleranza.
        /// </summary>
        public const float MovingTargetLagSeconds = 0.35f;

        public static bool IsOriginPlausible(Vector3 claimedOrigin, Vector3 serverEyePosition) =>
            (claimedOrigin - serverEyePosition).sqrMagnitude <= OriginTolerance * OriginTolerance;

        public static bool IsWithinRange(Vector3 origin, Vector3 hitPoint, float range) =>
            (hitPoint - origin).sqrMagnitude <= range * range * 1.01f;

        /// <summary>
        /// Il punto d'impatto deve essere vicino al collider del bersaglio come lo vede il server,
        /// con una tolleranza che cresce con la velocità del bersaglio e la latenza.
        /// </summary>
        public static bool IsTargetPlausible(Vector3 claimedHitPoint, Vector3 closestPointOnServerCollider,
            float targetSpeed, float roundTripSeconds)
        {
            float tolerance = TargetBaseTolerance + Mathf.Max(0f, targetSpeed) * Mathf.Clamp(roundTripSeconds, 0f, 0.5f);
            return (claimedHitPoint - closestPointOnServerCollider).sqrMagnitude <= tolerance * tolerance;
        }

        /// <summary>Direzione casuale in un cono di semi-apertura <paramref name="spreadDegrees"/> (u, v in [0,1)).</summary>
        public static Vector3 SpreadDirection(Vector3 forward, Vector3 up, float spreadDegrees, float u, float v)
        {
            if (spreadDegrees <= 0f)
                return forward.normalized;
            // Distribuzione uniforme nel disco: radice quadrata sul raggio.
            float angle = spreadDegrees * Mathf.Sqrt(u);
            float roll = v * 360f;
            Quaternion deviation = Quaternion.AngleAxis(roll, forward) * Quaternion.AngleAxis(angle, up);
            return (deviation * forward).normalized;
        }
    }
}
