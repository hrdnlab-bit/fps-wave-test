using UnityEngine;

namespace HrdnLab.FpsWave.Player.Character
{
    /// <summary>
    /// Matematica pura del personaggio in terza persona: ciclo del passo, IK a due ossa e
    /// quantizzazione dello stato inviato in rete. Nessuna dipendenza da scene o rete (test EditMode).
    /// </summary>
    public static class CharacterPoseMath
    {
        public const float MaxPitch = 85f;

        public const byte FlagCrouch = 1 << 0;
        public const byte FlagGrounded = 1 << 1;

        /// <summary>Pitch della visuale in gradi -> 1 byte (risoluzione ~0,67°).</summary>
        public static sbyte PackPitch(float degrees) =>
            (sbyte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(degrees, -MaxPitch, MaxPitch) / MaxPitch * 127f), -127, 127);

        public static float UnpackPitch(sbyte packed) => packed / 127f * MaxPitch;

        public static byte PackFlags(bool crouching, bool grounded) =>
            (byte)((crouching ? FlagCrouch : 0) | (grounded ? FlagGrounded : 0));

        public static bool HasFlag(byte flags, byte flag) => (flags & flag) != 0;

        /// <summary>
        /// Ciclo di un piede. <paramref name="phase"/> in giri (qualsiasi valore, si usa la parte frazionaria).
        /// x = spostamento lungo la direzione di marcia in frazioni di passo (+0,5 davanti, -0,5 dietro),
        /// y = sollevamento 0..1. Appoggio per <paramref name="stanceRatio"/> del ciclo, poi volo.
        /// </summary>
        public static Vector2 FootCycle(float phase, float stanceRatio)
        {
            stanceRatio = Mathf.Clamp(stanceRatio, 0.05f, 0.95f);
            phase -= Mathf.Floor(phase);
            if (phase < stanceRatio)
                return new Vector2(0.5f - phase / stanceRatio, 0f);
            float s = (phase - stanceRatio) / (1f - stanceRatio);
            float eased = s * s * (3f - 2f * s);
            return new Vector2(-0.5f + eased, Mathf.Sin(s * Mathf.PI));
        }

        /// <summary>Lunghezza del passo in metri in funzione della velocità (cammino 0,7 m, corsa ~1,5 m).</summary>
        public static float StrideLength(float speed, float walkStride, float runStride, float walkSpeed, float runSpeed)
        {
            float t = Mathf.InverseLerp(walkSpeed, runSpeed, speed);
            return Mathf.Lerp(walkStride, runStride, t);
        }

        /// <summary>
        /// IK a due ossa: posizione del giunto intermedio (ginocchio, gomito) data la radice <paramref name="root"/>,
        /// il bersaglio e le lunghezze dei segmenti. Il giunto si piega dalla parte di <paramref name="hint"/>.
        /// <paramref name="reached"/> è il bersaglio limitato alla portata della catena.
        /// </summary>
        public static Vector3 SolveMidJoint(Vector3 root, Vector3 target, float upperLength, float lowerLength,
            Vector3 hint, out Vector3 reached)
        {
            Vector3 toTarget = target - root;
            float max = (upperLength + lowerLength) * 0.999f;
            float min = Mathf.Abs(upperLength - lowerLength) * 1.001f + 1e-4f;
            float distance = Mathf.Clamp(toTarget.magnitude, min, max);
            Vector3 dir = toTarget.sqrMagnitude > 1e-10f ? toTarget.normalized : Vector3.forward;
            reached = root + dir * distance;

            float cos = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) /
                        (2f * upperLength * distance);
            float angle = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Rad2Deg;

            Vector3 bend = Vector3.ProjectOnPlane(hint, dir);
            if (bend.sqrMagnitude < 1e-10f)
                bend = Vector3.ProjectOnPlane(Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.forward, dir);
            Vector3 axis = Vector3.Cross(dir, bend.normalized);
            return root + Quaternion.AngleAxis(angle, axis) * dir * upperLength;
        }

        /// <summary>Smorzamento esponenziale indipendente dal frame rate.</summary>
        public static float Damp(float current, float target, float sharpness, float dt) =>
            Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));

        public static Vector3 Damp(Vector3 current, Vector3 target, float sharpness, float dt) =>
            Vector3.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));
    }
}
