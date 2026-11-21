using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Weapons;
using UnityEngine;

namespace HrdnLab.FpsWave.UI
{
    /// <summary>
    /// HUD temporaneo di M1 (OnGUI): mirino dinamico, hit marker, munizioni, salute, FPS e RTT.
    /// Attivo solo per il player locale. Le stringhe sono messe in cache e ricostruite solo quando
    /// cambia il valore, per contenere le allocazioni. Verrà sostituito da UI Toolkit.
    /// </summary>
    public sealed class PlayerHud : MonoBehaviour
    {
        [SerializeField] private PlayerWeapons _weapons;
        [SerializeField] private Health _health;
        [SerializeField] private float _hitMarkerDuration = 0.18f;

        private HitMarkerType _markerType;
        private float _markerUntil;

        private GUIStyle _bigStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _rightStyle;
        private GUIStyle _leftBigStyle;

        private int _cachedAmmo = -1, _cachedMag = -1;
        private bool _cachedReloading;
        private string _ammoText = string.Empty;
        private string _weaponText = string.Empty;
        private WeaponDefinition _cachedWeapon;
        private int _cachedHealth = -1;
        private string _healthText = string.Empty;
        private float _fpsAccum;
        private int _fpsFrames;
        private float _fpsNextUpdate;
        private string _statsText = string.Empty;

        private void OnEnable()
        {
            if (_weapons != null)
                _weapons.LocalHitMarker += HandleHitMarker;
        }

        private void OnDisable()
        {
            if (_weapons != null)
                _weapons.LocalHitMarker -= HandleHitMarker;
        }

        private void HandleHitMarker(HitMarkerType type)
        {
            // Un marker "più importante" (kill/critico confermati) non viene sovrascritto da uno minore.
            if (Time.time < _markerUntil && type < _markerType)
                return;
            _markerType = type;
            _markerUntil = Time.time + (type == HitMarkerType.Kill ? _hitMarkerDuration * 2f : _hitMarkerDuration);
        }

        private void Update()
        {
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (Time.unscaledTime >= _fpsNextUpdate)
            {
                float fps = _fpsFrames / Mathf.Max(0.0001f, _fpsAccum);
                long rtt = _weapons != null && _weapons.TimeManager != null ? _weapons.TimeManager.RoundTripTime : 0;
                _statsText = $"{fps:0} FPS  {1000f / Mathf.Max(1f, fps):0.0} ms  RTT {rtt} ms";
                _fpsAccum = 0f;
                _fpsFrames = 0;
                _fpsNextUpdate = Time.unscaledTime + 0.5f;
            }
        }

        private void EnsureStyles()
        {
            if (_bigStyle != null) return;
            _bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            _rightStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.LowerRight };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperRight };
            _leftBigStyle = new GUIStyle(_bigStyle) { alignment = TextAnchor.LowerLeft };
        }

        private void OnGUI()
        {
            if (_weapons == null)
                return;
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            DrawCrosshair(w, h);
            DrawHitMarker(w, h);

            WeaponState state = _weapons.Current;
            if (state != null)
            {
                if (state.Ammo != _cachedAmmo || state.MagazineSize != _cachedMag || state.IsReloading != _cachedReloading)
                {
                    _cachedAmmo = state.Ammo;
                    _cachedMag = state.MagazineSize;
                    _cachedReloading = state.IsReloading;
                    _ammoText = state.IsReloading ? "RICARICA..." : $"{state.Ammo} / {state.MagazineSize}";
                }
                if (state.Definition != _cachedWeapon)
                {
                    _cachedWeapon = state.Definition;
                    _weaponText = state.Definition.DisplayName;
                }
                GUI.Label(new Rect(w - 420, h - 110, 400, 50), _ammoText, _bigStyle);
                GUI.Label(new Rect(w - 420, h - 60, 400, 30), _weaponText, _rightStyle);
            }

            if (_health != null)
            {
                int hp = Mathf.CeilToInt(_health.Current);
                if (hp != _cachedHealth)
                {
                    _cachedHealth = hp;
                    _healthText = $"SALUTE {hp}";
                }
                GUI.Label(new Rect(20, h - 110, 400, 50), _healthText, _leftBigStyle);
            }

            GUI.Label(new Rect(w - 420, 10, 400, 24), _statsText, _smallStyle);
        }

        private void DrawCrosshair(float w, float h)
        {
            Camera cam = _weapons.ViewCamera;
            float fov = cam != null ? cam.fieldOfView : 90f;
            float spreadPx = Mathf.Tan(_weapons.CurrentSpread * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * h * 0.5f;
            float gap = Mathf.Max(4f, spreadPx);
            float len = 10f, thick = 2f;
            float cx = w * 0.5f, cy = h * 0.5f;
            float alpha = Mathf.Lerp(0.9f, 0.15f, _weapons.AimAmount);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(cx - thick * 0.5f, cy - gap - len, thick, len), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - thick * 0.5f, cy + gap, thick, len), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - gap - len, cy - thick * 0.5f, len, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + gap, cy - thick * 0.5f, len, thick), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cx - 1f, cy - 1f, 2f, 2f), Texture2D.whiteTexture);
        }

        private void DrawHitMarker(float w, float h)
        {
            if (Time.time >= _markerUntil)
                return;
            Color color = _markerType switch
            {
                HitMarkerType.Kill => new Color(1f, 0.2f, 0.15f),
                HitMarkerType.Critical => new Color(1f, 0.85f, 0.1f),
                HitMarkerType.Weakspot => new Color(1f, 0.6f, 0.2f),
                _ => Color.white,
            };
            float size = _markerType == HitMarkerType.Kill ? 12f : 8f;
            float cx = w * 0.5f, cy = h * 0.5f;
            Matrix4x4 m = GUI.matrix;
            GUI.color = color;
            for (int i = 0; i < 4; i++)
            {
                GUIUtility.RotateAroundPivot(45f + 90f * i, new Vector2(cx, cy));
                GUI.DrawTexture(new Rect(cx + 6f, cy - 1f, size, 2f), Texture2D.whiteTexture);
                GUI.matrix = m;
            }
            GUI.color = Color.white;
        }

#if UNITY_EDITOR
        public void EditorSetup(PlayerWeapons weapons, Health health)
        {
            _weapons = weapons;
            _health = health;
        }
#endif
    }
}
