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
        [SerializeField] private HrdnLab.FpsWave.Combat.Health _health;

        [Header("Colpi subiti (flinch)")]
        [Tooltip("Sobbalzo della visuale quando si viene colpiti (gradi), come il flinch dei Call of Duty. Ridotto mirando.")]
        [SerializeField, Min(0f)] private float _flinchDegrees = 1.2f;

        [Header("Posizione e perno")]
        [Tooltip("Spostamento della posa da fianco (m, spazio camera): z negativo = arma più vicina, y negativo = più in basso. Mirando sparisce.")]
        [SerializeField] private Vector3 _hipOffset = new(0f, -0.012f, -0.045f);
        [Tooltip("Punto (spazio camera) attorno a cui ruota l'arma per calcio, oscillazione e corsa: circa l'impugnatura. Ruotando attorno all'occhio l'arma salirebbe sullo schermo.")]
        [SerializeField] private Vector3 _pivot = new(0.12f, -0.17f, 0.32f);

        [Header("Posa di corsa (offset della radice dell'arma)")]
        [SerializeField] private Vector3 _sprintPosition = new(0.04f, -0.07f, -0.04f);
        [SerializeField] private Vector3 _sprintRotation = new(10f, -24f, 10f);

        [Header("Posa accovacciati")]
        [SerializeField] private Vector3 _crouchPosition = new(-0.008f, -0.014f, 0.006f);
        [SerializeField] private Vector3 _crouchRotation = new(0f, 0f, 4f);

        [Header("Estrazione")]
        [SerializeField] private Vector3 _drawPosition = new(0f, -0.14f, -0.06f);
        [SerializeField] private Vector3 _drawRotation = new(28f, -12f, 8f);

        private WeaponFeel _feel = new();
        private int _seed;
        private Spring3 _posSpring;
        private Spring3 _rotSpring;
        private Spring3 _swaySpring;
        private Vector3 _prevLocalVelocity;
        private Vector3 _inertia;
        private float _bobPhase;
        private float _bobWeight;
        private float _sprint;
        private float _sprintVelocity;
        private float _crouch;
        private float _crouchVelocity;
        private float _air;
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
            if (_health == null) _health = GetComponent<HrdnLab.FpsWave.Combat.Health>();
        }

        private void OnEnable()
        {
            if (_health != null)
                _health.ClientHealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.ClientHealthChanged -= HandleHealthChanged;
        }

        /// <summary>Flinch: un colpo subito sposta un po' la visuale (torna da sola) e fa tremare la camera.</summary>
        private void HandleHealthChanged(float previous, float next)
        {
            if (next >= previous || next <= 0f || _look == null || !_look.enabled)
                return;
            float size = _flinchDegrees * Mathf.Clamp(0.6f + (previous - next) / 25f, 0.6f, 1.6f);
            var kick = new Vector2(Random.Range(0.4f, 1f), Random.Range(-1f, 1f)) * size;
            _look.AddRecoilKick(kick, 0f, 30f, 7f);
            _look.AddTrauma(0.12f);
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
                _shotIndex, _seed, aim, crouching, Random.value, Random.value);
            if (_look != null)
            {
                // Da fianco il piccolo calcio torna tutto da solo (la mira non sale); mirando resta la parte permanente da compensare.
                _look.AddRecoilKick(kick, _feel.PermanentFraction * aim, _feel.Snappiness, _feel.ReturnSpeed);
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
            if (_feel.UsesHeat)
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
            light.color = Color.Lerp(_feel.MuzzleColor, _feel.HotColor, Mathf.Pow(_heat, 1.5f));
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

        /// <summary>
        /// Posa finale della radice del view model: base (anca/mira) + tutti gli effetti, pensati per sembrare un'arma
        /// tenuta da una persona e non incollata alla camera:
        /// - oscillazione del mouse su una molla (l'arma resta indietro e si assesta con un piccolo rimbalzo);
        /// - inerzia del corpo: partendo, frenando o cambiando direzione l'arma "pesa" e si inclina;
        /// - passo sincronizzato con la distanza percorsa (non col tempo), più ampio e veloce in corsa;
        /// - corsa e accovacciamento con transizioni smorzate (niente scatti), salto e atterraggio con molla;
        /// - respiro da fermi, tremolio a calore alto (solo armi a energia). Mirando quasi tutto si ferma.
        /// </summary>
        public void ApplyViewModel(Transform root, Vector3 basePosition, float aim, float dt)
        {
            if (root == null || dt <= 0f)
                return;

            _posSpring.Update(dt, _feel.PositionStiffness, _feel.PositionDamping);
            _rotSpring.Update(dt, _feel.RotationStiffness, _feel.RotationDamping);
            float steady = Mathf.Lerp(1f, 0.12f, aim); // mirando quasi tutto si ferma

            // Oscillazione del mouse: bersaglio proporzionale alla velocità di rotazione, inseguito da una molla
            // appena sotto lo smorzamento critico (si assesta con un rimbalzo minimo, come un'arma con un po' di peso).
            Vector2 look = _input != null ? _input.LookDelta : Vector2.zero;
            Vector2 lookSpeed = look / dt; // gradi al secondo
            float k = _feel.SwayAmount * 0.01f;
            float max = _feel.SwayMax;
            var swayTarget = new Vector3(
                Mathf.Clamp(lookSpeed.y * k, -max, max),
                Mathf.Clamp(-lookSpeed.x * k, -max, max),
                Mathf.Clamp(-lookSpeed.x * k * 0.8f, -max, max)) * steady;
            float swayStiffness = _feel.SwaySmoothing * _feel.SwaySmoothing * 1.6f;
            _swaySpring.Update(dt, swayStiffness, 2f * Mathf.Sqrt(swayStiffness) * 0.8f, swayTarget);
            Vector3 sway = _swaySpring.Value;
            var swayPos = new Vector3(-sway.y, sway.x, 0f) * 0.0022f;

            // Movimento del corpo
            Vector3 velocity = _motor != null ? _motor.Velocity : Vector3.zero;
            Vector3 flat = new(velocity.x, 0f, velocity.z);
            float speed = flat.magnitude;
            bool grounded = _motor == null || _motor.IsGrounded;
            bool sprinting = _motor != null && _motor.IsSprinting && aim < 0.1f && speed > 1f;
            bool crouching = _motor != null && _motor.IsCrouching;
            Vector3 localVelocity = transform.InverseTransformDirection(flat);

            // Inerzia: l'arma reagisce all'accelerazione (resta indietro partendo, va avanti frenando, si inclina sterzando).
            Vector3 accel = (localVelocity - _prevLocalVelocity) / dt;
            _prevLocalVelocity = localVelocity;
            _inertia = Vector3.Lerp(_inertia, accel, RecoilMath.ExpBlend(5f, dt));
            var inertiaPos = new Vector3(
                Mathf.Clamp(-_inertia.x * 0.0011f, -0.012f, 0.012f), 0f,
                Mathf.Clamp(-_inertia.z * 0.0009f, -0.012f, 0.012f)) * steady;
            var inertiaRot = new Vector3(Mathf.Clamp(_inertia.z * 0.08f, -2f, 2f), 0f, Mathf.Clamp(_inertia.x * 0.22f, -3f, 3f)) * steady;

            // Corsa e accovacciamento: transizioni smorzate.
            _sprint = Mathf.SmoothDamp(_sprint, sprinting ? 1f : 0f, ref _sprintVelocity, 0.24f, Mathf.Infinity, dt);
            _crouch = Mathf.SmoothDamp(_crouch, crouching ? 1f : 0f, ref _crouchVelocity, 0.2f, Mathf.Infinity, dt);

            // Passo: la fase avanza con la distanza percorsa, a un ritmo umano (~2 passi al secondo camminando,
            // ~2.7 correndo, come nei Call of Duty): un passo ogni ~2.2 m camminando e ~2.6 m correndo alle velocità del gioco.
            _bobWeight = Mathf.Lerp(_bobWeight, grounded ? Mathf.Clamp01(speed / 4.5f) : 0f, RecoilMath.ExpBlend(5f, dt));
            float stepLength = Mathf.Lerp(2.2f, 2.6f, _sprint);
            _bobPhase = (_bobPhase + speed * dt / stepLength * Mathf.PI) % (Mathf.PI * 2f);
            float amp = _feel.BobAmount * _bobWeight * steady;
            float big = 1f + 1.1f * _sprint;
            float s1 = Mathf.Sin(_bobPhase);
            float step = s1 * s1; // morbido: 0 sul piede, 1 a metà passo, senza spigoli
            var bobPos = new Vector3(s1 * 0.006f * big, (0.5f - step) * 0.006f * big, 0f) * amp;
            var bobRot = new Vector3(step * 0.6f * big, s1 * 0.5f * big, s1 * 0.9f * big) * amp;

            float strafe = Mathf.Clamp(localVelocity.x / 5f, -1f, 1f);
            _tilt = Mathf.Lerp(_tilt, -strafe * _feel.StrafeTilt * steady, RecoilMath.ExpBlend(4f, dt));

            // Salto e caduta: l'arma si abbassa allo stacco, sale leggermente mentre si cade, affonda all'atterraggio.
            if (!grounded)
                _fallSpeed = Mathf.Min(_fallSpeed, velocity.y);
            if (!grounded && _wasGrounded && velocity.y > 1f)
            {
                _posSpring.AddImpulse(new Vector3(0f, -0.025f, -0.01f) * Mathf.Sqrt(_feel.PositionStiffness));
                _rotSpring.AddImpulse(new Vector3(3f, 0f, 0f) * Mathf.Sqrt(_feel.RotationStiffness));
            }
            if (grounded && !_wasGrounded)
            {
                float impact = Mathf.Clamp01(-_fallSpeed / 10f);
                _posSpring.AddImpulse(new Vector3(0f, -0.05f, 0f) * (impact * Mathf.Sqrt(_feel.PositionStiffness)));
                _rotSpring.AddImpulse(new Vector3(6f, 0f, 0f) * (impact * Mathf.Sqrt(_feel.RotationStiffness)));
                _fallSpeed = 0f;
            }
            _wasGrounded = grounded;
            float airTarget = grounded ? 0f : Mathf.Clamp(-velocity.y * 0.003f, -0.015f, 0.02f);
            _air = Mathf.Lerp(_air, airTarget, RecoilMath.ExpBlend(4f, dt));
            var airPos = new Vector3(0f, _air, 0f) * steady;
            var airRot = new Vector3(-_air * 120f, 0f, 0f) * steady;

            // Respiro da fermi
            _breath += dt;
            float idle = _feel.IdleBreath * (1f - _bobWeight) * Mathf.Lerp(1f, 0.3f, aim);
            var breathPos = new Vector3(0f, Mathf.Sin(_breath * 1.6f) * 0.0025f, 0f) * idle;
            var breathRot = new Vector3(Mathf.Sin(_breath * 1.6f + 0.6f) * 0.35f, Mathf.Cos(_breath * 0.9f) * 0.25f, 0f) * idle;

            // Pose di corsa e accovacciati
            float pose = _sprint * _feel.SprintPose;
            float crouchPose = _crouch * (1f - aim);
            Vector3 posePos = _sprintPosition * pose + _crouchPosition * crouchPose;
            Vector3 poseRot = _sprintRotation * pose + _crouchRotation * crouchPose;

            // Calore alto (solo armi a energia): leggero tremolio dell'arma.
            UpdateHeat(dt);
            float hot = Mathf.Clamp01((_heat - 0.7f) / 0.3f);
            Vector3 jitter = Vector3.zero;
            if (hot > 0f)
            {
                float t = Time.time * 40f;
                jitter = new Vector3(Mathf.PerlinNoise(t, 1.3f) - 0.5f, Mathf.PerlinNoise(2.7f, t) - 0.5f, 0f) * (0.5f * hot);
            }

            Vector3 position = basePosition + _hipOffset * (1f - aim) + _posSpring.Value + swayPos + inertiaPos + bobPos + airPos + breathPos + posePos;
            Quaternion rotation = Quaternion.Euler(_rotSpring.Value + sway + inertiaRot + bobRot + airRot + breathRot + poseRot
                                                   + new Vector3(0f, 0f, _tilt) + jitter);
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
