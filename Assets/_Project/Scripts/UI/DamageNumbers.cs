using HrdnLab.FpsWave.Core;
using HrdnLab.FpsWave.Weapons;
using UnityEngine;

namespace HrdnLab.FpsWave.UI
{
    /// <summary>
    /// Numeri di danno sopra il punto colpito, solo per chi ha sparato e solo per colpi confermati dal server
    /// (<see cref="PlayerWeapons.LocalHitConfirmed"/>). Stile sobrio da gioco AAA: piccolo "pop" d'arrivo, salita breve, svanisce.
    /// Solo due toni: bianco per i colpi normali, oro tenue per i colpi di precisione (testa/punto debole/critico);
    /// l'uccisione è appena più grande, senza colori accesi né simboli.
    /// Pool fisso e stringhe in cache: nessuna allocazione durante il gioco. OnGUI come il resto dell'HUD temporaneo.
    /// </summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        private const int Capacity = 40;
        private const int CachedNumbers = 1000;

        private static readonly string[] s_plain = new string[CachedNumbers];

        [SerializeField] private PlayerWeapons _weapons;
        [Tooltip("Font dei numeri (Orbitron/Rajdhani da ThirdParty/GoogleFonts, assegnato da Setup/7).")]
        [SerializeField] private Font _font;
        [SerializeField, Min(0.2f)] private float _lifetime = 0.75f;
        [Tooltip("Salita sullo schermo in pixel (a 1080p): uguale da vicino e da lontano.")]
        [SerializeField, Min(0f)] private float _risePixels = 45f;
        [SerializeField, Min(8)] private int _fontSize = 24;

        [Header("Colori")]
        [SerializeField] private Color _normalColor = new(0.95f, 0.95f, 0.95f);
        [Tooltip("Colpi di precisione: testa, punto debole, critico.")]
        [SerializeField] private Color _precisionColor = new(1f, 0.83f, 0.42f);
        [Tooltip("Colpi su una piastra di corazza ancora intera.")]
        [SerializeField] private Color _armorColor = new(0.62f, 0.72f, 0.82f);

        private struct Entry
        {
            public bool Active;
            public Vector3 World;
            public float Born;
            public float DriftX;
            public string Text;
            public HitFlags Flags;
        }

        private readonly Entry[] _entries = new Entry[Capacity];
        private int _next;
        private GUIStyle _style;
        private readonly GUIContent _content = new();

        private void OnEnable()
        {
            if (_weapons == null)
                _weapons = GetComponent<PlayerWeapons>();
            if (_weapons != null)
                _weapons.LocalHitConfirmed += HandleHitConfirmed;
        }

        private void OnDisable()
        {
            if (_weapons != null)
                _weapons.LocalHitConfirmed -= HandleHitConfirmed;
        }

        private void HandleHitConfirmed(Vector3 point, ushort damage, HitFlags flags)
        {
            if (damage == 0 || !GameSettings.ShowDamageNumbers)
                return;
            ref Entry e = ref _entries[_next];
            _next = (_next + 1) % Capacity;
            e.Active = true;
            e.World = point;
            e.Born = Time.unscaledTime;
            e.DriftX = Random.Range(-1f, 1f);
            e.Flags = flags;
            e.Text = DamageText(damage);
        }

        private static string DamageText(ushort damage)
        {
            if (damage >= CachedNumbers)
                return damage.ToString(); // raro: danni oltre 999
            return s_plain[damage] ??= damage.ToString();
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            Camera cam = _weapons != null ? _weapons.ViewCamera : null;
            if (cam == null)
                return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Overflow,
                wordWrap = false,
            };
            if (_font != null && _style.font != _font)
                _style.font = _font;

            float now = Time.unscaledTime;
            Color previous = GUI.color;
            for (int i = 0; i < Capacity; i++)
            {
                ref Entry e = ref _entries[i];
                if (!e.Active)
                    continue;
                float t = (now - e.Born) / _lifetime;
                if (t >= 1f)
                {
                    e.Active = false;
                    continue;
                }

                // Ancorato al punto colpito; la salita è in pixel, così da vicino non schizza fuori dallo schermo.
                Vector3 screen = cam.WorldToScreenPoint(e.World);
                if (screen.z <= 0.1f)
                    continue; // dietro la camera

                bool kill = HitConfirm.Has(e.Flags, HitFlags.Kill);
                bool crit = HitConfirm.Has(e.Flags, HitFlags.Critical);
                bool weak = HitConfirm.Has(e.Flags, HitFlags.Weakspot);
                bool armorHit = HitConfirm.Has(e.Flags, HitFlags.ArmorHit);
                bool armorBreak = HitConfirm.Has(e.Flags, HitFlags.ArmorBreak);
                bool precision = crit || weak || armorBreak;
                float kindScale = (precision ? 1.12f : 1f) * (kill || armorBreak ? 1.1f : 1f) * (armorHit && !armorBreak ? 0.9f : 1f);
                float size = _fontSize * (Screen.height / 1080f) * kindScale * DamageNumberMotion.Pop(t) * DamageNumberMotion.DistanceScale(screen.z);
                _style.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));

                Color color = precision ? _precisionColor : armorHit ? _armorColor : _normalColor;
                float alpha = DamageNumberMotion.Alpha(t);

                float pixelScale = Screen.height / 1080f;
                float rise = DamageNumberMotion.Rise(t);
                float x = screen.x + e.DriftX * 16f * pixelScale * rise;
                float y = Screen.height - screen.y - (_risePixels * kindScale * pixelScale * rise) - 18f * pixelScale;
                var rect = new Rect(x - 80f, y - 30f, 160f, 60f);
                _content.text = e.Text;

                // Solo un'ombra morbida sotto (niente contorni pesanti): leggibile ma pulito.
                float o = Mathf.Max(1f, _style.fontSize / 16f);
                GUI.color = new Color(0f, 0f, 0f, alpha * 0.6f);
                GUI.Label(new Rect(rect.x + o, rect.y + o, rect.width, rect.height), _content, _style);

                GUI.color = new Color(color.r, color.g, color.b, alpha);
                GUI.Label(rect, _content, _style);
            }
            GUI.color = previous;
        }

#if UNITY_EDITOR
        public void EditorSetup(PlayerWeapons weapons) => _weapons = weapons;
        public void EditorSetFont(Font font) => _font = font;
#endif
    }

    /// <summary>Curve di animazione dei numeri di danno (logica pura, coperta da test). t va da 0 a 1 nella vita del numero.</summary>
    public static class DamageNumberMotion
    {
        /// <summary>"Pop" d'arrivo discreto: parte a 1.3x e torna a 1x nei primi 12% della vita.</summary>
        public static float Pop(float t)
        {
            const float popTime = 0.12f;
            if (t >= popTime)
                return 1f;
            float k = Mathf.Clamp01(t / popTime);
            return Mathf.Lerp(1.3f, 1f, 1f - (1f - k) * (1f - k));
        }

        /// <summary>Salita con frenata (ease-out): veloce all'inizio, quasi ferma alla fine.</summary>
        public static float Rise(float t)
        {
            float k = Mathf.Clamp01(t);
            return 1f - (1f - k) * (1f - k);
        }

        /// <summary>Pienamente visibile per il 60% della vita, poi svanisce.</summary>
        public static float Alpha(float t)
        {
            if (t <= 0.6f)
                return 1f;
            return Mathf.Clamp01(1f - (t - 0.6f) / 0.4f);
        }

        /// <summary>Un po' più grande da vicino, un po' più piccolo da lontano, sempre leggibile.</summary>
        public static float DistanceScale(float distance) => Mathf.Clamp(1.25f - distance / 60f, 0.75f, 1.2f);
    }
}
