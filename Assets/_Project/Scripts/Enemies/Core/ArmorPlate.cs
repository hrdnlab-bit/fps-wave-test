using HrdnLab.FpsWave.Combat;
using UnityEngine;

namespace HrdnLab.FpsWave.Enemies
{
    /// <summary>
    /// Piastra di corazza su un nemico (figlio con <see cref="HitZone"/>): finché è intera assorbe quasi tutto il danno
    /// (ne passa solo una parte al corpo); rotta, sparisce con detriti e scopre ciò che c'è sotto
    /// (di solito un punto debole luminoso). Stato e salute delle piastre li tiene <see cref="EnemyArmor"/>.
    /// </summary>
    [RequireComponent(typeof(HitZone))]
    public sealed class ArmorPlate : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _maxHealth = 120f;
        [Tooltip("Parte del danno che passa al corpo mentre la piastra regge (0.15 = 15%).")]
        [SerializeField, Range(0f, 1f)] private float _passThrough = 0.15f;
        [SerializeField] private Renderer[] _visuals = System.Array.Empty<Renderer>();
        [SerializeField] private Color _debrisColor = new(0.22f, 0.24f, 0.27f);

        private HitZone _zone;
        private bool _broken;

        public float MaxHealth => _maxHealth;
        public float PassThrough => _passThrough;
        public bool IsBroken => _broken;
        public HitZone Zone => _zone != null ? _zone : _zone = GetComponent<HitZone>();

        public void SetBroken(bool broken)
        {
            _broken = broken;
            foreach (Renderer r in _visuals)
                if (r != null)
                    r.enabled = !broken;
            if (Zone != null && Zone.Collider != null)
                Zone.Collider.enabled = !broken;
        }

        /// <summary>Client: effetto di rottura (detriti). Le dimensioni dei detriti seguono la piastra.</summary>
        public void PlayBreakFx()
        {
            Vector3 size = transform.lossyScale;
            ArmorDebris.Burst(transform.position, transform.forward, Mathf.Max(size.x, size.y), _debrisColor);
        }

#if UNITY_EDITOR
        public void EditorSetup(float maxHealth, float passThrough, Renderer[] visuals, Color debrisColor)
        {
            _maxHealth = maxHealth;
            _passThrough = passThrough;
            _visuals = visuals;
            _debrisColor = debrisColor;
        }
#endif
    }
}
