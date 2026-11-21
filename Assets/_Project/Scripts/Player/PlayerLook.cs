using FishNet.Object;
using HrdnLab.FpsWave.Core;
using UnityEngine;

namespace HrdnLab.FpsWave.Player
{
    /// <summary>
    /// Visuale in prima persona del player locale: yaw sul corpo (replicato dal NetworkTransform),
    /// pitch sul pivot della camera, rinculo con recupero e FOV con zoom di mira.
    /// </summary>
    public sealed class PlayerLook : NetworkBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private Camera _camera;
        [SerializeField] private float _minPitch = -85f;
        [SerializeField] private float _maxPitch = 85f;
        [SerializeField] private float _fovLerpSpeed = 14f;

        private float _yaw;
        private float _pitch;
        private float _recoilPitch;
        private float _recoilRecovery = 10f;
        private float _spawnYaw;

        /// <summary>Moltiplicatore di FOV richiesto dall'arma (1 = normale, 0.8 = zoom).</summary>
        public float FovMultiplier { get; set; } = 1f;
        public Camera Camera => _camera;
        public Transform CameraPivot => _cameraPivot;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
            {
                enabled = false;
                return;
            }
            _yaw = transform.eulerAngles.y;
            _spawnYaw = _yaw;
            if (_camera != null)
                _camera.fieldOfView = GameSettings.FieldOfView;
        }

        /// <summary>Applica un calcio di rinculo (gradi). Il pitch si recupera, lo yaw resta.</summary>
        public void AddRecoil(float pitchKick, float yawKick, float recoveryPerSecond)
        {
            _recoilPitch += pitchKick;
            _yaw += yawKick;
            _recoilRecovery = recoveryPerSecond;
        }

        private void LateUpdate()
        {
            if (_input == null)
                return;

            if (_input.DebugResetViewPressed)
            {
                _yaw = _spawnYaw;
                _pitch = 0f;
                _recoilPitch = 0f;
            }

            Vector2 delta = _input.LookDelta;
            _yaw += delta.x;
            _pitch = Mathf.Clamp(_pitch - delta.y, _minPitch, _maxPitch);

            _recoilPitch = Mathf.MoveTowards(_recoilPitch, 0f, _recoilRecovery * Time.deltaTime);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_cameraPivot != null)
                _cameraPivot.localRotation = Quaternion.Euler(Mathf.Clamp(_pitch - _recoilPitch, _minPitch, _maxPitch), 0f, 0f);

            if (_camera != null)
            {
                float targetFov = GameSettings.FieldOfView * FovMultiplier; // FOV scelto nelle impostazioni
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, 1f - Mathf.Exp(-_fovLerpSpeed * Time.deltaTime));
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(PlayerInputReader input, Transform pivot, Camera camera)
        {
            _input = input;
            _cameraPivot = pivot;
            _camera = camera;
        }
#endif
    }
}
