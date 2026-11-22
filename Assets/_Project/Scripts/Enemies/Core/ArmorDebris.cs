using UnityEngine;

namespace HrdnLab.FpsWave.Enemies
{
    /// <summary>
    /// Detriti cosmetici delle piastre rotte: pool fisso di schegge con fisica locale (nessun oggetto di rete),
    /// lanciate in avanti dal punto di rottura, si rimpiccioliscono e spariscono in pochi secondi.
    /// </summary>
    public sealed class ArmorDebris : MonoBehaviour
    {
        private const int Count = 32;
        private const int PerBurst = 6;
        private const float Lifetime = 2.2f;

        private static ArmorDebris s_instance;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Rigidbody[] _bodies;
        private Renderer[] _renderers;
        private float[] _born;
        private Vector3[] _scale;
        private int _next;
        private MaterialPropertyBlock _block;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_instance = null;

        public static void Burst(Vector3 position, Vector3 forward, float plateSize, Color color)
        {
            if (s_instance == null)
            {
                var go = new GameObject("[ArmorDebris]");
                s_instance = go.AddComponent<ArmorDebris>();
                s_instance.Build();
            }
            s_instance.Emit(position, forward, plateSize, color);
        }

        private void Build()
        {
            _bodies = new Rigidbody[Count];
            _renderers = new Renderer[Count];
            _born = new float[Count];
            _scale = new Vector3[Count];
            _block = new MaterialPropertyBlock();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { enableInstancing = true };
            material.SetFloat("_Metallic", 0.8f);
            material.SetFloat("_Smoothness", 0.55f);
            for (int i = 0; i < Count; i++)
            {
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "Shard";
                piece.transform.SetParent(transform, false);
                Destroy(piece.GetComponent<Collider>());
                var box = piece.AddComponent<BoxCollider>();
                box.size = Vector3.one;
                piece.layer = 2; // Ignore Raycast: i colpi non si fermano sui detriti
                var r = piece.GetComponent<Renderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var body = piece.AddComponent<Rigidbody>();
                body.mass = 0.2f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                piece.SetActive(false);
                _bodies[i] = body;
                _renderers[i] = r;
            }
        }

        private void Emit(Vector3 position, Vector3 forward, float plateSize, Color color)
        {
            _block.SetColor(BaseColorId, color);
            float now = Time.time;
            for (int n = 0; n < PerBurst; n++)
            {
                int i = _next;
                _next = (_next + 1) % Count;
                Rigidbody body = _bodies[i];
                Transform t = body.transform;
                float s = plateSize * Random.Range(0.18f, 0.32f);
                _scale[i] = new Vector3(s, s * Random.Range(0.25f, 0.6f), s * Random.Range(0.6f, 1f));
                t.localScale = _scale[i];
                t.SetPositionAndRotation(position + Random.insideUnitSphere * plateSize * 0.3f, Random.rotation);
                body.gameObject.SetActive(true);
                body.linearVelocity = (forward * Random.Range(2f, 4.5f) + Random.insideUnitSphere * 2.5f + Vector3.up * 2.5f);
                body.angularVelocity = Random.insideUnitSphere * 12f;
                _renderers[i].SetPropertyBlock(_block);
                _born[i] = now;
            }
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = 0; i < Count; i++)
            {
                if (!_bodies[i].gameObject.activeSelf)
                    continue;
                float t = (now - _born[i]) / Lifetime;
                if (t >= 1f)
                {
                    _bodies[i].gameObject.SetActive(false);
                    continue;
                }
                if (t > 0.6f)
                    _bodies[i].transform.localScale = _scale[i] * (1f - (t - 0.6f) / 0.4f);
            }
        }
    }
}
