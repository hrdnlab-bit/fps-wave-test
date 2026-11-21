using FishNet.Object;
using UnityEngine;

namespace HrdnLab.FpsWave.Combat
{
    /// <summary>
    /// Bersaglio di allenamento: cambia colore in base alla salute (client) e si rigenera dopo la morte (server).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class TrainingDummy : NetworkBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Min(0.1f)] private float _respawnDelay = 3f;
        [SerializeField] private Renderer[] _renderers = System.Array.Empty<Renderer>();
        [SerializeField] private Color _fullColor = new(0.85f, 0.85f, 0.82f);
        [SerializeField] private Color _lowColor = new(0.85f, 0.15f, 0.1f);
        [SerializeField] private Color _deadColor = new(0.15f, 0.15f, 0.15f);

        private Health _health;
        private MaterialPropertyBlock _block;
        private float _respawnAt = -1f;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _block = new MaterialPropertyBlock();
            _health.ServerDied += HandleServerDied;
            _health.ClientHealthChanged += HandleClientHealthChanged;
        }

        private void OnDestroy()
        {
            _health.ServerDied -= HandleServerDied;
            _health.ClientHealthChanged -= HandleClientHealthChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyColor(_health.Normalized);
        }

        private void HandleServerDied(Health health, DamageInfo info) => _respawnAt = Time.time + _respawnDelay;

        private void Update()
        {
            if (_respawnAt < 0f || !IsServerInitialized || Time.time < _respawnAt)
                return;
            _respawnAt = -1f;
            _health.ResetHealth();
        }

        private void HandleClientHealthChanged(float previous, float next) => ApplyColor(_health.Normalized);

        private void ApplyColor(float normalized)
        {
            Color color = normalized <= 0f ? _deadColor : Color.Lerp(_lowColor, _fullColor, normalized);
            _block.SetColor(BaseColorId, color);
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].SetPropertyBlock(_block);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(Renderer[] renderers) => _renderers = renderers;
#endif
    }
}
