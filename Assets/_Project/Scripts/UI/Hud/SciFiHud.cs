using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Core;
using HrdnLab.FpsWave.Enemies;
using HrdnLab.FpsWave.Player;
using HrdnLab.FpsWave.Weapons;
using UnityEngine;
using UnityEngine.UIElements;

namespace HrdnLab.FpsWave.UI
{
    /// <summary>
    /// HUD "visore della tuta" in stile Crysis (UI Toolkit, stile in UI/Styles/Hud.uss, texture in ThirdParty/_Own/Hud),
    /// solo per il player locale. Elementi sottili e puliti, con dettagli ma senza pannelli pieni:
    /// - in alto: obiettivo a sinistra, bussola con direzione al centro, telemetria a destra (se attiva);
    /// - in basso a sinistra: integrità della tuta (valore, stato, segmenti, righello);
    /// - in basso a destra: arma (slot, nome, modalità di fuoco), munizioni grandi con una tacca per colpo, ricarica,
    ///   calore solo per le armi a energia, striscia delle armi in dotazione;
    /// - al centro: mirino a quattro tratti che si apre con lo spread, hit marker; rosso del danno solo ai bordi.
    /// Dimensioni pensate su 1920x1080 e scalate dal PanelSettings (ScaleWithScreenSize), quindi proporzionate a ogni schermo.
    /// I testi si aggiornano solo quando cambia il valore (niente allocazioni a ogni frame).
    /// Abilitato/disabilitato da PlayerView insieme al suo UIDocument (owner-only).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SciFiHud : MonoBehaviour
    {
        private const int HealthSegments = 20;
        private const float WarnThreshold = 0.5f;
        private const float CriticalThreshold = 0.25f;
        private const float CompassWidth = 600f;
        private const float CompassPixelsPerDegree = 4f;

        private static readonly string[] CompassNames = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        [SerializeField] private PlayerWeapons _weapons;
        [SerializeField] private Health _health;
        [SerializeField] private StyleSheet _styleSheet;
        [SerializeField, Min(0.05f)] private float _hitMarkerDuration = 0.18f;

        private const int PingSlots = 8;
        private const int ScanSlots = 24;
        private const int DownedSlots = 4;
        private const float PingLifetime = 7f;
        private const float CalloutTime = 2.6f;

        private UIDocument _document;
        private SuitAbilities _suit;
        private PlayerLife _life;
        private VisualElement _root;
        private VisualElement _vitals;
        private VisualElement _weaponBlock;

        // Integrità
        private Label _healthValue;
        private Label _healthMax;
        private Label _healthStatus;
        private Label _healthAlert;
        private int _shownMaxHealth = -1;
        private readonly VisualElement[] _segments = new VisualElement[HealthSegments];
        private int _shownHealth = -1;
        private int _shownSegments = -1;
        private int _vitalsLevel = -1;

        // Arma
        private Label _weaponName;
        private Label _weaponSlot;
        private Label _fireMode;
        private Label _ammo;
        private VisualElement _ticksRow;
        private VisualElement[] _ticks = System.Array.Empty<VisualElement>();
        private VisualElement _loadoutRow;
        private Label[] _loadoutLabels = System.Array.Empty<Label>();
        private int _shownEquipped = -1;
        private bool _shownUsesHeat = true;
        private Label _ammoMax;
        private VisualElement _reloadRow;
        private Label _reloadLabel;
        private VisualElement _reloadFill;
        private VisualElement _heatRow;
        private Label _heatLabel;
        private VisualElement _heatFill;
        private int _heatLevel = -1;
        private WeaponDefinition _shownWeapon;
        private int _shownAmmo = -1;
        private int _shownMag = -1;
        private bool _shownReloading;
        private float _reloadStart;
        private float _reloadDuration = 1f;

        // Bussola
        private VisualElement _compassTicks;
        private readonly Label[] _compassLabels = new Label[CompassNames.Length];
        private Label _compassHeading;
        private int _shownHeading = -1;

        // Centro
        private VisualElement _chUp, _chDown, _chLeft, _chRight, _chDot;
        private VisualElement _hitMarker;
        private HitMarkerType _markerType;
        private float _markerUntil;

        // Tuta
        private VisualElement _energyFill;
        private Label _energyValue;
        private int _shownEnergy = -1;
        private VisualElement _chipScan, _chipArmor, _chipBoost;
        private VisualElement _tintArmor, _tintBoost;

        // Voce della tuta
        private Label _callout;
        private float _calloutUntil;

        // Indicatori nel mondo (ping, scansione, compagni a terra)
        private VisualElement _markerLayer;
        private struct PingEntry { public int Pinger; public Vector3 Point; public Transform Target; public bool Enemy; public float Until; }
        private readonly PingEntry[] _pings = new PingEntry[PingSlots];
        private readonly VisualElement[] _pingMarkers = new VisualElement[PingSlots];
        private readonly Label[] _pingLabels = new Label[PingSlots];
        private readonly Label[] _pingKinds = new Label[PingSlots];
        private readonly EnemyAgent[] _scanTargets = new EnemyAgent[ScanSlots];
        private readonly VisualElement[] _scanMarkers = new VisualElement[ScanSlots];
        private float _scanUntil;
        private readonly VisualElement[] _downedMarkers = new VisualElement[DownedSlots];
        private readonly Label[] _downedLabels = new Label[DownedSlots];

        // A terra e rianimazione
        private VisualElement _downedPanel;
        private Label _downedTitle;
        private Label _downedDetail;
        private VisualElement _downedFill;
        private VisualElement _revivePrompt;
        private VisualElement _reviveFill;
        private int _shownTimeLeft = -1;
        private LifeState _shownState = LifeState.Up;

        private static readonly string[] s_distances = new string[1000];

        // Danno e telemetria
        private VisualElement _damageFlash;
        private float _damageFlashAmount;
        private Label _telemetry;
        private float _fpsAccum;
        private int _fpsFrames;
        private float _fpsNextUpdate;
        private int _shownFps = -1;
        private long _shownRtt = -1;

        private static readonly string[] s_numbers = new string[1001];
        private static readonly string[] s_headings = new string[360];

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            if (_weapons == null)
                _weapons = GetComponent<PlayerWeapons>();
            if (_health == null)
                _health = GetComponent<Health>();
            _suit = GetComponent<SuitAbilities>();
            _life = GetComponent<PlayerLife>();
            Build();
            SuitCallouts.Raised += HandleCallout;
            PlayerPing.ClientPinged += HandlePing;
            SuitAbilities.ClientScanPulse += HandleScan;
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
            SuitCallouts.Raised -= HandleCallout;
            PlayerPing.ClientPinged -= HandlePing;
            SuitAbilities.ClientScanPulse -= HandleScan;
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

            _root.Add(Element("layer", "visor-vignette"));
            _tintArmor = Element("layer", "tint-armor");
            _tintBoost = Element("layer", "tint-boost");
            _root.Add(_tintArmor);
            _root.Add(_tintBoost);
            _markerLayer = Element("layer", "marker-layer");
            _root.Add(_markerLayer);
            BuildMarkers();
            _damageFlash = Element("layer", "hud-damage");
            _root.Add(_damageFlash);

            BuildTop();
            BuildVitals();
            BuildWeapon();
            BuildReticle();

            _healthAlert = Text("ARMOR CRITICAL · TAKE COVER", "alert", "font-bold");
            _healthAlert.style.display = DisplayStyle.None;
            _root.Add(_healthAlert);
            BuildLifePanels();
        }

        private void BuildMarkers()
        {
            for (int i = 0; i < ScanSlots; i++)
            {
                _scanMarkers[i] = Element("marker", "marker--scan");
                _scanMarkers[i].Add(Element("marker-icon"));
                _scanMarkers[i].style.display = DisplayStyle.None;
                _markerLayer.Add(_scanMarkers[i]);
            }
            for (int i = 0; i < PingSlots; i++)
            {
                _pingMarkers[i] = Element("marker", "marker--ping");
                _pingMarkers[i].Add(Element("marker-icon"));
                _pingKinds[i] = Text("PING", "marker-kind", "font-bold");
                _pingLabels[i] = Text("", "marker-label", "font-mono");
                _pingMarkers[i].Add(_pingKinds[i]);
                _pingMarkers[i].Add(_pingLabels[i]);
                _pingMarkers[i].style.display = DisplayStyle.None;
                _markerLayer.Add(_pingMarkers[i]);
            }
            for (int i = 0; i < DownedSlots; i++)
            {
                _downedMarkers[i] = Element("marker", "marker--downed");
                _downedMarkers[i].Add(Element("marker-icon"));
                _downedMarkers[i].Add(Text("REVIVE", "marker-kind", "font-bold"));
                _downedLabels[i] = Text("", "marker-label", "font-mono");
                _downedMarkers[i].Add(_downedLabels[i]);
                _downedMarkers[i].style.display = DisplayStyle.None;
                _markerLayer.Add(_downedMarkers[i]);
            }
        }

        private void BuildLifePanels()
        {
            _downedPanel = Element("downed-panel");
            _downedTitle = Text("DOWNED", "downed-title", "font-display");
            _downedDetail = Text("", "downed-detail", "font-mono");
            var track = Element("revive-track");
            _downedFill = Element("revive-fill");
            track.Add(_downedFill);
            _downedPanel.Add(_downedTitle);
            _downedPanel.Add(_downedDetail);
            _downedPanel.Add(track);
            _downedPanel.style.display = DisplayStyle.None;
            _root.Add(_downedPanel);

            _revivePrompt = Element("revive-prompt");
            _revivePrompt.Add(Text("HOLD [E] TO REVIVE", "revive-text", "font-bold"));
            var promptTrack = Element("revive-track");
            _reviveFill = Element("revive-fill");
            promptTrack.Add(_reviveFill);
            _revivePrompt.Add(promptTrack);
            _revivePrompt.style.display = DisplayStyle.None;
            _root.Add(_revivePrompt);
        }

        private void BuildTop()
        {
            var objective = Element("objective");
            objective.Add(Text("OBJECTIVE", "objective-tag", "font-bold"));
            objective.Add(Text("SURVIVE THE WAVES", "objective-text", "font-bold"));
            objective.Add(Element("objective-line"));
            _root.Add(objective);

            var top = Element("hud-top");
            var compass = Element("compass");
            _compassTicks = Element("compass-ticks");
            compass.Add(_compassTicks);
            for (int i = 0; i < CompassNames.Length; i++)
            {
                _compassLabels[i] = Text(CompassNames[i], "compass-label", "font-bold");
                if (i % 2 == 0)
                    _compassLabels[i].AddToClassList("compass-label--cardinal");
                compass.Add(_compassLabels[i]);
            }
            compass.Add(Element("compass-center"));
            compass.Add(Element("compass-fade", "compass-fade--left"));
            compass.Add(Element("compass-fade", "compass-fade--right"));
            top.Add(compass);
            _callout = Text("", "callout", "font-bold");
            _callout.style.opacity = 0f;
            var headingBox = Element("compass-heading-box");
            _compassHeading = Text("000", "compass-heading", "font-mono");
            headingBox.Add(_compassHeading);
            top.Add(headingBox);
            top.Add(_callout);
            _root.Add(top);

            _telemetry = Text("", "telemetry", "font-mono");
            _root.Add(_telemetry);
        }

        /// <summary>Integrità della tuta, in basso a sinistra.</summary>
        private void BuildVitals()
        {
            _vitals = Element("block", "block--left");
            _vitals.Add(Element("block-backdrop"));
            Brackets(_vitals);

            var header = Element("block-header");
            header.Add(Text("ARMOR", "block-title", "font-bold"));
            header.Add(Element("spacer"));
            _healthStatus = Text("NOMINAL", "status-tag", "font-mono");
            header.Add(_healthStatus);
            _vitals.Add(header);

            var valueRow = Element("value-row");
            _healthValue = Text("100", "health-value", "font-display");
            _healthMax = Text("/100", "health-max", "font-display");
            valueRow.Add(_healthValue);
            valueRow.Add(_healthMax);
            valueRow.Add(Element("spacer"));
            valueRow.Add(Text("SUIT MK-VII", "micro", "font-mono"));
            _vitals.Add(valueRow);

            var segments = Element("segments");
            for (int i = 0; i < HealthSegments; i++)
            {
                _segments[i] = Element("segment", "segment--on");
                segments.Add(_segments[i]);
            }
            _vitals.Add(segments);
            _vitals.Add(Element("ruler"));

            // Energia della tuta e abilità
            var suitRow = Element("suit-row");
            suitRow.Add(Text("SUIT", "bar-label", "font-bold"));
            var energyTrack = Element("energy-track");
            _energyFill = Element("energy-fill");
            energyTrack.Add(_energyFill);
            suitRow.Add(energyTrack);
            _energyValue = Text("100", "energy-value", "font-mono");
            suitRow.Add(_energyValue);
            _vitals.Add(suitRow);
            var chips = Element("ability-row");
            _chipScan = Chip("Q", "SCAN");
            _chipArmor = Chip("F", "ARMOR");
            _chipBoost = Chip("V", "BOOST");
            chips.Add(_chipScan);
            chips.Add(_chipArmor);
            chips.Add(_chipBoost);
            _vitals.Add(chips);
            _root.Add(_vitals);
        }

        /// <summary>Arma impugnata, munizioni e dotazione, in basso a destra.</summary>
        private void BuildWeapon()
        {
            _weaponBlock = Element("block", "block--right");
            _weaponBlock.Add(Element("block-backdrop"));
            Brackets(_weaponBlock);

            var header = Element("block-header");
            _weaponSlot = Text("1", "slot-box", "font-display");
            _weaponName = Text("—", "block-title", "font-bold");
            _fireMode = Text("AUTO", "status-tag", "font-mono");
            header.Add(_weaponSlot);
            header.Add(_weaponName);
            header.Add(Element("spacer"));
            header.Add(_fireMode);
            _weaponBlock.Add(header);

            var ammoRow = Element("value-row", "value-row--right");
            _ammo = Text("0", "ammo", "font-display");
            _ammoMax = Text("/0", "ammo-max", "font-display");
            ammoRow.Add(_ammo);
            ammoRow.Add(_ammoMax);
            _weaponBlock.Add(ammoRow);

            _ticksRow = Element("ammo-ticks");
            _weaponBlock.Add(_ticksRow);

            _reloadRow = Bar("RELOADING", out _reloadLabel, out _reloadFill);
            _reloadRow.AddToClassList("bar-row--reload");
            _reloadRow.style.display = DisplayStyle.None;
            _weaponBlock.Add(_reloadRow);

            _heatRow = Bar("HEAT", out _heatLabel, out _heatFill);
            _weaponBlock.Add(_heatRow);

            _weaponBlock.Add(Element("ruler"));
            _loadoutRow = Element("loadout");
            _weaponBlock.Add(_loadoutRow);
            _root.Add(_weaponBlock);
        }

        private void BuildReticle()
        {
            var reticle = Element("reticle");
            _chUp = Element("ch-line", "ch-line--v");
            _chDown = Element("ch-line", "ch-line--v");
            _chLeft = Element("ch-line", "ch-line--h");
            _chRight = Element("ch-line", "ch-line--h");
            _chDot = Element("ch-dot");
            reticle.Add(_chUp);
            reticle.Add(_chDown);
            reticle.Add(_chLeft);
            reticle.Add(_chRight);
            reticle.Add(_chDot);
            _root.Add(reticle);

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
            UpdateCompass();
            UpdateReticle();
            UpdateEffects();
            UpdateTelemetry();
            UpdateSuit();
            UpdateCallout();
            UpdateMarkers();
            UpdateLife();
        }

        // ------------------------------------------------------------------ Tuta, voce, indicatori, a terra

        private void UpdateSuit()
        {
            if (_suit == null)
            {
                _energyFill.parent.parent.style.display = DisplayStyle.None;
                return;
            }
            float energy = _suit.Energy;
            _energyFill.style.width = Length.Percent(_suit.Energy01 * 100f);
            int shown = Mathf.RoundToInt(energy);
            if (shown != _shownEnergy)
            {
                _shownEnergy = shown;
                _energyValue.text = Number(shown);
            }
            SetChip(_chipScan, false, _suit.ScanCooldownRemaining > 0f, energy + 0.5f < _suit.Cost(SuitAbility.PulseScan));
            SetChip(_chipArmor, _suit.ArmorActive, false, energy + 0.5f < _suit.Cost(SuitAbility.ArmorMode));
            SetChip(_chipBoost, _suit.OverchargeActive, false, energy + 0.5f < _suit.Cost(SuitAbility.Overcharge));
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
            _tintArmor.style.opacity = _suit.ArmorActive || _suit.Shielded ? pulse : 0f;
            _tintBoost.style.opacity = _suit.OverchargeActive ? pulse : 0f;
        }

        private static void SetChip(VisualElement chip, bool active, bool cooldown, bool low)
        {
            chip.EnableInClassList("chip--active", active);
            chip.EnableInClassList("chip--cooldown", !active && cooldown);
            chip.EnableInClassList("chip--low", !active && !cooldown && low);
        }

        private static VisualElement Chip(string key, string label)
        {
            var chip = Element("ability-chip");
            chip.Add(Text(key, "chip-key", "font-display"));
            chip.Add(Text(label, "chip-label", "font-bold"));
            return chip;
        }

        private void HandleCallout(string text, CalloutKind kind)
        {
            if (_callout == null)
                return;
            _callout.text = "SUIT // " + text;
            _callout.EnableInClassList("callout--warning", kind == CalloutKind.Warning);
            _callout.EnableInClassList("callout--positive", kind == CalloutKind.Positive);
            _calloutUntil = Time.unscaledTime + CalloutTime;
        }

        private void UpdateCallout()
        {
            float remaining = _calloutUntil - Time.unscaledTime;
            _callout.style.opacity = remaining <= 0f ? 0f : Mathf.Clamp01(remaining / 0.4f);
        }

        private void HandlePing(PlayerPing.PingInfo info)
        {
            int slot = -1;
            for (int i = 0; i < PingSlots; i++)
                if (_pings[i].Until > Time.time && _pings[i].Pinger == info.PingerId) { slot = i; break; }
            if (slot < 0)
            {
                float oldest = float.MaxValue;
                for (int i = 0; i < PingSlots; i++)
                    if (_pings[i].Until < oldest) { oldest = _pings[i].Until; slot = i; }
            }
            _pings[slot] = new PingEntry
            {
                Pinger = info.PingerId,
                Point = info.Point,
                Target = info.Target != null ? info.Target.transform : null,
                Enemy = info.Target != null,
                Until = Time.time + PingLifetime,
            };
            _pingKinds[slot].text = info.Target != null ? "HOSTILE" : "PING";
            _pingMarkers[slot].EnableInClassList("marker--enemy", info.Target != null);
        }

        private void HandleScan(Vector3 origin, float radius, float duration)
        {
            int n = 0;
            float radiusSqr = radius * radius;
            var enemies = EnemyAgent.ClientActive;
            for (int i = 0; i < enemies.Count && n < ScanSlots; i++)
            {
                EnemyAgent e = enemies[i];
                if (e != null && !e.ClientIsDead && (e.transform.position - origin).sqrMagnitude <= radiusSqr)
                    _scanTargets[n++] = e;
            }
            for (int i = n; i < ScanSlots; i++)
                _scanTargets[i] = null;
            _scanUntil = Time.time + duration;
        }

        private void UpdateMarkers()
        {
            Camera cam = _weapons != null ? _weapons.ViewCamera : null;
            Rect area = _markerLayer.layout;
            if (cam == null || float.IsNaN(area.width) || area.width <= 1f)
                return;
            Vector3 eye = cam.transform.position;
            float now = Time.time;

            for (int i = 0; i < PingSlots; i++)
            {
                ref PingEntry p = ref _pings[i];
                bool alive = p.Until > now && (!p.Enemy || p.Target != null);
                if (alive && p.Target != null)
                    p.Point = p.Target.position + Vector3.up * 2.4f;
                if (!alive || !Place(_pingMarkers[i], cam, p.Point, area, true))
                {
                    _pingMarkers[i].style.display = DisplayStyle.None;
                    continue;
                }
                _pingLabels[i].text = Distance(Vector3.Distance(eye, p.Point));
                _pingMarkers[i].style.opacity = Mathf.Clamp01((p.Until - now) / 0.6f);
            }

            bool scanning = now < _scanUntil;
            for (int i = 0; i < ScanSlots; i++)
            {
                EnemyAgent e = _scanTargets[i];
                bool show = scanning && e != null && !e.ClientIsDead
                            && Place(_scanMarkers[i], cam, e.transform.position + Vector3.up * 1.2f, area, false);
                if (!show)
                    _scanMarkers[i].style.display = DisplayStyle.None;
                else
                    _scanMarkers[i].style.opacity = Mathf.Clamp01((_scanUntil - now) / 0.8f);
            }

            int d = 0;
            var players = PlayerLife.All;
            for (int i = 0; i < players.Count && d < DownedSlots; i++)
            {
                PlayerLife other = players[i];
                if (other == null || other == _life || !other.IsDowned)
                    continue;
                Vector3 point = other.transform.position + Vector3.up * 1.2f;
                if (Place(_downedMarkers[d], cam, point, area, true))
                {
                    _downedLabels[d].text = Distance(Vector3.Distance(eye, point));
                    d++;
                }
            }
            for (int i = d; i < DownedSlots; i++)
                _downedMarkers[i].style.display = DisplayStyle.None;
        }

        /// <summary>Posiziona un indicatore sul punto del mondo; se <paramref name="clamp"/>, fuori schermo resta sul bordo.</summary>
        private static bool Place(VisualElement marker, Camera cam, Vector3 world, Rect area, bool clamp)
        {
            Vector3 vp = cam.WorldToViewportPoint(world);
            bool behind = vp.z < 0f;
            if (behind)
            {
                if (!clamp)
                    return false;
                vp.x = 1f - vp.x;
                vp.y = vp.y > 0.5f ? 0f : 1f;
            }
            bool offscreen = vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f;
            if (offscreen && !clamp)
                return false;
            vp.x = Mathf.Clamp(vp.x, 0.03f, 0.97f);
            vp.y = Mathf.Clamp(vp.y, 0.06f, 0.94f);
            marker.style.display = DisplayStyle.Flex;
            marker.style.translate = new Translate(vp.x * area.width, (1f - vp.y) * area.height);
            marker.EnableInClassList("marker--edge", offscreen || behind);
            return true;
        }

        private static string Distance(float meters)
        {
            int m = Mathf.Clamp(Mathf.RoundToInt(meters), 0, s_distances.Length - 1);
            return s_distances[m] ??= m + "M";
        }

        private void UpdateLife()
        {
            if (_life == null)
                return;
            LifeState state = _life.State;
            if (state != _shownState)
            {
                _shownState = state;
                _shownTimeLeft = -1;
                _downedPanel.style.display = state == LifeState.Up ? DisplayStyle.None : DisplayStyle.Flex;
                _downedPanel.EnableInClassList("downed-panel--dead", state == LifeState.Dead);
                _downedTitle.text = state == LifeState.Dead ? "SIGNAL LOST" : "DOWNED";
            }
            if (state != LifeState.Up)
            {
                int left = Mathf.CeilToInt(_life.TimeLeft);
                bool beingRevived = state == LifeState.Downed && _life.ReviveProgress > 0.01f;
                if (left != _shownTimeLeft || beingRevived)
                {
                    _shownTimeLeft = left;
                    _downedDetail.text = beingRevived ? "BEING REVIVED"
                        : state == LifeState.Dead ? "REDEPLOYING IN " + Number(left)
                        : "BLEEDING OUT " + Number(left);
                }
                _downedFill.style.width = Length.Percent(_life.ReviveProgress * 100f);
            }

            PlayerLife target = _life.ReviveTarget;
            _revivePrompt.style.display = target != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (target != null)
                _reviveFill.style.width = Length.Percent(target.ReviveProgress * 100f);
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
            }
            int max = Mathf.CeilToInt(_health.Max);
            if (max != _shownMaxHealth)
            {
                _shownMaxHealth = max;
                _healthMax.text = "/" + Number(max);
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
                _vitals.EnableInClassList("block--warn", level == 1);
                _vitals.EnableInClassList("block--critical", level == 2);
                _healthStatus.text = level == 2 ? "CRITICAL" : level == 1 ? "DAMAGED" : "NOMINAL";
                _healthAlert.style.display = level == 2 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (level == 2)
                _healthAlert.style.opacity = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
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
                _fireMode.text = state.Definition.Automatic ? "AUTO" : "SEMI";
                _shownAmmo = -1;
                _shownMag = -1;
            }

            int mag = state.MagazineSize;
            if (mag != _shownMag)
            {
                _shownMag = mag;
                _ammoMax.text = "/" + Number(mag);
                RebuildTicks(mag);
                _shownAmmo = -1;
            }

            if (state.Ammo != _shownAmmo)
            {
                _shownAmmo = state.Ammo;
                _ammo.text = Number(state.Ammo);
                _ammo.EnableInClassList("ammo--empty", state.Ammo == 0);
                _ammo.EnableInClassList("ammo--low", state.Ammo > 0 && state.Ammo <= Mathf.Max(1, mag / 4));
                for (int i = 0; i < _ticks.Length; i++)
                    _ticks[i].EnableInClassList("tick--spent", i >= state.Ammo);
                if (!state.IsReloading)
                {
                    bool empty = state.Ammo == 0;
                    _reloadRow.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
                    _reloadLabel.text = "EMPTY [R]";
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
                    _reloadLabel.text = "RELOADING";
                }
                _reloadRow.style.display = state.IsReloading || state.Ammo == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (state.IsReloading)
                _reloadFill.style.width = Length.Percent(Mathf.Clamp01((Time.time - _reloadStart) / _reloadDuration) * 100f);

            if (_weapons.EquippedIndex != _shownEquipped)
            {
                _shownEquipped = _weapons.EquippedIndex;
                _weaponSlot.text = Number(_shownEquipped + 1);
                RebuildLoadout();
            }

            // Calore (cosmetico) solo per le armi a energia: con le armi a proiettili la barra non c'è.
            bool usesHeat = _weapons.UsesHeat;
            if (usesHeat != _shownUsesHeat)
            {
                _shownUsesHeat = usesHeat;
                _heatRow.style.display = usesHeat ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (!usesHeat)
                return;
            float heat = _weapons.Heat;
            _heatFill.style.width = Length.Percent(heat * 100f);
            int heatLevel = _weapons.IsVenting ? 2 : heat > 0.7f ? 1 : 0;
            if (heatLevel != _heatLevel)
            {
                _heatLevel = heatLevel;
                _heatRow.EnableInClassList("bar-row--hot", heatLevel >= 1);
                _heatRow.EnableInClassList("bar-row--venting", heatLevel == 2);
                _heatLabel.text = heatLevel == 2 ? "VENTING" : "HEAT";
            }
            _heatLabel.style.opacity = heatLevel == 2 ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
        }

        /// <summary>Una tacca sottile per colpo del caricatore (fino a 60), come sui visori di Crysis.</summary>
        private void RebuildTicks(int magazine)
        {
            _ticksRow.Clear();
            int count = Mathf.Clamp(magazine, 0, 60);
            _ticks = new VisualElement[count];
            for (int i = count - 1; i >= 0; i--) // la prima tacca a consumarsi è quella più a sinistra
            {
                _ticks[i] = Element("tick");
                _ticksRow.Add(_ticks[i]);
            }
        }

        /// <summary>Striscia delle armi in dotazione: numero del tasto e nome, quella impugnata evidenziata.</summary>
        private void RebuildLoadout()
        {
            _loadoutRow.Clear();
            var loadout = _weapons.Loadout;
            _loadoutLabels = new Label[loadout.Count];
            for (int i = 0; i < loadout.Count; i++)
            {
                if (loadout[i] == null)
                    continue;
                var label = Text(Number(i + 1) + "  " + loadout[i].DisplayName.ToUpperInvariant(), "loadout-item", "font-bold");
                label.EnableInClassList("loadout-item--active", i == _shownEquipped);
                _loadoutLabels[i] = label;
                _loadoutRow.Add(label);
            }
        }

        private void UpdateCompass()
        {
            Camera cam = _weapons != null ? _weapons.ViewCamera : null;
            if (cam == null)
                return;
            float heading = HudMath.Heading(cam.transform.forward);

            // La striscia di tacche scorre: al centro c'è la direzione attuale.
            float offset = CompassWidth * 0.5f - heading * CompassPixelsPerDegree;
            _compassTicks.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left, offset);

            for (int i = 0; i < _compassLabels.Length; i++)
            {
                float delta = Mathf.DeltaAngle(heading, i * 45f);
                bool visible = Mathf.Abs(delta) < 52f;
                Label label = _compassLabels[i];
                label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible)
                {
                    label.style.left = CompassWidth * 0.5f + delta * CompassPixelsPerDegree - 20f;
                    label.style.opacity = 1f - Mathf.Clamp01((Mathf.Abs(delta) - 30f) / 22f);
                }
            }

            int rounded = Mathf.RoundToInt(heading) % 360;
            if (rounded != _shownHeading)
            {
                _shownHeading = rounded;
                _compassHeading.text = s_headings[rounded] ??= rounded.ToString("000");
            }
        }

        private void UpdateReticle()
        {
            if (_weapons == null)
                return;
            Camera cam = _weapons.ViewCamera;
            float fov = cam != null ? cam.fieldOfView : 90f;
            float gap = HudMath.SpreadToPixels(_weapons.CurrentSpread, fov, 1080f);
            gap = Mathf.Max(4f, gap) + 5f;
            _chUp.style.translate = new Translate(0f, -gap);
            _chDown.style.translate = new Translate(0f, gap);
            _chLeft.style.translate = new Translate(-gap, 0f);
            _chRight.style.translate = new Translate(gap, 0f);
            float alpha = 1f - _weapons.AimAmount; // mirando c'è il mirino dell'arma
            _chUp.style.opacity = alpha;
            _chDown.style.opacity = alpha;
            _chLeft.style.opacity = alpha;
            _chRight.style.opacity = alpha;
            _chDot.style.opacity = alpha;
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

            // Il rosso resta ai bordi (texture a vignetta): non copre mai la mira.
            if (_damageFlashAmount > 0f)
            {
                _damageFlashAmount = Mathf.MoveTowards(_damageFlashAmount, 0f, Time.deltaTime * 2.2f);
                _damageFlash.style.opacity = Mathf.Min(0.8f, _damageFlashAmount);
            }
            else if (_vitalsLevel == 2)
            {
                _damageFlash.style.opacity = 0.15f + 0.2f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
            }
            else
            {
                _damageFlash.style.opacity = 0f;
            }
        }

        private void UpdateTelemetry()
        {
            if (!GameSettings.ShowFps)
                return;
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (Time.unscaledTime < _fpsNextUpdate)
                return;
            int fps = Mathf.Min(1000, Mathf.RoundToInt(_fpsFrames / Mathf.Max(0.0001f, _fpsAccum)));
            _fpsAccum = 0f;
            _fpsFrames = 0;
            _fpsNextUpdate = Time.unscaledTime + 0.5f;
            long rtt = _weapons != null && _weapons.TimeManager != null ? _weapons.TimeManager.RoundTripTime : 0;
            rtt = (long)Mathf.Min(rtt, 1000);
            if (fps != _shownFps || rtt != _shownRtt)
            {
                _shownFps = fps;
                _shownRtt = rtt;
                _telemetry.text = Number(fps) + " FPS  ·  " + Number((int)rtt) + " MS";
            }
        }

        // ------------------------------------------------------------------ Eventi

        private void HandleHitMarker(HitMarkerType type)
        {
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
                _damageFlashAmount = Mathf.Clamp01(_damageFlashAmount + 0.3f + (previous - next) / _health.Max * 2f);
        }

        private void HandleSettingsChanged()
        {
            if (_telemetry != null)
                _telemetry.style.display = GameSettings.ShowFps ? DisplayStyle.Flex : DisplayStyle.None;
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

        /// <summary>Quattro angoli sottili attorno a un blocco (dettaglio da visore, senza pannello pieno).</summary>
        private static void Brackets(VisualElement block)
        {
            block.Add(Element("bracket", "bracket--tl"));
            block.Add(Element("bracket", "bracket--tr"));
            block.Add(Element("bracket", "bracket--bl"));
            block.Add(Element("bracket", "bracket--br"));
        }

        private static VisualElement Bar(string label, out Label labelElement, out VisualElement fill)
        {
            var row = Element("bar-row");
            labelElement = Text(label, "bar-label", "font-bold");
            var track = Element("bar-track");
            fill = Element("bar-fill");
            track.Add(fill);
            track.Add(Element("bar-hazard"));
            row.Add(labelElement);
            row.Add(track);
            return row;
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

        /// <summary>Direzione sulla bussola in gradi [0, 360): 0 = +Z (nord), 90 = +X (est).</summary>
        public static float Heading(Vector3 forward)
        {
            float deg = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            return deg < 0f ? deg + 360f : deg;
        }
    }
}
