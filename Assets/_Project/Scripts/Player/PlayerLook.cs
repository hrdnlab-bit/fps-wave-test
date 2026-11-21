using FishNet.Object;
using HrdnLab.FpsWave.Core;
using HrdnLab.FpsWave.Weapons;
using UnityEngine;

namespace HrdnLab.FpsWave.Player
{
    /// <summary>
    /// Visuale in prima persona del player locale: yaw sul corpo (replicato dal NetworkTransform),
    /// pitch sul pivot della camera, FOV con zoom di mira.
    /// Rinculo a due parti: una permanente (entra nella mira, va compensata) e una recuperabile che scatta
    /// rapida (snappiness) e torna morbida (return speed). Tremore a "trauma" (cresce con il fuoco prolungato,
    /// rumore di Perlin, svanisce da solo) e punch del FOV a ogni colpo.
    /// </summary>
    public sealed class PlayerLook : NetworkBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private Camera _camera;
        [SerializeField] private float _minPitch = -85f;
        [SerializeField] private float _maxPitch = 85f;
        [SerializeField] private float _fovLerpSpeed = 14f;

        [Header("Tremore")]
        [SerializeField, Min(0f)] private float _shakeMaxDegrees = 1.4f;
        [SerializeField, Min(0f)] private float _shakeFrequency = 26f;
        [SerializeField, Min(0f)] private float _traumaDecay = 1.8f;

        private float _yaw;
        private float _pitch;
        private float _spawnYaw;
        private Vector2 _recoilTarget;   // parte recuperabile richiesta (x = su, y = destra)
        private Vector2 _recoilCurrent;  // parte recuperabile applicata
        private float _recoilSnappiness = 20f;
        private float _recoilReturn = 7f;
        private float _trauma;
        private float _fovPunch;
        private float _smoothFov = -1f;

        /// <summary>Moltiplicatore di FOV richiesto dall'arma (1 = normale, 0.8 = zoom).</summary>
        public float FovMultiplier { get; set; } = 1f;
        public Camera Camera => _camera;
        public Transform CameraPivot => _cameraPivot;
        /// <summary>Pitch effettivo della visuale (gradi, positivo = in basso), usato dal corpo in terza persona.</summary>
        public float ViewPitch => Mathf.Clamp(_pitch - _recoilCurrent.x, _minPitch, _maxPitch);

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

        /// <summary>
        /// Calcio di rinculo (gradi: x su, y destra). <paramref name="permanentFraction"/> entra subito nella mira
        /// (il giocatore la compensa), il resto scatta a <paramref name="snappiness"/> e torna a <paramref name="returnSpeed"/>.
        /// </summary>
        public void AddRecoilKick(Vector2 kick, float permanentFraction, float snappiness, float returnSpeed)
        {
            float permanent = Mathf.Clamp01(permanentFraction);
            _pitch = Mathf.Clamp(_pitch - kick.x * permanent, _minPitch, _maxPitch);
            _yaw += kick.y * permanent;
            _recoilTarget += kick * (1f - permanent);
            _recoilSnappiness = snappiness;
            _recoilReturn = returnSpeed;
        }

        /// <summary>Compatibilità: calcio con pitch recuperabile e yaw permanente.</summary>
        public void AddRecoil(float pitchKick, float yawKick, float recoveryPerSecond)
        {
            _yaw += yawKick;
            AddRecoilKick(new Vector2(pitchKick, 0f), 0f, 20f, recoveryPerSecond);
        }

        /// <summary>Aggiunge "trauma" (0-1): il tremore è proporzionale al quadrato, quindi i colpi singoli restano puliti.</summary>
        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        /// <summary>Allargamento istantaneo del FOV (frazione), che rientra subito.</summary>
        public void AddFovPunch(float amount) => _fovPunch = Mathf.Min(_fovPunch + Mathf.Max(0f, amount), 0.08f);

        private void LateUpdate()
        {
            if (_input == null)
                return;

            if (_input.DebugResetViewPressed)
            {
                _yaw = _spawnYaw;
                _pitch = 0f;
                _recoilTarget = Vector2.zero;
                _recoilCurrent = Vector2.zero;
            }

            Vector2 delta = _input.LookDelta;
            _yaw += delta.x;
            _pitch = Mathf.Clamp(_pitch - delta.y, _minPitch, _maxPitch);

            float dt = Time.deltaTime;
            _recoilTarget = Vector2.Lerp(_recoilTarget, Vector2.zero, RecoilMath.ExpBlend(_recoilReturn, dt));
            _recoilCurrent = Vector2.Lerp(_recoilCurrent, _recoilTarget, RecoilMath.ExpBlend(_recoilSnappiness, dt));

            _trauma = Mathf.MoveTowards(_trauma, 0f, _traumaDecay * dt);
            float shake = _trauma * _trauma * _shakeMaxDegrees;
            float t = Time.time * _shakeFrequency;
            float shakePitch = shake == 0f ? 0f : (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * shake;
            float shakeYaw = shake == 0f ? 0f : (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 1.4f * shake;
            float shakeRoll = shake == 0f ? 0f : (Mathf.PerlinNoise(t, 5.1f) - 0.5f) * 2.4f * shake;

            transform.rotation = Quaternion.Euler(0f, _yaw + _recoilCurrent.y, 0f);
            if (_cameraPivot != null)
                _cameraPivot.localRotation = Quaternion.Euler(
                    Mathf.Clamp(_pitch - _recoilCurrent.x + shakePitch, _minPitch, _maxPitch), shakeYaw, shakeRoll);

            if (_camera != null)
            {
                float targetFov = GameSettings.FieldOfView * FovMultiplier; // FOV scelto nelle impostazioni
                if (_smoothFov < 0f)
                    _smoothFov = targetFov;
                _smoothFov = Mathf.Lerp(_smoothFov, targetFov, RecoilMath.ExpBlend(_fovLerpSpeed, dt));
                _fovPunch = Mathf.Lerp(_fovPunch, 0f, RecoilMath.ExpBlend(16f, dt));
                _camera.fieldOfView = _smoothFov * (1f + _fovPunch);
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
