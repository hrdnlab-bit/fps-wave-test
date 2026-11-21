using HrdnLab.FpsWave.Player;
using HrdnLab.FpsWave.Stats;
using UnityEngine;

namespace HrdnLab.FpsWave.Weapons
{
    /// <summary>
    /// "Sensazione" delle armi per il player locale, valida per qualunque modello (agisce sulla radice del view model
    /// e sulla camera, non sulle animazioni dell'asset). I numeri vengono dal <see cref="WeaponFeel"/> dell'arma impugnata.
    /// - Colpo: rinculo della camera (schema + crescita + parte permanente), calcio a molla dell'arma, tremore, punch del FOV, calore.
    /// - Ogni frame: molle, oscillazione che segue il mouse in ritardo, ondeggio di passo e corsa, inclinazione laterale,
    ///   affondo all'atterraggio, posa di corsa, respiro da fermi, tremolio a calore alto. Tutto si calma mirando.
    /// - Luce di volata: colore dell'energia dell'arma che vira al "caldo" con il fuoco prolungato.
    /// Puramente cosmetico e locale: nessun dato di rete, nessun effetto sulla validazione dei colpi.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponFeelDriver : MonoBehaviour
    {
        private const float MuzzleFlashTime = 0.055f;
        private const float VentStart = 0.85f;
        private const float VentEnd = 0.15f;

        [SerializeField] private PlayerLook _look;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private PlayerInputReader _input;

        [Header("Posizione e perno")]
        [Tooltip("Spostamento della posa da fianco (m, spazio camera): z negativo = arma più vicina, y negativo = più in basso. Mirando sparisce.")]
        [SerializeField] private Vector3 _hipOffset = new(0f, -0.012f, -0.045f);
        [Tooltip("Punto (spazio camera) attorno a cui ruota l'arma per calcio, oscillazione e corsa: circa l'impugnatura. Ruotando attorno all'occhio l'arma salirebbe sullo schermo.")]
        [SerializeField] private Vector3 _pivot = new(0.12f, -0.17f, 0.32f);

        [Header("Posa di corsa (offset della radice dell'arma)")]
        [SerializeField] private Vector3 _sprintPosition = new(0.04f, -0.07f, -0.04f);
        [SerializeField] private Vector3 _sprintRotation = new(10f, -24f, 10f);

        [Header("Estrazione")]
        [SerializeField] private Vector3 _drawPosition = new(0f, -0.14f, -0.06f);
        [SerializeField] private Vector3 _drawRotation = new(28f, -12f, 8f);

        private WeaponFeel _feel = new();
        private int _seed;
        private Spring3 _posSpring;
        private Spring3 _rotSpring;
        private Vector3 _sway;
        private float _bobPhase;
        private float _bobWeight;
        private float _sprint;
        private float _tilt;
        private bool _wasGrounded = true;
        private float _fallSpeed;
        private float _breath;
        private int _shotIndex;
        private float _lastShotTime = -10f;
        private float _heat;
        private bool _venting;
        private float _flashTime = -10f;

        /// <summary>Calore 0-1 (cosmetico): cresce sparando, cala dopo una breve pausa.</summary>
        public float Heat => _heat;
        /// <summary>Raffreddamento dopo aver superato l'85% di calore, fino al 15%.</summary>
        public bool IsVenting => _venting;
        public Color EnergyColor => _feel.EnergyColor;
        public WeaponFeel Profile => _feel;

        private void Awake()
        {
            if (_look == null) _look = GetComponent<PlayerLook>();
            if (_motor == null) _motor = GetComponent<PlayerMotor>();
            if (_input == null) _input = GetComponent<PlayerInputReader>();
        }

        /// <summary>Cambio arma: nuovo profilo, raffica e calore azzerati, l'arma entra dal basso.</summary>
        public void OnEquip(WeaponDefinition definition)
        {
            _feel = definition != null ? definition.Feel : new WeaponFeel();
            _seed = RecoilMath.StableSeed(definition != null ? definition.Id : string.Empty);
            _shotIndex = 0;
            _heat = 0f;
            _venting = false;
            _posSpring.Reset();
            _rotSpring.Reset();
            _posSpring.Value = _drawPosition;
            _rotSpring.Value = _drawRotation;
        }

        /// <summary>Un colpo sparato dal player locale.</summary>
        public void OnShot(WeaponState state, float aim)
        {
            float now = Time.time;
            if (now - _lastShotTime > _feel.BurstResetTime)
                _shotIndex = 0;
            bool crouching = _motor != null && _motor.IsCrouching;

            // Camera
            Vector2 kick = RecoilMath.Kick(_feel, state.Stats.Get(StatType.RecoilPitch), state.Stats.Get(StatType.RecoilYaw),
                _shotIndex, _seed, aim, crouching, Random.value);
            if (_look != null)
            {
                _look.AddRecoilKick(kick, _feel.PermanentFraction, _feel.Snappiness, _feel.ReturnSpeed);
                _look.AddTrauma(_feel.ShakePerShot * Mathf.Lerp(1f, 0.5f, aim));
                _look.AddFovPunch(_feel.FovPunch * Mathf.Lerp(1f, 0.4f, aim));
            }

            // Arma in mano: impulso = spostamento di picco desiderato * pulsazione della molla
            float ramp = Mathf.Clamp01(_shotIndex / (float)Mathf.Max(1, _feel.RampShots));
            float scale = _feel.ViewKickScale * Mathf.Lerp(1f, 0.55f, aim) * Mathf.Lerp(1f, 1.25f, ramp);
            float r = _feel.KickRandomness;
            Vector3 kp = _feel.KickPosition;
            Vector3 kr = _feel.KickRotation;
            var posKick = new Vector3(kp.x * Random.Range(-1f, 1f), kp.y * (1f + Random.Range(-r, r)), kp.z * (1f + Random.Range(-r, r)));
            var rotKick = new Vector3(kr.x * (1f + Random.Range(-r, r)), kr.y * Random.Range(-1f, 1f), kr.z * Random.Range(-1f, 1f));
            _posSpring.AddImpulse(posKick * (scale * Mathf.Sqrt(_feel.PositionStiffness)));
            _rotSpring.AddImpulse(rotKick * (scale * Mathf.Sqrt(_feel.RotationStiffness)));

            // Calore
            _heat = Mathf.Clamp01(_heat + _feel.HeatPerShot);
            if (_heat >= VentStart)
                _venting = true;

            _shotIndex++;
            _lastShotTime = now;
            _flashTime = now;
        }

        /// <summary>Prepara la luce di volata per il colpo: colore dell'energia, verso il "caldo" con il calore.</summary>
        public void ConfigureMuzzleLight(Light light)
        {
            if (light == null)
                return;
            light.color = Color.Lerp(_feel.EnergyColor, _feel.HotColor, Mathf.Pow(_heat, 1.5f));
            light.range = Random.Range(4f, 6f);
            light.intensity = _feel.MuzzleLightIntensity * (1f + _heat * 0.6f);
        }

        /// <summary>Lampo che si spegne in fretta invece di accendersi e spegnersi di colpo.</summary>
        public void UpdateMuzzleLight(Light light)
        {
            if (light == null || !light.enabled)
                return;
            float k = 1f - Mathf.Clamp01((Time.time - _flashTime) / MuzzleFlashTime);
            light.intensity = _feel.MuzzleLightIntensity * (1f + _heat * 0.6f) * k * k;
        }

        public bool MuzzleFlashOver => Time.time - _flashTime >= MuzzleFlashTime;

        /// <summary>Posa finale della radice del view model: base (anca/mira) + tutti gli effetti.</summary>
        public void ApplyViewModel(Transform root, Vector3 basePosition, float aim, float dt)
        {
            if (root == null || dt <= 0f)
                return;

            _posSpring.Update(dt, _feel.PositionStiffness, _feel.PositionDamping);
            _rotSpring.Update(dt, _feel.RotationStiffness, _feel.RotationDamping);
            float steady = Mathf.Lerp(1f, 0.15f, aim); // mirando quasi tutto si ferma

            // Oscillazione: l'arma resta indietro rispetto alla rotazione della visuale.
            Vector2 look = _input != null ? _input.LookDelta : Vector2.zero;
            Vector2 lookSpeed = look / dt; // gradi al secondo
            float k = _feel.SwayAmount * 0.012f;
            var swayTarget = new Vector3(
                Mathf.Clamp(lookSpeed.y * k, -_feel.SwayMax, _feel.SwayMax),
                Mathf.Clamp(-lookSpeed.x * k, -_feel.SwayMax, _feel.SwayMax),
                Mathf.Clamp(-lookSpeed.x * k * 0.6f, -_feel.SwayMax, _feel.SwayMax)) * steady;
            _sway = Vector3.Lerp(_sway, swayTarget, RecoilMath.ExpBlend(_feel.SwaySmoothing, dt));
            var swayPos = new Vector3(-_sway.y, _sway.x, 0f) * 0.0025f;

            // Passo, corsa, inclinazione
            Vector3 velocity = _motor != null ? _motor.Velocity : Vector3.zero;
            Vector3 flat = new(velocity.x, 0f, velocity.z);
            float speed = flat.magnitude;
            bool grounded = _motor == null || _motor.IsGrounded;
            bool sprinting = _motor != null && _motor.IsSprinting && aim < 0.1f && speed > 1f;
            _sprint = Mathf.Lerp(_sprint, sprinting ? 1f : 0f, RecoilMath.ExpBlend(8f, dt));
            _bobWeight = Mathf.Lerp(_bobWeight, grounded ? Mathf.Clamp01(speed / 5f) : 0f, RecoilMath.ExpBlend(10f, dt));
            _bobPhase += dt * Mathf.Lerp(7.5f, 11f, _sprint) * Mathf.Clamp(speed / 5f, 0.6f, 1.6f);
            float amp = _feel.BobAmount * _bobWeight * steady * Mathf.Lerp(1f, 1.8f, _sprint);
            var bobPos = new Vector3(Mathf.Cos(_bobPhase) * 0.012f, Mathf.Sin(_bobPhase * 2f) * 0.008f, 0f) * amp;
            var bobRot = new Vector3(Mathf.Sin(_bobPhase * 2f) * 0.8f, Mathf.Cos(_bobPhase) * 0.6f, Mathf.Cos(_bobPhase) * 1.1f) * amp;

            Vector3 localVelocity = transform.InverseTransformDirection(flat);
            float strafe = Mathf.Clamp(localVelocity.x / 5f, -1f, 1f);
            _tilt = Mathf.Lerp(_tilt, -strafe * _feel.StrafeTilt * steady, RecoilMath.ExpBlend(8f, dt));

            // Atterraggio: affondo proporzionale alla velocità di caduta.
            if (!grounded)
                _fallSpeed = Mathf.Min(_fallSpeed, velocity.y);
            if (grounded && !_wasGrounded)
            {
                float impact = Mathf.Clamp01(-_fallSpeed / 10f);
                _posSpring.AddImpulse(new Vector3(0f, -0.05f, 0f) * (impact * Mathf.Sqrt(_feel.PositionStiffness)));
                _rotSpring.AddImpulse(new Vector3(6f, 0f, 0f) * (impact * Mathf.Sqrt(_feel.RotationStiffness)));
                _fallSpeed = 0f;
            }
            _wasGrounded = grounded;

            // Respiro da fermi
            _breath += dt;
            float idle = _feel.IdleBreath * (1f - _bobWeight) * Mathf.Lerp(1f, 0.3f, aim);
            var breathPos = new Vector3(0f, Mathf.Sin(_breath * 1.6f) * 0.0025f, 0f) * idle;
            var breathRot = new Vector3(Mathf.Sin(_breath * 1.6f + 0.6f) * 0.35f, Mathf.Cos(_breath * 0.9f) * 0.25f, 0f) * idle;

            // Posa di corsa
            float pose = _sprint * _feel.SprintPose;
            Vector3 sprintPos = _sprintPosition * pose;
            Vector3 sprintRot = _sprintRotation * pose;

            // Calore alto: leggero tremolio dell'arma.
            UpdateHeat(dt);
            float hot = Mathf.Clamp01((_heat - 0.7f) / 0.3f);
            Vector3 jitter = Vector3.zero;
            if (hot > 0f)
            {
                float t = Time.time * 40f;
                jitter = new Vector3(Mathf.PerlinNoise(t, 1.3f) - 0.5f, Mathf.PerlinNoise(2.7f, t) - 0.5f, 0f) * (0.5f * hot);
            }

            Vector3 position = basePosition + _hipOffset * (1f - aim) + _posSpring.Value + swayPos + bobPos + breathPos + sprintPos;
            Quaternion rotation = Quaternion.Euler(_rotSpring.Value + _sway + bobRot + breathRot + sprintRot + new Vector3(0f, 0f, _tilt) + jitter);
            // Rotazione attorno all'impugnatura: il punto del perno resta fermo, l'arma "scalcia" invece di salire tutta.
            Vector3 pivot = Vector3.Lerp(_pivot, basePosition, aim);
            root.localPosition = position + (pivot - basePosition) - rotation * (pivot - basePosition);
            root.localRotation = rotation;
        }

        private void UpdateHeat(float dt)
        {
            if (Time.time - _lastShotTime > _feel.HeatDecayDelay)
                _heat = Mathf.MoveTowards(_heat, 0f, _feel.HeatDecay * dt);
            if (_venting && _heat <= VentEnd)
                _venting = false;
        }

#if UNITY_EDITOR
        public void EditorSetup(PlayerLook look, PlayerMotor motor, PlayerInputReader input)
        {
            _look = look;
            _motor = motor;
            _input = input;
        }
#endif
    }
}
