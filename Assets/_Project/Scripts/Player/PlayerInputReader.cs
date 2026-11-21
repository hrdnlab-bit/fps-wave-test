using FishNet.Object;
using UnityEngine;

namespace HrdnLab.FpsWave.Player
{
    /// <summary>
    /// Legge l'input solo per il player locale (owner) e gestisce il blocco del cursore.
    /// Gli altri componenti leggono da qui, così l'input è centralizzato e si può disattivare in blocco (menu, pausa).
    /// </summary>
    public sealed class PlayerInputReader : NetworkBehaviour
    {
        [SerializeField, Range(0.01f, 2f)] private float _mouseSensitivity = 0.12f;
        [SerializeField, Range(10f, 400f)] private float _stickSensitivity = 180f;

        private FpsInput _input;

        /// <summary>True se questo è il player locale e il gioco ha il focus (cursore bloccato).</summary>
        public bool HasControl => _input != null && Cursor.lockState == CursorLockMode.Locked;
        public FpsInput Input => _input;

        public Vector2 MoveInput => HasControl ? Vector2.ClampMagnitude(_input.Move.ReadValue<Vector2>(), 1f) : Vector2.zero;

        /// <summary>Rotazione richiesta in gradi per questo frame (x = yaw, y = pitch).</summary>
        public Vector2 LookDelta
        {
            get
            {
                if (!HasControl)
                    return Vector2.zero;
                Vector2 mouse = _input.Look.ReadValue<Vector2>() * _mouseSensitivity;
                Vector2 stick = _input.LookStick.ReadValue<Vector2>() * (_stickSensitivity * Time.deltaTime);
                return mouse + stick;
            }
        }

        public bool FireHeld => HasControl && _input.Fire.IsPressed();
        public bool FirePressed => HasControl && _input.Fire.WasPressedThisFrame();
        public bool AimHeld => HasControl && _input.Aim.IsPressed();
        public bool JumpPressed => HasControl && _input.Jump.WasPressedThisFrame();
        public bool SprintHeld => HasControl && _input.Sprint.IsPressed();
        public bool CrouchHeld => HasControl && _input.Crouch.IsPressed();
        public bool ReloadPressed => HasControl && _input.Reload.WasPressedThisFrame();
        public bool Weapon1Pressed => HasControl && _input.Weapon1.WasPressedThisFrame();
        public bool Weapon2Pressed => HasControl && _input.Weapon2.WasPressedThisFrame();
        public bool SwitchWeaponPressed => HasControl && _input.SwitchWeapon.WasPressedThisFrame();
        public bool DebugResetViewPressed => _input != null && (Application.isEditor || Debug.isDebugBuild) && _input.DebugResetView.WasPressedThisFrame();

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
            {
                enabled = false;
                return;
            }
            _input = new FpsInput();
            _input.Enable();
            LockCursor(true);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            ReleaseInput();
        }

        private void OnDestroy() => ReleaseInput();

        private void ReleaseInput()
        {
            if (_input == null)
                return;
            _input.Dispose();
            _input = null;
            LockCursor(false);
        }

        private void Update()
        {
            if (_input == null)
                return;
            if (_input.Pause.WasPressedThisFrame())
                LockCursor(Cursor.lockState != CursorLockMode.Locked);
            else if (Cursor.lockState != CursorLockMode.Locked && _input.Fire.WasPressedThisFrame() && Application.isFocused)
                LockCursor(true);
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
