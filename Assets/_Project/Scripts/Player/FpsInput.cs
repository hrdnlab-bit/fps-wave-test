using System;
using UnityEngine.InputSystem;

namespace HrdnLab.FpsWave.Player
{
    /// <summary>
    /// Azioni di input definite in codice (tastiera/mouse + gamepad). Tenerle qui evita di dipendere
    /// da un asset generato; la rimappatura arriverà con il menu impostazioni.
    /// </summary>
    public sealed class FpsInput : IDisposable
    {
        public readonly InputAction Move = new("Move", InputActionType.Value);
        public readonly InputAction Look = new("Look", InputActionType.Value, "<Mouse>/delta");
        public readonly InputAction LookStick = new("LookStick", InputActionType.Value, "<Gamepad>/rightStick");
        public readonly InputAction Fire = new("Fire", InputActionType.Button, "<Mouse>/leftButton");
        public readonly InputAction Aim = new("Aim", InputActionType.Button, "<Mouse>/rightButton");
        public readonly InputAction Jump = new("Jump", InputActionType.Button, "<Keyboard>/space");
        public readonly InputAction Sprint = new("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
        public readonly InputAction Crouch = new("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
        public readonly InputAction Reload = new("Reload", InputActionType.Button, "<Keyboard>/r");
        public readonly InputAction Weapon1 = new("Weapon1", InputActionType.Button, "<Keyboard>/1");
        public readonly InputAction Weapon2 = new("Weapon2", InputActionType.Button, "<Keyboard>/2");
        public readonly InputAction SwitchWeapon = new("SwitchWeapon", InputActionType.Button, "<Gamepad>/buttonNorth");
        public readonly InputAction Interact = new("Interact", InputActionType.Button, "<Keyboard>/e");
        public readonly InputAction Pause = new("Pause", InputActionType.Button, "<Keyboard>/escape");
        public readonly InputAction AbilityScan = new("AbilityScan", InputActionType.Button, "<Keyboard>/q");
        public readonly InputAction AbilityArmor = new("AbilityArmor", InputActionType.Button, "<Keyboard>/f");
        public readonly InputAction AbilityOvercharge = new("AbilityOvercharge", InputActionType.Button, "<Keyboard>/v");
        public readonly InputAction Ping = new("Ping", InputActionType.Button, "<Mouse>/middleButton");
        /// <summary>Solo sviluppo: riallinea la visuale alla direzione di spawn (utile nei test automatici).</summary>
        public readonly InputAction DebugResetView = new("DebugResetView", InputActionType.Button, "<Keyboard>/home");

        private readonly InputAction[] _all;

        public FpsInput()
        {
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick");
            Fire.AddBinding("<Gamepad>/rightTrigger");
            Aim.AddBinding("<Gamepad>/leftTrigger");
            Jump.AddBinding("<Gamepad>/buttonSouth");
            Sprint.AddBinding("<Gamepad>/leftStickPress");
            Crouch.AddBinding("<Keyboard>/c");
            Crouch.AddBinding("<Gamepad>/buttonEast");
            Reload.AddBinding("<Gamepad>/buttonWest");
            Interact.AddBinding("<Gamepad>/dpad/up");
            Pause.AddBinding("<Gamepad>/start");
            AbilityScan.AddBinding("<Gamepad>/leftShoulder");
            AbilityArmor.AddBinding("<Gamepad>/rightShoulder");
            AbilityOvercharge.AddBinding("<Gamepad>/dpad/left");
            Ping.AddBinding("<Keyboard>/z");
            Ping.AddBinding("<Gamepad>/dpad/right");

            _all = new[] { Move, Look, LookStick, Fire, Aim, Jump, Sprint, Crouch, Reload, Weapon1, Weapon2, SwitchWeapon, Interact, Pause, DebugResetView, AbilityScan, AbilityArmor, AbilityOvercharge, Ping };
        }

        public void Enable()
        {
            foreach (InputAction action in _all)
                action.Enable();
        }

        public void Disable()
        {
            foreach (InputAction action in _all)
                action.Disable();
        }

        public void Dispose()
        {
            foreach (InputAction action in _all)
                action.Dispose();
        }
    }
}
