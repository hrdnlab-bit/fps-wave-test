using UnityEngine;

namespace HrdnLab.FpsWave.Combat
{
    /// <summary>Descrive un colpo già validato e calcolato dal server.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly bool IsCritical;
        public readonly bool IsWeakspot;
        public readonly int AttackerClientId;
        public readonly Vector3 Point;

        public DamageInfo(float amount, bool isCritical, bool isWeakspot, int attackerClientId, Vector3 point)
        {
            Amount = amount;
            IsCritical = isCritical;
            IsWeakspot = isWeakspot;
            AttackerClientId = attackerClientId;
            Point = point;
        }
    }

    /// <summary>Formule di danno (logica pura, coperta da test).</summary>
    public static class DamageCalculator
    {
        /// <summary>
        /// danno = base * moltiplicatoreZona * (weakspot ? moltWeakspot : 1) * (critico ? moltCritico : 1)
        /// </summary>
        public static float Compute(float baseDamage, float zoneMultiplier, bool isWeakspot, float weakspotMultiplier,
            bool isCritical, float critMultiplier)
        {
            float damage = baseDamage * zoneMultiplier;
            if (isWeakspot)
                damage *= weakspotMultiplier;
            if (isCritical)
                damage *= critMultiplier;
            return damage < 0f ? 0f : damage;
        }

        /// <summary>Tiro del critico: <paramref name="roll01"/> è un numero casuale in [0, 1).</summary>
        public static bool RollCritical(float critChance, float roll01) => roll01 < critChance;
    }
}
