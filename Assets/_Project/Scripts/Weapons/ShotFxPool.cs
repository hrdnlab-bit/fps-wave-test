using UnityEngine;

namespace HrdnLab.FpsWave.Weapons
{
    /// <summary>
    /// Effetti cosmetici degli spari (tracer e segni d'impatto), puramente locali e in pool:
    /// nessuna allocazione durante il gioco e nessun oggetto di rete (vedi docs/NETCODE.md).
    /// </summary>
    public sealed class ShotFxPool : MonoBehaviour
    {
        private const int TracerCount = 24;
        private const int ImpactCount = 48;
        private const int FleshCount = 16;
        private const float FleshLifetime = 0.14f;
        private const float TracerLifetime = 0.06f;
        private const float ImpactLifetime = 8f;

        private static ShotFxPool _instance;

        private LineRenderer[] _tracers;
        private float[] _tracerExpiry;
        private Transform[] _impacts;
        private float[] _impactExpiry;
        private int _nextTracer;
        private int _nextImpact;
        private Transform[] _flesh;
        private float[] _fleshExpiry;
        private int _nextFlesh;
        private Material _fleshMaterial;
        private MaterialPropertyBlock _tracerBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Restituisce il pool della scena corrente, creandolo al primo uso.</summary>
        public static ShotFxPool Get(Material tracerMaterial, Material impactMaterial)
        {
            if (_instance != null)
                return _instance;
            var go = new GameObject("[ShotFxPool]");
            _instance = go.AddComponent<ShotFxPool>();
            _instance.Build(tracerMaterial, impactMaterial);
            return _instance;
        }

        private void Build(Material tracerMaterial, Material impactMaterial)
        {
            _tracers = new LineRenderer[TracerCount];
            _tracerExpiry = new float[TracerCount];
            for (int i = 0; i < TracerCount; i++)
            {
                var t = new GameObject("Tracer");
                t.transform.SetParent(transform, false);
                var lr = t.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.startWidth = 0.025f;
                lr.endWidth = 0.01f;
                lr.sharedMaterial = tracerMaterial;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                _tracers[i] = lr;
            }

            _impacts = new Transform[ImpactCount];
            _impactExpiry = new float[ImpactCount];
            for (int i = 0; i < ImpactCount; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = "Impact";
                quad.transform.SetParent(transform, false);
                quad.transform.localScale = Vector3.one * 0.08f;
                var r = quad.GetComponent<MeshRenderer>();
                r.sharedMaterial = impactMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                quad.SetActive(false);
                _impacts[i] = quad.transform;
            }

            // Colpo sui personaggi: schizzo rosso che si allarga e sparisce in un attimo (nessun segno permanente).
            _fleshMaterial = new Material(impactMaterial);
            _fleshMaterial.SetColor("_BaseColor", new Color(0.7f, 0.04f, 0.02f));
            _flesh = new Transform[FleshCount];
            _fleshExpiry = new float[FleshCount];
            for (int i = 0; i < FleshCount; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = "FleshHit";
                quad.transform.SetParent(transform, false);
                var r = quad.GetComponent<MeshRenderer>();
                r.sharedMaterial = _fleshMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                quad.SetActive(false);
                _flesh[i] = quad.transform;
            }
        }

        private void OnDestroy()
        {
            if (_fleshMaterial != null)
                Destroy(_fleshMaterial);
            if (_instance == this)
                _instance = null;
        }

        /// <summary>Tracciante del colore dell'energia dell'arma (più chiaro al centro: sembra una scarica).</summary>
        public void Tracer(Vector3 from, Vector3 to, Color energy)
        {
            _tracerBlock ??= new MaterialPropertyBlock();
            _tracerBlock.SetColor(BaseColorId, Color.Lerp(energy, Color.white, 0.35f) * 1.6f);
            _tracers[_nextTracer].SetPropertyBlock(_tracerBlock);
            Tracer(from, to);
        }

        public void Tracer(Vector3 from, Vector3 to)
        {
            LineRenderer lr = _tracers[_nextTracer];
            _tracerExpiry[_nextTracer] = Time.time + TracerLifetime;
            _nextTracer = (_nextTracer + 1) % TracerCount;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.enabled = true;
        }

        public void Impact(Vector3 point, Vector3 normal)
        {
            Transform t = _impacts[_nextImpact];
            _impactExpiry[_nextImpact] = Time.time + ImpactLifetime;
            _nextImpact = (_nextImpact + 1) % ImpactCount;
            // Leggermente staccato dalla superficie per evitare z-fighting; il quad guarda fuori dalla superficie.
            t.SetPositionAndRotation(point + normal * 0.01f, Quaternion.LookRotation(-normal));
            t.gameObject.SetActive(true);
        }

        /// <summary>Colpo su un personaggio: effetto brevissimo, mai lasciato in scena.</summary>
        public void FleshImpact(Vector3 point, Vector3 normal)
        {
            Transform t = _flesh[_nextFlesh];
            _fleshExpiry[_nextFlesh] = Time.time + FleshLifetime;
            _nextFlesh = (_nextFlesh + 1) % FleshCount;
            t.SetPositionAndRotation(point + normal * 0.02f, Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.value * 360f));
            t.localScale = Vector3.one * 0.06f;
            t.gameObject.SetActive(true);
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = 0; i < FleshCount; i++)
            {
                if (_fleshExpiry[i] <= 0f)
                    continue;
                float remaining = _fleshExpiry[i] - now;
                if (remaining <= 0f)
                {
                    _fleshExpiry[i] = 0f;
                    _flesh[i].gameObject.SetActive(false);
                    continue;
                }
                float k = 1f - remaining / FleshLifetime; // 0 -> 1
                _flesh[i].localScale = Vector3.one * Mathf.Lerp(0.06f, 0.2f, k);
            }
            for (int i = 0; i < TracerCount; i++)
            {
                if (_tracers[i].enabled && now >= _tracerExpiry[i])
                    _tracers[i].enabled = false;
            }
            for (int i = 0; i < ImpactCount; i++)
            {
                if (_impactExpiry[i] > 0f && now >= _impactExpiry[i])
                {
                    _impactExpiry[i] = 0f;
                    _impacts[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
