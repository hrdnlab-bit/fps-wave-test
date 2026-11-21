using UnityEngine;

namespace HrdnLab.FpsWave.Combat
{
    /// <summary>
    /// Collider colpibile collegato a una <see cref="Health"/>. Il moltiplicatore di zona e il flag weakspot
    /// vengono letti dal server: il client invia solo l'indice della zona.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class HitZone : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _damageMultiplier = 1f;
        [SerializeField] private bool _isWeakspot;

        private Collider _collider;

        public Health Owner { get; private set; }
        public int Index { get; private set; } = -1;
        public float DamageMultiplier => _damageMultiplier;
        public bool IsWeakspot => _isWeakspot;
        public Collider Collider => _collider != null ? _collider : _collider = GetComponent<Collider>();

        internal void Bind(Health owner, int index)
        {
            Owner = owner;
            Index = index;
        }

#if UNITY_EDITOR
        public void EditorSetup(float multiplier, bool weakspot)
        {
            _damageMultiplier = multiplier;
            _isWeakspot = weakspot;
        }
#endif
    }
}
