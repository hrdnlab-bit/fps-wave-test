using UnityEngine;

namespace HrdnLab.FpsWave.Player.Character
{
    /// <summary>Parametri dell'animazione procedurale del personaggio (passo, crouch, postura con l'arma).</summary>
    [CreateAssetMenu(menuName = "FpsWave/Character Animation Settings", fileName = "CharacterAnimation")]
    public sealed class CharacterAnimationSettings : ScriptableObject
    {
        [Header("Passo")]
        [Min(0.1f)] public float WalkSpeed = 4.5f;
        [Min(0.1f)] public float RunSpeed = 7f;
        [Min(0.1f)] public float WalkStride = 0.75f;
        [Min(0.1f)] public float RunStride = 1.5f;
        [Range(0.3f, 0.8f)] public float StanceRatio = 0.6f;
        [Min(0f)] public float WalkLift = 0.09f;
        [Min(0f)] public float RunLift = 0.16f;
        [Min(0f)] public float HipBob = 0.03f;
        [Min(0f)] public float HipSway = 6f;
        [Tooltip("Inclinazione del busto in avanti alla velocità di corsa (gradi).")]
        [Min(0f)] public float RunLean = 9f;

        [Header("Crouch e salto")]
        [Min(0f)] public float CrouchHipDrop = 0.42f;
        [Min(0f)] public float CrouchLean = 22f;
        [Min(0f)] public float AirFootLift = 0.22f;

        [Header("Mira")]
        [Range(0f, 1f)] public float SpinePitchShare = 0.45f;
        [Range(0f, 1f)] public float HeadPitchShare = 0.45f;

        [Header("Arma in terza persona (spazio del personaggio, metri)")]
        public Vector3 AimPivot = new(0f, 1.45f, 0f);
        public Vector3 RifleGrip = new(0.09f, -0.06f, 0.2f);
        public Vector3 PistolGrip = new(0.05f, -0.07f, 0.42f);
        [Min(0f)] public float RecoilKickBack = 0.035f;
        [Min(0f)] public float RecoilKickPitch = 6f;

        [Header("Mani")]
        public float GripCurl = 68f;
        public float SupportCurl = 55f;
        public float ThumbCurl = 25f;

        [Header("Ottimizzazione")]
        [Tooltip("Oltre questa distanza dalla camera i player remoti si aggiornano a frequenza ridotta.")]
        [Min(1f)] public float LowRateDistance = 30f;
        [Min(1)] public int LowRateFrameInterval = 3;
    }
}
