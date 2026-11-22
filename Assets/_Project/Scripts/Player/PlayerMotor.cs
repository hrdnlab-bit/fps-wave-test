using FishNet.Object;
using UnityEngine;

namespace HrdnLab.FpsWave.Player
{
    /// <summary>
    /// Movimento FPS client-authoritative (modello PvE, vedi docs/NETCODE.md): il proprietario muove il
    /// CharacterController in locale e il NetworkTransform replica la posizione. Il server esegue solo
    /// un controllo di plausibilità e segnala valori anomali.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : NetworkBehaviour
    {
        [Header("Velocità (m/s)")]
        [SerializeField] private float _walkSpeed = 4.5f;
        [SerializeField] private float _sprintSpeed = 7f;
        [SerializeField] private float _crouchSpeed = 2.5f;
        [SerializeField] private float _acceleration = 60f;
        [SerializeField] private float _airAcceleration = 15f;

        [Header("Salto e gravità")]
        [SerializeField] private float _jumpHeight = 1.2f;
        [SerializeField] private float _gravity = -22f;

        [Header("Crouch")]
        [SerializeField] private float _standingHeight = 1.8f;
        [SerializeField] private float _crouchHeight = 1.1f;
        [SerializeField] private float _standingEyeHeight = 1.62f;
        [SerializeField] private float _crouchEyeHeight = 0.95f;
        [SerializeField] private float _crouchTransitionSpeed = 10f;

        [Header("Riferimenti")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Transform _cameraPivot;

        [Header("Controllo server")]
        [SerializeField] private float _suspiciousSpeed = 15f;
        [SerializeField] private float _teleportDistance = 10f;

        private CharacterController _controller;
        private Vector3 _velocity;
        private float _currentHeight;
        private bool _isCrouching;

        // Stato del controllo server.
        private Vector3 _serverLastPosition;
        private float _serverSuspiciousTime;
        private float _serverNextWarningTime;

        /// <summary>Moltiplicatore applicato da altri sistemi (ADS, debuff). 1 = normale.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        /// <summary>Corsa impedita dall'arma (si spara o si mira): come nei Call of Duty, premere il grilletto interrompe lo sprint.</summary>
        public bool SprintBlocked { get; set; }
        /// <summary>Moltiplicatore delle abilità della tuta (Overcharge, Armor mode). 1 = normale.</summary>
        public float AbilitySpeedMultiplier { get; set; } = 1f;
        /// <summary>A terra (PlayerLife): si striscia accovacciati, niente corsa né salto.</summary>
        public bool Downed { get; set; }
        [SerializeField, Range(0.1f, 1f)] private float _downedSpeedMultiplier = 0.45f;
        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public bool IsSprinting { get; private set; }
        public bool IsCrouching => _isCrouching;
        public Vector3 Velocity => _velocity;
        public float EyeHeight => Mathf.Lerp(_crouchEyeHeight, _standingEyeHeight,
            Mathf.InverseLerp(_crouchHeight, _standingHeight, _currentHeight));

        /// <summary>Posizione degli occhi in piedi, usata dal server come riferimento per validare l'origine degli spari.</summary>
        public Vector3 ServerEyePosition => transform.position + Vector3.up * _standingEyeHeight;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _currentHeight = _standingHeight;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Solo il proprietario simula il CharacterController; per gli altri la posizione arriva dalla rete.
            _controller.enabled = IsOwner;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _serverLastPosition = transform.position;
            if (!Owner.IsLocalClient)
                _controller.enabled = false;
        }

        private void Update()
        {
            if (IsOwner)
                SimulateOwner(Time.deltaTime);
            else if (IsServerInitialized)
                CheckPlausibility(Time.deltaTime);
        }

        private void SimulateOwner(float dt)
        {
            if (!_controller.enabled)
                return;

            Vector2 move = _input != null ? _input.MoveInput : Vector2.zero;
            bool wantsCrouch = (_input != null && _input.CrouchHeld) || Downed;
            UpdateCrouch(wantsCrouch, dt);

            IsSprinting = !_isCrouching && _input != null && _input.SprintHeld && move.y > 0.1f && SpeedMultiplier >= 0.99f && !SprintBlocked && !Downed;
            float targetSpeed = _isCrouching ? _crouchSpeed : IsSprinting ? _sprintSpeed : _walkSpeed;
            targetSpeed *= SpeedMultiplier * AbilitySpeedMultiplier * (Downed ? _downedSpeedMultiplier : 1f);

            Vector3 wishDir = transform.right * move.x + transform.forward * move.y;
            Vector3 targetHorizontal = wishDir * targetSpeed;
            Vector3 horizontal = new(_velocity.x, 0f, _velocity.z);
            float accel = _controller.isGrounded ? _acceleration : _airAcceleration;
            horizontal = Vector3.MoveTowards(horizontal, targetHorizontal, accel * dt);

            if (_controller.isGrounded)
            {
                if (_velocity.y < 0f)
                    _velocity.y = -2f; // mantiene il contatto col terreno sulle discese
                if (_input != null && _input.JumpPressed && !_isCrouching && !Downed)
                    _velocity.y = Mathf.Sqrt(2f * -_gravity * _jumpHeight);
            }
            _velocity.y += _gravity * dt;
            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;

            CollisionFlags flags = _controller.Move(_velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && _velocity.y > 0f)
                _velocity.y = 0f;
        }

        /// <summary>Spinta improvvisa (carica del Brute): sommata alla velocità del proprietario.</summary>
        public void AddImpulse(Vector3 impulse)
        {
            if (IsOwner)
                _velocity += impulse;
        }

        /// <summary>Server → proprietario: spintone (la posizione è simulata dal proprietario).</summary>
        [TargetRpc]
        public void TargetKnockback(FishNet.Connection.NetworkConnection connection, Vector3 impulse) => AddImpulse(impulse);

        private void UpdateCrouch(bool wantsCrouch, float dt)
        {
            if (!wantsCrouch && _isCrouching && !HasHeadroom())
                wantsCrouch = true; // non ci si alza sotto un ostacolo
            _isCrouching = wantsCrouch;

            float target = _isCrouching ? _crouchHeight : _standingHeight;
            if (!Mathf.Approximately(_currentHeight, target))
            {
                _currentHeight = Mathf.MoveTowards(_currentHeight, target, _crouchTransitionSpeed * dt);
                _controller.height = _currentHeight;
                _controller.center = new Vector3(0f, _currentHeight * 0.5f, 0f);
            }
            if (_cameraPivot != null)
                _cameraPivot.localPosition = new Vector3(0f, EyeHeight, 0f);
        }

        private bool HasHeadroom()
        {
            float radius = _controller.radius * 0.95f;
            Vector3 bottom = transform.position + Vector3.up * (radius + 0.05f);
            Vector3 top = transform.position + Vector3.up * (_standingHeight - radius);
            return !Physics.CheckCapsule(bottom, top, radius, ~(1 << Core.GameLayers.Player), QueryTriggerInteraction.Ignore);
        }

        /// <summary>Controllo di plausibilità lato server: registra (non punisce) movimenti impossibili.</summary>
        private void CheckPlausibility(float dt)
        {
            if (dt <= 0f)
                return;
            Vector3 position = transform.position;
            Vector3 delta = position - _serverLastPosition;
            _serverLastPosition = position;

            float horizontalSpeed = new Vector2(delta.x, delta.z).magnitude / dt;
            bool teleport = delta.magnitude > _teleportDistance;
            _serverSuspiciousTime = horizontalSpeed > _suspiciousSpeed ? _serverSuspiciousTime + dt : 0f;

            if ((teleport || _serverSuspiciousTime > 0.5f) && Time.time >= _serverNextWarningTime)
            {
                _serverNextWarningTime = Time.time + 5f;
                Debug.LogWarning($"[FpsWave][Server] Movimento anomalo del client {OwnerId}: " +
                                 $"{(teleport ? "teletrasporto" : "velocità")} {horizontalSpeed:F1} m/s");
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(PlayerInputReader input, Transform cameraPivot)
        {
            _input = input;
            _cameraPivot = cameraPivot;
        }
#endif
    }
}
