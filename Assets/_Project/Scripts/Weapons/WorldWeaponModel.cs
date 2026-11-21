using HrdnLab.FpsWave.Player.Character;
using UnityEngine;

namespace HrdnLab.FpsWave.Weapons
{
    /// <summary>
    /// Modello dell'arma in terza persona (solo cosmetico, nessun componente di rete). L'origine è l'impugnatura:
    /// i bersagli delle mani indicano dove vanno i polsi (forward = direzione delle dita, up = normale del palmo).
    /// Generato da <c>FpsWave/Setup/9</c> a partire dai modelli in prima persona.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldWeaponModel : MonoBehaviour
    {
        [SerializeField] private CharacterGrip _grip = CharacterGrip.Rifle;
        [SerializeField] private Transform _rightHand;
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _muzzle;

        public CharacterGrip Grip => _grip;
        public Transform RightHand => _rightHand;
        public Transform LeftHand => _leftHand;
        public Transform Muzzle => _muzzle;

#if UNITY_EDITOR
        public void EditorSetup(CharacterGrip grip, Transform rightHand, Transform leftHand, Transform muzzle)
        {
            _grip = grip;
            _rightHand = rightHand;
            _leftHand = leftHand;
            _muzzle = muzzle;
        }
#endif
    }

    /// <summary>Chi mostra il corpo in terza persona fornisce la volata dell'arma per i tracer dei player remoti.</summary>
    public interface IThirdPersonMuzzle
    {
        Transform WorldMuzzle { get; }
    }
}
