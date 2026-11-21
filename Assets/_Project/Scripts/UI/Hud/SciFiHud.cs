using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Core;
using HrdnLab.FpsWave.Weapons;
using UnityEngine;
using UnityEngine.UIElements;

namespace HrdnLab.FpsWave.UI
{
    /// <summary>
    /// HUD "visore da combattimento" in UI Toolkit (stile in UI/Styles/Hud.uss), solo per il player locale:
    /// integrità a segmenti, munizioni con una tacca per colpo e ricarica, mirino dinamico, hit marker,
    /// bordo rosso quando si subisce danno, avviso di integrità critica, telemetria (link e FPS).
    /// Costruito da codice; i testi si aggiornano solo quando cambia il valore (niente allocazioni a ogni frame).
    /// Abilitato/disabilitato da PlayerView insieme al suo UIDocument (owner-only).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SciFiHud : MonoBehaviour
    {
        private const int HealthSegments = 20;
        private const float WarnThreshold = 0.5f;
        private const float CriticalThreshold = 0.25f;

        [SerializeField] private PlayerWeapons _weapons;
        [SerializeField] private Health _health;
        [SerializeField] private StyleSheet _styleSheet;
        [SerializeField, Min(0.05f)] private float _hitMarkerDuration = 0.18f;

        private UIDocument _document;
        private VisualElement _root;

        // Integrità
        private VisualElement _vitals;
        private Label _healthValue;
        private Label _healthPercent;
        private Label _healthAlert;
        private readonly VisualElement[] _segments = new VisualElement[HealthSegments];
        private int _shownHealth = -1;
        private int _shownSegments = -1;
        private int _vitalsLevel = -1;

        // Arma
        private Label _weaponName;
        private Label _slot;
        private Label _ammo;
        private Label _ammoMax;
        private VisualElement _pipsRow;
        private VisualElement[] _pips = System.Array.Empty<VisualElement>();
        private VisualElement _reloadBox;
        private Label _reloadLabel;
        private VisualElement _reloadFill;
        private WeaponDefinition _shownWeapon;
        private int _shownAmmo = -1;
        private int _shownMag = -1;
        private bool _shownReloading;
        private float _reloadStart;
        private float _reloadDuration = 1f;

        // Mirino e hit marker
        private VisualElement _chUp, _chDown, _chLeft, _chRight;
        private VisualElement _hitMarker;
        private HitMarkerType _markerType;
        private float _markerUntil;

        // Danno e telemetria
        private VisualElement _damageFlash;
        private float _damageFlashAmount;
        private Label _linkLabel;
        private Label _fpsLabel;
        private float _fpsAccum;
        private int _fpsFrames;
        private float _fpsNextUpdate;
        private int _shownFps = -1;
        private long _shownRtt = -1;

        private static readonly string[] s_numbers = new string[1001];

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            if (_weapons == null)
                _weapons = GetComponent<PlayerWeapons>();
            if (_health == null)
                _health = GetComponent<Health>();
            Build();
            if (_weapons != null)
                _weapons.LocalHitMarker += HandleHitMarker;
            if (_health != null)
                _health.ClientHealthChanged += HandleHealthChanged;
            GameSettings.Changed += HandleSettingsChanged;
            HandleSettingsChanged();
        }

        private void OnDisable()
        {
            if (_weapons != null)
                _weapons.LocalHitMarker -= HandleHitMarker;
            if (_health != null)
                _health.ClientHealthChanged -= HandleHealthChanged;
            GameSettings.Changed -= HandleSettingsChanged;
            _root?.RemoveFromHierarchy();
            _root = null;
        }

        // ------------------------------------------------------------------ Costruzione

        private void Build()
        {
            VisualElement docRoot = _document != null ? _document.rootVisualElement : null;
            if (docRoot == null)
                return;
            _root = new VisualElement { name = "hud", pickingMode = PickingMode.Ignore };
            _root.AddToClassList("hud");
            if (_styleSheet != null)
                _root.styleSheets.Add(_styleSheet);
            docRoot.Add(_root);

            // Leggerissime righe di scansione, solo atmosfera.
            for (int i = 0; i < 6; i++)
            {
                var line = Element("hud-scanline");
                line.style.top = Length.Percent(12f + i * 15f);
                _root.Add(line);
            }

            _damageFlash = Element("hud-damage");
            _root.Add(_damageFlash);

            BuildStatus();
            BuildMission();
            BuildVitals();
            BuildWeapon();
            BuildCrosshair();
        }

        private void BuildStatus()
        {
            var status = Element("hud-status");
            _linkLabel = Text("LINK -- MS", "hud-status-line", "hud-status-line--accent");
            _fpsLabel = Text("FPS --", "hud-status-line");
            status.Add(Text("VISORE TATTICO // V-7", "hud-status-line"));
            status.Add(_linkLabel);
            status.Add(_fpsLabel);
            _root.Add(status);
        }

        private void BuildMission()
        {
            var mission = Element("hud-mission");
            var strip = Element("hud-mission-strip");
            strip.Add(Text("OBIETTIVO", "hud-mission-tag"));
            strip.Add(Text("SOPRAVVIVI ALLE ONDATE", "hud-mission-text"));
            mission.Add(strip);
            _root.Add(mission);
        }

        private void BuildVitals()
        {
            _vitals = Panel("hud-vitals");
            var header = Element("hud-row");
            header.Add(Text("INTEGRITÀ", "hud-caption"));
            header.Add(Element("hud-spacer"));
            _healthPercent = Text("100%", "hud-caption");
            header.Add(_healthPercent);
            _vitals.Add(header);

            var valueRow = Element("hud-row");
            _healthValue = Text("100", "hud-big-number");
            valueRow.Add(_healthValue);
            valueRow.Add(Text("HP", "hud-big-unit"));
            _vitals.Add(valueRow);

            var bar = Element("hud-segments");
            for (int i = 0; i < HealthSegments; i++)
            {
                _segments[i] = Element("segment", "segment--on");
                bar.Add(_segments[i]);
            }
            _vitals.Add(bar);

            _healthAlert = Text("▲ INTEGRITÀ CRITICA", "hud-alert");
            _healthAlert.style.display = DisplayStyle.None;
            _vitals.Add(_healthAlert);
            _root.Add(_vitals);
        }

        private void BuildWeapon()
        {
            VisualElement weapon = Panel("hud-weapon");
            var header = Element("hud-weapon-header");
            _weaponName = Text("—", "hud-weapon-name");
            _slot = Text("01", "hud-slot");
            header.Add(_weaponName);
            header.Add(_slot);
            weapon.Add(header);

            var ammoRow = Element("hud-ammo-row");
            _ammo = Text("0", "hud-ammo");
            _ammoMax = Text("/ 0", "hud-ammo-max");
            ammoRow.Add(_ammo);
            ammoRow.Add(_ammoMax);
            weapon.Add(ammoRow);

            _pipsRow = Element("hud-pips");
            weapon.Add(_pipsRow);

            _reloadBox = Element("hud-reload");
            _reloadLabel = Text("RICARICA", "hud-reload-label");
            var track = Element("hud-reload-track");
            _reloadFill = Element("hud-reload-fill");
            track.Add(_reloadFill);
            _reloadBox.Add(_reloadLabel);
            _reloadBox.Add(track);
            _reloadBox.style.display = DisplayStyle.None;
            weapon.Add(_reloadBox);
            _root.Add(weapon);
        }

        private void BuildCrosshair()
        {
            var crosshair = Element("crosshair");
            _chUp = Element("ch-line", "ch-line--v");
            _chDown = Element("ch-line", "ch-line--v");
            _chLeft = Element("ch-line", "ch-line--h");
            _chRight = Element("ch-line", "ch-line--h");
            crosshair.Add(_chUp);
            crosshair.Add(_chDown);
            crosshair.Add(_chLeft);
            crosshair.Add(_chRight);
            crosshair.Add(Element("ch-dot"));
            _root.Add(crosshair);

            _hitMarker = Element("hitmarker");
            for (int i = 0; i < 4; i++)
            {
                var tick = Element("hm-tick");
                float angle = 45f + 90f * i;
                float rad = angle * Mathf.Deg2Rad;
                tick.style.translate = new Translate(Mathf.Cos(rad) * 12f, Mathf.Sin(rad) * 12f);
                tick.style.rotate = new Rotate(angle);
                _hitMarker.Add(tick);
            }
            _root.Add(_hitMarker);
        }

        // ------------------------------------------------------------------ Aggiornamento

        private void Update()
        {
            if (_root == null)
            {
                // Il UIDocument può creare il suo albero dopo il nostro OnEnable: si ritenta qui.
                Build();
                if (_root == null)
                    return;
                HandleSettingsChanged();
            }

            // Con un menu aperto (pausa, impostazioni) il HUD si nasconde: il menu ha il suo stile e non si sovrappongono.
            DisplayStyle display = UiState.MenuOpen ? DisplayStyle.None : DisplayStyle.Flex;
            if (_root.style.display != display)
                _root.style.display = display;
            if (UiState.MenuOpen)
                return;
            UpdateVitals();
            UpdateWeapon();
            UpdateCrosshair();
            UpdateEffects();
            UpdateTelemetry();
        }

        private void UpdateVitals()
        {
            if (_health == null)
                return;
            float normalized = Mathf.Clamp01(_health.Normalized);
            int hp = Mathf.CeilToInt(_health.Current);
            if (hp != _shownHealth)
            {
                _shownHealth = hp;
                _healthValue.text = Number(hp);
                _healthPercent.text = Number(Mathf.RoundToInt(normalized * 100f)) + "%";
            }

            int lit = HudMath.LitSegments(normalized, HealthSegments);
            if (lit != _shownSegments)
            {
                _shownSegments = lit;
                for (int i = 0; i < HealthSegments; i++)
                    _segments[i].EnableInClassList("segment--on", i < lit);
            }

            int level = normalized <= CriticalThreshold ? 2 : normalized <= WarnThreshold ? 1 : 0;
            if (level != _vitalsLevel)
            {
                _vitalsLevel = level;
                _vitals.EnableInClassList("hud-vitals--warn", level == 1);
                _vitals.EnableInClassList("hud-vitals--critical", level == 2);
                _healthAlert.style.display = level == 2 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (level == 2)
                _healthAlert.style.opacity = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f));
        }

        private void UpdateWeapon()
        {
            WeaponState state = _weapons != null ? _weapons.Current : null;
            if (state == null)
                return;

            if (state.Definition != _shownWeapon)
            {
                _shownWeapon = state.Definition;
                _weaponName.text = state.Definition.DisplayName.ToUpperInvariant();
                _slot.text = state.Definition.Slot == WeaponSlot.Primary ? "PRIMARIA" : "SECONDARIA";
                _shownAmmo = -1;
                _shownMag = -1;
            }

            int mag = state.MagazineSize;
            if (mag != _shownMag)
            {
                _shownMag = mag;
                _ammoMax.text = "/ " + Number(mag);
                RebuildPips(mag);
                _shownAmmo = -1;
            }

            if (state.Ammo != _shownAmmo)
            {
                _shownAmmo = state.Ammo;
                _ammo.text = Number(state.Ammo);
                _ammo.EnableInClassList("hud-ammo--empty", state.Ammo == 0);
                _ammo.EnableInClassList("hud-ammo--low", state.Ammo > 0 && state.Ammo <= Mathf.Max(1, mag / 4));
                for (int i = 0; i < _pips.Length; i++)
                    _pips[i].EnableInClassList("pip--spent", i >= state.Ammo);
                if (!state.IsReloading)
                {
                    bool empty = state.Ammo == 0;
                    _reloadBox.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
                    _reloadLabel.text = "CARICATORE VUOTO · [R]";
                    _reloadFill.style.width = Length.Percent(0f);
                }
            }

            if (state.IsReloading != _shownReloading)
            {
                _shownReloading = state.IsReloading;
                if (state.IsReloading)
                {
                    _reloadStart = Time.time;
                    _reloadDuration = Mathf.Max(0.05f, state.ReloadTime);
                    _reloadLabel.text = "RICARICA IN CORSO";
                }
                _reloadBox.style.display = state.IsReloading || state.Ammo == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (state.IsReloading)
                _reloadFill.style.width = Length.Percent(Mathf.Clamp01((Time.time - _reloadStart) / _reloadDuration) * 100f);
        }

        private void RebuildPips(int magazine)
        {
            _pipsRow.Clear();
            int count = Mathf.Clamp(magazine, 0, 60);
            _pips = new VisualElement[count];
            for (int i = 0; i < count; i++)
            {
                _pips[i] = Element("pip");
                _pipsRow.Add(_pips[i]);
            }
        }

        private void UpdateCrosshair()
        {
            if (_weapons == null)
                return;
            Camera cam = _weapons.ViewCamera;
            float fov = cam != null ? cam.fieldOfView : 90f;
            // Spread in pixel nello spazio di riferimento 1080p del pannello.
            float gap = HudMath.SpreadToPixels(_weapons.CurrentSpread, fov, 1080f);
            gap = Mathf.Max(5f, gap) + 6f;
            _chUp.style.translate = new Translate(0f, -gap);
            _chDown.style.translate = new Translate(0f, gap);
            _chLeft.style.translate = new Translate(-gap, 0f);
            _chRight.style.translate = new Translate(gap, 0f);
            float alpha = Mathf.Lerp(1f, 0.15f, _weapons.AimAmount);
            _chUp.style.opacity = alpha;
            _chDown.style.opacity = alpha;
            _chLeft.style.opacity = alpha;
            _chRight.style.opacity = alpha;
        }

        private void UpdateEffects()
        {
            float now = Time.time;
            float remaining = _markerUntil - now;
            _hitMarker.style.opacity = remaining > 0f ? Mathf.Clamp01(remaining / (_hitMarkerDuration * 0.5f)) : 0f;
            if (remaining > 0f)
            {
                float pop = 1f + 0.35f * Mathf.Clamp01(remaining / _hitMarkerDuration);
                _hitMarker.style.scale = new Scale(new Vector2(pop, pop));
            }

            if (_damageFlashAmount > 0f)
            {
                _damageFlashAmount = Mathf.MoveTowards(_damageFlashAmount, 0f, Time.deltaTime * 2.2f);
                _damageFlash.style.opacity = _damageFlashAmount;
            }
            else if (_vitalsLevel == 2)
            {
                // Integrità critica: il bordo pulsa piano.
                _damageFlash.style.opacity = 0.25f + 0.2f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
            }
            else
            {
                _damageFlash.style.opacity = 0f;
            }
        }

        private void UpdateTelemetry()
        {
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (Time.unscaledTime < _fpsNextUpdate)
                return;
            int fps = Mathf.RoundToInt(_fpsFrames / Mathf.Max(0.0001f, _fpsAccum));
            _fpsAccum = 0f;
            _fpsFrames = 0;
            _fpsNextUpdate = Time.unscaledTime + 0.5f;
            if (fps != _shownFps)
            {
                _shownFps = fps;
                _fpsLabel.text = "FPS " + Number(Mathf.Min(fps, 1000));
            }
            long rtt = _weapons != null && _weapons.TimeManager != null ? _weapons.TimeManager.RoundTripTime : 0;
            if (rtt != _shownRtt)
            {
                _shownRtt = rtt;
                _linkLabel.text = "LINK " + Number((int)Mathf.Min(rtt, 1000)) + " MS";
            }
        }

        // ------------------------------------------------------------------ Eventi

        private void HandleHitMarker(HitMarkerType type)
        {
            // Un marker più importante (kill/critico confermati) non viene sovrascritto da uno minore.
            if (Time.time < _markerUntil && type < _markerType)
                return;
            _markerType = type;
            _markerUntil = Time.time + (type == HitMarkerType.Kill ? _hitMarkerDuration * 2f : _hitMarkerDuration);
            _hitMarker.EnableInClassList("hitmarker--weak", type == HitMarkerType.Weakspot);
            _hitMarker.EnableInClassList("hitmarker--crit", type == HitMarkerType.Critical);
            _hitMarker.EnableInClassList("hitmarker--kill", type == HitMarkerType.Kill);
        }

        private void HandleHealthChanged(float previous, float next)
        {
            if (next < previous && _health != null && _health.Max > 0f)
                _damageFlashAmount = Mathf.Clamp01(_damageFlashAmount + 0.35f + (previous - next) / _health.Max * 2f);
        }

        private void HandleSettingsChanged()
        {
            if (_fpsLabel != null)
                _fpsLabel.style.display = GameSettings.ShowFps ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ------------------------------------------------------------------ Utilità

        private static string Number(int value)
        {
            if (value < 0 || value >= s_numbers.Length)
                return value.ToString();
            return s_numbers[value] ??= value.ToString();
        }

        private static VisualElement Element(params string[] classes)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (string c in classes)
                e.AddToClassList(c);
            return e;
        }

        private static Label Text(string text, params string[] classes)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (string c in classes)
                label.AddToClassList(c);
            return label;
        }

        private static VisualElement Panel(string cls)
        {
            VisualElement panel = Element("hud-panel", cls);
            panel.Add(Element("corner", "corner--tl"));
            panel.Add(Element("corner", "corner--tr"));
            panel.Add(Element("corner", "corner--bl"));
            panel.Add(Element("corner", "corner--br"));
            return panel;
        }

#if UNITY_EDITOR
        public void EditorSetup(PlayerWeapons weapons, Health health, StyleSheet styleSheet)
        {
            _weapons = weapons;
            _health = health;
            _styleSheet = styleSheet;
        }
#endif
    }

    /// <summary>Calcoli del HUD (logica pura, coperta da test).</summary>
    public static class HudMath
    {
        /// <summary>Segmenti accesi: un segmento resta acceso finché ne rimane anche un po' (0 solo a salute zero).</summary>
        public static int LitSegments(float normalized, int segments)
        {
            if (normalized <= 0f)
                return 0;
            return Mathf.Clamp(Mathf.CeilToInt(normalized * segments - 0.0001f), 1, segments);
        }

        /// <summary>Spread in gradi -> distanza in pixel dal centro, per uno schermo alto <paramref name="screenHeight"/>.</summary>
        public static float SpreadToPixels(float spreadDegrees, float verticalFov, float screenHeight)
        {
            float half = Mathf.Tan(Mathf.Clamp(verticalFov, 1f, 179f) * 0.5f * Mathf.Deg2Rad);
            return Mathf.Tan(Mathf.Max(0f, spreadDegrees) * Mathf.Deg2Rad) / half * screenHeight * 0.5f;
        }
    }
}
