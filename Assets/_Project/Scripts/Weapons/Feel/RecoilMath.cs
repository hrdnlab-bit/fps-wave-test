using UnityEngine;

namespace HrdnLab.FpsWave.Weapons
{
    /// <summary>
    /// Calcolo del rinculo di un colpo (logica pura, coperta da test).
    /// Verticale: base * (primo colpo ? FirstShotMultiplier : crescita nella raffica).
    /// Orizzontale: base * (schema fisso dell'arma * PatternStrength + casuale * HorizontalRandomness).
    /// Lo schema è una curva morbida che dipende dal numero del colpo e da un seme dell'arma:
    /// la stessa arma tira sempre "a modo suo" (si impara a controllarla), armi diverse tirano diversamente.
    /// </summary>
    public static class RecoilMath
    {
        /// <summary>Calcio (gradi): x = su (pitch), y = lato (yaw, positivo a destra).</summary>
        public static Vector2 Kick(WeaponFeel feel, float basePitch, float baseYaw, int shotIndex, int seed,
            float aim01, bool crouching, float random01)
        {
            float vertical = basePitch * feel.VerticalMultiplier * VerticalScale(feel, shotIndex);
            float pattern = PatternValue(shotIndex, seed);
            float randomSigned = random01 * 2f - 1f;
            float horizontal = baseYaw * feel.HorizontalMultiplier * (pattern * feel.PatternStrength + randomSigned * feel.HorizontalRandomness);

            float stance = Mathf.Lerp(1f, feel.AdsMultiplier, Mathf.Clamp01(aim01)) * (crouching ? feel.CrouchMultiplier : 1f);
            return new Vector2(vertical, horizontal) * stance;
        }

        /// <summary>Moltiplicatore verticale per il colpo n-esimo della raffica (0 = primo).</summary>
        public static float VerticalScale(WeaponFeel feel, int shotIndex)
        {
            if (shotIndex <= 0)
                return feel.FirstShotMultiplier;
            float t = Mathf.Clamp01(shotIndex / (float)Mathf.Max(1, feel.RampShots));
            return Mathf.Lerp(1f, feel.RampMultiplier, t);
        }

        /// <summary>Schema orizzontale in [-1, 1]: due sinusoidi lente con fase data dal seme dell'arma.</summary>
        public static float PatternValue(int shotIndex, int seed)
        {
            float phase = (seed & 1023) * 0.0061f;
            float v = Mathf.Sin(shotIndex * 0.55f + phase) * 0.7f + Mathf.Sin(shotIndex * 1.37f + phase * 2.3f) * 0.3f;
            return Mathf.Clamp(v, -1f, 1f);
        }

        /// <summary>Seme stabile dal nome dell'arma (FNV-1a), uguale su tutte le macchine.</summary>
        public static int StableSeed(string id)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (id != null)
                    foreach (char c in id)
                    {
                        hash ^= c;
                        hash *= 16777619u;
                    }
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        /// <summary>Interpolazione esponenziale indipendente dal frame rate.</summary>
        public static float ExpBlend(float speed, float dt) => 1f - Mathf.Exp(-Mathf.Max(0f, speed) * Mathf.Max(0f, dt));
    }

    /// <summary>
    /// Molla smorzata su tre assi (logica pura): x'' = -k x - c x' + impulsi.
    /// Integrazione semi-implicita a passi fissi, stabile anche con frame lenti.
    /// </summary>
    public struct Spring3
    {
        private const float Step = 1f / 240f;
        private const int MaxSteps = 32;

        public Vector3 Value;
        public Vector3 Velocity;

        public void AddImpulse(Vector3 velocityChange) => Velocity += velocityChange;

        public void Update(float dt, float stiffness, float damping)
        {
            int steps = Mathf.Min(MaxSteps, Mathf.CeilToInt(Mathf.Max(0f, dt) / Step));
            if (steps <= 0)
                return;
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                Vector3 accel = -stiffness * Value - damping * Velocity;
                Velocity += accel * h;
                Value += Velocity * h;
            }
        }

        public void Reset()
        {
            Value = Vector3.zero;
            Velocity = Vector3.zero;
        }
    }
}
