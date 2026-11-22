using System;
using UnityEngine;

namespace HrdnLab.FpsWave.Weapons
{
    /// <summary>Esito di un colpo confermato dal server (un byte in rete).</summary>
    [Flags]
    public enum HitFlags : byte
    {
        None = 0,
        Critical = 1,
        Weakspot = 2,
        Kill = 4,
        /// <summary>Il colpo ha preso una piastra di corazza.</summary>
        ArmorHit = 8,
        /// <summary>Il colpo ha rotto una piastra di corazza.</summary>
        ArmorBreak = 16,
    }

    /// <summary>Conversioni della conferma colpo (logica pura, coperta da test).</summary>
    public static class HitConfirm
    {
        public static HitFlags MakeFlags(bool critical, bool weakspot, bool kill)
        {
            HitFlags flags = HitFlags.None;
            if (critical)
                flags |= HitFlags.Critical;
            if (weakspot)
                flags |= HitFlags.Weakspot;
            if (kill)
                flags |= HitFlags.Kill;
            return flags;
        }

        public static HitFlags MakeFlags(bool critical, bool weakspot, bool kill, bool armorHit, bool armorBreak)
        {
            HitFlags flags = MakeFlags(critical, weakspot, kill);
            if (armorHit)
                flags |= HitFlags.ArmorHit;
            if (armorBreak)
                flags |= HitFlags.ArmorBreak;
            return flags;
        }

        public static bool Has(HitFlags flags, HitFlags flag) => (flags & flag) != 0;

        /// <summary>Danno da mostrare, arrotondato e compresso in 16 bit (si mostra almeno 1 per ogni colpo andato a segno).</summary>
        public static ushort PackDamage(float damage)
        {
            if (damage <= 0f)
                return 0;
            return (ushort)Mathf.Clamp(Mathf.RoundToInt(damage), 1, ushort.MaxValue);
        }
    }
}
