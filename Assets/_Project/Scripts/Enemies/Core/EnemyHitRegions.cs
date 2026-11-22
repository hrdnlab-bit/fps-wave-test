using UnityEngine;

namespace HrdnLab.FpsWave.Enemies
{
    /// <summary>Parte del corpo colpita. Da Head in poi l'indice dell'arto è (int)regione - 1 (stesso ordine di EnemyGore).</summary>
    public enum HitRegion : byte
    {
        Body,
        Head,
        LeftArm,
        LeftForeArm,
        RightArm,
        RightForeArm,
        LeftUpLeg,
        LeftLeg,
        RightUpLeg,
        RightLeg,
    }

    /// <summary>
    /// Logica pura (coperta da test) dello smembramento: da punto e direzione del colpo, nello spazio "in piedi" del nemico
    /// (+Z davanti, -X la sua sinistra, Y da terra, altezza 1.8 m), ricava la parte colpita, le soglie di danno per
    /// staccare un arto e il tipo di caduta. Gira solo sul server; i client ricevono il risultato (maschera e tipo).
    /// </summary>
    public static class EnemyHitRegions
    {
        public const int LimbCount = 9;
        /// <summary>Bit della maschera: testa esplosa (insieme al bit 0, testa staccata).</summary>
        public const int HeadExplodedBit = 9;
        public const ushort LegMask = (1 << 5) | (1 << 6) | (1 << 7) | (1 << 8);

        // Tipi di caduta (stati Death_<tipo>_<variante> del controller).
        public const byte DeathBack = 0, DeathForward = 1, DeathHeadFront = 2, DeathHeadBack = 3,
            DeathLeft = 4, DeathRight = 5, DeathLegs = 6;
        public const int DeathTypeCount = 7;

        public static int LimbIndex(HitRegion region) => region == HitRegion.Body ? -1 : (int)region - 1;

        /// <summary>
        /// Parte colpita. <paramref name="local"/> è relativo al centro del busto sul piano (x, z) e al suolo in y,
        /// <paramref name="height"/> l'altezza del nemico (le soglie sono per 1.8 m).
        /// </summary>
        public static HitRegion Classify(Vector3 local, bool weakspot, float height = 1.8f)
        {
            if (weakspot)
                return HitRegion.Head;
            float k = 1.8f / Mathf.Max(0.5f, height);
            float x = local.x * k, y = local.y * k, z = local.z * k;
            bool left = x < 0f;
            if (y < 0.9f)
            {
                bool lower = y < 0.45f;
                return left ? (lower ? HitRegion.LeftLeg : HitRegion.LeftUpLeg) : (lower ? HitRegion.RightLeg : HitRegion.RightUpLeg);
            }
            float ax = Mathf.Abs(x);
            if (y < 1.5f && (ax > 0.2f || (z > 0.22f && ax > 0.08f)))
            {
                bool fore = z > 0.3f || y < 1.15f;
                return left ? (fore ? HitRegion.LeftForeArm : HitRegion.LeftArm) : (fore ? HitRegion.RightForeArm : HitRegion.RightArm);
            }
            return HitRegion.Body;
        }

        /// <summary>Danno accumulato su un arto (frazione della salute massima) oltre il quale si stacca da vivo.</summary>
        public static float SeverThreshold(HitRegion region) => region switch
        {
            HitRegion.LeftArm or HitRegion.RightArm => 0.34f,
            HitRegion.LeftForeArm or HitRegion.RightForeArm => 0.26f,
            HitRegion.LeftUpLeg or HitRegion.RightUpLeg => 0.45f,
            HitRegion.LeftLeg or HitRegion.RightLeg => 0.36f,
            _ => float.PositiveInfinity, // busto e testa non si staccano da vivi
        };

        /// <summary>Arto (o il suo genitore) già staccato: l'arto padre di un avambraccio/stinco è il precedente.</summary>
        public static bool IsLost(ushort mask, int limb)
        {
            if (limb < 0)
                return false;
            if ((mask & (1 << limb)) != 0)
                return true;
            // 2 avambraccio sx (padre 1), 4 avambraccio dx (3), 6 stinco sx (5), 8 stinco dx (7)
            return limb is 2 or 4 or 6 or 8 && (mask & (1 << (limb - 1))) != 0;
        }

        public static bool IsCrippled(ushort mask) => (mask & LegMask) != 0;

        /// <summary>
        /// Tipo di caduta. <paramref name="localDir"/>: direzione del tiro nello spazio del nemico (z &gt; 0 = da dietro).
        /// <paramref name="roll"/> in [0,1) aggiunge varietà: a volte crolla di lato anche colpito di fronte.
        /// </summary>
        public static byte ChooseDeathType(HitRegion region, Vector3 localDir, float roll)
        {
            bool fromBehind = localDir.z > 0.25f;
            if (region == HitRegion.Head)
                return fromBehind ? DeathHeadBack : DeathHeadFront;
            if (region is HitRegion.LeftUpLeg or HitRegion.LeftLeg or HitRegion.RightUpLeg or HitRegion.RightLeg)
                return roll < 0.7f ? DeathLegs : DeathForward;
            float side = localDir.x;
            if (region is HitRegion.LeftArm or HitRegion.LeftForeArm) side -= 0.4f;   // colpito al braccio sx: gira a sinistra
            if (region is HitRegion.RightArm or HitRegion.RightForeArm) side += 0.4f;
            if (Mathf.Abs(side) > 0.65f || roll < 0.2f)
                return side < 0f ? DeathLeft : DeathRight;
            return fromBehind ? DeathForward : DeathBack;
        }
    }
}
