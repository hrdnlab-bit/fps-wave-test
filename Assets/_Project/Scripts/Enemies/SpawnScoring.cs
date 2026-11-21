using UnityEngine;

namespace HrdnLab.FpsWave.Enemies
{
    /// <summary>
    /// Regole della scelta dei punti di spawn (logica pura, coperta da test). Vedi SPAWN_DIRECTOR.md §8:
    /// anello 25-60 m attorno al bersaglio, mai sotto 15 m da nessun player, distanza ideale ~40 m,
    /// preferenza per spalle e fianchi, varietà rispetto agli ultimi spawn, scelta pesata tra i migliori 3.
    /// </summary>
    public static class SpawnScoring
    {
        public const float DefaultRingMin = 25f;
        public const float DefaultRingMax = 60f;
        public const float DefaultMinDistance = 15f;
        public const float DefaultIdealDistance = 40f;
        public const int TopCount = 3;

        /// <summary>Peso del bonus "arrivo dalle spalle" rispetto al punteggio di distanza (0-1).</summary>
        public const float FlankWeight = 0.6f;

        /// <summary>Il nodo è nell'anello del bersaglio e abbastanza lontano da tutti i player.</summary>
        public static bool IsInRing(float distanceToTarget, float nearestPlayerDistance, float ringMin, float ringMax, float minDistance) =>
            distanceToTarget >= ringMin && distanceToTarget <= ringMax && nearestPlayerDistance >= minDistance;

        /// <summary>
        /// Punteggio di un nodo valido.
        /// <paramref name="facingDot"/> = dot(sguardo del bersaglio, direzione bersaglio -> nodo): 1 davanti, -1 alle spalle.
        /// <paramref name="recentUsePenalty"/> penalizza i nodi usati da poco (0 = mai usato).
        /// </summary>
        public static float Score(float distanceToTarget, float facingDot, float recentUsePenalty, float idealDistance)
        {
            float ideal = Mathf.Max(1f, idealDistance);
            float distanceScore = 1f - Mathf.Clamp01(Mathf.Abs(distanceToTarget - ideal) / ideal);
            float flankScore = (1f - Mathf.Clamp(facingDot, -1f, 1f)) * 0.5f; // 1 alle spalle, 0.5 di lato, 0 davanti
            return distanceScore + FlankWeight * flankScore - Mathf.Max(0f, recentUsePenalty);
        }

        /// <summary>Penalità di varietà: massima subito dopo l'uso, zero dopo <paramref name="window"/> secondi.</summary>
        public static float RecentUsePenalty(float secondsSinceUse, float window = 10f, float maxPenalty = 0.5f) =>
            window <= 0f ? 0f : Mathf.Clamp01(1f - secondsSinceUse / window) * maxPenalty;

        /// <summary>
        /// Inserisce un candidato nella classifica dei migliori (array ordinati per punteggio decrescente, senza allocazioni).
        /// </summary>
        public static void InsertTop(int[] ids, float[] scores, ref int count, int id, float score)
        {
            int capacity = ids.Length;
            int pos = count;
            while (pos > 0 && scores[pos - 1] < score)
                pos--;
            if (pos >= capacity)
                return;
            int last = Mathf.Min(count, capacity - 1);
            for (int i = last; i > pos; i--)
            {
                ids[i] = ids[i - 1];
                scores[i] = scores[i - 1];
            }
            ids[pos] = id;
            scores[pos] = score;
            if (count < capacity)
                count++;
        }

        /// <summary>Rimuove l'elemento <paramref name="index"/> dalla classifica mantenendo l'ordine.</summary>
        public static void RemoveTopAt(int[] ids, float[] scores, ref int count, int index)
        {
            for (int i = index; i < count - 1; i++)
            {
                ids[i] = ids[i + 1];
                scores[i] = scores[i + 1];
            }
            count--;
        }

        /// <summary>
        /// Sceglie un indice in [0, count) in modo casuale pesato sul punteggio (<paramref name="roll01"/> in [0, 1)).
        /// I punteggi negativi valgono come un peso minimo, così anche un nodo penalizzato resta possibile.
        /// </summary>
        public static int PickWeighted(float[] scores, int count, float roll01)
        {
            if (count <= 0)
                return -1;
            const float minWeight = 0.05f;
            float total = 0f;
            for (int i = 0; i < count; i++)
                total += Mathf.Max(minWeight, scores[i]);
            float r = Mathf.Clamp01(roll01) * total;
            for (int i = 0; i < count; i++)
            {
                r -= Mathf.Max(minWeight, scores[i]);
                if (r < 0f)
                    return i;
            }
            return count - 1;
        }

        /// <summary>Quanti nemici vivi tenere in campo (test M2, prima che M3 introduca le wave).</summary>
        public static int TargetAlive(int alivePlayers, int baseAlive, int perPlayer, int hardCap)
        {
            if (alivePlayers <= 0)
                return 0;
            return Mathf.Clamp(baseAlive + perPlayer * alivePlayers, 0, Mathf.Max(0, hardCap));
        }
    }
}
